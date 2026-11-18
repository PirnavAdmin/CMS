
using System;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

public class PaymentMerchantRouter
{
    private readonly AppDbContext _db;

    public PaymentMerchantRouter(AppDbContext db)
    {
        _db = db;
    }

    // Subscriptions always use the CMS owner's account.
    public async Task<CmsOwnerMerchantSetting>
        GetSubscriptionMerchantAsync()
    {
        var merchant = await _db.CmsOwnerMerchantSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1);

        if (merchant == null || !merchant.IsEnabled || merchant.KycStatus != "Approved" || string.IsNullOrWhiteSpace(merchant.ProviderMerchantAccountId))
            throw new InvalidOperationException(
                "CMS subscription merchant is not configured.");

        return merchant;
    }

    // Patient billing always uses the bill's hospital.
    public async Task<HospitalMerchantSetting>
        GetHospitalBillingMerchantAsync(int hospitalId)
    {
        if (hospitalId <= 0)
            throw new ArgumentException(
                "Valid HospitalId is required.");

        var merchant = await _db.HospitalMerchantSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.HospitalId == hospitalId);

        if (merchant == null || !merchant.IsEnabled || merchant.KycStatus != "Approved" || string.IsNullOrWhiteSpace(merchant.ProviderMerchantAccountId))
            throw new InvalidOperationException(
                "Hospital payment merchant is not configured.");

        return merchant;
    }
}
