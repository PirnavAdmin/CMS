using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Roles = "SuperAdmin")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private const string MaskedSecret = "********";

    public SettingsController(AppDbContext context)
    {
        _context = context;
    }

    private async Task<Setting> GetOrCreateSetting()
    {
        var setting = await _context.Settings.FirstOrDefaultAsync();

        if (setting != null)
            return setting;

        setting = new Setting
        {
            UpdatedAt = DateTime.UtcNow
        };

        _context.Settings.Add(setting);
        await _context.SaveChangesAsync();

        return setting;
    }

    private static bool HasText(string? value) =>
        !string.IsNullOrWhiteSpace(value);

    private static bool IsMaskedSecret(string? value) =>
        string.Equals(value?.Trim(), MaskedSecret, StringComparison.Ordinal);

    private static string MaskSecret(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : MaskedSecret;

    // =====================================================
    // GET ALL
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var setting = await _context.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (setting == null)
        {
            return Ok(new
            {
                general = (object?)null,
                email = (object?)null,
                sms = (object?)null,
                payment = (object?)null
            });
        }

        return Ok(new
        {
            general = BuildGeneralResponse(setting),
            email = BuildEmailResponse(setting),
            sms = BuildSmsResponse(setting),
            payment = BuildPaymentResponse(setting)
        });
    }

    // =====================================================
    // GENERAL SETTINGS
    // =====================================================

    [HttpGet("general")]
    public async Task<IActionResult> GetGeneral()
    {
        var setting = await _context.Settings.AsNoTracking().FirstOrDefaultAsync();

        if (setting == null || string.IsNullOrWhiteSpace(setting.ApplicationName))
            return NotFound(new { message = "General settings not found." });

        return Ok(BuildGeneralResponse(setting));
    }

    [HttpPost("general")]
    public async Task<IActionResult> CreateGeneral(GeneralSettingsRequest model)
    {
        var setting = await GetOrCreateSetting();

        if (!string.IsNullOrWhiteSpace(setting.ApplicationName))
            return BadRequest(new { message = "General settings already exist. Use PUT to edit." });

        ApplyGeneral(setting, model);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "General settings created successfully.",
            settings = BuildGeneralResponse(setting)
        });
    }

    [HttpPut("general")]
    public async Task<IActionResult> UpdateGeneral(GeneralSettingsRequest model)
    {
        var setting = await GetOrCreateSetting();

        ApplyGeneral(setting, model);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "General settings updated successfully.",
            settings = BuildGeneralResponse(setting)
        });
    }

    [HttpDelete("general")]
    public async Task<IActionResult> DeleteGeneral()
    {
        var setting = await _context.Settings.FirstOrDefaultAsync();

        if (setting == null)
            return NotFound(new { message = "General settings not found." });

        setting.ApplicationName = null;
        setting.Timezone = null;
        setting.Currency = null;
        setting.GeneralStatus = null;
        setting.ConfigurationNotes = null;
        setting.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "General settings deleted successfully." });
    }

    // =====================================================
    // EMAIL SETTINGS
    // =====================================================

    [HttpGet("email")]
    public async Task<IActionResult> GetEmail()
    {
        var setting = await _context.Settings.AsNoTracking().FirstOrDefaultAsync();

        if (setting == null || !HasEmailSettings(setting))
            return NotFound(new { message = "Email settings not found." });

        return Ok(BuildEmailResponse(setting));
    }

    [HttpPost("email")]
    public async Task<IActionResult> CreateEmail(EmailSettingsRequest model)
    {
        var setting = await GetOrCreateSetting();

        if (HasEmailSettings(setting))
            return BadRequest(new { message = "Email settings already exist. Use PUT to edit." });

        ApplyEmail(setting, model, false);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Email settings created successfully.",
            settings = BuildEmailResponse(setting)
        });
    }

    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmail(EmailSettingsRequest model)
    {
        var setting = await GetOrCreateSetting();

        ApplyEmail(setting, model, true);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Email settings updated successfully.",
            settings = BuildEmailResponse(setting)
        });
    }

    [HttpDelete("email")]
    public async Task<IActionResult> DeleteEmail()
    {
        var setting = await _context.Settings.FirstOrDefaultAsync();

        if (setting == null || !HasEmailSettings(setting))
            return NotFound(new { message = "Email settings not found." });

        setting.SenderName = null;
        setting.FromEmail = null;
        setting.SmtpHost = null;
        setting.SmtpPort = null;
        setting.SmtpUsername = null;
        setting.SmtpPassword = null;
        setting.EmailStatus = null;
        setting.EmailConfigurationNotes = null;
        setting.EmailNotificationsEnabled = false;
        setting.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Email settings deleted successfully." });
    }

    // =====================================================
    // SMS SETTINGS
    // =====================================================

    [HttpGet("sms")]
    public async Task<IActionResult> GetSms()
    {
        var setting = await _context.Settings.AsNoTracking().FirstOrDefaultAsync();

        if (setting == null || !HasSmsSettings(setting))
            return NotFound(new { message = "SMS settings not found." });

        return Ok(BuildSmsResponse(setting));
    }

    [HttpPost("sms")]
    public async Task<IActionResult> CreateSms(SmsSettingsRequest model)
    {
        var setting = await GetOrCreateSetting();

        if (HasSmsSettings(setting))
            return BadRequest(new { message = "SMS settings already exist. Use PUT to edit." });

        ApplySms(setting, model, false);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "SMS settings created successfully.",
            settings = BuildSmsResponse(setting)
        });
    }

    [HttpPut("sms")]
    public async Task<IActionResult> UpdateSms(SmsSettingsRequest model)
    {
        var setting = await GetOrCreateSetting();

        ApplySms(setting, model, true);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "SMS settings updated successfully.",
            settings = BuildSmsResponse(setting)
        });
    }

    [HttpDelete("sms")]
    public async Task<IActionResult> DeleteSms()
    {
        var setting = await _context.Settings.FirstOrDefaultAsync();

        if (setting == null || !HasSmsSettings(setting))
            return NotFound(new { message = "SMS settings not found." });

        setting.SmsConfigurationName = null;
        setting.SmsProvider = null;
        setting.SmsSenderId = null;
        setting.SmsApiKey = null;
        setting.SmsApiSecret = null;
        setting.SmsStatus = null;
        setting.SmsConfigurationNotes = null;
        setting.SmsNotificationsEnabled = false;
        setting.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "SMS settings deleted successfully." });
    }

    // =====================================================
    // PAYMENT SETTINGS
    // =====================================================

    [HttpGet("payment")]
    public async Task<IActionResult> GetPayment()
    {
        var setting = await _context.Settings.AsNoTracking().FirstOrDefaultAsync();

        if (setting == null || !HasPaymentSettings(setting))
            return NotFound(new { message = "Payment settings not found." });

        return Ok(BuildPaymentResponse(setting));
    }

    [HttpPost("payment")]
    public async Task<IActionResult> CreatePayment(PaymentSettingsRequest model)
    {
        var setting = await GetOrCreateSetting();

        if (HasPaymentSettings(setting))
            return BadRequest(new { message = "Payment settings already exist. Use PUT to edit." });

        ApplyPayment(setting, model, false);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Payment settings created successfully.",
            settings = BuildPaymentResponse(setting)
        });
    }

    [HttpPut("payment")]
    public async Task<IActionResult> UpdatePayment(PaymentSettingsRequest model)
    {
        var setting = await GetOrCreateSetting();

        ApplyPayment(setting, model, true);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Payment settings updated successfully.",
            settings = BuildPaymentResponse(setting)
        });
    }

    [HttpDelete("payment")]
    public async Task<IActionResult> DeletePayment()
    {
        var setting = await _context.Settings.FirstOrDefaultAsync();

        if (setting == null || !HasPaymentSettings(setting))
            return NotFound(new { message = "Payment settings not found." });

        setting.PaymentConfigurationName = null;
        setting.GatewayProvider = null;
        setting.MerchantId = null;
        setting.PublicKey = null;
        setting.SecretKey = null;
        setting.PaymentMode = null;
        setting.PaymentStatus = null;
        setting.PaymentConfigurationNotes = null;
        setting.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Payment settings deleted successfully." });
    }

    private static void ApplyGeneral(Setting setting, GeneralSettingsRequest model)
    {
        setting.ApplicationName = model.AppName?.Trim();
        setting.Timezone = model.Timezone?.Trim();
        setting.Currency = model.Currency?.Trim();
        setting.GeneralStatus = model.Status?.Trim();
        setting.ConfigurationNotes = model.ConfigurationNotes?.Trim();
        setting.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyEmail(Setting setting, EmailSettingsRequest model, bool isUpdate)
    {
        setting.SenderName = model.SenderName?.Trim();
        setting.FromEmail = model.FromEmail?.Trim();
        setting.SmtpHost = model.SmtpHost?.Trim();
        setting.SmtpPort = model.SmtpPort;
        setting.SmtpUsername = model.SmtpUsername?.Trim();

        if (!string.IsNullOrWhiteSpace(model.SmtpPassword) &&
            !IsMaskedSecret(model.SmtpPassword))
        {
            setting.SmtpPassword = model.SmtpPassword;
        }
        else if (!isUpdate)
        {
            setting.SmtpPassword = null;
        }

        setting.EmailStatus = model.Status?.Trim();
        setting.EmailConfigurationNotes = model.ConfigurationNotes?.Trim();
        setting.EmailNotificationsEnabled =
            string.Equals(model.Status, "Enabled", StringComparison.OrdinalIgnoreCase);
        setting.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplySms(Setting setting, SmsSettingsRequest model, bool isUpdate)
    {
        setting.SmsConfigurationName = model.ConfigurationName?.Trim();
        setting.SmsProvider = model.Provider?.Trim();
        setting.SmsSenderId = model.SenderId?.Trim();
        setting.SmsApiKey = model.ApiKey?.Trim();

        if (!string.IsNullOrWhiteSpace(model.ApiSecret) &&
            !IsMaskedSecret(model.ApiSecret))
        {
            setting.SmsApiSecret = model.ApiSecret;
        }
        else if (!isUpdate)
        {
            setting.SmsApiSecret = null;
        }

        setting.SmsStatus = model.Status?.Trim();
        setting.SmsConfigurationNotes = model.ConfigurationNotes?.Trim();
        setting.SmsNotificationsEnabled =
            string.Equals(model.Status, "Enabled", StringComparison.OrdinalIgnoreCase);
        setting.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyPayment(Setting setting, PaymentSettingsRequest model, bool isUpdate)
    {
        setting.PaymentConfigurationName = model.ConfigurationName?.Trim();
        setting.GatewayProvider = model.GatewayProvider?.Trim();
        setting.MerchantId = model.MerchantId?.Trim();
        setting.PublicKey = model.PublicKey?.Trim();

        if (!string.IsNullOrWhiteSpace(model.SecretKey) &&
            !IsMaskedSecret(model.SecretKey))
        {
            setting.SecretKey = model.SecretKey;
        }
        else if (!isUpdate)
        {
            setting.SecretKey = null;
        }

        setting.PaymentMode = model.Mode?.Trim();
        setting.PaymentStatus = model.Status?.Trim();
        setting.PaymentConfigurationNotes = model.ConfigurationNotes?.Trim();
        setting.UpdatedAt = DateTime.UtcNow;
    }

    private static object BuildGeneralResponse(Setting setting)
    {
        return new
        {
            id = setting.Id,
            appName = setting.ApplicationName,
            timezone = setting.Timezone,
            currency = setting.Currency,
            status = setting.GeneralStatus,
            configurationNotes = setting.ConfigurationNotes,
            updatedAt = setting.UpdatedAt
        };
    }

    private static object BuildEmailResponse(Setting setting)
    {
        return new
        {
            id = setting.Id,
            senderName = setting.SenderName,
            fromEmail = setting.FromEmail,
            smtpHost = setting.SmtpHost,
            smtpPort = setting.SmtpPort,
            smtpUsername = setting.SmtpUsername,
            smtpPassword = MaskSecret(setting.SmtpPassword),
            hasSmtpPassword = !string.IsNullOrWhiteSpace(setting.SmtpPassword),
            status = setting.EmailStatus,
            configurationNotes = setting.EmailConfigurationNotes,
            updatedAt = setting.UpdatedAt
        };
    }

    private static object BuildSmsResponse(Setting setting)
    {
        return new
        {
            id = setting.Id,
            configurationName = setting.SmsConfigurationName,
            provider = setting.SmsProvider,
            senderId = setting.SmsSenderId,
            apiKey = setting.SmsApiKey,
            apiSecret = MaskSecret(setting.SmsApiSecret),
            hasApiSecret = !string.IsNullOrWhiteSpace(setting.SmsApiSecret),
            status = setting.SmsStatus,
            configurationNotes = setting.SmsConfigurationNotes,
            updatedAt = setting.UpdatedAt
        };
    }

    private static object BuildPaymentResponse(Setting setting)
    {
        return new
        {
            id = setting.Id,
            configurationName = setting.PaymentConfigurationName,
            gatewayProvider = setting.GatewayProvider,
            merchantId = setting.MerchantId,
            publicKey = setting.PublicKey,
            secretKey = MaskSecret(setting.SecretKey),
            hasSecretKey = !string.IsNullOrWhiteSpace(setting.SecretKey),
            mode = setting.PaymentMode,
            status = setting.PaymentStatus,
            configurationNotes = setting.PaymentConfigurationNotes,
            updatedAt = setting.UpdatedAt
        };
    }

    private static bool HasEmailSettings(Setting setting)
    {
        return HasText(setting.SenderName) ||
               HasText(setting.FromEmail) ||
               HasText(setting.SmtpHost) ||
               setting.SmtpPort.HasValue ||
               HasText(setting.SmtpUsername) ||
               HasText(setting.SmtpPassword);
    }

    private static bool HasSmsSettings(Setting setting)
    {
        return HasText(setting.SmsConfigurationName) ||
               HasText(setting.SmsProvider) ||
               HasText(setting.SmsSenderId) ||
               HasText(setting.SmsApiKey) ||
               HasText(setting.SmsApiSecret);
    }

    private static bool HasPaymentSettings(Setting setting)
    {
        return HasText(setting.PaymentConfigurationName) ||
               HasText(setting.GatewayProvider) ||
               HasText(setting.MerchantId) ||
               HasText(setting.PublicKey) ||
               HasText(setting.SecretKey);
    }
}
