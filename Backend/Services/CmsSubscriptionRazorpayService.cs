
using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

// =====================================================
// PAYMENTS - CMS OWNER RAZORPAY GATEWAY SERVICE
//
// Used ONLY for CMS subscription payments.
// Never use hospital merchant credentials here.
// =====================================================

public sealed class CmsSubscriptionRazorpayService
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly RazorpayCredentialStore _credentialStore;

    public CmsSubscriptionRazorpayService(
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

    // Explicit credential IDs may reference retired versions for existing purchases.
    // Null retains the legacy active-credential/configuration behavior.
    private (string KeyId, string KeySecret, int? CredentialId)
        ResolveCredentials(string mode, int? credentialId = null)
    {
        if (mode != "Test" && mode != "Live")
            throw new InvalidOperationException("Invalid Razorpay mode.");

        var query = _db.RazorpayMerchantCredentials.AsNoTracking();
        var saved = credentialId.HasValue
            ? query.SingleOrDefault(x => x.Id == credentialId.Value)
            : query.SingleOrDefault(x =>
                x.MerchantType == "CMS" &&
                x.HospitalId == null &&
                x.Mode == mode &&
                x.IsActive);

        if (credentialId.HasValue && saved == null)
            throw new InvalidOperationException("CMS Razorpay credential version was not found.");

        if (saved != null)
        {
            if (saved.MerchantType != "CMS" ||
                saved.HospitalId != null ||
                saved.Mode != mode)
                throw new InvalidOperationException(
                    "Razorpay credential does not belong to the requested CMS merchant and mode.");

            if (string.IsNullOrWhiteSpace(saved.KeyId) ||
                string.IsNullOrWhiteSpace(saved.EncryptedKeySecret))
                throw new InvalidOperationException("Stored CMS Razorpay credentials are incomplete.");

            return (saved.KeyId,
                _credentialStore.Decrypt(saved.EncryptedKeySecret), saved.Id);
        }

        // Do not silently fall back to legacy credentials if this mode has
        // stored CMS credentials that are currently inactive or retired.
        if (_db.RazorpayMerchantCredentials.AsNoTracking().Any(x =>
                x.MerchantType == "CMS" &&
                x.HospitalId == null &&
                x.Mode == mode))
            throw new InvalidOperationException(
                "No active stored CMS Razorpay credentials are available for this mode.");

        // Legacy configuration fallback only when no stored CMS credential exists.
        var prefix = $"Payments:CmsOwner:Razorpay:{mode}:";
        var keyId = _configuration[prefix + "KeyId"];
        var secret = _configuration[prefix + "KeySecret"];
        if (string.IsNullOrWhiteSpace(keyId) ||
            string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("CMS owner Razorpay credentials are missing.");

        return (keyId, secret, null);
    }

    private (string KeyId, string KeySecret) GetCredentials(
        string mode, int? credentialId = null)
    {
        var credentials = ResolveCredentials(mode, credentialId);
        return (credentials.KeyId, credentials.KeySecret);
    }

    private HttpClient CreateClient(string mode, int? credentialId = null) =>
        CreateClient(GetCredentials(mode, credentialId));

    private HttpClient CreateClient((string KeyId, string KeySecret) credentials)
    {
        var client = _factory.CreateClient();

        client.BaseAddress =
            new Uri("https://api.razorpay.com/v1/");

        var encoded = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{credentials.KeyId}:{credentials.KeySecret}"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", encoded);

        return client;
    }

    public string GetPublicKeyId(string mode, int? credentialId = null) =>
        GetCredentials(mode, credentialId).KeyId;

    // =================================================
    // PAYMENTS - CREATE CMS SUBSCRIPTION ORDER
    // =================================================

    public async Task<(string OrderId, string KeyId, int? CredentialId)>
        CreateOrderWithCredentialAsync(
            decimal amount,
            string receipt,
            string mode)
    {
        if (amount <= 0)
            throw new ArgumentException(
                "Payment amount must be positive.");

        var amountPaise = checked(
            decimal.ToInt64(amount * 100m));

        if (amount * 100m != amountPaise)
            throw new ArgumentException(
                "Invalid payment precision.");

        var credentials = ResolveCredentials(mode);

        using var client = CreateClient(
    (credentials.KeyId, credentials.KeySecret));

        var request = new
        {
            amount = amountPaise,
            currency = "INR",
            receipt = receipt
        };

        using var response = await client.PostAsync(
            "orders",
            new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"));

        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var orderId =
            json.RootElement.GetProperty("id").GetString();

        if (string.IsNullOrWhiteSpace(orderId))
            throw new InvalidOperationException(
                "Razorpay returned an invalid order.");

        return (orderId, credentials.KeyId, credentials.CredentialId);
    }

    // Backward-compatible method for existing callers.
    public async Task<(string OrderId, string KeyId)> CreateOrderAsync(
        decimal amount, string receipt, string mode)
    {
        var result = await CreateOrderWithCredentialAsync(amount, receipt, mode);
        return (result.OrderId, result.KeyId);
    }

    // =================================================
    // PAYMENTS - VERIFY RAZORPAY CHECKOUT SIGNATURE
    // =================================================

    public bool VerifySignature(
        string orderId,
        string paymentId,
        string signature,
        string mode,
        int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(orderId) ||
            string.IsNullOrWhiteSpace(paymentId) ||
            string.IsNullOrWhiteSpace(signature))
            return false;

        var secret = GetCredentials(mode, credentialId).KeySecret;

        var message = $"{orderId}|{paymentId}";

        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(secret));

        var expected = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(message));

        byte[] supplied;

        try
        {
            supplied = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            expected, supplied);
    }

    // =================================================
    // PAYMENTS - VERIFY CAPTURED PAYMENT WITH RAZORPAY
    // =================================================

    public async Task<bool> IsCapturedPaymentAsync(
        string paymentId,
        string expectedOrderId,
        decimal expectedAmount,
        string mode,
        int? credentialId = null)
    {
        using var client = CreateClient(mode, credentialId);

        using var response = await client.GetAsync(
            "payments/" + Uri.EscapeDataString(paymentId));

        if (!response.IsSuccessStatusCode)
            return false;

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var payment = json.RootElement;

        var status = payment.GetProperty("status").GetString();
        var orderId = payment.GetProperty("order_id").GetString();
        var currency = payment.GetProperty("currency").GetString();
        var amount = payment.GetProperty("amount").GetInt64();

        var expectedPaise =
            decimal.ToInt64(expectedAmount * 100m);

        return status == "captured" &&
               orderId == expectedOrderId &&
               currency == "INR" &&
               amount == expectedPaise;
    }
    public async Task<List<RazorpaySettlementSummary>> FetchSettlementsAsync(string mode, DateTime fromUtc, DateTime toUtc, int? credentialId = null)
    {
        if (toUtc <= fromUtc || (toUtc - fromUtc).TotalDays > 93) throw new ArgumentException("Settlement sync range must be 1-93 days.");
        using var client = CreateClient(mode, credentialId);
        var results = new List<RazorpaySettlementSummary>();
        var from = new DateTimeOffset(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var to = new DateTimeOffset(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        for (var skip = 0; skip < 1000; skip += 100)
        {
            using var response = await client.GetAsync($"settlements?from={from}&to={to}&count=100&skip={skip}");
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay settlement fetch failed: {text}");
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

    public async Task<string> UploadDisputeDocumentAsync(string mode, Stream stream, string fileName, string contentType, int? credentialId = null)
    {
        using var client = CreateClient(mode, credentialId);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("dispute_evidence"), "purpose");
        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", Path.GetFileName(fileName));
        using var response = await client.PostAsync("documents", form);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay dispute document upload failed: {text}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Razorpay did not return a document ID.");
    }

    public async Task<(string Status, string PayloadJson)> ContestDisputeAsync(string mode, string disputeId, decimal amount, string summary, string evidenceType, IReadOnlyList<string> documentIds, int? credentialId = null)
    {
        var paise = checked(decimal.ToInt64(amount * 100m));
        if (amount <= 0 || amount * 100m != paise || documentIds.Count == 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var body = new Dictionary<string, object> { ["amount"] = paise, ["summary"] = summary, [evidenceType] = documentIds, ["action"] = "submit" };
        using var client = CreateClient(mode, credentialId);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"disputes/{Uri.EscapeDataString(disputeId)}/contest") { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay dispute contest failed: {text}");
        using var doc = JsonDocument.Parse(text);
        return (doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() ?? "under_review" : "under_review", text);
    }

    public async Task<(string Status, string PayloadJson)> AcceptDisputeAsync(string mode, string disputeId, int? credentialId = null)
    {
        using var client = CreateClient(mode, credentialId);
        using var response = await client.PostAsync($"disputes/{Uri.EscapeDataString(disputeId)}/accept", null);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay dispute acceptance failed: {text}");
        using var doc = JsonDocument.Parse(text);
        return (doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() ?? "lost" : "lost", text);
    }

    public async Task<(string RefundId, string Status)> RefundAsync(
        string paymentId, decimal amount, string mode, string? reason, int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ArgumentException("Gateway payment ID is required.", nameof(paymentId));

        if (amount <= 0 || amount * 100m != decimal.Truncate(amount * 100m))
            throw new ArgumentOutOfRangeException(nameof(amount),
                "Refund amount must be positive and have at most two decimal places.");

        var paise = checked(decimal.ToInt64(amount * 100m));
        using var client = CreateClient(mode, credentialId);
        var body = JsonSerializer.Serialize(new
        {
            amount = paise,
            notes = new { reason = reason ?? "CMS subscription refund" }
        });

        // An HTTP timeout or lost response does not prove Razorpay rejected
        // the request. The caller must reconcile before attempting a retry.
        using var response = await client.PostAsync(
            $"payments/{Uri.EscapeDataString(paymentId)}/refund",
            new StringContent(body, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Razorpay refund request returned HTTP {(int)response.StatusCode}; " +
                "check the gateway and reconcile before retrying.");

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        if (!root.TryGetProperty("id", out var refundIdElement) ||
            refundIdElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(refundIdElement.GetString()))
            throw new InvalidOperationException(
                "Razorpay refund ID missing; reconcile before retrying.");

        if (!root.TryGetProperty("payment_id", out var paymentIdElement) ||
            paymentIdElement.ValueKind != JsonValueKind.String ||
            paymentIdElement.GetString() != paymentId)
            throw new InvalidOperationException(
                "Razorpay refund payment ID mismatch; reconcile before retrying.");

        if (!root.TryGetProperty("amount", out var amountElement) ||
            !amountElement.TryGetInt64(out var returnedPaise) ||
            returnedPaise != paise)
            throw new InvalidOperationException(
                "Razorpay refund amount mismatch; reconcile before retrying.");

        var status = root.TryGetProperty("status", out var statusElement) &&
                     statusElement.ValueKind == JsonValueKind.String
            ? statusElement.GetString() ?? "pending"
            : "pending";

        return (refundIdElement.GetString()!, status);
    }


    // =================================================
    // CMS REFUND RECONCILIATION - READ ONLY GATEWAY CALLS
    // =================================================
    // These methods never create a refund. Always use the credential version
    // pinned to the original CMS payment/order when one is available.
    public async Task<(string RefundId, string PaymentId, long AmountPaise,
        string? Currency, string Status)> FetchRefundAsync(
        string refundId, string mode, int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(refundId))
            throw new ArgumentException("Refund ID is required.", nameof(refundId));

        using var client = CreateClient(mode, credentialId);
        using var response = await client.GetAsync(
            "refunds/" + Uri.EscapeDataString(refundId));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"CMS Razorpay refund lookup returned HTTP {(int)response.StatusCode}.");

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var refund = ParseGatewayRefund(document.RootElement);
        if (!string.Equals(refund.RefundId, refundId, StringComparison.Ordinal))
            throw new InvalidOperationException("Razorpay returned a different refund ID.");
        return refund;
    }

    public async Task<List<(string RefundId, string PaymentId, long AmountPaise,
        string? Currency, string Status)>> FetchPaymentRefundsAsync(
        string paymentId, string mode, int? credentialId = null)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ArgumentException("Payment ID is required.", nameof(paymentId));

        using var client = CreateClient(mode, credentialId);
        var results = new List<(string RefundId, string PaymentId,
            long AmountPaise, string? Currency, string Status)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        const int pageSize = 100;
        const int maxPages = 100;

        for (var page = 0; page < maxPages; page++)
        {
            var skip = page * pageSize;
            using var response = await client.GetAsync(
                $"payments/{Uri.EscapeDataString(paymentId)}/refunds?count={pageSize}&skip={skip}");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"CMS Razorpay payment refunds lookup returned HTTP {(int)response.StatusCode}.");

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Razorpay refund list is missing items.");

            foreach (var item in items.EnumerateArray())
            {
                var refund = ParseGatewayRefund(item);
                if (!string.Equals(refund.PaymentId, paymentId, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Razorpay returned a refund for another payment.");
                if (!seen.Add(refund.RefundId))
                    throw new InvalidOperationException(
                        "Razorpay returned a duplicate refund across pages.");
                results.Add(refund);
            }

            if (items.GetArrayLength() < pageSize)
                return results;
        }

        throw new InvalidOperationException(
            "Razorpay refund list exceeded the pagination limit; manual review required.");
    }

    private static (string RefundId, string PaymentId, long AmountPaise,
        string? Currency, string Status) ParseGatewayRefund(JsonElement element)
    {
        static string RequiredString(JsonElement item, string property)
        {
            if (!item.TryGetProperty(property, out var value) ||
                value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
                throw new InvalidOperationException(
                    $"Razorpay refund response has an invalid {property}.");
            return value.GetString()!;
        }

        var refundId = RequiredString(element, "id");
        var paymentId = RequiredString(element, "payment_id");
        var status = RequiredString(element, "status");
        if (!element.TryGetProperty("amount", out var amountElement) ||
            !amountElement.TryGetInt64(out var amountPaise) || amountPaise <= 0)
            throw new InvalidOperationException("Razorpay refund amount is invalid.");

        string? currency = null;
        if (element.TryGetProperty("currency", out var currencyElement) &&
            currencyElement.ValueKind == JsonValueKind.String)
            currency = currencyElement.GetString();

        return (refundId, paymentId, amountPaise, currency, status);
    }

}
