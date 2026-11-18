using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AuthDemo.Services;

// Uses only the selected hospital's merchant credentials. Central CMS credentials
// are deliberately never read by this service.
public sealed class HospitalRazorpayGatewayService
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly RazorpayCredentialStore _credentialStore;

    public HospitalRazorpayGatewayService(
        IHttpClientFactory factory,
        IConfiguration configuration,
        AppDbContext db,
        RazorpayCredentialStore credentialStore)
    {
        _factory = factory;
        _configuration = configuration;
        _db = db;
        _credentialStore = credentialStore;
    }

    // Preserve existing public method signatures for current controllers/services.
    // Passing null selects active credentials for legacy callers; pinned operations MUST pass an ID.
    // Prefer credentials saved for this hospital and mode, falling back to
    // existing server configuration only when no database record exists.
    private (string KeyId, string Secret, string WebhookSecret, int? CredentialId) Credentials(int hospitalId, string mode, int? credentialId = null)
    {
        if (hospitalId <= 0 || (mode != "Test" && mode != "Live"))
            throw new InvalidOperationException("Invalid hospital payment merchant context.");

        // A historical operation must use the exact persisted credential ID.
        // Active credentials are for NEW operations; never substitute one for a pin.
        var candidates = _db.RazorpayMerchantCredentials
            .AsNoTracking()
            .Where(x => x.MerchantType == "Hospital" &&
                        x.HospitalId == hospitalId && x.Mode == mode);

        var stored = credentialId.HasValue
            ? candidates.SingleOrDefault(x => x.Id == credentialId.Value)
            : candidates.SingleOrDefault(x => x.IsActive);

        if (credentialId.HasValue && (credentialId.Value <= 0 || stored == null))
            throw new InvalidOperationException(
                "Pinned hospital Razorpay credential is missing or belongs to a different merchant/mode.");

        // Never fall back to configuration when this hospital/mode has DB credentials
        // but none is active: that would silently change the merchant key source.
        if (!credentialId.HasValue && stored == null && candidates.Any())
            throw new InvalidOperationException(
                "No active hospital Razorpay credential exists for this hospital and mode.");

        if (stored != null)
        {
            // Fail closed if stored credentials are damaged or cannot be decrypted.
            // Never silently switch to another merchant's credentials.
            if (stored.HospitalId != hospitalId ||
                stored.MerchantType != "Hospital" || stored.Mode != mode ||
                string.IsNullOrWhiteSpace(stored.KeyId) ||
                string.IsNullOrWhiteSpace(stored.EncryptedKeySecret) ||
                string.IsNullOrWhiteSpace(stored.EncryptedWebhookSecret))
                throw new InvalidOperationException("Stored hospital Razorpay credentials are incomplete.");

            return (stored.KeyId,
                    _credentialStore.Decrypt(stored.EncryptedKeySecret),
                    _credentialStore.Decrypt(stored.EncryptedWebhookSecret), stored.Id);
        }

        var prefix = $"Payments:Hospitals:{hospitalId}:Razorpay:{mode}:";
        var key = _configuration[prefix + "KeyId"];
        var secret = _configuration[prefix + "KeySecret"];
        var webhook = _configuration[prefix + "WebhookSecret"];
        if (string.IsNullOrWhiteSpace(key) ||
            string.IsNullOrWhiteSpace(secret) ||
            string.IsNullOrWhiteSpace(webhook))
            throw new InvalidOperationException(
                $"Razorpay credentials are not configured for HospitalId {hospitalId} ({mode}).");

        return (key, secret, webhook, null);
    }

    private HttpClient Client(int hospitalId, string mode, int? credentialId = null)
    {
        var c = Credentials(hospitalId, mode, credentialId);
        return ClientFromCredentials(c.KeyId, c.Secret);
    }

    public string PublicKey(int hospitalId, string mode) => Credentials(hospitalId, mode).KeyId;

    public string PublicKey(int hospitalId, string mode, int? credentialId) =>
        Credentials(hospitalId, mode, credentialId).KeyId;

    // New payments must use the active, approved merchant and a database credential.
    // Historical verification/refunds/reconciliation continue to use their pinned ID.
    private async Task<(string MerchantName, int CredentialId)> RequireActiveMerchantForNewPaymentAsync(
        int hospitalId, string mode)
    {
        var merchant = await _db.HospitalMerchantSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.HospitalId == hospitalId);
        if (merchant == null || !merchant.IsEnabled ||
            merchant.KycStatus != "Approved" ||
            string.IsNullOrWhiteSpace(merchant.ProviderMerchantAccountId) ||
            string.IsNullOrWhiteSpace(merchant.MerchantName) ||
            merchant.Mode != mode)
            throw new InvalidOperationException(
                "Hospital Razorpay merchant is not approved, enabled, or configured for this mode.");

        var credential = await _db.RazorpayMerchantCredentials.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MerchantType == "Hospital" &&
                x.HospitalId == hospitalId && x.Mode == mode && x.IsActive);
        if (credential == null)
            throw new InvalidOperationException(
                "An active database credential is required for new hospital payments.");

        return (merchant.MerchantName.Trim(), credential.Id);
    }

    // Use this result when creating a new HospitalPaymentOrder, saving both values.
    // The same resolved credentials are used for the remote request and returned ID.
    public async Task<(string GatewayOrderId, int? RazorpayCredentialId)> CreateOrderWithCredentialAsync(
        int hospitalId, string mode, decimal amount, string receipt)
    {
        var merchant = await RequireActiveMerchantForNewPaymentAsync(hospitalId, mode);
        var selected = Credentials(hospitalId, mode, merchant.CredentialId);
        var paise = checked(decimal.ToInt64(amount * 100m));
        if (amount <= 0 || amount * 100m != paise)
            throw new ArgumentOutOfRangeException(nameof(amount));

        using var client = ClientFromCredentials(selected.KeyId, selected.Secret);
        var body = JsonSerializer.Serialize(new { amount = paise, currency = "INR", receipt });
        using var response = await client.PostAsync("orders", new StringContent(body, Encoding.UTF8, "application/json"));
        var jsonText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Razorpay order creation failed (HTTP {(int)response.StatusCode}).");
        using var json = JsonDocument.Parse(jsonText);
        var orderId = json.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Razorpay did not return an order ID.");
        return (orderId, selected.CredentialId);
    }

    // Creates a fixed-amount, single-use Razorpay UPI QR under the selected
    // hospital's pinned merchant credential. This is NOT a payment confirmation.
    // Caller must persist the returned QR ID and associate it with a local
    // payment attempt before displaying it to a patient.
    public async Task<(string QrId, string ImageUrl, string Status, int? CredentialId)>
        CreateFixedAmountQrAsync(
            int hospitalId,
            string mode,
            decimal amount,
            string localPaymentReference,
            DateTime expiresAtUtc,
            int? credentialId = null)
    {
        if (amount <= 0m || amount * 100m != decimal.Truncate(amount * 100m))
            throw new ArgumentOutOfRangeException(nameof(amount), "Use a positive amount with at most two decimals.");
        if (string.IsNullOrWhiteSpace(localPaymentReference) || localPaymentReference.Length > 80)
            throw new ArgumentException("A valid local payment reference is required.", nameof(localPaymentReference));
        if (expiresAtUtc <= DateTime.UtcNow.AddMinutes(1) ||
            expiresAtUtc > DateTime.UtcNow.AddDays(1))
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "QR expiry must be within 24 hours.");

        // This method creates NEW QRs only. A retired credential cannot be used
        // to create a new QR, even if its historical ID is supplied.
        var merchant = await RequireActiveMerchantForNewPaymentAsync(hospitalId, mode);
        if (credentialId.HasValue && credentialId.Value != merchant.CredentialId)
            throw new InvalidOperationException("The selected QR credential is no longer active.");
        var selected = Credentials(hospitalId, mode, merchant.CredentialId);
        var paise = checked(decimal.ToInt64(amount * 100m));
        var closeBy = new DateTimeOffset(expiresAtUtc.ToUniversalTime()).ToUnixTimeSeconds();
        var body = JsonSerializer.Serialize(new
        {
            type = "upi_qr",
            // Razorpay QR name is a dashboard/internal label, not a guaranteed
            // UPI beneficiary display name. The API credentials determine the payee.
            name = $"{merchant.MerchantName[..Math.Min(merchant.MerchantName.Length, 35)]} invoice QR",
            usage = "single_use",
            fixed_amount = true,
            payment_amount = paise,
            description = $"Invoice payment to {merchant.MerchantName[..Math.Min(merchant.MerchantName.Length, 60)]}",
            close_by = closeBy,
            notes = new
            {
                local_payment_reference = localPaymentReference,
                hospital_id = hospitalId.ToString()
            }
        });

        using var client = ClientFromCredentials(selected.KeyId, selected.Secret);
        using var response = await client.PostAsync(
            "payments/qr_codes",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Razorpay QR creation failed (HTTP {(int)response.StatusCode}).");

        using var json = JsonDocument.Parse(responseText);
        var root = json.RootElement;
        var qrId = root.GetProperty("id").GetString();
        var imageUrl = root.GetProperty("image_url").GetString();
        var status = root.GetProperty("status").GetString() ?? "created";
        if (string.IsNullOrWhiteSpace(qrId) || string.IsNullOrWhiteSpace(imageUrl))
            throw new InvalidOperationException("Razorpay QR response did not contain a QR ID and image URL.");
        return (qrId, imageUrl, status, selected.CredentialId);
    }

    private HttpClient ClientFromCredentials(string keyId, string secret)
    {
        var client = _factory.CreateClient();
        client.BaseAddress = new Uri("https://api.razorpay.com/v1/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{secret}")));
        return client;
    }

    // Compatibility method. New callers should use CreateOrderWithCredentialAsync
    // and persist the returned RazorpayCredentialId with the order.
    public async Task<string> CreateOrderAsync(int hospitalId, string mode, decimal amount, string receipt) =>
        (await CreateOrderWithCredentialAsync(hospitalId, mode, amount, receipt)).GatewayOrderId;

    public bool VerifyCheckoutSignature(int hospitalId, string mode, string orderId, string paymentId, string signature, int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(signature)) return false;
        var secret = Credentials(hospitalId, mode, credentialId).Secret;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{orderId}|{paymentId}"));
        try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature)); }
        catch (FormatException) { return false; }
    }

    public bool VerifyWebhookSignature(int hospitalId, string mode, string rawBody, string signature, int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(rawBody) || string.IsNullOrWhiteSpace(signature)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Credentials(hospitalId, mode, credentialId).WebhookSecret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature)); }
        catch (FormatException) { return false; }
    }

    // Independently confirm that a captured payment belongs to the exact QR
    // created under this hospital merchant credential. Never trust webhook
    // payload identifiers alone to allocate money to an invoice.
    public async Task<bool> VerifyCapturedQrPaymentAsync(
        int hospitalId, string mode, string gatewayQrId, string gatewayPaymentId,
        decimal expectedAmount, int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(gatewayQrId) ||
            string.IsNullOrWhiteSpace(gatewayPaymentId) ||
            expectedAmount <= 0m ||
            expectedAmount * 100m != decimal.Truncate(expectedAmount * 100m))
            return false;

        var payment = await FetchPaymentAsync(hospitalId, mode, gatewayPaymentId, credentialId);
        if (!payment.Captured ||
            !string.Equals(payment.Currency, "INR", StringComparison.Ordinal) ||
            payment.AmountPaise != checked(decimal.ToInt64(expectedAmount * 100m)))
            return false;

        using var client = Client(hospitalId, mode, credentialId);
        using var response = await client.GetAsync(
            $"payments/qr_codes/{Uri.EscapeDataString(gatewayQrId)}/payments?count=100");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Unable to verify Razorpay QR payment association (HTTP {(int)response.StatusCode}).");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Razorpay QR payments response has no items array.");

        foreach (var item in items.EnumerateArray())
        {
            // Razorpay QR payment association responses may contain a payment
            // identifier directly or a nested payment entity.
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            string? id = null;
            if (item.TryGetProperty("payment_id", out var pid) && pid.ValueKind == JsonValueKind.String)
                id = pid.GetString();
            if (id == null && item.TryGetProperty("id", out var directId) && directId.ValueKind == JsonValueKind.String)
                id = directId.GetString();
            if (id == null && item.TryGetProperty("payment", out var nested) &&
                nested.ValueKind == JsonValueKind.Object &&
                nested.TryGetProperty("id", out var nestedId) && nestedId.ValueKind == JsonValueKind.String)
                id = nestedId.GetString();
            if (string.Equals(id, gatewayPaymentId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    // Read-only Razorpay lookup. The returned payment identity must match the
    // requested identity; malformed or incomplete gateway responses fail closed.
    // Keep this public signature for existing callers.
    public async Task<(bool Captured, string OrderId, long AmountPaise, string Currency, string Status)> FetchPaymentAsync(
        int hospitalId, string mode, string paymentId, int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ArgumentException("Razorpay payment ID is required.", nameof(paymentId));

        paymentId = paymentId.Trim();
        using var client = Client(hospitalId, mode, credentialId);
        using var response = await client.GetAsync($"payments/{Uri.EscapeDataString(paymentId)}");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Unable to fetch Razorpay payment (HTTP {(int)response.StatusCode}).");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("id", out var idElement) ||
            idElement.ValueKind != JsonValueKind.String ||
            !string.Equals(idElement.GetString(), paymentId, StringComparison.Ordinal))
            throw new InvalidOperationException("Razorpay returned a mismatched payment identity.");

        if (!root.TryGetProperty("status", out var statusElement) ||
            statusElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(statusElement.GetString()) ||
            !root.TryGetProperty("amount", out var amountElement) ||
            amountElement.ValueKind != JsonValueKind.Number ||
            !amountElement.TryGetInt64(out var amount) || amount <= 0 ||
            !root.TryGetProperty("currency", out var currencyElement) ||
            currencyElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(currencyElement.GetString()))
            throw new InvalidOperationException("Razorpay payment response is incomplete or invalid.");

        var status = statusElement.GetString()!;
        var currency = currencyElement.GetString()!;
        var orderId = root.TryGetProperty("order_id", out var orderElement) &&
                      orderElement.ValueKind == JsonValueKind.String
            ? orderElement.GetString() ?? ""
            : "";

        return (string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase),
            orderId, amount, currency, status);
    }

    // Step 9: Verify an existing order payment without changing any records.
    // Pass the order's ORIGINAL pinned credential ID, if one was stored.
    // A false result means NOT verified; it does not prove the payment failed.
    public async Task<bool> VerifyCapturedOrderPaymentAsync(
        int hospitalId,
        string mode,
        string gatewayOrderId,
        string gatewayPaymentId,
        decimal expectedAmount,
        int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(gatewayOrderId) ||
            string.IsNullOrWhiteSpace(gatewayPaymentId) ||
            expectedAmount <= 0m ||
            expectedAmount * 100m != decimal.Truncate(expectedAmount * 100m))
            return false;

        var expectedPaise = checked(decimal.ToInt64(expectedAmount * 100m));
        var payment = await FetchPaymentAsync(
            hospitalId, mode, gatewayPaymentId.Trim(), credentialId);

        return payment.Captured &&
               string.Equals(payment.OrderId, gatewayOrderId.Trim(), StringComparison.Ordinal) &&
               string.Equals(payment.Currency, "INR", StringComparison.Ordinal) &&
               payment.AmountPaise == expectedPaise;
    }

    public async Task<List<RazorpaySettlementSummary>> FetchSettlementsAsync(int hospitalId, string mode, DateTime fromUtc, DateTime toUtc, int? credentialId = null)
    {
        if (toUtc <= fromUtc || (toUtc - fromUtc).TotalDays > 93) throw new ArgumentException("Settlement sync range must be 1-93 days.");
        using var client = Client(hospitalId, mode, credentialId);
        var results = new List<RazorpaySettlementSummary>();
        var from = new DateTimeOffset(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var to = new DateTimeOffset(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        for (var skip = 0; skip < 1000; skip += 100)
        {
            using var response = await client.GetAsync($"settlements?from={from}&to={to}&count=100&skip={skip}");
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay settlement fetch failed (HTTP {(int)response.StatusCode}).");
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) break;
            foreach (var item in items.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                long ReadLong(string name) => item.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : 0;
                var created = ReadLong("created_at");
                results.Add(new RazorpaySettlementSummary(id, item.TryGetProperty("status", out var st) ? st.GetString() ?? "unknown" : "unknown", ReadLong("amount") / 100m, ReadLong("fees") / 100m, ReadLong("tax") / 100m, item.TryGetProperty("utr", out var utr) ? utr.GetString() : null, created > 0 ? DateTimeOffset.FromUnixTimeSeconds(created).UtcDateTime : fromUtc));
            }
            if (items.GetArrayLength() < 100) break;
        }
        return results;
    }

    public async Task<string> UploadDisputeDocumentAsync(int hospitalId, string mode, Stream stream, string fileName, string contentType, int? credentialId = null)
    {
        using var client = Client(hospitalId, mode, credentialId);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("dispute_evidence"), "purpose");
        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", Path.GetFileName(fileName));
        using var response = await client.PostAsync("documents", form);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay dispute document upload failed (HTTP {(int)response.StatusCode}).");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Razorpay did not return a document ID.");
    }

    public async Task<(string Status, string PayloadJson)> ContestDisputeAsync(int hospitalId, string mode, string disputeId, decimal amount, string summary, string evidenceType, IReadOnlyList<string> documentIds, int? credentialId = null)
    {
        var paise = checked(decimal.ToInt64(amount * 100m));
        if (amount <= 0 || amount * 100m != paise || documentIds.Count == 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var body = new Dictionary<string, object> { ["amount"] = paise, ["summary"] = summary, [evidenceType] = documentIds, ["action"] = "submit" };
        using var client = Client(hospitalId, mode, credentialId);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"disputes/{Uri.EscapeDataString(disputeId)}/contest") { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay dispute contest failed (HTTP {(int)response.StatusCode}).");
        using var doc = JsonDocument.Parse(text);
        return (doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() ?? "under_review" : "under_review", text);
    }

    public async Task<(string Status, string PayloadJson)> AcceptDisputeAsync(int hospitalId, string mode, string disputeId, int? credentialId = null)
    {
        using var client = Client(hospitalId, mode, credentialId);
        using var response = await client.PostAsync($"disputes/{Uri.EscapeDataString(disputeId)}/accept", null);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay dispute acceptance failed (HTTP {(int)response.StatusCode}).");
        using var doc = JsonDocument.Parse(text);
        return (doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() ?? "lost" : "lost", text);
    }

    // Read-only lookup: use this to reconcile a refund whose POST response was lost.
    // Never create a second refund merely because this lookup fails.
    public async Task<(string RefundId, string PaymentId, long AmountPaise, string Currency, string Status)> FetchRefundAsync(
        int hospitalId, string mode, string refundId, int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(refundId))
            throw new ArgumentException("Razorpay refund ID is required.", nameof(refundId));

        using var client = Client(hospitalId, mode, credentialId);
        using var response = await client.GetAsync($"refunds/{Uri.EscapeDataString(refundId)}");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Unable to fetch Razorpay refund (HTTP {(int)response.StatusCode}).");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return ParseRefund(document.RootElement);
    }

    // Read-only list of refunds associated with the ORIGINAL gateway payment.
    // Callers must match the candidate to their local refund using amount,
    // merchant mode, payment ID and other reliable evidence before linking it.
    // Multiple same-amount partial refunds can be ambiguous: require manual review.
    public async Task<List<(string RefundId, string PaymentId, long AmountPaise, string Currency, string Status)>> FetchPaymentRefundsAsync(
        int hospitalId, string mode, string paymentId, int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ArgumentException("Razorpay payment ID is required.", nameof(paymentId));

        using var client = Client(hospitalId, mode, credentialId);
        var results = new List<(string RefundId, string PaymentId, long AmountPaise, string Currency, string Status)>();
        const int pageSize = 100;
        const int maxPages = 100;

        for (var page = 0; page < maxPages; page++)
        {
            using var response = await client.GetAsync(
                $"payments/{Uri.EscapeDataString(paymentId)}/refunds?count={pageSize}&skip={page * pageSize}");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Unable to list Razorpay payment refunds (HTTP {(int)response.StatusCode}).");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Razorpay returned an invalid refund list.");

            foreach (var item in items.EnumerateArray())
            {
                var refund = ParseRefund(item);
                if (refund.PaymentId != paymentId)
                    throw new InvalidOperationException("Razorpay returned a refund for a different payment.");
                results.Add(refund);
            }

            if (items.GetArrayLength() < pageSize)
                return results;
        }

        throw new InvalidOperationException("Razorpay refund list exceeded the configured pagination limit; reconciliation is incomplete.");
    }

    private static (string RefundId, string PaymentId, long AmountPaise, string Currency, string Status)
        ParseRefund(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Razorpay refund response is not an object.");

        static string RequiredString(JsonElement source, string property)
        {
            if (!source.TryGetProperty(property, out var value) ||
                value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
                throw new InvalidOperationException($"Razorpay refund response is missing {property}.");
            return value.GetString()!;
        }

        var refundId = RequiredString(element, "id");
        var paymentId = RequiredString(element, "payment_id");
        var status = RequiredString(element, "status");
        if (!element.TryGetProperty("amount", out var amountElement) ||
            amountElement.ValueKind != JsonValueKind.Number ||
            !amountElement.TryGetInt64(out var amountPaise) || amountPaise <= 0)
            throw new InvalidOperationException("Razorpay refund response has an invalid amount.");

        // Razorpay refund responses may omit currency. The parent payment's
        // currency must be verified separately before financial reconciliation.
        var currency = element.TryGetProperty("currency", out var currencyElement) &&
                       currencyElement.ValueKind == JsonValueKind.String
            ? currencyElement.GetString() ?? ""
            : "";

        return (refundId, paymentId, amountPaise, currency, status);
    }

    public async Task<(string RefundId, string Status)> RefundAsync(int hospitalId, string mode, string paymentId, decimal amount, string? reason, int? credentialId = null)
    {
        var paise = checked(decimal.ToInt64(amount * 100m));
        if (amount <= 0 || amount * 100m != paise) throw new ArgumentOutOfRangeException(nameof(amount));
        using var client = Client(hospitalId, mode, credentialId);
        var body = JsonSerializer.Serialize(new { amount = paise, notes = new { reason = reason ?? "Hospital initiated refund" } });
        using var response = await client.PostAsync($"payments/{Uri.EscapeDataString(paymentId)}/refund", new StringContent(body, Encoding.UTF8, "application/json"));
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay refund failed (HTTP {(int)response.StatusCode}).");
        using var doc = JsonDocument.Parse(text);
        var refund = ParseRefund(doc.RootElement);
        if (refund.PaymentId != paymentId || refund.AmountPaise != paise)
            throw new InvalidOperationException("Razorpay refund response does not match the requested payment and amount. Reconciliation required.");
        return (refund.RefundId, refund.Status);
    }
    // Razorpay combined settlement reconciliation report; all amounts in paise.
    public sealed record HospitalSettlementTransactionEvidence(
        string SettlementId, string EntityId, string EntityType,
        long AmountPaise, long FeePaise, long TaxPaise,
        string? GatewayTransactionId, string? Currency, DateTime? CreatedAtUtc,
        long DebitPaise, long CreditPaise);

    // The official combined report is date-scoped, not settlement-ID scoped.
    // Fetch every page for the settlement's settlement date and filter locally.
    public async Task<List<HospitalSettlementTransactionEvidence>> FetchSettlementTransactionsAsync(
        int hospitalId, string mode, string settlementId, DateTime settledAtUtc,
        int? credentialId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settlementId))
            throw new ArgumentException("Settlement ID is required.", nameof(settlementId));
        if (settledAtUtc == default)
            throw new ArgumentException("Verified settlement date is required.", nameof(settledAtUtc));
        if (!_configuration.GetValue<bool>("Payments:HospitalSettlementReconciliation:GatewayEvidenceVerified"))
            throw new InvalidOperationException("Gateway evidence is disabled pending merchant-specific verification.");
        if (credentialId is null or <= 0)
            throw new InvalidOperationException("Historical merchant credential pin is required.");

        using var client = Client(hospitalId, mode, credentialId);
        // Use conservative pagination until the merchant's report contract is verified.
        const int pageSize = 100;
        const int maxPages = 100;
        var results = new List<HospitalSettlementTransactionEvidence>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (settledAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Settlement timestamp must be explicitly UTC.");
        // Report dates may be merchant-local rather than UTC. Verify the reporting
        // timezone and day boundary before enabling automatic reconciliation.
        var date = settledAtUtc.Date;
        for (int page = 0; page < maxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = $"settlements/recon/combined?year={date.Year}&month={date.Month}&day={date.Day}&count={pageSize}&skip={page * pageSize}";
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Razorpay settlement reconciliation request failed (HTTP {(int)response.StatusCode}).");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array ||
                items.GetArrayLength() > pageSize)
                throw new InvalidOperationException("Invalid or incomplete Razorpay reconciliation page.");

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("Invalid Razorpay reconciliation item.");
                static string RequiredString(JsonElement element, string key)
                {
                    if (!element.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(v.GetString()))
                        throw new InvalidOperationException($"Missing reconciliation field: {key}.");
                    return v.GetString()!;
                }
                static long RequiredLong(JsonElement element, string key)
                {
                    if (!element.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt64(out var result))
                        throw new InvalidOperationException($"Invalid reconciliation field: {key}.");
                    return result;
                }
                var actualSettlementId = RequiredString(item, "settlement_id");
                if (!string.Equals(actualSettlementId, settlementId, StringComparison.Ordinal))
                    continue;
                var entityId = RequiredString(item, "entity_id");
                var type = RequiredString(item, "type");
                var amount = RequiredLong(item, "amount");
                var fee = RequiredLong(item, "fee");
                var tax = RequiredLong(item, "tax");
                var debit = RequiredLong(item, "debit");
                var credit = RequiredLong(item, "credit");
                if (amount < 0 || fee < 0 || tax < 0 || debit < 0 || credit < 0 ||
                    (debit > 0 && credit > 0))
                    throw new InvalidOperationException("Invalid signed settlement evidence; manual review required.");
                var identity = $"{actualSettlementId}:{type}:{entityId}";
                if (!seen.Add(identity))
                    throw new InvalidOperationException("Duplicate settlement entry; manual review required.");
                string? currency = item.TryGetProperty("currency", out var cv) && cv.ValueKind == JsonValueKind.String ? cv.GetString() : null;
                DateTime? created = null;
                if (item.TryGetProperty("created_at", out var ts) && ts.ValueKind == JsonValueKind.Number &&
                    ts.TryGetInt64(out var seconds) && seconds > 0)
                    created = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
                results.Add(new HospitalSettlementTransactionEvidence(
                    actualSettlementId, entityId, type, amount, fee, tax,
                    null, currency, created, debit, credit));
            }
            if (items.GetArrayLength() < pageSize)
                return results;
        }
        throw new InvalidOperationException("Reconciliation pagination safety limit reached; results incomplete.");
    }
}
