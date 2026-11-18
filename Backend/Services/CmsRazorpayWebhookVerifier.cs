using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AuthDemo.Services;

// Verifies CMS-owner Razorpay webhooks only; never uses hospital secrets.
public sealed class CmsRazorpayWebhookVerifier
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly RazorpayCredentialStore _credentialStore;

    public CmsRazorpayWebhookVerifier(
        IConfiguration configuration,
        AppDbContext db,
        RazorpayCredentialStore credentialStore)
    {
        _configuration = configuration;
        _db = db;
        _credentialStore = credentialStore;
    }

    // Preserve the existing public signature for controller callers.
    public bool Verify(string rawBody, string signature, string mode)
    {
        if (string.IsNullOrWhiteSpace(rawBody) ||
            string.IsNullOrWhiteSpace(signature) ||
            (mode != "Test" && mode != "Live"))
            return false;

        byte[] received;
        try
        {
            received = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        if (received.Length != 32)
            return false;

        // Allow recent retired webhook secrets for delayed Razorpay deliveries.
        var gracePeriodStart = DateTime.UtcNow.AddDays(-7);
        var stored = _db.RazorpayMerchantCredentials
            .AsNoTracking()
            .Where(x => x.MerchantType == "CMS" &&
                        x.HospitalId == null &&
                        x.Mode == mode &&
                        (x.IsActive ||
                         (x.RetiredAt.HasValue &&
                          x.RetiredAt.Value >= gracePeriodStart)))
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => x.EncryptedWebhookSecret)
            .ToList();

        if (stored.Count > 0)
        {
            foreach (var encryptedSecret in stored)
            {
                // A malformed stored secret must not authorize a webhook.
                if (string.IsNullOrWhiteSpace(encryptedSecret))
                    return false;

                string secret;
                try
                {
                    secret = _credentialStore.Decrypt(encryptedSecret);
                }
                catch (CryptographicException)
                {
                    // Fail closed; never switch to legacy configuration.
                    return false;
                }

                if (string.IsNullOrWhiteSpace(secret))
                    return false;

                if (Matches(rawBody, received, secret))
                    return true;
            }

            return false;
        }

        // Legacy configuration is allowed only if no CMS credential for
        // this mode has ever been stored in the database.
        var hasAnyStored = _db.RazorpayMerchantCredentials
            .AsNoTracking()
            .Any(x => x.MerchantType == "CMS" &&
                      x.HospitalId == null &&
                      x.Mode == mode);

        if (hasAnyStored)
            return false;

        var legacySecret = _configuration[
            $"Payments:CmsOwner:Razorpay:{mode}:WebhookSecret"];

        return !string.IsNullOrWhiteSpace(legacySecret) &&
               Matches(rawBody, received, legacySecret);
    }

    private static bool Matches(string rawBody, byte[] received, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }
}
