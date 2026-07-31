

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

    public DbSet<UserPermission> UserPermissions { get; set; }
    //auditlog//
    public DbSet<AuditLog>
    AuditLogs
    { get; set; }
    //email//
    public DbSet<Notification>
    Notifications
    { get; set; }
    //settings//
    public DbSet<Setting>
    Settings
    { get; set; }


    public DbSet<Dosage> Dosages { get; set; }
    public DbSet<Frequency> Frequencies { get; set; }

    public DbSet<ChiefComplaint> ChiefComplaints { get; set; }

    public DbSet<Medicine> Medicines { get; set; }

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
        //Schedule → Branch(if applicable)
        modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Branch)
                .WithMany()
                .HasForeignKey(s => s.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
    .HasOne(x => x.Branch)
    .WithMany()
    .HasForeignKey(x => x.BranchId)
    .OnDelete(DeleteBehavior.Restrict);

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
    }
}