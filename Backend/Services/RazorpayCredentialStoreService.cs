
using Microsoft.AspNetCore.DataProtection;

namespace AuthDemo.Services;

public class RazorpayCredentialStore
{
    private readonly IDataProtector _protector;

    public RazorpayCredentialStore(
        IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(
            "RazorpayMerchantCredentials.v1");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new ArgumentException(
                "Credential cannot be empty.");
        }

        return _protector.Protect(plainText);
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
        {
            throw new ArgumentException(
                "Encrypted credential cannot be empty.");
        }

        return _protector.Unprotect(encryptedText);
    }
}
