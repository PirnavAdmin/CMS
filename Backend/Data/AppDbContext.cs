

using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Data;

namespace AuthDemo.Data;

public class AppDbContext
    : DbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext>
            options
    ) : base(options)
    {
    }


    // =====================================================
    // HOSPITALS
    // =====================================================

    public DbSet<Hospital>
        Hospitals
    { get; set; }
    public DbSet<AppointmentDocument>
    AppointmentDocuments
    { get; set; }

    // =====================================================
    // USERS
    // =====================================================

    public DbSet<User>
        Users
    { get; set; }

    public DbSet<OtpVerification>
        OtpVerifications
    { get; set; }

    // =====================================================
    // DOCTORS
    // =====================================================

    public DbSet<Doctor>
        Doctors
    { get; set; }

    public DbSet<DoctorBranch> DoctorBranches { get; set; }


    public DbSet<Branch> Branches { get; set; }

    // =====================================================
    // DOCTOR DIAGNOSIS
    // =====================================================

    public DbSet<DoctorDiagnosis>
        DoctorDiagnoses
    { get; set; }

    // =====================================================
    // RECEPTIONISTS
    // =====================================================

    public DbSet<Receptionist>
        Receptionists
    { get; set; }

    // =====================================================
    // STAFF
    // =====================================================

    public DbSet<Staff>
        Staffs
    { get; set; }

    // =====================================================
    // PATIENTS
    // =====================================================

    public DbSet<Patient>
        Patients
    { get; set; }

    // =====================================================
    // MEDICAL HISTORY
    // =====================================================

    public DbSet<MedicalHistory>
        MedicalHistories
    { get; set; }

    public DbSet<InstructionTemplate> InstructionTemplates { get; set; }

    public DbSet<MedicineNote> MedicineNotes { get; set; }

    // =====================================================
    // PATIENT VITALS
    // =====================================================

    public DbSet<PatientVitals>
        PatientVitals
    { get; set; }

    // =====================================================
    // APPOINTMENTS
    // =====================================================

    public DbSet<Appointment>
        Appointments
    { get; set; }

    //======================================================
    //PAYMENT
    //======================================================
    public DbSet<Payment> Payments { get; set; }

    // =====================================================
    // PAYMENTS - DUAL MERCHANT CONFIGURATION
    // CMS subscriptions -> application owner merchant
    // Hospital billing -> corresponding hospital merchant
    // =====================================================

    public DbSet<RazorpayMerchantCredential> RazorpayMerchantCredentials { get; set; }
    public DbSet<CmsOwnerMerchantSetting> CmsOwnerMerchantSettings { get; set; }
    public DbSet<HospitalMerchantSetting> HospitalMerchantSettings { get; set; }
    public DbSet<HospitalPaymentOrder> HospitalPaymentOrders { get; set; }
    public DbSet<HospitalPaymentQr> HospitalPaymentQrs { get; set; }
    public DbSet<HospitalPaymentTransaction> HospitalPaymentTransactions { get; set; }
    public DbSet<HospitalPaymentWebhookEvent> HospitalPaymentWebhookEvents { get; set; }
    public DbSet<HospitalPaymentRefund> HospitalPaymentRefunds { get; set; }
    public DbSet<HospitalSettlementRecord> HospitalSettlementRecords { get; set; }
    public DbSet<CmsSubscriptionRefund> CmsSubscriptionRefunds { get; set; }
    public DbSet<CmsSubscriptionSettlementRecord> CmsSubscriptionSettlementRecords { get; set; }
    public DbSet<HospitalPaymentDispute> HospitalPaymentDisputes { get; set; }
    public DbSet<CmsSubscriptionDispute> CmsSubscriptionDisputes { get; set; }
    public DbSet<HospitalGatewaySettlement> HospitalGatewaySettlements { get; set; }
    public DbSet<CmsGatewaySettlement> CmsGatewaySettlements { get; set; }
    // Automatic payment recovery attempt history
    public DbSet<HospitalPaymentRecoveryAttempt> HospitalPaymentRecoveryAttempts
        => Set<HospitalPaymentRecoveryAttempt>();

    public DbSet<HospitalPaymentLink> HospitalPaymentLinks{ get; set; }


    public DbSet<PatientAppointmentPayment>PatientAppointmentPayments{ get; set; }


    // =====================================================
    // PAYMENTS - CMS RAZORPAY WEBHOOK EVENT STORAGE
    // =====================================================

    public DbSet<CmsPaymentWebhookEvent>
        CmsPaymentWebhookEvents
    { get; set; }


    // =====================================================
    // CONSULTATIONS
    // =====================================================

    public DbSet<Consultation>
        Consultations
    { get; set; }

    // =====================================================
    // PRESCRIPTIONS
    // =====================================================

    public DbSet<Prescription>
        Prescriptions
    { get; set; }

    public DbSet<PrescriptionItem>
        PrescriptionItems
    { get; set; }

    public DbSet<PrescriptionLabTest> PrescriptionLabTests { get; set; }

    public DbSet<LabTestMaster> LabTestMasters { get; set; }
    public DbSet<LabOrder> LabOrders { get; set; }
    public DbSet<DoctorSpecialization> DoctorSpecializations { get; set; }

    public DbSet<DoctorQualification> DoctorQualifications { get; set; }

    // =====================================================
    // SCHEDULES
    // =====================================================

    public DbSet<Schedule>
        Schedules
    { get; set; }

    public DbSet<ScheduleSetting>
        ScheduleSettings
    { get; set; }

    public DbSet<ScheduleOverride> ScheduleOverrides { get; set; }

    public DbSet<Holiday>
        Holidays
    { get; set; }
    public DbSet<SuperAdmin>
    SuperAdmins
    { get; set; }
    public DbSet<Clinic>
    Clinics
    { get; set; }

    // =====================================================
    // BILLINGS
    // =====================================================

    public DbSet<Billing>
        Billings
    { get; set; }
    //ROLES//
    public DbSet<RolePermission>
    RolePermissions
    { get; set; }
    public DbSet<BillingPaymentTransaction> BillingPaymentTransactions
    => Set<BillingPaymentTransaction>();
    public DbSet<UserPermission> UserPermissions { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<ClinicSubscription> ClinicSubscriptions { get; set; }
    public DbSet<SubscriptionRequest> SubscriptionRequests { get; set; }
    public DbSet<SubscriptionPurchase> SubscriptionPurchases { get; set; }
    //auditlog//
    public DbSet<AuditLog>
    AuditLogs
    { get; set; }
    //email//
    public DbSet<Notification>
    Notifications
    { get; set; }
    // Per-user notification read tracking
    public DbSet<NotificationRead> NotificationReads { get; set; }

    //settings//
    public DbSet<Setting>
    Settings
    { get; set; }

    // =====================================================
    // INVOICE SETTINGS
    // =====================================================

    public DbSet<InvoiceSetting>
        InvoiceSettings
    { get; set; }



    public DbSet<Dosage> Dosages { get; set; }
    public DbSet<Frequency> Frequencies { get; set; }

    public DbSet<ChiefComplaint> ChiefComplaints { get; set; }

    public DbSet<Medicine> Medicines { get; set; }

    // =====================================================
    // SIMPLE CLINIC PHARMACY
    // =====================================================
    public DbSet<PharmacyStockBatch> PharmacyStockBatches { get; set; }
    public DbSet<PharmacyDispense> PharmacyDispenses { get; set; }
    public DbSet<PharmacyDispenseItem> PharmacyDispenseItems { get; set; }
    public DbSet<PharmacyBill> PharmacyBills { get; set; }
    public DbSet<PharmacyPayment> PharmacyPayments { get; set; }
    public DbSet<PharmacyOtcSale> PharmacyOtcSales { get; set; }
    public DbSet<PharmacyOtcSaleItem> PharmacyOtcSaleItems { get; set; }


    public DbSet<ClinicalNoteTemplate>
    ClinicalNoteTemplates
    { get; set; }


    public DbSet<State> States { get; set; }

    public DbSet<District> Districts { get; set; }

    public DbSet<City> Cities { get; set; }
    // =====================================================
    // MODEL RELATIONSHIPS
    // =====================================================

    protected override void
        OnModelCreating(
            ModelBuilder modelBuilder)
    {
        base.OnModelCreating(
            modelBuilder
        );

        // =================================================
        // PAYMENTS - CMS OWNER AND HOSPITAL MERCHANT SETTINGS
        // Subscription payments must use the CMS owner merchant.
        // Patient billing payments must use the bill's HospitalId.
        // These mappings only store configuration metadata;
        // gateway credentials must remain in secure secret storage.
        // =================================================
        modelBuilder.Entity<User>()
            .Property(x => x.EmailVerified)
            .HasDefaultValue(true);

        modelBuilder.Entity<OtpVerification>(entity =>
        {
            entity.Property(x => x.Purpose).HasMaxLength(30).IsRequired().HasDefaultValue("PasswordReset");
        });

        modelBuilder.Entity<CmsOwnerMerchantSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MerchantName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CredentialReference).HasMaxLength(200);
            entity.Property(x => x.KycStatus).HasMaxLength(30).IsRequired().HasDefaultValue("NotStarted");
            entity.Property(x => x.ProviderMerchantAccountId).HasMaxLength(150);
            entity.Property(x => x.KycNotes).HasMaxLength(500);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_CmsOwnerMerchantSettings_Mode",
                "Mode IN ('Test', 'Live')"));
        });

        modelBuilder.Entity<HospitalMerchantSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MerchantName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CredentialReference).HasMaxLength(200);
            entity.Property(x => x.KycStatus).HasMaxLength(30).IsRequired().HasDefaultValue("NotStarted");
            entity.Property(x => x.ProviderMerchantAccountId).HasMaxLength(150);
            entity.Property(x => x.KycNotes).HasMaxLength(500);
            entity.HasIndex(x => x.HospitalId).IsUnique();
            entity.HasOne<Hospital>()
                .WithMany()
                .HasForeignKey(x => x.HospitalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_HospitalMerchantSettings_Mode",
                "Mode IN ('Test', 'Live')"));
        });

        // =================================================
        // SUBSCRIPTION PRICING AND PURCHASE SNAPSHOTS
        // Basic/Super/Premium: four CMS-based combinations at 1/3/6/12 months.
        // Existing records and APIs are preserved.
        // =================================================
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            // Basic, Super and Premium are distinct subscription offerings.
            // Existing records receive Basic when the migration adds this column;
            // review historical plan categories before applying the migration.
            entity.Property(x => x.PlanType)
                .HasMaxLength(30)
                .IsRequired()
                .HasDefaultValue("Basic");
            entity.Property(x => x.Price).HasPrecision(18, 2);
            // Super Admin-configured limits; purchase snapshots are stored separately.
            entity.Property(x => x.MaxBranches).HasDefaultValue(1).IsRequired();
            entity.Property(x => x.DailyAppointmentLimit).HasDefaultValue(20).IsRequired();
            entity.HasIndex(x => new
            {
                x.PlanType,
                x.IncludesCms,
                x.IncludesLab,
                x.IncludesPharmacy,
                x.DurationMonths
            })
            .IsUnique()
            // Match the index already created by AddSubscriptionPlanType migration.
            // Different plan types may share identical modules and duration.
            .HasDatabaseName("IX_SubscriptionPlans_PlanType_IncludesCms_IncludesLab_IncludesPharmacy_DurationMonths");
        });

        modelBuilder.Entity<SubscriptionPurchase>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PurchasedMaxBranches).HasDefaultValue(1).IsRequired();
            entity.Property(x => x.PurchasedDailyAppointmentLimit).HasDefaultValue(20).IsRequired();
            entity.Property(x => x.OriginalPaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.UnusedCreditAmount).HasPrecision(18, 2);
            entity.Property(x => x.GrossNewPlanAmount).HasPrecision(18, 2);
            entity.Property(x => x.FinalPayableAmount).HasPrecision(18, 2);
            entity.Property(x => x.UpgradeMode).HasMaxLength(30);
            entity.HasOne(x => x.OriginalSubscription)
                .WithMany()
                .HasForeignKey(x => x.OriginalSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.OriginalSubscriptionId);
            entity.HasIndex(x => x.GatewayOrderId)
                .IsUnique()
                ;
            entity.HasIndex(x => x.GatewayPaymentId)
                .IsUnique()
                ;
            // Pin CMS subscription orders to the credential used at creation.
            // Nullable to preserve historical purchases.
            entity.Property(x => x.RazorpayCredentialId).IsRequired(false);
            entity.HasIndex(x => x.RazorpayCredentialId);
            entity.HasOne<RazorpayMerchantCredential>()
                .WithMany()
                .HasForeignKey(x => x.RazorpayCredentialId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SubscriptionPlan)
                .WithMany()
                .HasForeignKey(x => x.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.Email, x.PaymentStatus });

            // Payment attempt tracking (properties already added to SubscriptionPurchase).
            entity.Property(x => x.TransactionReference).HasMaxLength(150);
            entity.Property(x => x.PaymentFailureReason).HasMaxLength(500);
            entity.Property(x => x.PaymentAttemptCount).HasDefaultValue(0);
            // Distinguish initial onboarding purchases from renewals.
            entity.Property(x => x.PurchaseType)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("Initial");
            entity.HasIndex(x => new { x.PurchaseType, x.AdminUserId, x.HospitalId });
            entity.HasIndex(x => x.TransactionReference)
                .IsUnique()
                ;
            entity.HasIndex(x => new { x.AdminUserId, x.PaymentStatus });
            entity.HasIndex(x => new { x.HospitalId, x.ActivatedAt });
        });
        modelBuilder.Entity<SubscriptionRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.SubscriptionPlan)
                .WithMany()
                .HasForeignKey(x => x.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.Email, x.Status });
        });
        modelBuilder.Entity<ClinicSubscription>().Property(x => x.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<ClinicSubscription>()
            .Property(x => x.PurchasedMaxBranches).HasDefaultValue(1).IsRequired();
        modelBuilder.Entity<ClinicSubscription>()
            .Property(x => x.PurchasedDailyAppointmentLimit).HasDefaultValue(20).IsRequired();
        modelBuilder.Entity<ClinicSubscription>()
            .HasOne(x => x.SupersededBySubscription)
            .WithMany()
            .HasForeignKey(x => x.SupersededBySubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ClinicSubscription>()
            .HasIndex(x => x.SupersededBySubscriptionId);
        modelBuilder.Entity<ClinicSubscription>()
            .HasIndex(x => new { x.HospitalId, x.Status, x.StartDate, x.EndDate });
        modelBuilder.Entity<ClinicSubscription>()
            .HasOne(x => x.Hospital).WithMany().HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ClinicSubscription>()
            .HasOne(x => x.SubscriptionPlan).WithMany().HasForeignKey(x => x.SubscriptionPlanId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ClinicSubscription>()
            .HasIndex(x => new { x.HospitalId, x.Status });

        // =================================================
        // SIMPLE CLINIC PHARMACY CONFIGURATION
        // =================================================
        // Requires the corresponding properties in Models/Medicine.cs.
        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.Property(x => x.MedicineCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RequiresPrescription).HasDefaultValue(true).IsRequired();

            // Medicine CRUD audit fields. Existing medicine and stock mappings remain unchanged.
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt);
            entity.Property(x => x.DeactivatedAt);
            entity.Property(x => x.ReactivatedAt);
            entity.HasIndex(x => new { x.HospitalId, x.MedicineCode }).IsUnique();
        });

        // Additional non-prescription sales are separate from prescribed dispensing.
        modelBuilder.Entity<PharmacyOtcSale>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.HospitalId, x.BranchId, x.AppointmentId });
            entity.HasIndex(x => x.BillingId);
            entity.HasMany(x => x.Items)
                .WithOne(x => x.PharmacyOtcSale)
                .HasForeignKey(x => x.PharmacyOtcSaleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PharmacyOtcSaleItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.GstPercentage).HasPrecision(5, 2);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_PharmacyOtcSaleItems_Quantity", "Quantity > 0"));
            entity.HasOne<Medicine>().WithMany()
                .HasForeignKey(x => x.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PharmacyStockBatch>().WithMany()
                .HasForeignKey(x => x.StockBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PharmacyStockBatch>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BatchNumber).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SellingPrice).HasPrecision(18, 2);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_PharmacyStockBatches_Quantity",
                "Quantity >= 0"));
            entity.HasIndex(x => new
            {
                x.HospitalId,
                x.BranchId,
                x.MedicineId,
                x.BatchNumber
            }).IsUnique();
            entity.HasOne(x => x.Medicine).WithMany()
                .HasForeignKey(x => x.MedicineId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.HospitalId, x.BranchId, x.ExpiryDate });
        });

        modelBuilder.Entity<PharmacyDispense>(entity =>
        {
            entity.HasKey(x => x.Id);
            // First version: one complete dispensing operation per prescription.
            entity.HasIndex(x => x.PrescriptionId).IsUnique();
            entity.HasMany(x => x.Items).WithOne()
                .HasForeignKey(x => x.PharmacyDispenseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PharmacyDispenseItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.HasOne<PrescriptionItem>().WithMany()
                .HasForeignKey(x => x.PrescriptionItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PharmacyStockBatch>().WithMany()
                .HasForeignKey(x => x.StockBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PharmacyBill>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PaymentMethod).HasMaxLength(30);
            entity.HasIndex(x => x.PharmacyDispenseId).IsUnique();
            entity.HasOne<PharmacyDispense>().WithMany()
                .HasForeignKey(x => x.PharmacyDispenseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PharmacyPayment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ReferenceNumber).HasMaxLength(150);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.HasOne<PharmacyBill>().WithMany()
                .HasForeignKey(x => x.PharmacyBillId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.HospitalId, x.BranchId, x.PharmacyBillId });
        });

        // =================================================
        // LAB MANAGEMENT
        // =================================================
        // Explicit SQL table mapping for the lab test catalogue.
        // Keep this name stable for the migrated database schema.
        modelBuilder.Entity<LabTestMaster>()
            .ToTable("LabTestMasters", "dbo");

        modelBuilder.Entity<LabTestMaster>()
            .HasIndex(x => new { x.HospitalId, x.TestCode })
            .IsUnique();

        modelBuilder.Entity<LabTestMaster>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<LabTestMaster>()
            .HasOne(x => x.Hospital).WithMany()
            .HasForeignKey(x => x.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LabTestMaster>()
            .HasOne(x => x.Branch).WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LabOrder>()
            .HasIndex(x => x.PrescriptionLabTestId)
            .IsUnique();

        modelBuilder.Entity<LabOrder>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<LabOrder>()
            .HasOne(x => x.PrescriptionLabTest).WithMany()
            .HasForeignKey(x => x.PrescriptionLabTestId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LabOrder>()
            .HasOne(x => x.Appointment).WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LabOrder>()
            .HasOne(x => x.Patient).WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LabOrder>()
            .HasOne(x => x.Doctor).WithMany()
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LabOrder>()
            .HasOne(x => x.LabTestMaster).WithMany()
            .HasForeignKey(x => x.LabTestMasterId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PrescriptionLabTest>()
            .HasOne(x => x.Prescription)
            .WithMany(x => x.LabTests)
            .HasForeignKey(x => x.PrescriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserPermission>()
            .HasIndex(x => new { x.UserId, x.Module })
            .IsUnique();

        modelBuilder.Entity<UserPermission>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserPermission>()
            .HasOne(x => x.Hospital)
            .WithMany()
            .HasForeignKey(x => x.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        // =================================================
        // ROLE PERMISSIONS
        // SuperAdmin -> Clinic -> Individual Admin -> Module
        // =================================================

        modelBuilder.Entity<RolePermission>()
            .HasIndex(x => new
            {
                x.HospitalId,
                x.AdminUserId,
                x.RoleName,
                x.Module
            })
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .Property(x => x.RoleName)
            .HasMaxLength(100);

        modelBuilder.Entity<RolePermission>()
            .Property(x => x.Module)
            .HasMaxLength(200);

        modelBuilder.Entity<RolePermission>()
            .HasOne(x => x.Hospital)
            .WithMany()
            .HasForeignKey(x => x.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DoctorBranch>()
            .HasIndex(x => new { x.DoctorId, x.BranchId })
            .IsUnique();

        modelBuilder.Entity<DoctorBranch>()
            .HasOne(x => x.Doctor).WithMany()
            .HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DoctorBranch>()
            .HasOne(x => x.Branch).WithMany()
            .HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);

        // Super Admin Email Unique

        modelBuilder.Entity<SuperAdmin>()
            .HasIndex(x => x.Email)
            .IsUnique();

        // =================================================
        // DECIMAL PRECISION
        // =================================================

        modelBuilder.Entity<Doctor>()

            .Property(x => x.Fees)

            .HasPrecision(18, 2);

        modelBuilder.Entity<Appointment>()
            .Property(x => x.ConsultationFee)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Billing>()
            .Property(x => x.ConsultationCharge)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Billing>()
            .Property(x => x.LabCharge)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Billing>()
            .Property(x => x.MedicineCharge)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Billing>()
            .Property(x => x.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Billing>()
            .Property(x => x.SubTotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Billing>()
            .Property(x => x.Discount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Billing>()
            .Property(x => x.GstPercentage)
            .HasPrecision(5, 2);

        modelBuilder.Entity<Billing>()
            .Property(x => x.GstAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Billing>()
            .Property(x => x.BillingType)
            .HasMaxLength(20)
            .IsRequired();

        modelBuilder.Entity<Billing>()
            .Property(x => x.PaymentMode)
            .HasMaxLength(30)
            .IsRequired();

        modelBuilder.Entity<Billing>()
            .Property(x => x.Status)
            .HasMaxLength(20)
            .IsRequired();


        modelBuilder.Entity<Branch>()
    .HasOne(b => b.Hospital)
    .WithMany()
    .HasForeignKey(b => b.HospitalId)
    .OnDelete(DeleteBehavior.Restrict);

        // =================================================
        // USER -> HOSPITAL
        // =================================================

        modelBuilder.Entity<User>()

            .HasOne(x => x.Hospital)

            .WithMany()

            .HasForeignKey(x =>
                x.HospitalId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // DOCTOR -> HOSPITAL
        // =================================================

        modelBuilder.Entity<Doctor>()

            .HasOne(x => x.Hospital)

            .WithMany()

            .HasForeignKey(x =>
                x.HospitalId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // DOCTOR DIAGNOSIS -> DOCTOR
        // =================================================

        modelBuilder.Entity<DoctorDiagnosis>()

            .HasOne(x => x.Doctor)

            .WithMany()

            .HasForeignKey(x =>
                x.DoctorId)

            .OnDelete(
                DeleteBehavior.Cascade
            );

        // =================================================
        // DOCTOR DIAGNOSIS -> HOSPITAL
        // =================================================

        modelBuilder.Entity<DoctorDiagnosis>()

            .HasOne(x => x.Hospital)

            .WithMany()

            .HasForeignKey(x =>
                x.HospitalId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // RECEPTIONIST -> HOSPITAL
        // =================================================

        modelBuilder.Entity<Receptionist>()

            .HasOne(x => x.Hospital)

            .WithMany()

            .HasForeignKey(x =>
                x.HospitalId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // STAFF -> HOSPITAL
        // =================================================

        modelBuilder.Entity<Staff>()

            .HasOne(x => x.Hospital)

            .WithMany()

            .HasForeignKey(x =>
                x.HospitalId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // PATIENT -> HOSPITAL
        // =================================================

        modelBuilder.Entity<Patient>()

            .HasOne(x => x.Hospital)

            .WithMany()

            .HasForeignKey(x =>
                x.HospitalId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // MEDICAL HISTORY -> PATIENT
        // =================================================

        modelBuilder.Entity<MedicalHistory>()

            .HasOne(x => x.Patient)

            .WithMany(x =>
                x.MedicalHistories)

            .HasForeignKey(x =>
                x.PatientId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // MEDICAL HISTORY -> HOSPITAL
        // =================================================

        modelBuilder.Entity<MedicalHistory>()

            .HasOne(x => x.Hospital)

            .WithMany()

            .HasForeignKey(x =>
                x.HospitalId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // PATIENT VITALS -> PATIENT
        // =================================================

        modelBuilder.Entity<PatientVitals>()

            .HasOne(x => x.Patient)

            .WithMany()

            .HasForeignKey(x =>
                x.PatientId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // PATIENT VITALS -> APPOINTMENT
        // =================================================

        modelBuilder.Entity<PatientVitals>()

            .HasOne(x => x.Appointment)

            .WithMany()

            .HasForeignKey(x =>
                x.AppointmentId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // APPOINTMENT -> DOCTOR
        // =================================================

        modelBuilder.Entity<Appointment>()

            .HasOne(x => x.Doctor)

            .WithMany()

            .HasForeignKey(x =>
                x.DoctorId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // APPOINTMENT -> PATIENT
        // =================================================

        modelBuilder.Entity<Appointment>()

            .HasOne(x => x.Patient)

            .WithMany(x =>
                x.Appointments)

            .HasForeignKey(x =>
                x.PatientId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // APPOINTMENT -> HOSPITAL
        // =================================================

        modelBuilder.Entity<Appointment>()

            .HasOne(x => x.Hospital)

            .WithMany()

            .HasForeignKey(x =>
                x.HospitalId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // CONSULTATION -> PATIENT
        // =================================================

        modelBuilder.Entity<Consultation>()

            .HasOne(x => x.Patient)

            .WithMany()

            .HasForeignKey(x =>
                x.PatientId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // CONSULTATION -> APPOINTMENT
        // =================================================

        modelBuilder.Entity<Consultation>()

            .HasOne(x => x.Appointment)

            .WithMany()

            .HasForeignKey(x =>
                x.AppointmentId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // PRESCRIPTION -> PATIENT
        // =================================================

        modelBuilder.Entity<Prescription>()

            .HasOne(x => x.Patient)

            .WithMany()

            .HasForeignKey(x =>
                x.PatientId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // PRESCRIPTION -> APPOINTMENT
        // =================================================

        modelBuilder.Entity<Prescription>()

            .HasOne(x => x.Appointment)

            .WithMany()

            .HasForeignKey(x =>
                x.AppointmentId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // PRESCRIPTION -> HOSPITAL
        // =================================================

        modelBuilder.Entity<Prescription>()

            .HasOne(x => x.Hospital)

            .WithMany()

            .HasForeignKey(x =>
                x.HospitalId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // PRESCRIPTION ITEM -> PRESCRIPTION
        // =================================================

        modelBuilder.Entity<PrescriptionItem>()

            .HasOne(x => x.Prescription)

            .WithMany(x =>
                x.Medicines)

            .HasForeignKey(x =>
                x.PrescriptionId)

            .OnDelete(
                DeleteBehavior.Cascade
            );

        // =================================================
        // SCHEDULE -> DOCTOR
        // =================================================

        modelBuilder.Entity<Schedule>()

            .HasOne(x => x.Doctor)

            .WithMany()

            .HasForeignKey(x =>
                x.DoctorId)

            .OnDelete(
                DeleteBehavior.Cascade
            );

        // =================================================
        // BILLING -> APPOINTMENT
        // =================================================

        modelBuilder.Entity<Billing>()

            .HasOne(x => x.Appointment)

            .WithMany()

            .HasForeignKey(x =>
                x.AppointmentId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // BILLING -> PATIENT
        // =================================================

        modelBuilder.Entity<Billing>()

            .HasOne(x => x.Patient)

            .WithMany()

            .HasForeignKey(x =>
                x.PatientId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // BILLING -> DOCTOR
        // =================================================

        modelBuilder.Entity<Billing>()

            .HasOne(x => x.Doctor)

            .WithMany()

            .HasForeignKey(x =>
                x.DoctorId)

            .OnDelete(
                DeleteBehavior.Restrict
            );

        // =================================================
        // BILLING -> HOSPITAL
        // =================================================

        modelBuilder.Entity<Billing>()
            .HasOne(x => x.Hospital)
            .WithMany()
            .HasForeignKey(x => x.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        // =================================================
        // BILLING -> BRANCH
        // =================================================

        modelBuilder.Entity<Billing>()
            .HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        /// Doctor → Branch
        modelBuilder.Entity<Doctor>()
            .HasOne(d => d.Branch)
            .WithMany()
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        //Receptionist → Branch
        modelBuilder.Entity<Receptionist>()
            .HasOne(r => r.Branch)
            .WithMany()
            .HasForeignKey(r => r.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        //Staff → Branch
        modelBuilder.Entity<Staff>()
            .HasOne(s => s.Branch)
            .WithMany()
            .HasForeignKey(s => s.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        //Appointment → Branch
        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Branch)
            .WithMany()
            .HasForeignKey(a => a.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        // =================================================
        // NOTIFICATION -> PATIENT
        // =================================================

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Patient)
            .WithMany()
            .HasForeignKey(n => n.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
        // =================================================
        // NOTIFICATION READ STATUS PER USER
        // =================================================
        modelBuilder.Entity<NotificationRead>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.NotificationId, x.UserId })
                .IsUnique()
                ;

            entity.HasIndex(x => new { x.NotificationId, x.SuperAdminId })
                .IsUnique()
                ;

            entity.HasOne(x => x.Notification)
                .WithMany(x => x.NotificationReads)
                .HasForeignKey(x => x.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SuperAdmin)
                .WithMany()
                .HasForeignKey(x => x.SuperAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_NotificationReads_OneRecipient",
                "(UserId IS NOT NULL AND SuperAdminId IS NULL) OR " +
                "(UserId IS NULL AND SuperAdminId IS NOT NULL)"));
        });

        //Schedule → Branch(if applicable)
        modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Branch)
                .WithMany()
                .HasForeignKey(s => s.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ScheduleOverride>()
            .HasOne(x => x.Doctor)
            .WithMany()
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ScheduleOverride>()
            .HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ScheduleOverride>()
            .HasIndex(x => new { x.HospitalId, x.DoctorId, x.Date, x.IsActive });

        modelBuilder.Entity<User>()
    .HasOne(x => x.Branch)
    .WithMany()
    .HasForeignKey(x => x.BranchId)
    .OnDelete(DeleteBehavior.Restrict);

        // =================================================
        // HOSPITAL INVOICE / REGISTRATION DETAILS
        // =================================================
        modelBuilder.Entity<Hospital>(entity =>
        {
            entity.Property(x => x.Gstin).HasMaxLength(20);
            entity.Property(x => x.RegistrationNumber).HasMaxLength(100);
            entity.Property(x => x.LogoUrl).HasMaxLength(1000);
        });

        // =================================================
        // INVOICE SETTINGS
        // =================================================

        modelBuilder.Entity<InvoiceSetting>()
            .HasIndex(x => x.HospitalId)
            .IsUnique();

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.ClinicName)
            .HasMaxLength(200);

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.InvoiceTemplate)
            .HasMaxLength(50);

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.HeaderTitle)
            .HasMaxLength(200);

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.HeaderSubtitle)
            .HasMaxLength(300);

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.ClinicPhone)
            .HasMaxLength(50);

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.ClinicEmail)
            .HasMaxLength(200);

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.GstNumber)
            .HasMaxLength(100);

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.RegistrationNumber)
            .HasMaxLength(100);

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.AccentColor)
            .HasMaxLength(20);

        modelBuilder.Entity<InvoiceSetting>()
            .Property(x => x.LogoUrl)
            .HasMaxLength(1000);

        modelBuilder.Entity<InvoiceSetting>()
            .HasOne(x => x.Hospital)
            .WithMany()
            .HasForeignKey(x => x.HospitalId)
            .OnDelete(DeleteBehavior.Cascade);

        // =================================================
        // BILLING INVOICE NUMBER
        // =================================================

        modelBuilder.Entity<Billing>(entity =>
        {
            entity.Property(x => x.InvoiceNumber)
                .HasMaxLength(50);

            entity.HasIndex(x => x.InvoiceNumber)
                .IsUnique()
                ;

            // =================================================
            // STEP 4: DISCOUNT AND GST BREAKDOWN
            // =================================================
            // Monetary amounts use two decimal places.
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxableAmount).HasPrecision(18, 2);
            entity.Property(x => x.CgstAmount).HasPrecision(18, 2);
            entity.Property(x => x.SgstAmount).HasPrecision(18, 2);
            entity.Property(x => x.IgstAmount).HasPrecision(18, 2);

            // Tax rates use two decimal places.
            entity.Property(x => x.CgstPercentage).HasPrecision(5, 2);
            entity.Property(x => x.SgstPercentage).HasPrecision(5, 2);
            entity.Property(x => x.IgstPercentage).HasPrecision(5, 2);

            // Existing bills have no saved tax split; use None until
            // their historical tax treatment can be verified.
            entity.Property(x => x.TaxType)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("None");
        });


        // =================================================
        // PERFORMANCE INDEXES
        // =================================================

        modelBuilder.Entity<Appointment>()
            .HasIndex(x =>
                x.DoctorId);

        modelBuilder.Entity<Appointment>()
            .HasIndex(x =>
                x.PatientId);

        modelBuilder.Entity<Appointment>()
            .HasIndex(x =>
                x.HospitalId);

        modelBuilder.Entity<Appointment>()
            .HasIndex(x =>
                x.Date);

        modelBuilder.Entity<Consultation>()
            .HasIndex(x =>
                x.PatientId);

        modelBuilder.Entity<Consultation>()
            .HasIndex(x =>
                x.AppointmentId);

        modelBuilder.Entity<Prescription>()
            .HasIndex(x =>
                x.PatientId);

        modelBuilder.Entity<Prescription>()
            .HasIndex(x =>
                x.HospitalId);

        modelBuilder.Entity<DoctorDiagnosis>()
            .HasIndex(x =>
                x.DoctorId);

        modelBuilder.Entity<PrescriptionItem>()
            .HasOne<Medicine>()
            .WithMany()
            .HasForeignKey(x => x.MedicineId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PrescriptionItem>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_PrescriptionItems_PrescribedQuantity",
                "PrescribedQuantity >= 0"));
        modelBuilder.Entity<BillingPaymentTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2);

            entity.Property(x => x.PaymentMethod)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.ReferenceNumber)
                .HasMaxLength(150);

            entity.Property(x => x.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(x => x.BillingId);

            // Change #18: One billing-ledger credit per Razorpay payment ID.
            // Other payment methods may legitimately reuse a reference.
            entity.HasIndex(x => x.ReferenceNumber)
                .IsUnique()
                .HasDatabaseName("UX_BillingPaymentTransactions_RazorpayReference")
                ;

            entity.HasOne(x => x.Billing)
                .WithMany(x => x.PaymentTransactions)
                .HasForeignKey(x => x.BillingId)
                .OnDelete(DeleteBehavior.Restrict);

        });

        // =====================================================
        // PAYMENTS - WEBHOOK IDEMPOTENCY AND LOOKUP INDEXES
        // =====================================================

        modelBuilder.Entity<CmsPaymentWebhookEvent>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.Mode, x.EventId })
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.ProcessingStatus,
                x.ReceivedAt
            });

            entity.HasIndex(x => x.GatewayOrderId);

            entity.Property(x => x.EventType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ProcessingStatus)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Mode)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.LastError)
                .HasMaxLength(1000);
        });

        // Staff-created Razorpay QR requests. Payment confirmation is handled separately.
        // =====================================================
        // AUTOMATIC PAYMENT RECOVERY - ATTEMPT HISTORY
        // =====================================================
        modelBuilder.Entity<HospitalPaymentRecoveryAttempt>(entity =>
        {
            entity.ToTable("HospitalPaymentRecoveryAttempts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PaymentSource)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(e => e.GatewayPaymentId)
                .HasMaxLength(150)
                .IsRequired();
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            entity.HasIndex(e => new { e.HospitalId, e.GatewayPaymentId });
            entity.HasIndex(e => new { e.HospitalId, e.Status, e.NextRetryAtUtc });

            // Change #18: retries must have distinct attempt numbers.
            // Do not make GatewayPaymentId alone unique: retries need history.
            entity.HasIndex(e => new
            {
                e.HospitalId,
                e.GatewayPaymentId,
                e.AttemptNumber
            })
                .IsUnique()
                .HasDatabaseName("UX_HospitalPaymentRecoveryAttempts_PaymentAttempt");
        });

        modelBuilder.Entity<HospitalPaymentQr>(entity =>
        {
            entity.ToTable("HospitalPaymentQrs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LocalReference).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.GatewayQrId).HasMaxLength(150);
            entity.Property(x => x.GatewayPaymentId).HasMaxLength(150);
            entity.Property(x => x.ImageUrl).HasMaxLength(2048);
            entity.HasIndex(x => x.LocalReference).IsUnique();
            entity.HasIndex(x => x.GatewayQrId).IsUnique()
                ;
            entity.HasIndex(x => x.GatewayPaymentId)
                ;
            entity.HasIndex(x => new { x.HospitalId, x.BillingId, x.Status });
            entity.HasOne<Billing>().WithMany()
                .HasForeignKey(x => x.BillingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Hospital>().WithMany()
                .HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RazorpayMerchantCredential>().WithMany()
                .HasForeignKey(x => x.RazorpayCredentialId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HospitalPaymentOrder>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.LocalOrderId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            // Pin each new order to its original hospital Razorpay credential.
            // Existing orders remain nullable until historical credentials are verified.
            entity.Property(x => x.RazorpayCredentialId).IsRequired(false);
            entity.HasIndex(x => x.RazorpayCredentialId);
            entity.HasOne<RazorpayMerchantCredential>()
                .WithMany()
                .HasForeignKey(x => x.RazorpayCredentialId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.LocalOrderId).IsUnique();
            entity.HasIndex(x => x.GatewayOrderId).IsUnique();
            entity.HasIndex(x => new { x.HospitalId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => new { x.HospitalId, x.BillingId, x.Status });
            entity.HasOne<Billing>().WithMany().HasForeignKey(x => x.BillingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Hospital>().WithMany().HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Restrict);
        });

        // A captured payment originates from either a checkout order or a staff-issued QR.
        // Existing order transactions are retained with PaymentSource = "Order".
        modelBuilder.Entity<HospitalPaymentTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.RefundedAmount).HasPrecision(18, 2);
            entity.Property(x => x.GatewayPaymentId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.GatewayOrderId).HasMaxLength(150).IsRequired(false);
            entity.Property(x => x.PaymentSource).HasMaxLength(10)
                .IsRequired().HasDefaultValue("Order");
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();

            // Change #19: Settlement evidence and reconciliation metadata.
            entity.Property(x => x.GatewaySettlementId).HasMaxLength(150);
            entity.Property(x => x.SettlementFeeAmount).HasPrecision(18, 2);
            entity.Property(x => x.SettlementTaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.SettlementNetAmount).HasPrecision(18, 2);
            entity.Property(x => x.SettlementReconciliationStatus)
                .HasMaxLength(30).IsRequired().HasDefaultValue("Pending");
            entity.Property(x => x.SettlementReconciliationNotes).HasMaxLength(1000);
            entity.Property(x => x.GatewaySettledAtUtc);
            entity.Property(x => x.SettlementReconciledAtUtc);
            entity.HasIndex(x => new { x.HospitalId, x.Mode, x.GatewaySettlementId })
                .HasDatabaseName("IX_HospitalPaymentTransactions_SettlementLookup")
                ;

            entity.HasIndex(x => x.GatewayPaymentId).IsUnique();
            entity.HasIndex(x => new { x.HospitalId, x.BillingId });
            entity.HasIndex(x => x.HospitalPaymentQrId)
                ;

            entity.HasOne(x => x.Order).WithMany()
                .HasForeignKey(x => x.HospitalPaymentOrderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Qr).WithMany()
                .HasForeignKey(x => x.HospitalPaymentQrId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Billing>().WithMany()
                .HasForeignKey(x => x.BillingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Hospital>().WithMany()
                .HasForeignKey(x => x.HospitalId)
                .OnDelete(DeleteBehavior.Restrict);

            // Exactly one source is required; do not permit unlinked payment rows.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_HospitalPaymentTransactions_Source",
                "(PaymentSource = 'Order' AND HospitalPaymentOrderId IS NOT NULL AND HospitalPaymentQrId IS NULL AND GatewayOrderId IS NOT NULL) " +
                "OR (PaymentSource = 'Qr' AND HospitalPaymentQrId IS NOT NULL AND HospitalPaymentOrderId IS NULL AND GatewayOrderId IS NULL)"));
        });

        modelBuilder.Entity<HospitalPaymentWebhookEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Mode, x.EventId }).IsUnique();
            entity.Property(x => x.EventId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ProcessingStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.HasIndex(x => new { x.HospitalId, x.ReceivedAt });
        });

        modelBuilder.Entity<HospitalPaymentRefund>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.GatewayRefundId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(300);
            // Change #19: Refund settlement adjustments and reconciliation metadata.
            entity.Property(x => x.GatewaySettlementId).HasMaxLength(150);
            entity.Property(x => x.SettlementAdjustmentAmount).HasPrecision(18, 2);
            entity.Property(x => x.SettlementFeeAmount).HasPrecision(18, 2);
            entity.Property(x => x.SettlementTaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.SettlementReconciliationStatus)
                .HasMaxLength(30).IsRequired().HasDefaultValue("Pending");
            entity.Property(x => x.SettlementReconciliationNotes).HasMaxLength(1000);
            entity.Property(x => x.GatewaySettledAtUtc);
            entity.Property(x => x.SettlementReconciledAtUtc);
            entity.HasIndex(x => new { x.HospitalId, x.GatewaySettlementId })
                .HasDatabaseName("IX_HospitalPaymentRefunds_SettlementLookup")
                ;
            entity.HasIndex(x => x.GatewayRefundId).IsUnique();
            entity.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.HospitalPaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Hospital>().WithMany().HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HospitalSettlementRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GrossAmount).HasPrecision(18, 2);
            entity.Property(x => x.FeeAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.NetAmount).HasPrecision(18, 2);
            entity.Property(x => x.SettlementId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.ReconciliationStatus)
                .HasMaxLength(30).IsRequired().HasDefaultValue("Pending");
            // Change #19: Transaction-level settlement evidence.
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired().HasDefaultValue("Test");
            entity.Property(x => x.GatewayPaymentId).HasMaxLength(150);
            entity.Property(x => x.GatewaySettlementTransactionId).HasMaxLength(150);
            entity.Property(x => x.GatewayTransactionType).HasMaxLength(30);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("INR");
            entity.Property(x => x.ExpectedGrossAmount).HasPrecision(18, 2);
            entity.Property(x => x.ExpectedNetAmount).HasPrecision(18, 2);
            entity.Property(x => x.GrossDifference).HasPrecision(18, 2);
            entity.Property(x => x.NetDifference).HasPrecision(18, 2);
            entity.Property(x => x.ReconciliationNotes).HasMaxLength(1000);
            entity.Property(x => x.ReconciliationAttemptCount).HasDefaultValue(0);
            entity.Property(x => x.RequiresManualReview).HasDefaultValue(false);
            entity.Property(x => x.LastReconciliationAttemptAtUtc);
            entity.Property(x => x.ReconciledAtUtc);
            entity.Property(x => x.UpdatedAtUtc);
            entity.HasIndex(x => new { x.HospitalId, x.Mode, x.SettlementId })
                .HasDatabaseName("IX_HospitalSettlementRecords_ModeLookup");
            entity.HasIndex(x => new { x.HospitalId, x.SettlementId, x.HospitalPaymentTransactionId }).IsUnique();
            entity.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.HospitalPaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Hospital>().WithMany().HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CmsSubscriptionRefund>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.GatewayRefundId).HasMaxLength(150).IsRequired();
            // PAYMENTS - Link a CMS refund to its original gateway payment.
            entity.Property(x => x.GatewayPaymentId).HasMaxLength(150);
            // PAYMENTS - Stable key for safe retry/duplicate detection.
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(300);
            entity.HasIndex(x => x.GatewayRefundId).IsUnique();
            entity.HasIndex(x => x.GatewayPaymentId)
                ;
            entity.HasIndex(x => new { x.SubscriptionPurchaseId, x.IdempotencyKey })
                .IsUnique()
                ;
            entity.HasOne<SubscriptionPurchase>().WithMany().HasForeignKey(x => x.SubscriptionPurchaseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CmsSubscriptionSettlementRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GrossAmount).HasPrecision(18, 2);
            entity.Property(x => x.FeeAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.NetAmount).HasPrecision(18, 2);
            entity.Property(x => x.GatewayPaymentId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SettlementId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.SubscriptionPurchaseId, x.SettlementId, x.GatewayPaymentId }).IsUnique();
            entity.HasOne<SubscriptionPurchase>().WithMany().HasForeignKey(x => x.SubscriptionPurchaseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HospitalPaymentDispute>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.GatewayDisputeId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.GatewayPaymentId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(10);
            entity.Property(x => x.Reason).HasMaxLength(300);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.EvidenceDocumentIdsJson).IsRequired().HasDefaultValue("");
            entity.HasIndex(x => new { x.HospitalId, x.GatewayDisputeId }).IsUnique();
            entity.HasIndex(x => new { x.HospitalId, x.Status });
            entity.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.HospitalPaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Hospital>().WithMany().HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CmsSubscriptionDispute>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.GatewayDisputeId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.GatewayPaymentId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(10);
            entity.Property(x => x.Reason).HasMaxLength(300);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.EvidenceDocumentIdsJson).IsRequired().HasDefaultValue("");
            entity.HasIndex(x => x.GatewayDisputeId).IsUnique();
            entity.HasIndex(x => new { x.SubscriptionPurchaseId, x.Mode });
            entity.HasOne<SubscriptionPurchase>().WithMany().HasForeignKey(x => x.SubscriptionPurchaseId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HospitalGatewaySettlement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.FeeAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SettlementId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Utr).HasMaxLength(150);
            // Change #19: Settlement-level reconciliation aggregates.
            entity.Property(x => x.ReconciliationStatus)
                .HasMaxLength(30).IsRequired().HasDefaultValue("Pending");
            entity.Property(x => x.MatchedPaymentAmount).HasPrecision(18, 2);
            entity.Property(x => x.MatchedRefundAmount).HasPrecision(18, 2);
            entity.Property(x => x.ExpectedNetAmount).HasPrecision(18, 2);
            entity.Property(x => x.SettlementDifference).HasPrecision(18, 2);
            entity.Property(x => x.ReconciliationNotes).HasMaxLength(1000);
            entity.Property(x => x.ReconciliationAttemptCount).HasDefaultValue(0);
            entity.Property(x => x.RequiresManualReview).HasDefaultValue(false);
            entity.Property(x => x.GatewayTransactionCount).HasDefaultValue(0);
            entity.Property(x => x.UnmatchedGatewayTransactionCount).HasDefaultValue(0);
            entity.Property(x => x.UnmatchedLocalTransactionCount).HasDefaultValue(0);
            entity.Property(x => x.UnmatchedTransactionCount).HasDefaultValue(0);
            entity.Property(x => x.MatchedPaymentCount).HasDefaultValue(0);
            entity.Property(x => x.MatchedRefundCount).HasDefaultValue(0);
            entity.Property(x => x.LastReconciliationAttemptAtUtc);
            entity.Property(x => x.GatewayEvidenceFetchedAtUtc);
            entity.Property(x => x.ReconciledAtUtc);
            entity.Property(x => x.UpdatedAtUtc);
            entity.HasIndex(x => new { x.HospitalId, x.Mode, x.ReconciliationStatus })
                .HasDatabaseName("IX_HospitalGatewaySettlements_ReconciliationQueue");
            entity.HasIndex(x => new { x.HospitalId, x.Mode, x.SettlementId }).IsUnique();
            entity.HasOne<Hospital>().WithMany().HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CmsGatewaySettlement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.FeeAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.Mode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SettlementId).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Utr).HasMaxLength(150);
            entity.HasIndex(x => new { x.Mode, x.SettlementId }).IsUnique();
        });

        modelBuilder.Entity<RazorpayMerchantCredential>(entity =>
        {
            entity.ToTable("RazorpayMerchantCredentials", table =>
            {
                table.HasCheckConstraint(
                    "CK_RazorpayCredentials_MerchantType",
                    "MerchantType IN ('CMS', 'Hospital')");

                table.HasCheckConstraint(
                    "CK_RazorpayCredentials_Mode",
                    "Mode IN ('Test', 'Live')");

                table.HasCheckConstraint(
                    "CK_RazorpayCredentials_Hospital",
                    "(MerchantType = 'CMS' AND HospitalId IS NULL) OR " +
                    "(MerchantType = 'Hospital' AND HospitalId IS NOT NULL)");
            });

            entity.HasKey(x => x.Id);

            entity.Property(x => x.MerchantType)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Mode)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.KeyId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.EncryptedKeySecret)
                .IsRequired();

            entity.Property(x => x.EncryptedWebhookSecret)
                .IsRequired();

            entity.HasOne<Hospital>()
                .WithMany()
                .HasForeignKey(x => x.HospitalId)
                .OnDelete(DeleteBehavior.Restrict);

            // Only one active CMS credential per mode.
            entity.HasIndex(x => new
            {
                x.MerchantType,
                x.Mode
            })
                .IsUnique()
                ;

            // Only one active credential per hospital and mode.
            entity.HasIndex(x => new
            {
                x.HospitalId,
                x.Mode
            })
                .IsUnique()
                ;
        });

        modelBuilder.Entity<HospitalPaymentLink>(entity =>
        {
            entity.ToTable("HospitalPaymentLinks");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.HospitalId,
                x.BillingId
            });

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.ExpiresAtUtc)
                .IsRequired();

            entity.HasOne(x => x.Billing)
                .WithMany()
                .HasForeignKey(x => x.BillingId)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<PatientAppointmentPayment>(entity =>
            {
                entity.ToTable("PatientAppointmentPayments");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Amount)
                    .HasPrecision(18, 2);

                entity.Property(x => x.Currency)
                    .HasMaxLength(3).IsRequired();

                entity.Property(x => x.Mode)
                    .HasMaxLength(20).IsRequired();

                entity.Property(x => x.Status)
                    .HasMaxLength(30).IsRequired();

                entity.HasIndex(x => x.LocalOrderId)
                    .IsUnique();

                entity.HasIndex(x => x.RazorpayOrderId)
                    .IsUnique()
                    ;

                entity.HasIndex(x => x.RazorpayPaymentId)
                    .IsUnique()
                    ;

                entity.HasIndex(x => new
                {
                    x.AppointmentId,
                    x.Status
                });

                entity.HasOne(x => x.Appointment)
                    .WithMany()
                    .HasForeignKey(x => x.AppointmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<RazorpayMerchantCredential>()
                    .WithMany()
                    .HasForeignKey(x => x.RazorpayCredentialId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

        });

    }
}
