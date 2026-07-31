/****** Object:  Database `ClinicalManagementSystemDB`    Script Date: 7/31/2026 5:43:27 PM ******/
CREATE TABLE `dbo`.`__EFMigrationsHistory`(
	`MigrationId` `VARCHAR`(150) NOT NULL,
	`ProductVersion` `VARCHAR`(32) NOT NULL,
 CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY CLUSTERED 
(
	`MigrationId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
) ON `PRIMARY`
/****** Object:  Table `dbo`.`AppointmentDocuments`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`AppointmentDocuments`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`AppointmentId` `int` NOT NULL,
	`FileName` `VARCHAR`(max) NOT NULL,
	`FilePath` `VARCHAR`(max) NOT NULL,
	`UploadedAt` `DATETIME`(7) NOT NULL,
	`HospitalId` `int` NOT NULL,
 CONSTRAINT `PK_AppointmentDocuments` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Appointments`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Appointments`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`DoctorId` `int` NOT NULL,
	`BookingType` `VARCHAR`(max) NOT NULL,
	`PatientId` `int` NOT NULL,
	`Date` `DATETIME`(7) NOT NULL,
	`StartTime` `time`(7) NOT NULL,
	`TokenNumber` `VARCHAR`(max) NOT NULL,
	`ConsultationFee` `decimal`(18, 2) NOT NULL,
	`PaymentMode` `VARCHAR`(max) NULL,
	`PaymentStatus` `VARCHAR`(max) NOT NULL,
	`PaymentDate` `DATETIME`(7) NULL,
	`TransactionId` `VARCHAR`(max) NULL,
	`ChiefComplaints` `VARCHAR`(max) NULL,
	`BloodPressure` `VARCHAR`(max) NULL,
	`SugarLevel` `VARCHAR`(max) NULL,
	`Temperature` `VARCHAR`(max) NULL,
	`Weight` `VARCHAR`(max) NULL,
	`PulseRate` `VARCHAR`(max) NULL,
	`RespiratoryRate` `VARCHAR`(max) NULL,
	`Status` `VARCHAR`(max) NOT NULL,
	`HospitalId` `int` NOT NULL,
	`BranchId` `int` NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Appointments` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`AuditLogs`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`AuditLogs`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`UserId` `int` NOT NULL,
	`UserName` `VARCHAR`(max) NOT NULL,
	`Role` `VARCHAR`(max) NOT NULL,
	`ClinicId` `int` NULL,
	`BranchId` `int` NULL,
	`Action` `VARCHAR`(max) NOT NULL,
	`SystemAction` `VARCHAR`(max) NULL,
	`IsLoginActivity` `BOOLEAN` NOT NULL,
	`IpAddress` `VARCHAR`(max) NULL,
	`Browser` `VARCHAR`(max) NULL,
	`Device` `VARCHAR`(max) NULL,
	`LoginTime` `DATETIME`(7) NOT NULL,
	`LogoutTime` `DATETIME`(7) NULL,
	`IsOnline` `BOOLEAN` NOT NULL,
	`Timestamp` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_AuditLogs` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Billings`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Billings`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`AppointmentId` `int` NOT NULL,
	`PatientId` `int` NOT NULL,
	`DoctorId` `int` NOT NULL,
	`ConsultationCharge` `decimal`(18, 2) NOT NULL,
	`MedicineCharge` `decimal`(18, 2) NOT NULL,
	`LabCharge` `decimal`(18, 2) NOT NULL,
	`TotalAmount` `decimal`(18, 2) NOT NULL,
	`PaymentMode` `VARCHAR`(max) NOT NULL,
	`Status` `VARCHAR`(max) NOT NULL,
	`HospitalId` `int` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
	`BillingType` `VARCHAR`(max) NOT NULL,
	`BranchId` `int` NULL,
	`SubTotal` `decimal`(18, 2) NOT NULL,
	`GstPercentage` `decimal`(18, 2) NOT NULL,
	`GstAmount` `decimal`(18, 2) NOT NULL,
 CONSTRAINT `PK_Billings` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Branches`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Branches`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`HospitalId` `int` NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
	`Phone` `VARCHAR`(max) NULL,
	`Email` `VARCHAR`(max) NULL,
	`Address` `VARCHAR`(max) NULL,
	`City` `VARCHAR`(max) NULL,
	`State` `VARCHAR`(max) NULL,
	`District` `VARCHAR`(max) NULL,
	`Country` `VARCHAR`(max) NULL,
	`PostalCode` `VARCHAR`(max) NULL,
	`IsActive` `BOOLEAN` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Branches` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`ChiefComplaints`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`ChiefComplaints`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_ChiefComplaints` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Cities`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Cities`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
	`DistrictId` `int` NOT NULL,
	`PostalCode` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_Cities` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`ClinicalNoteTemplates`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`ClinicalNoteTemplates`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
	`Notes` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_ClinicalNoteTemplates` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Clinics`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Clinics`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`ClinicName` `VARCHAR`(max) NULL,
	`Email` `VARCHAR`(max) NULL,
	`PhoneNumber` `VARCHAR`(max) NULL,
	`Address` `VARCHAR`(max) NULL,
	`City` `VARCHAR`(max) NULL,
	`State` `VARCHAR`(max) NULL,
	`District` `VARCHAR`(max) NULL,
	`Country` `VARCHAR`(max) NULL,
	`PostalCode` `VARCHAR`(max) NULL,
	`IsActive` `BOOLEAN` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Clinics` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Consultations`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Consultations`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`AppointmentId` `int` NOT NULL,
	`PatientId` `int` NOT NULL,
	`Diagnosis` `VARCHAR`(max) NOT NULL,
	`ClinicalNotes` `VARCHAR`(max) NOT NULL,
	`HospitalId` `int` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Consultations` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Districts`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Districts`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
	`StateId` `int` NOT NULL,
 CONSTRAINT `PK_Districts` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`DoctorBranches`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`DoctorBranches`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`DoctorId` `int` NOT NULL,
	`BranchId` `int` NOT NULL,
	`HospitalId` `int` NOT NULL,
	`IsActive` `BOOLEAN` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_DoctorBranches` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
) ON `PRIMARY`
/****** Object:  Table `dbo`.`DoctorDiagnoses`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`DoctorDiagnoses`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`DoctorId` `int` NOT NULL,
	`Name` `VARCHAR`(max) NULL,
	`HospitalId` `int` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_DoctorDiagnoses` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`DoctorQualifications`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`DoctorQualifications`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_DoctorQualifications` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Doctors`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Doctors`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NULL,
	`Specialization` `VARCHAR`(max) NULL,
	`Experience` `int` NOT NULL,
	`Fees` `decimal`(18, 2) NOT NULL,
	`Email` `VARCHAR`(max) NULL,
	`Image` `VARCHAR`(max) NULL,
	`Phone` `VARCHAR`(max) NULL,
	`Qualification` `VARCHAR`(max) NULL,
	`AreaofExpertise` `VARCHAR`(max) NULL,
	`BranchId` `int` NULL,
	`Role` `VARCHAR`(max) NULL,
	`IsActive` `BOOLEAN` NOT NULL,
	`HospitalId` `int` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Doctors` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`DoctorSpecializations`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`DoctorSpecializations`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_DoctorSpecializations` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Dosages`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Dosages`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_Dosages` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Frequencies`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Frequencies`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_Frequencies` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Holidays`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Holidays`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
	`Date` `DATETIME`(7) NOT NULL,
	`HospitalId` `int` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Holidays` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Hospitals`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Hospitals`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NULL,
	`Address` `VARCHAR`(max) NULL,
	`Phone` `VARCHAR`(max) NULL,
	`Email` `VARCHAR`(max) NULL,
	`City` `VARCHAR`(max) NULL,
	`State` `VARCHAR`(max) NULL,
	`District` `VARCHAR`(max) NULL,
	`Country` `VARCHAR`(max) NULL,
	`PostalCode` `VARCHAR`(max) NULL,
	`IsActive` `BOOLEAN` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Hospitals` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`InstructionTemplates`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`InstructionTemplates`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_InstructionTemplates` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`MedicalHistories`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`MedicalHistories`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`PatientId` `int` NOT NULL,
	`Allergies` `VARCHAR`(max) NULL,
	`ChronicDiseases` `VARCHAR`(max) NULL,
	`CurrentMedications` `VARCHAR`(max) NULL,
	`Surgeries` `VARCHAR`(max) NULL,
	`HospitalId` `int` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_MedicalHistories` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`MedicineNotes`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`MedicineNotes`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_MedicineNotes` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Medicines`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Medicines`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_Medicines` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Notifications`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Notifications`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Title` `VARCHAR`(max) NOT NULL,
	`Message` `VARCHAR`(max) NOT NULL,
	`PatientId` `int` NULL,
	`IsRead` `BOOLEAN` NOT NULL,
	`IsSent` `BOOLEAN` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Notifications` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`OtpVerifications`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`OtpVerifications`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Email` `VARCHAR`(max) NOT NULL,
	`Otp` `VARCHAR`(max) NOT NULL,
	`ExpiryTime` `DATETIME`(7) NOT NULL,
	`IsUsed` `BOOLEAN` NOT NULL,
	`ResetToken` `VARCHAR`(max) NULL,
	`ResetTokenExpiry` `DATETIME`(7) NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_OtpVerifications` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Patients`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Patients`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`PatientCode` `VARCHAR`(max) NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
	`Phone` `VARCHAR`(max) NOT NULL,
	`Age` `int` NOT NULL,
	`Gender` `VARCHAR`(max) NOT NULL,
	`Email` `VARCHAR`(max) NULL,
	`Address` `VARCHAR`(max) NULL,
	`BloodGroup` `VARCHAR`(max) NULL,
	`DateOfBirth` `DATETIME`(7) NULL,
	`EmergencyContactName` `VARCHAR`(max) NULL,
	`EmergencyContactPhone` `VARCHAR`(max) NULL,
	`HospitalId` `int` NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Patients` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`PatientVitals`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`PatientVitals`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`AppointmentId` `int` NOT NULL,
	`PatientId` `int` NOT NULL,
	`Symptoms` `VARCHAR`(max) NOT NULL,
	`BloodPressure` `VARCHAR`(max) NOT NULL,
	`SugarLevel` `VARCHAR`(max) NOT NULL,
	`Temperature` `VARCHAR`(max) NOT NULL,
	`Weight` `VARCHAR`(max) NOT NULL,
	`PulseRate` `VARCHAR`(max) NOT NULL,
	`RespiratoryRate` `VARCHAR`(max) NOT NULL,
	`HospitalId` `int` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_PatientVitals` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Payments`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Payments`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`AppointmentId` `int` NULL,
	`DoctorId` `int` NOT NULL,
	`BranchId` `int` NOT NULL,
	`AppointmentDate` `DATETIME`(7) NOT NULL,
	`AppointmentTime` `time`(7) NOT NULL,
	`HospitalId` `int` NOT NULL,
	`PatientId` `int` NOT NULL,
	`Amount` `decimal`(18, 2) NOT NULL,
	`PaymentMode` `VARCHAR`(max) NOT NULL,
	`Status` `VARCHAR`(max) NOT NULL,
	`TransactionId` `VARCHAR`(max) NULL,
	`PaymentDate` `DATETIME`(7) NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Payments` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`PrescriptionItems`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`PrescriptionItems`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`PrescriptionId` `int` NOT NULL,
	`MedicineName` `VARCHAR`(max) NULL,
	`Dosage` `VARCHAR`(max) NULL,
	`Frequency` `VARCHAR`(max) NULL,
	`Duration` `VARCHAR`(max) NULL,
	`Notes` `VARCHAR`(max) NULL,
 CONSTRAINT `PK_PrescriptionItems` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`PrescriptionLabTests`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`PrescriptionLabTests`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`PrescriptionId` `int` NOT NULL,
	`TestName` `VARCHAR`(250) NOT NULL,
	`Instructions` `VARCHAR`(1000) NULL,
	`Priority` `VARCHAR`(50) NOT NULL,
	`Status` `VARCHAR`(50) NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_PrescriptionLabTests` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
) ON `PRIMARY`
/****** Object:  Table `dbo`.`Prescriptions`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Prescriptions`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`AppointmentId` `int` NOT NULL,
	`PatientId` `int` NOT NULL,
	`Diagnosis` `VARCHAR`(max) NULL,
	`Instructions` `VARCHAR`(max) NULL,
	`FollowUpDate` `DATETIME`(7) NOT NULL,
	`Status` `VARCHAR`(max) NULL,
	`HospitalId` `int` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
	`IsPrinted` `BOOLEAN` NOT NULL,
	`PrintedAt` `DATETIME`(7) NULL,
	`PrintedByUserId` `int` NULL,
 CONSTRAINT `PK_Prescriptions` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Receptionists`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Receptionists`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NULL,
	`Email` `VARCHAR`(max) NULL,
	`Phone` `VARCHAR`(max) NULL,
	`PasswordHash` `VARCHAR`(max) NULL,
	`IsActive` `BOOLEAN` NOT NULL,
	`HospitalId` `int` NOT NULL,
	`BranchId` `int` NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Receptionists` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`RolePermissions`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`RolePermissions`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`RoleName` `VARCHAR`(max) NULL,
	`CanView` `BOOLEAN` NOT NULL,
	`CanCreate` `BOOLEAN` NOT NULL,
	`CanEdit` `BOOLEAN` NOT NULL,
	`CanDelete` `BOOLEAN` NOT NULL,
 CONSTRAINT `PK_RolePermissions` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Schedules`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Schedules`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`DoctorId` `int` NOT NULL,
	`StartDate` `DATETIME`(7) NOT NULL,
	`EndDate` `DATETIME`(7) NOT NULL,
	`Days` `VARCHAR`(max) NULL,
	`WorkStart` `time`(7) NOT NULL,
	`WorkEnd` `time`(7) NOT NULL,
	`BreakStart` `time`(7) NOT NULL,
	`BreakEnd` `time`(7) NOT NULL,
	`HospitalId` `int` NOT NULL,
	`BranchId` `int` NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Schedules` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`ScheduleSettings`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`ScheduleSettings`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`SlotDuration` `int` NOT NULL,
	`ClinicOpen` `time`(7) NOT NULL,
	`ClinicClose` `time`(7) NOT NULL,
	`HospitalId` `int` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_ScheduleSettings` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
) ON `PRIMARY`
/****** Object:  Table `dbo`.`Settings`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Settings`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`ApplicationName` `VARCHAR`(max) NULL,
	`SupportEmail` `VARCHAR`(max) NULL,
	`SupportPhone` `VARCHAR`(max) NULL,
	`Address` `VARCHAR`(max) NULL,
	`EmailNotificationsEnabled` `BOOLEAN` NOT NULL,
	`SmsNotificationsEnabled` `BOOLEAN` NOT NULL,
	`UpdatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Settings` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`Staffs`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Staffs`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`UserId` `int` NOT NULL,
	`Role` `VARCHAR`(max) NOT NULL,
	`IsActive` `BOOLEAN` NOT NULL,
	`HospitalId` `int` NOT NULL,
	`BranchId` `int` NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_Staffs` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`States`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`States`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NOT NULL,
 CONSTRAINT `PK_States` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`SuperAdmins`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`SuperAdmins`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NULL,
	`Email` `VARCHAR`(450) NULL,
	`PasswordHash` `VARCHAR`(max) NULL,
	`IsActive` `BOOLEAN` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_SuperAdmins` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Table `dbo`.`UserPermissions`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`UserPermissions`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`UserId` `int` NOT NULL,
	`HospitalId` `int` NOT NULL,
	`Module` `VARCHAR`(450) NOT NULL,
	`CanView` `BOOLEAN` NOT NULL,
	`CanCreate` `BOOLEAN` NOT NULL,
	`CanEdit` `BOOLEAN` NOT NULL,
	`CanDelete` `BOOLEAN` NOT NULL,
	`AssignedByUserId` `int` NOT NULL,
	`UpdatedAt` `DATETIME`(7) NOT NULL,
 CONSTRAINT `PK_UserPermissions` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
) ON `PRIMARY`
/****** Object:  Table `dbo`.`Users`    Script Date: 7/31/2026 5:43:28 PM ******/
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE `dbo`.`Users`(
	`Id` `int` AUTO_INCREMENT NOT NULL,
	`Name` `VARCHAR`(max) NULL,
	`MobileNumber` `VARCHAR`(max) NULL,
	`Email` `VARCHAR`(max) NULL,
	`PasswordHash` `VARCHAR`(max) NULL,
	`Role` `VARCHAR`(max) NULL,
	`DoctorId` `int` NULL,
	`HospitalId` `int` NULL,
	`BranchId` `int` NULL,
	`IsActive` `BOOLEAN` NOT NULL,
	`CreatedAt` `DATETIME`(7) NOT NULL,
	`MustChangePassword` `BOOLEAN` NOT NULL,
 CONSTRAINT `PK_Users` PRIMARY KEY CLUSTERED 
(
	`Id` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
INSERT `dbo`.`__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES (N'20260720093746_InitialCreate', N'8.0.26')
INSERT `dbo`.`__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES (N'20260728090000_AddUserPermissions', N'8.0.26')
INSERT `dbo`.`__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES (N'20260731064349_AddNurseBillingGstAndDoctorBranches', N'8.0.26')
INSERT `dbo`.`__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES (N'20260731103000_AddPrescriptionLabTestsAndPrintTracking', N'8.0.26')
SET IDENTITY_INSERT `dbo`.`Appointments` ON 

INSERT `dbo`.`Appointments` (`Id`, `DoctorId`, `BookingType`, `PatientId`, `Date`, `StartTime`, `TokenNumber`, `ConsultationFee`, `PaymentMode`, `PaymentStatus`, `PaymentDate`, `TransactionId`, `ChiefComplaints`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `Status`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (1, 1, N'Offline', 1, CAST(N'2026-07-30T00:00:00.0000000' AS DATETIME), CAST(N'14:15:00' AS Time), N'TKN001', CAST(500.00 AS Decimal(18, 2)), N'Cash', N'Paid', CAST(N'2026-07-30T07:45:37.7693078' AS DATETIME), NULL, N'Chest pain', N'', N'', N'', N'', N'', N'', N'Waiting', 1, 2, CAST(N'2026-07-30T07:45:37.7686553' AS DATETIME))
INSERT `dbo`.`Appointments` (`Id`, `DoctorId`, `BookingType`, `PatientId`, `Date`, `StartTime`, `TokenNumber`, `ConsultationFee`, `PaymentMode`, `PaymentStatus`, `PaymentDate`, `TransactionId`, `ChiefComplaints`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `Status`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (2, 2, N'Online', 2, CAST(N'2026-07-30T00:00:00.0000000' AS DATETIME), CAST(N'15:00:00' AS Time), N'APT-20260730092157-395', CAST(700.00 AS Decimal(18, 2)), N'Card', N'Paid', CAST(N'2026-07-30T09:21:59.1226969' AS DATETIME), N'PAT-1785403319443', N'stomach pain', NULL, NULL, NULL, NULL, NULL, NULL, N'Cancelled', 1, 1, CAST(N'2026-07-30T09:21:57.9358216' AS DATETIME))
INSERT `dbo`.`Appointments` (`Id`, `DoctorId`, `BookingType`, `PatientId`, `Date`, `StartTime`, `TokenNumber`, `ConsultationFee`, `PaymentMode`, `PaymentStatus`, `PaymentDate`, `TransactionId`, `ChiefComplaints`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `Status`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (3, 1, N'Online', 2, CAST(N'2026-07-30T00:00:00.0000000' AS DATETIME), CAST(N'16:15:00' AS Time), N'APT-20260730103436-792', CAST(500.00 AS Decimal(18, 2)), N'UPI', N'Paid', CAST(N'2026-07-30T10:34:36.7045948' AS DATETIME), N'PAT-1785407677306', N'gastric', N'120/80 mmHg', N'120 mg/dL', N'98 F', N'60 kg', N'82 bpm', N'18 breaths/min', N'Completed', 1, 2, CAST(N'2026-07-30T10:34:36.3877138' AS DATETIME))
INSERT `dbo`.`Appointments` (`Id`, `DoctorId`, `BookingType`, `PatientId`, `Date`, `StartTime`, `TokenNumber`, `ConsultationFee`, `PaymentMode`, `PaymentStatus`, `PaymentDate`, `TransactionId`, `ChiefComplaints`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `Status`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (4, 3, N'Online', 3, CAST(N'2026-07-30T00:00:00.0000000' AS DATETIME), CAST(N'17:00:00' AS Time), N'APT-20260730112158-806', CAST(400.00 AS Decimal(18, 2)), N'Card', N'Paid', CAST(N'2026-07-30T11:21:58.8468964' AS DATETIME), N'PAT-1785410519439', N'fever', N'100/80 mmHg', N'120 mg/dL', N'97 F', N'58 kg', N'82 bpm', N'16 breaths/min', N'Completed', 2, 3, CAST(N'2026-07-30T11:21:58.3986291' AS DATETIME))
INSERT `dbo`.`Appointments` (`Id`, `DoctorId`, `BookingType`, `PatientId`, `Date`, `StartTime`, `TokenNumber`, `ConsultationFee`, `PaymentMode`, `PaymentStatus`, `PaymentDate`, `TransactionId`, `ChiefComplaints`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `Status`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (5, 1, N'Offline', 1, CAST(N'2026-07-31T00:00:00.0000000' AS DATETIME), CAST(N'11:15:00' AS Time), N'TKN001', CAST(500.00 AS Decimal(18, 2)), N'UPI', N'Paid', CAST(N'2026-07-30T12:23:16.4905974' AS DATETIME), N'CONS-1785414195810', N'Chest pain', N'', N'', N'', N'', N'', N'', N'Waiting', 1, 2, CAST(N'2026-07-30T12:23:16.4905950' AS DATETIME))
INSERT `dbo`.`Appointments` (`Id`, `DoctorId`, `BookingType`, `PatientId`, `Date`, `StartTime`, `TokenNumber`, `ConsultationFee`, `PaymentMode`, `PaymentStatus`, `PaymentDate`, `TransactionId`, `ChiefComplaints`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `Status`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (6, 3, N'Online', 4, CAST(N'2026-07-31T00:00:00.0000000' AS DATETIME), CAST(N'14:00:00' AS Time), N'APT-20260731071017-529', CAST(400.00 AS Decimal(18, 2)), N'UPI', N'Paid', CAST(N'2026-07-31T07:10:18.4658776' AS DATETIME), N'PAT-1785481816432', N'Headech', N'120/100 mmHg', N'120 mg/dL', N'97 F', N'60 kg', N'82 bpm', N'18 breaths/min', N'Waiting', 2, 3, CAST(N'2026-07-31T07:10:17.4034788' AS DATETIME))
INSERT `dbo`.`Appointments` (`Id`, `DoctorId`, `BookingType`, `PatientId`, `Date`, `StartTime`, `TokenNumber`, `ConsultationFee`, `PaymentMode`, `PaymentStatus`, `PaymentDate`, `TransactionId`, `ChiefComplaints`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `Status`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (7, 3, N'Online', 5, CAST(N'2026-07-31T00:00:00.0000000' AS DATETIME), CAST(N'14:30:00' AS Time), N'APT-20260731071423-688', CAST(400.00 AS Decimal(18, 2)), N'Card', N'Paid', CAST(N'2026-07-31T07:14:24.4387236' AS DATETIME), N'PAT-1785482062476', N'stomach pain', NULL, NULL, NULL, NULL, NULL, NULL, N'Waiting', 2, 3, CAST(N'2026-07-31T07:14:23.8939891' AS DATETIME))
INSERT `dbo`.`Appointments` (`Id`, `DoctorId`, `BookingType`, `PatientId`, `Date`, `StartTime`, `TokenNumber`, `ConsultationFee`, `PaymentMode`, `PaymentStatus`, `PaymentDate`, `TransactionId`, `ChiefComplaints`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `Status`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (8, 3, N'Online', 7, CAST(N'2026-07-31T00:00:00.0000000' AS DATETIME), CAST(N'15:00:00' AS Time), N'APT-20260731072012-650', CAST(400.00 AS Decimal(18, 2)), N'UPI', N'Paid', CAST(N'2026-07-31T07:20:12.4650803' AS DATETIME), N'PAT-1785482410495', N'cold and caugh', NULL, NULL, NULL, NULL, NULL, NULL, N'Completed', 2, 3, CAST(N'2026-07-31T07:20:12.1314903' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Appointments` OFF
SET IDENTITY_INSERT `dbo`.`AuditLogs` ON 

INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (1, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T06:01:43.0678977' AS DATETIME), NULL, 1, CAST(N'2026-07-30T06:01:43.0679854' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (2, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T06:05:34.8355248' AS DATETIME), NULL, 1, CAST(N'2026-07-30T06:05:34.8355250' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (3, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T06:05:40.5683100' AS DATETIME), NULL, 0, CAST(N'2026-07-30T06:05:40.6012444' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (4, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Created clinic', N'Clinics', 0, N'', NULL, NULL, CAST(N'2026-07-30T06:15:48.1288569' AS DATETIME), NULL, 0, CAST(N'2026-07-30T06:15:48.1309375' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (5, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Created admin', N'Admins', 0, N'', NULL, NULL, CAST(N'2026-07-30T06:16:24.1465688' AS DATETIME), NULL, 0, CAST(N'2026-07-30T06:16:24.1473981' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (6, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Sent notification', N'Notifications', 0, N'', NULL, NULL, CAST(N'2026-07-30T06:21:27.1373795' AS DATETIME), NULL, 0, CAST(N'2026-07-30T06:21:27.1381695' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (7, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T06:22:43.0732117' AS DATETIME), NULL, 1, CAST(N'2026-07-30T06:22:43.0732119' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (8, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Super Admin logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T06:22:44.3034447' AS DATETIME), NULL, 0, CAST(N'2026-07-30T06:22:44.3038280' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (9, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T06:22:47.9903542' AS DATETIME), NULL, 0, CAST(N'2026-07-30T06:22:47.9907229' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (10, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T06:46:13.6893378' AS DATETIME), NULL, 1, CAST(N'2026-07-30T06:46:13.6893379' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (11, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T06:46:15.6512979' AS DATETIME), NULL, 0, CAST(N'2026-07-30T06:46:15.6526304' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (12, 3, N'Durga Prasad', N'Doctor', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T06:50:37.2816505' AS DATETIME), NULL, 1, CAST(N'2026-07-30T06:50:37.2816509' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (13, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T06:53:04.3999451' AS DATETIME), NULL, 1, CAST(N'2026-07-30T06:53:04.3999455' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (14, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T06:53:09.5957161' AS DATETIME), NULL, 0, CAST(N'2026-07-30T06:53:09.5966657' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (15, 3, N'Durga Prasad', N'Doctor', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T07:29:26.0802702' AS DATETIME), NULL, 1, CAST(N'2026-07-30T07:29:26.0802703' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (16, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T07:31:09.4312566' AS DATETIME), NULL, 1, CAST(N'2026-07-30T07:31:09.4312568' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (17, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T07:33:53.5103204' AS DATETIME), NULL, 1, CAST(N'2026-07-30T07:33:53.5103205' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (18, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Durga Prasad logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T07:34:07.5246620' AS DATETIME), NULL, 0, CAST(N'2026-07-30T07:34:07.5258527' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (19, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T07:34:21.0196284' AS DATETIME), NULL, 0, CAST(N'2026-07-30T07:34:21.0197508' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (20, 3, N'Durga Prasad', N'Doctor', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T07:48:22.6820460' AS DATETIME), NULL, 1, CAST(N'2026-07-30T07:48:22.6820462' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (21, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T08:22:55.0357514' AS DATETIME), NULL, 1, CAST(N'2026-07-30T08:22:55.0357515' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (22, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T08:49:33.3284234' AS DATETIME), NULL, 1, CAST(N'2026-07-30T08:49:33.3284236' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (23, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T08:49:58.5529224' AS DATETIME), NULL, 0, CAST(N'2026-07-30T08:49:58.5531650' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (24, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T09:02:10.2877012' AS DATETIME), NULL, 1, CAST(N'2026-07-30T09:02:10.2877014' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (25, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Vara Lakshmi logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T09:02:25.9568063' AS DATETIME), NULL, 0, CAST(N'2026-07-30T09:02:25.9570221' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (26, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T09:02:26.1619545' AS DATETIME), NULL, 0, CAST(N'2026-07-30T09:02:26.1623199' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (27, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T09:05:18.0281142' AS DATETIME), NULL, 1, CAST(N'2026-07-30T09:05:18.0281144' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (28, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T09:05:32.0557348' AS DATETIME), NULL, 0, CAST(N'2026-07-30T09:05:32.0562954' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (29, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T09:07:12.9624022' AS DATETIME), NULL, 1, CAST(N'2026-07-30T09:07:12.9624025' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (30, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T09:07:39.2966445' AS DATETIME), NULL, 0, CAST(N'2026-07-30T09:07:39.2968427' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (31, 6, N'Jyothi Mutyala', N'Patient', 1, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T09:19:09.5306820' AS DATETIME), NULL, 1, CAST(N'2026-07-30T09:19:09.5306824' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (32, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T09:56:16.0670415' AS DATETIME), NULL, 1, CAST(N'2026-07-30T09:56:16.0670416' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (33, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T09:56:41.2513239' AS DATETIME), NULL, 0, CAST(N'2026-07-30T09:56:41.2541931' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (34, 3, N'Durga Prasad', N'Doctor', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T09:57:59.1415329' AS DATETIME), NULL, 1, CAST(N'2026-07-30T09:57:59.1415331' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (35, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:00:32.6104712' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:00:32.6104714' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (36, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:23:21.7390375' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:23:21.7390377' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (37, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T10:23:26.2466139' AS DATETIME), NULL, 0, CAST(N'2026-07-30T10:23:26.2477124' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (38, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:24:07.6395603' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:24:07.6395605' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (39, 6, N'Jyothi Mutyala', N'Patient', 1, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:26:14.6660092' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:26:14.6660094' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (40, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:35:26.7977327' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:35:26.7977332' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (41, 5, N'Devi', N'', 1, 2, N'Update Patient Vitals', N'Vitals recorded for appointment 3 and patient Jyothi Mutyala', 0, N'::1', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', NULL, CAST(N'2026-07-30T10:36:10.0898709' AS DATETIME), NULL, 0, CAST(N'2026-07-30T10:36:10.0898701' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (42, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:36:54.5574972' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:36:54.5574974' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (43, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T10:37:20.3714919' AS DATETIME), NULL, 0, CAST(N'2026-07-30T10:37:20.3717902' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (44, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Jyothi Mutyala logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T10:37:20.5420402' AS DATETIME), NULL, 0, CAST(N'2026-07-30T10:37:20.5421448' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (45, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:37:20.5756926' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:37:20.5756928' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (46, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T10:37:25.5877872' AS DATETIME), NULL, 0, CAST(N'2026-07-30T10:37:25.5886011' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (47, 3, N'Durga Prasad', N'Doctor', 1, 2, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:39:15.2330287' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:39:15.2330290' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (48, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:54:09.1826153' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:54:09.1826155' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (49, 6, N'Jyothi Mutyala', N'Patient', 1, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T10:55:29.1587023' AS DATETIME), NULL, 1, CAST(N'2026-07-30T10:55:29.1587025' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (50, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:07:55.6606165' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:07:55.6606167' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (51, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Devi logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T11:08:07.4782090' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:08:07.4786175' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (52, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T11:08:07.6463379' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:08:07.6466199' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (53, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:08:32.4321689' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:08:32.4321692' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (54, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T11:08:56.7807057' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:08:56.7808282' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (55, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Created clinic', N'Clinics', 0, N'27.57.89.111', NULL, NULL, CAST(N'2026-07-30T11:10:30.5904222' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:10:30.5906272' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (56, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Created admin', N'Admins', 0, N'27.57.89.111', NULL, NULL, CAST(N'2026-07-30T11:11:22.6656400' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:11:22.6696882' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (57, 7, N'Jyothi Mutyala', N'Admin', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:12:28.7529324' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:12:28.7529327' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (58, 7, N'Jyothi Mutyala', N'Admin', 2, NULL, N'Jyothi Mutyala logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T11:12:33.4366363' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:12:33.4367885' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (59, 7, N'Jyothi Mutyala', N'Admin', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:12:55.9992279' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:12:55.9992281' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (60, 7, N'Jyothi Mutyala', N'Admin', 2, NULL, N'Jyothi Mutyala logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T11:13:00.2183499' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:13:00.2191211' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (61, 7, N'Jyothi Mutyala', N'Admin', 2, NULL, N'Patient logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T11:19:39.9799931' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:19:39.9849093' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (62, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:21:08.4615678' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:21:08.4615685' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (63, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:35:30.2322691' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:35:30.2322694' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (64, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:49:34.6439748' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:49:34.6439751' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (65, 7, N'Jyothi Mutyala', N'Admin', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:50:20.8834817' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:50:20.8834819' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (66, 7, N'Jyothi Mutyala', N'Admin', 2, NULL, N'Karuna Mutyala logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T11:50:27.0401078' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:50:27.0430590' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (67, 7, N'Jyothi Mutyala', N'Admin', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:51:07.3616003' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:51:07.3616005' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (68, 7, N'Jyothi Mutyala', N'Admin', 2, NULL, N'Jyothi Mutyala logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T11:51:27.1290961' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:51:27.1293497' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (69, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:52:43.6877227' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:52:43.6877229' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (70, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:52:49.9685358' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:52:49.9685361' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (71, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:54:16.1494588' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:54:16.1494591' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (72, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T11:54:29.5107625' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:54:29.5110086' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (73, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:56:47.7735945' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:56:47.7735947' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (74, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T11:56:52.7392897' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:56:52.7403240' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (75, 9, N'Karuna Mutyala', N'Receptionist', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T11:57:50.8674527' AS DATETIME), NULL, 1, CAST(N'2026-07-30T11:57:50.8674531' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (76, 9, N'Karuna Mutyala', N'', 2, 3, N'Update Patient Vitals', N'Vitals recorded for appointment 4 and patient Karuna Mutyala', 0, N'::1', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', NULL, CAST(N'2026-07-30T11:59:20.6499725' AS DATETIME), NULL, 0, CAST(N'2026-07-30T11:59:20.6499714' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (77, 8, N'Jyothi', N'Doctor', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:00:18.5543491' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:00:18.5543492' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (78, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:00:34.8409030' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:00:34.8409031' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (79, 8, N'Jyothi', N'Doctor', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:00:51.1268544' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:00:51.1268547' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (80, 9, N'Karuna Mutyala', N'Receptionist', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:03:28.6693273' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:03:28.6693277' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (81, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:04:26.1525767' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:04:26.1525769' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (82, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:04:44.1953167' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:04:44.1953168' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (83, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:29:15.7274231' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:29:15.7274231' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (84, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Devi logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T12:29:17.9541091' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:29:17.9551540' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (85, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T12:29:19.8040370' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:29:19.8041638' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (86, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:32:56.4176926' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:32:56.4176927' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (87, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Pilla Durga Prasad logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T12:33:14.7691704' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:33:14.7698660' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (88, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T12:33:22.9620886' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:33:22.9621652' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (89, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:49:45.8428585' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:49:45.8430620' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (90, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T12:49:52.7675814' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:49:52.7913329' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (91, 3, N'Durga Prasad', N'Doctor', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:52:12.4006900' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:52:12.4006902' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (92, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:53:12.3806019' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:53:12.3806022' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (93, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:53:34.5201648' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:53:34.5201650' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (94, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Devi logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T12:53:35.9545630' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:53:35.9551203' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (95, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Devi logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T12:53:38.4559736' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:53:38.4567227' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (96, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:54:12.3089201' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:54:12.3089203' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (97, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:55:55.3234048' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:55:55.3234051' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (98, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Devi logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-30T12:55:58.0260256' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:55:58.0263982' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (99, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T12:56:00.1011182' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:56:00.1013994' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (100, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:57:37.4564221' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:57:37.4564223' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (101, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T12:57:58.9671751' AS DATETIME), NULL, 0, CAST(N'2026-07-30T12:57:58.9674988' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (102, 3, N'Durga Prasad', N'Doctor', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:59:00.8558450' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:59:00.8558472' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (103, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T12:59:40.0489083' AS DATETIME), NULL, 1, CAST(N'2026-07-30T12:59:40.0489085' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (104, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T13:18:22.4501981' AS DATETIME), NULL, 1, CAST(N'2026-07-30T13:18:22.4501984' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (105, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T13:18:44.9573926' AS DATETIME), NULL, 1, CAST(N'2026-07-30T13:18:44.9573928' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (106, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-30T13:18:49.7830262' AS DATETIME), NULL, 0, CAST(N'2026-07-30T13:18:49.7833747' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (107, 3, N'Durga Prasad', N'Doctor', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:9170:f0d5:9348:ce38:21b2', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-30T13:20:00.7215897' AS DATETIME), NULL, 1, CAST(N'2026-07-30T13:20:00.7215901' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (108, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:02:00.8840432' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:02:00.8841949' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (109, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:03:11.6437146' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:03:11.6437149' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (110, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T03:03:15.5222393' AS DATETIME), NULL, 0, CAST(N'2026-07-31T03:03:15.5318956' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (111, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:04:13.6391494' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:04:13.6391495' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (112, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:25:37.3670942' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:25:37.3670943' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (113, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T03:25:40.9087309' AS DATETIME), NULL, 0, CAST(N'2026-07-31T03:25:40.9089889' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (114, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:33:26.1768630' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:33:26.1768632' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (115, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:50:00.6931618' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:50:00.6932313' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (116, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:50:08.4122377' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:50:08.4122378' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (117, 9, N'Karuna Mutyala', N'Receptionist', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:51:23.1313167' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:51:23.1313169' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (118, 8, N'Jyothi', N'Doctor', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:52:20.9510019' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:52:20.9510020' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (119, 9, N'Karuna Mutyala', N'Receptionist', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:53:03.1102391' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:53:03.1102391' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (120, 9, N'Karuna Mutyala', N'', 2, 3, N'Update Patient Vitals', N'Vitals recorded for appointment 4 and patient Karuna Mutyala', 0, N'::1', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', NULL, CAST(N'2026-07-31T03:53:23.4895269' AS DATETIME), NULL, 0, CAST(N'2026-07-31T03:53:23.4895263' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (121, 8, N'Jyothi', N'Doctor', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:54:02.8420439' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:54:02.8420440' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (122, 9, N'Karuna Mutyala', N'Receptionist', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:56:07.7865895' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:56:07.7865897' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (123, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:ed03:90ca:4330:9d9f', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T03:57:46.5655614' AS DATETIME), NULL, 1, CAST(N'2026-07-31T03:57:46.5655615' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (124, 8, N'Jyothi', N'Doctor', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T04:39:42.8969176' AS DATETIME), NULL, 1, CAST(N'2026-07-31T04:39:42.8969177' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (125, 9, N'Karuna Mutyala', N'Receptionist', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T04:41:54.5464064' AS DATETIME), NULL, 1, CAST(N'2026-07-31T04:41:54.5464067' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (126, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T04:45:11.1859812' AS DATETIME), NULL, 1, CAST(N'2026-07-31T04:45:11.1859814' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (127, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T04:47:42.8885237' AS DATETIME), NULL, 1, CAST(N'2026-07-31T04:47:42.8885239' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (128, 6, N'Jyothi Mutyala', N'Patient', 1, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T04:53:21.4655545' AS DATETIME), NULL, 1, CAST(N'2026-07-31T04:53:21.4656698' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (129, 9, N'Karuna Mutyala', N'Receptionist', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T04:54:38.8975696' AS DATETIME), NULL, 1, CAST(N'2026-07-31T04:54:38.8975698' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (130, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T04:56:55.2261155' AS DATETIME), NULL, 1, CAST(N'2026-07-31T04:56:55.2261159' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (131, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T05:05:00.5306016' AS DATETIME), NULL, 1, CAST(N'2026-07-31T05:05:00.5306018' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (132, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T05:05:05.3103131' AS DATETIME), NULL, 0, CAST(N'2026-07-31T05:05:05.3367309' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (133, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T05:19:28.7542454' AS DATETIME), NULL, 1, CAST(N'2026-07-31T05:19:28.7542456' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (134, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T05:25:20.6231515' AS DATETIME), NULL, 1, CAST(N'2026-07-31T05:25:20.6231516' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (135, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T05:26:37.2165189' AS DATETIME), NULL, 1, CAST(N'2026-07-31T05:26:37.2165194' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (136, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T05:26:41.9918350' AS DATETIME), NULL, 0, CAST(N'2026-07-31T05:26:41.9948039' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (137, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T05:53:42.1694357' AS DATETIME), NULL, 1, CAST(N'2026-07-31T05:53:42.1694359' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (138, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T05:59:50.3511249' AS DATETIME), NULL, 1, CAST(N'2026-07-31T05:59:50.3511255' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (139, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'49.43.225.202', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T06:10:31.2506307' AS DATETIME), NULL, 1, CAST(N'2026-07-31T06:10:31.2506308' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (140, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T06:37:01.2165666' AS DATETIME), NULL, 1, CAST(N'2026-07-31T06:37:01.2166218' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (141, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T06:37:07.2719424' AS DATETIME), NULL, 0, CAST(N'2026-07-31T06:37:07.3019429' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (142, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T06:44:02.4460568' AS DATETIME), NULL, 1, CAST(N'2026-07-31T06:44:02.4462059' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (143, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:01:26.8321476' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:01:26.8321479' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (144, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:02:18.1202273' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:02:18.1202277' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (145, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:07:47.7393092' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:07:47.7393094' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (146, 12, N'Anusha Sharma', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:07:50.9063549' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:07:50.9063552' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (147, 13, N'Vasanth Reddy', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:12:45.1708982' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:12:45.1708984' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (148, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:13:25.7424921' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:13:25.7424924' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (149, 14, N'Lakshmi Prasanthi', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:19:32.1616108' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:19:32.1616110' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (150, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:20:04.8251341' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:20:04.8251346' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (151, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:24:30.1956484' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:24:30.1956488' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (152, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:38:54.6459233' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:38:54.6459235' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (153, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T07:42:58.1285947' AS DATETIME), NULL, 1, CAST(N'2026-07-31T07:42:58.1285949' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (154, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T08:01:25.2871025' AS DATETIME), NULL, 1, CAST(N'2026-07-31T08:01:25.2871027' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (155, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T08:07:13.9959200' AS DATETIME), NULL, 1, CAST(N'2026-07-31T08:07:13.9959202' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (156, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T08:09:14.4612780' AS DATETIME), NULL, 1, CAST(N'2026-07-31T08:09:14.4612783' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (157, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T08:09:18.1018029' AS DATETIME), NULL, 0, CAST(N'2026-07-31T08:09:18.1893550' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (158, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T08:09:43.3334877' AS DATETIME), NULL, 1, CAST(N'2026-07-31T08:09:43.3334879' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (159, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T09:14:45.0426411' AS DATETIME), NULL, 1, CAST(N'2026-07-31T09:14:45.0426413' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (160, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T09:15:50.6519945' AS DATETIME), NULL, 1, CAST(N'2026-07-31T09:15:50.6519947' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (161, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T09:15:55.5032167' AS DATETIME), NULL, 0, CAST(N'2026-07-31T09:15:55.5088934' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (162, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T09:16:36.8228199' AS DATETIME), NULL, 1, CAST(N'2026-07-31T09:16:36.8228202' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (163, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T09:30:39.4955575' AS DATETIME), NULL, 1, CAST(N'2026-07-31T09:30:39.4955577' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (164, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T09:32:07.1446101' AS DATETIME), NULL, 1, CAST(N'2026-07-31T09:32:07.1446103' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (165, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T09:37:21.7925281' AS DATETIME), NULL, 1, CAST(N'2026-07-31T09:37:21.7925283' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (166, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T09:38:05.4218217' AS DATETIME), NULL, 1, CAST(N'2026-07-31T09:38:05.4218219' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (167, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T09:53:30.2865572' AS DATETIME), NULL, 1, CAST(N'2026-07-31T09:53:30.2865573' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (168, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T09:57:08.0989074' AS DATETIME), NULL, 1, CAST(N'2026-07-31T09:57:08.0989075' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (169, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T10:16:26.2641013' AS DATETIME), NULL, 1, CAST(N'2026-07-31T10:16:26.2641015' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (170, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Pilla logged out', N'Logout', 0, N'', NULL, NULL, CAST(N'2026-07-31T10:16:37.7076714' AS DATETIME), NULL, 0, CAST(N'2026-07-31T10:16:37.7106321' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (171, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T10:16:50.7034097' AS DATETIME), NULL, 0, CAST(N'2026-07-31T10:16:50.7045003' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (172, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T10:25:44.2198660' AS DATETIME), NULL, 1, CAST(N'2026-07-31T10:25:44.2198661' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (173, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T10:26:10.6319176' AS DATETIME), NULL, 0, CAST(N'2026-07-31T10:26:10.6360717' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (174, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T10:37:04.9465693' AS DATETIME), NULL, 1, CAST(N'2026-07-31T10:37:04.9465694' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (175, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T10:37:31.4958645' AS DATETIME), NULL, 0, CAST(N'2026-07-31T10:37:31.4974725' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (176, 3, N'Durga Prasad', N'Doctor', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T10:42:27.2364804' AS DATETIME), NULL, 1, CAST(N'2026-07-31T10:42:27.2364809' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (177, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'49.43.225.202', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:02:31.9650727' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:02:31.9651849' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (178, 9, N'Karuna Mutyala', N'', 2, 3, N'Update Patient Vitals', N'Vitals recorded for appointment 6 and patient Anusha Sharma', 0, N'::1', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', NULL, CAST(N'2026-07-31T11:10:13.7494046' AS DATETIME), NULL, 0, CAST(N'2026-07-31T11:10:13.7494042' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (179, 8, N'Jyothi', N'Doctor', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:10:41.3833874' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:10:41.3833882' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (180, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:14:15.1818553' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:14:15.1818555' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (181, 11, N'Pilla', N'Nurse', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:14:40.8200764' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:14:40.8200767' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (182, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:15:08.4244821' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:15:08.4244823' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (183, 2, N'Pilla Durga Prasad', N'Admin', 1, NULL, N'Pilla Durga Prasad logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T11:15:13.5216034' AS DATETIME), NULL, 0, CAST(N'2026-07-31T11:15:13.5402466' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (184, 3, N'Durga Prasad', N'Doctor', 1, 1, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:16:32.2514504' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:16:32.2514508' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (185, 5, N'Devi', N'Receptionist', 1, 2, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:18:23.6970969' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:18:23.6970972' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (186, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:b200:2016:aa22:40e8:6509:ab52:5cab', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:20:11.3697947' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:20:11.3697949' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (187, 9, N'Karuna Mutyala', N'Receptionist', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:22:41.5732091' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:22:41.5732092' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (188, 9, N'Karuna Mutyala', N'', 2, 3, N'Update Patient Vitals', N'Vitals recorded for appointment 6 and patient Anusha Sharma', 0, N'::1', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', NULL, CAST(N'2026-07-31T11:22:58.2302762' AS DATETIME), NULL, 0, CAST(N'2026-07-31T11:22:58.2302755' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (189, 8, N'Jyothi', N'Doctor', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:24:15.5004289' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:24:15.5004291' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (190, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Login', N'User Login', 1, N'183.82.2.138', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:41:03.6443682' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:41:03.6444548' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (191, 9, N'Karuna Mutyala', N'Receptionist', 2, 3, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:41:10.4777358' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:41:10.4777361' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (192, 1, N'Super Admin', N'SuperAdmin', NULL, NULL, N'Super Admin logged in', N'Login', 1, N'', NULL, NULL, CAST(N'2026-07-31T11:41:29.5927360' AS DATETIME), NULL, 0, CAST(N'2026-07-31T11:41:29.6036143' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (193, 10, N'Karuna Mutyala', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:42:10.7677102' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:42:10.7677104' AS DATETIME))
INSERT `dbo`.`AuditLogs` (`Id`, `UserId`, `UserName`, `Role`, `ClinicId`, `BranchId`, `Action`, `SystemAction`, `IsLoginActivity`, `IpAddress`, `Browser`, `Device`, `LoginTime`, `LogoutTime`, `IsOnline`, `Timestamp`) VALUES (194, 14, N'Lakshmi Prasanthi', N'Patient', 2, NULL, N'Login', N'User Login', 1, N'2401:4900:1cb1:90d5:61ee:65c1:8ed5:c12e', N'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36', N'Desktop', CAST(N'2026-07-31T11:43:13.0733421' AS DATETIME), NULL, 1, CAST(N'2026-07-31T11:43:13.0733424' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`AuditLogs` OFF
SET IDENTITY_INSERT `dbo`.`Billings` ON 

INSERT `dbo`.`Billings` (`Id`, `AppointmentId`, `PatientId`, `DoctorId`, `ConsultationCharge`, `MedicineCharge`, `LabCharge`, `TotalAmount`, `PaymentMode`, `Status`, `HospitalId`, `CreatedAt`, `BillingType`, `BranchId`, `SubTotal`, `GstPercentage`, `GstAmount`) VALUES (1, 3, 2, 1, CAST(500.00 AS Decimal(18, 2)), CAST(300.00 AS Decimal(18, 2)), CAST(2000.00 AS Decimal(18, 2)), CAST(2800.00 AS Decimal(18, 2)), N'UPI', N'Paid', 1, CAST(N'2026-07-30T10:54:38.8693071' AS DATETIME), N'OP', NULL, CAST(0.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)))
INSERT `dbo`.`Billings` (`Id`, `AppointmentId`, `PatientId`, `DoctorId`, `ConsultationCharge`, `MedicineCharge`, `LabCharge`, `TotalAmount`, `PaymentMode`, `Status`, `HospitalId`, `CreatedAt`, `BillingType`, `BranchId`, `SubTotal`, `GstPercentage`, `GstAmount`) VALUES (2, 5, 1, 1, CAST(500.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), CAST(5664.00 AS Decimal(18, 2)), CAST(6164.00 AS Decimal(18, 2)), N'UPI', N'Paid', 1, CAST(N'2026-07-30T13:10:23.9147276' AS DATETIME), N'OP', NULL, CAST(0.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)))
INSERT `dbo`.`Billings` (`Id`, `AppointmentId`, `PatientId`, `DoctorId`, `ConsultationCharge`, `MedicineCharge`, `LabCharge`, `TotalAmount`, `PaymentMode`, `Status`, `HospitalId`, `CreatedAt`, `BillingType`, `BranchId`, `SubTotal`, `GstPercentage`, `GstAmount`) VALUES (3, 5, 1, 1, CAST(500.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), CAST(5664.00 AS Decimal(18, 2)), CAST(6164.00 AS Decimal(18, 2)), N'UPI', N'Paid', 1, CAST(N'2026-07-30T13:14:58.1397215' AS DATETIME), N'OP', NULL, CAST(0.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)))
SET IDENTITY_INSERT `dbo`.`Billings` OFF
SET IDENTITY_INSERT `dbo`.`Branches` ON 

INSERT `dbo`.`Branches` (`Id`, `HospitalId`, `Name`, `Phone`, `Email`, `Address`, `City`, `State`, `District`, `Country`, `PostalCode`, `IsActive`, `CreatedAt`) VALUES (1, 1, N'Kuku', N'9873625226', N'kuku@gmail.com', N'Lic Building Backside, I.M.Colony, Hyderabad, Telangana, India, 500082', N'Hyderabad', N'Telangana', N'Hyderabad', N'India', N'500082', 1, CAST(N'2026-07-30T06:25:04.8979857' AS DATETIME))
INSERT `dbo`.`Branches` (`Id`, `HospitalId`, `Name`, `Phone`, `Email`, `Address`, `City`, `State`, `District`, `Country`, `PostalCode`, `IsActive`, `CreatedAt`) VALUES (2, 1, N'Dmp', N'9857436346', N'Dmp@gmail.com', N'Baba Temple Opposite, Gaganpahad, K.V.Rangareddy, Telangana, India, 500052', N'K.V.Rangareddy', N'Telangana', N'K.V.Rangareddy', N'India', N'500052', 1, CAST(N'2026-07-30T06:26:22.1202739' AS DATETIME))
INSERT `dbo`.`Branches` (`Id`, `HospitalId`, `Name`, `Phone`, `Email`, `Address`, `City`, `State`, `District`, `Country`, `PostalCode`, `IsActive`, `CreatedAt`) VALUES (3, 2, N'Hanuman', N'9729362735', N'jyothimutyala543@gmail.com', N'College Road, Jntu Kukat Pally, Hyderabad, Telangana, India, 500085', N'Hyderabad', N'Telangana', N'Hyderabad', N'India', N'500085', 1, CAST(N'2026-07-30T11:15:11.7665337' AS DATETIME))
INSERT `dbo`.`Branches` (`Id`, `HospitalId`, `Name`, `Phone`, `Email`, `Address`, `City`, `State`, `District`, `Country`, `PostalCode`, `IsActive`, `CreatedAt`) VALUES (4, 2, N'Kukatpally', N'9177881861', N'jyothimutyala543@gmail.com', N'Kukatpally, Jntu Kukat Pally, Hyderabad, Telangana, India, 500085', N'Hyderabad', N'Telangana', N'Hyderabad', N'India', N'500085', 1, CAST(N'2026-07-30T11:52:01.6781281' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Branches` OFF
SET IDENTITY_INSERT `dbo`.`ClinicalNoteTemplates` ON 

INSERT `dbo`.`ClinicalNoteTemplates` (`Id`, `Name`, `Notes`) VALUES (1, N'Burn Injury', N'Subjective


Assessment


Plan')
INSERT `dbo`.`ClinicalNoteTemplates` (`Id`, `Name`, `Notes`) VALUES (2, N'Fever', N'Subjective
Subjective


Assessment


Plan

Assessment


Plan')
SET IDENTITY_INSERT `dbo`.`ClinicalNoteTemplates` OFF
SET IDENTITY_INSERT `dbo`.`Consultations` ON 

INSERT `dbo`.`Consultations` (`Id`, `AppointmentId`, `PatientId`, `Diagnosis`, `ClinicalNotes`, `HospitalId`, `CreatedAt`) VALUES (1, 3, 2, N'Burn Injury', N'Subjective


Assessment


Plan', 1, CAST(N'2026-07-30T10:50:01.0107894' AS DATETIME))
INSERT `dbo`.`Consultations` (`Id`, `AppointmentId`, `PatientId`, `Diagnosis`, `ClinicalNotes`, `HospitalId`, `CreatedAt`) VALUES (2, 4, 3, N'Fever', N'Subjective
Subjective


Assessment


Plan

Assessment


Plan', 2, CAST(N'2026-07-30T12:02:13.6733210' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Consultations` OFF
SET IDENTITY_INSERT `dbo`.`DoctorBranches` ON 

INSERT `dbo`.`DoctorBranches` (`Id`, `DoctorId`, `BranchId`, `HospitalId`, `IsActive`, `CreatedAt`) VALUES (1, 1, 1, 1, 1, CAST(N'2026-07-31T11:03:19.8372629' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`DoctorBranches` OFF
SET IDENTITY_INSERT `dbo`.`DoctorDiagnoses` ON 

INSERT `dbo`.`DoctorDiagnoses` (`Id`, `DoctorId`, `Name`, `HospitalId`, `CreatedAt`) VALUES (1, 1, N'Burn Injury', 1, CAST(N'2026-07-30T10:50:00.9289577' AS DATETIME))
INSERT `dbo`.`DoctorDiagnoses` (`Id`, `DoctorId`, `Name`, `HospitalId`, `CreatedAt`) VALUES (2, 3, N'Fever', 2, CAST(N'2026-07-30T12:02:13.6552032' AS DATETIME))
INSERT `dbo`.`DoctorDiagnoses` (`Id`, `DoctorId`, `Name`, `HospitalId`, `CreatedAt`) VALUES (3, 3, N'Migraine', 2, CAST(N'2026-07-31T11:12:17.9661591' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`DoctorDiagnoses` OFF
SET IDENTITY_INSERT `dbo`.`Doctors` ON 

INSERT `dbo`.`Doctors` (`Id`, `Name`, `Specialization`, `Experience`, `Fees`, `Email`, `Image`, `Phone`, `Qualification`, `AreaofExpertise`, `BranchId`, `Role`, `IsActive`, `HospitalId`, `CreatedAt`) VALUES (1, N'Durga Prasad', N'Cardiology', 3, CAST(500.00 AS Decimal(18, 2)), N'durgaprasad.pilla4321@gmail.com', N'/images/doctors/87d3d583-84bd-4836-90f2-a21918594a4b.jpg', N'9785845635', N'MBBS', N'Heart Diseases', 1, N'Doctor', 1, 1, CAST(N'2026-07-30T06:34:56.9546638' AS DATETIME))
INSERT `dbo`.`Doctors` (`Id`, `Name`, `Specialization`, `Experience`, `Fees`, `Email`, `Image`, `Phone`, `Qualification`, `AreaofExpertise`, `BranchId`, `Role`, `IsActive`, `HospitalId`, `CreatedAt`) VALUES (2, N'Laxmi', N'Gynecology', 5, CAST(700.00 AS Decimal(18, 2)), N'pilladurgaprasad669@gmail.com', N'/images/doctors/46c19b66-044d-4e50-9c50-233f8773d88d.jpg', N'9585684745', N'MBBS', N'Gynecological Surgery', 2, N'Doctor', 1, 1, CAST(N'2026-07-30T07:21:46.9267607' AS DATETIME))
INSERT `dbo`.`Doctors` (`Id`, `Name`, `Specialization`, `Experience`, `Fees`, `Email`, `Image`, `Phone`, `Qualification`, `AreaofExpertise`, `BranchId`, `Role`, `IsActive`, `HospitalId`, `CreatedAt`) VALUES (3, N'Jyothi', N'General Specialist', 3, CAST(400.00 AS Decimal(18, 2)), N'jyothimutyala544@gmail.com', NULL, N'9177881856', N'MD', N'General Medical', 3, N'Doctor', 1, 2, CAST(N'2026-07-30T11:18:09.0625384' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Doctors` OFF
SET IDENTITY_INSERT `dbo`.`DoctorSpecializations` ON 

INSERT `dbo`.`DoctorSpecializations` (`Id`, `Name`) VALUES (1, N'Cardiology')
INSERT `dbo`.`DoctorSpecializations` (`Id`, `Name`) VALUES (2, N'Gynecology')
INSERT `dbo`.`DoctorSpecializations` (`Id`, `Name`) VALUES (3, N'General Specialist')
SET IDENTITY_INSERT `dbo`.`DoctorSpecializations` OFF
SET IDENTITY_INSERT `dbo`.`Hospitals` ON 

INSERT `dbo`.`Hospitals` (`Id`, `Name`, `Address`, `Phone`, `Email`, `City`, `State`, `District`, `Country`, `PostalCode`, `IsActive`, `CreatedAt`) VALUES (1, N'VIMS Clinic', N'Main road opposite, Athava, Vizianagaram, Andhra Pradesh, India, 535161', N'8685645747', N'Vims@gmail.com', NULL, NULL, NULL, NULL, NULL, 1, CAST(N'2026-07-30T06:15:40.9017096' AS DATETIME))
INSERT `dbo`.`Hospitals` (`Id`, `Name`, `Address`, `Phone`, `Email`, `City`, `State`, `District`, `Country`, `PostalCode`, `IsActive`, `CreatedAt`) VALUES (2, N'Pragathi Clinic', N'Main Road, Kondapur, K.V.Rangareddy, Telangana, India, 500084', N'8237235264', N'jytohi@gmail.com', NULL, NULL, NULL, NULL, NULL, 1, CAST(N'2026-07-30T11:10:24.6029521' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Hospitals` OFF
SET IDENTITY_INSERT `dbo`.`MedicalHistories` ON 

INSERT `dbo`.`MedicalHistories` (`Id`, `PatientId`, `Allergies`, `ChronicDiseases`, `CurrentMedications`, `Surgeries`, `HospitalId`, `CreatedAt`) VALUES (1, 3, N'Dust Alergy', N'', N'Dolo', N'', 2, CAST(N'2026-07-31T04:04:44.3059490' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`MedicalHistories` OFF
SET IDENTITY_INSERT `dbo`.`Notifications` ON 

INSERT `dbo`.`Notifications` (`Id`, `Title`, `Message`, `PatientId`, `IsRead`, `IsSent`, `CreatedAt`) VALUES (1, N'Hi admin', N'Welcome to the CMS', NULL, 0, 1, CAST(N'2026-07-30T06:21:00.2296494' AS DATETIME))
INSERT `dbo`.`Notifications` (`Id`, `Title`, `Message`, `PatientId`, `IsRead`, `IsSent`, `CreatedAt`) VALUES (2, N'Appointment Booked', N'Appointment APT-20260730092157-395 has been booked successfully.', NULL, 0, 1, CAST(N'2026-07-30T09:21:58.0183778' AS DATETIME))
INSERT `dbo`.`Notifications` (`Id`, `Title`, `Message`, `PatientId`, `IsRead`, `IsSent`, `CreatedAt`) VALUES (3, N'Appointment Cancelled', N'Appointment APT-20260730092157-395 has been cancelled.', NULL, 0, 1, CAST(N'2026-07-30T10:33:55.2172191' AS DATETIME))
INSERT `dbo`.`Notifications` (`Id`, `Title`, `Message`, `PatientId`, `IsRead`, `IsSent`, `CreatedAt`) VALUES (4, N'Appointment Booked', N'Appointment APT-20260730103436-792 has been booked successfully.', NULL, 0, 1, CAST(N'2026-07-30T10:34:36.4075052' AS DATETIME))
INSERT `dbo`.`Notifications` (`Id`, `Title`, `Message`, `PatientId`, `IsRead`, `IsSent`, `CreatedAt`) VALUES (5, N'Appointment Booked', N'Appointment APT-20260730112158-806 has been booked successfully.', NULL, 0, 1, CAST(N'2026-07-30T11:21:58.4537339' AS DATETIME))
INSERT `dbo`.`Notifications` (`Id`, `Title`, `Message`, `PatientId`, `IsRead`, `IsSent`, `CreatedAt`) VALUES (6, N'Appointment Booked', N'Appointment APT-20260731071017-529 has been booked successfully.', NULL, 0, 1, CAST(N'2026-07-31T07:10:17.6511000' AS DATETIME))
INSERT `dbo`.`Notifications` (`Id`, `Title`, `Message`, `PatientId`, `IsRead`, `IsSent`, `CreatedAt`) VALUES (7, N'Appointment Booked', N'Appointment APT-20260731071423-688 has been booked successfully.', NULL, 0, 1, CAST(N'2026-07-31T07:14:23.9439264' AS DATETIME))
INSERT `dbo`.`Notifications` (`Id`, `Title`, `Message`, `PatientId`, `IsRead`, `IsSent`, `CreatedAt`) VALUES (8, N'Appointment Booked', N'Appointment APT-20260731072012-650 has been booked successfully.', NULL, 0, 1, CAST(N'2026-07-31T07:20:12.1596175' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Notifications` OFF
SET IDENTITY_INSERT `dbo`.`OtpVerifications` ON 

INSERT `dbo`.`OtpVerifications` (`Id`, `Email`, `Otp`, `ExpiryTime`, `IsUsed`, `ResetToken`, `ResetTokenExpiry`, `CreatedAt`) VALUES (1, N'pilla.durgaprasad666@gmail.com', N'901012', CAST(N'2026-07-30T10:05:26.8271154' AS DATETIME), 1, N'76693d58-ad0a-4545-b5b4-00ab5e348c22', CAST(N'2026-07-30T10:10:54.9559167' AS DATETIME), CAST(N'2026-07-30T09:55:26.8268843' AS DATETIME))
INSERT `dbo`.`OtpVerifications` (`Id`, `Email`, `Otp`, `ExpiryTime`, `IsUsed`, `ResetToken`, `ResetTokenExpiry`, `CreatedAt`) VALUES (2, N'durgaprasad.pilla4321@gmail.com', N'815606', CAST(N'2026-07-30T10:07:18.7517112' AS DATETIME), 1, N'fa73ea63-a9e3-43e1-93a5-3ed22b150f2d', CAST(N'2026-07-30T10:12:42.3070888' AS DATETIME), CAST(N'2026-07-30T09:57:18.7517096' AS DATETIME))
INSERT `dbo`.`OtpVerifications` (`Id`, `Email`, `Otp`, `ExpiryTime`, `IsUsed`, `ResetToken`, `ResetTokenExpiry`, `CreatedAt`) VALUES (3, N'pilladurgaprasad669@gmail.com', N'826225', CAST(N'2026-07-30T10:08:28.0302992' AS DATETIME), 1, N'4d2bab47-e506-4613-a973-80360139432f', CAST(N'2026-07-30T10:14:19.5040234' AS DATETIME), CAST(N'2026-07-30T09:58:28.0302965' AS DATETIME))
INSERT `dbo`.`OtpVerifications` (`Id`, `Email`, `Otp`, `ExpiryTime`, `IsUsed`, `ResetToken`, `ResetTokenExpiry`, `CreatedAt`) VALUES (4, N'pilladurgaprasad6966@gmail.com', N'774707', CAST(N'2026-07-30T10:09:51.5553075' AS DATETIME), 1, N'7d45f6c6-512f-4601-97bb-943792698430', CAST(N'2026-07-30T10:15:09.3474336' AS DATETIME), CAST(N'2026-07-30T09:59:51.5553058' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`OtpVerifications` OFF
SET IDENTITY_INSERT `dbo`.`Patients` ON 

INSERT `dbo`.`Patients` (`Id`, `PatientCode`, `Name`, `Phone`, `Age`, `Gender`, `Email`, `Address`, `BloodGroup`, `DateOfBirth`, `EmergencyContactName`, `EmergencyContactPhone`, `HospitalId`, `CreatedAt`) VALUES (1, N'P-47195', N'Pilla Prasad', N'9787685764', 22, N'Male', N'pilla.durgaprasad66786@gmail.com', N'Main street, Athava, Vizianagaram, Andhra Pradesh, India, 535161', N'B-', CAST(N'2004-06-30T00:00:00.0000000' AS DATETIME), N'Hari', N'9678854848', 1, CAST(N'2026-07-30T07:33:15.1160167' AS DATETIME))
INSERT `dbo`.`Patients` (`Id`, `PatientCode`, `Name`, `Phone`, `Age`, `Gender`, `Email`, `Address`, `BloodGroup`, `DateOfBirth`, `EmergencyContactName`, `EmergencyContactPhone`, `HospitalId`, `CreatedAt`) VALUES (2, N'P-82637', N'Jyothi Mutyala', N'9323238523', 26, N'Female', N'jyothi@gmail.com', N'Ameenpur, A.I.E. R.C.Puram, Medak, Telangana, India, 502032', NULL, CAST(N'1999-10-10T00:00:00.0000000' AS DATETIME), NULL, NULL, 1, CAST(N'2026-07-30T09:19:00.2070178' AS DATETIME))
INSERT `dbo`.`Patients` (`Id`, `PatientCode`, `Name`, `Phone`, `Age`, `Gender`, `Email`, `Address`, `BloodGroup`, `DateOfBirth`, `EmergencyContactName`, `EmergencyContactPhone`, `HospitalId`, `CreatedAt`) VALUES (3, N'P-73108', N'Karuna Mutyala', N'7293462837', 26, N'Female', N'karuna@gmail.com', N'Temple Road, I.M.Colony, Hyderabad, Telangana, India, 500082', NULL, CAST(N'2000-05-10T00:00:00.0000000' AS DATETIME), NULL, NULL, 2, CAST(N'2026-07-30T11:20:57.3933964' AS DATETIME))
INSERT `dbo`.`Patients` (`Id`, `PatientCode`, `Name`, `Phone`, `Age`, `Gender`, `Email`, `Address`, `BloodGroup`, `DateOfBirth`, `EmergencyContactName`, `EmergencyContactPhone`, `HospitalId`, `CreatedAt`) VALUES (4, N'P-90478', N'Anusha Sharma', N'9884763465', 25, N'Female', N'anusha@gmail.com', N'college road, Jntu Kukat Pally, Hyderabad, Telangana, India, 500085', NULL, CAST(N'2001-07-10T00:00:00.0000000' AS DATETIME), NULL, NULL, 2, CAST(N'2026-07-31T07:07:26.3573781' AS DATETIME))
INSERT `dbo`.`Patients` (`Id`, `PatientCode`, `Name`, `Phone`, `Age`, `Gender`, `Email`, `Address`, `BloodGroup`, `DateOfBirth`, `EmergencyContactName`, `EmergencyContactPhone`, `HospitalId`, `CreatedAt`) VALUES (5, N'P-79429', N'Vasanth Reddy', N'7837236623', 28, N'Male', N'vasanth@gmail.com', N'Temple Street, Kondapur, K.V.Rangareddy, Telangana, India, 500084', NULL, CAST(N'1998-05-02T00:00:00.0000000' AS DATETIME), NULL, NULL, 2, CAST(N'2026-07-31T07:12:34.8117576' AS DATETIME))
INSERT `dbo`.`Patients` (`Id`, `PatientCode`, `Name`, `Phone`, `Age`, `Gender`, `Email`, `Address`, `BloodGroup`, `DateOfBirth`, `EmergencyContactName`, `EmergencyContactPhone`, `HospitalId`, `CreatedAt`) VALUES (6, N'P-55899', N'God', N'7484474747', 19, N'Male', N'gani@gmail.com', N'1-12, Athava, Vizianagaram, Andhra Pradesh, India, 535161', N'A-', CAST(N'2007-02-28T00:00:00.0000000' AS DATETIME), N'Kiran', N'9769573574', 1, CAST(N'2026-07-31T07:14:30.4217251' AS DATETIME))
INSERT `dbo`.`Patients` (`Id`, `PatientCode`, `Name`, `Phone`, `Age`, `Gender`, `Email`, `Address`, `BloodGroup`, `DateOfBirth`, `EmergencyContactName`, `EmergencyContactPhone`, `HospitalId`, `CreatedAt`) VALUES (7, N'P-23646', N'Lakshmi Prasanthi', N'8977665564', 30, N'Female', N'lakshmi@gmail.com', N'Main street, Katchavanisingaram, Hyderabad, Telangana, India, 500088', NULL, CAST(N'1995-08-02T00:00:00.0000000' AS DATETIME), NULL, NULL, 2, CAST(N'2026-07-31T07:19:02.9758907' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Patients` OFF
SET IDENTITY_INSERT `dbo`.`PatientVitals` ON 

INSERT `dbo`.`PatientVitals` (`Id`, `AppointmentId`, `PatientId`, `Symptoms`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `HospitalId`, `CreatedAt`) VALUES (1, 3, 2, N'', N'120/80 mmHg', N'120 mg/dL', N'98 F', N'60 kg', N'82 bpm', N'18 breaths/min', 1, CAST(N'2026-07-30T10:36:10.0526311' AS DATETIME))
INSERT `dbo`.`PatientVitals` (`Id`, `AppointmentId`, `PatientId`, `Symptoms`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `HospitalId`, `CreatedAt`) VALUES (2, 4, 3, N'', N'100/80 mmHg', N'120 mg/dL', N'97 F', N'58 kg', N'82 bpm', N'16 breaths/min', 2, CAST(N'2026-07-31T03:53:23.4759270' AS DATETIME))
INSERT `dbo`.`PatientVitals` (`Id`, `AppointmentId`, `PatientId`, `Symptoms`, `BloodPressure`, `SugarLevel`, `Temperature`, `Weight`, `PulseRate`, `RespiratoryRate`, `HospitalId`, `CreatedAt`) VALUES (3, 6, 4, N'', N'120/100 mmHg', N'120 mg/dL', N'97 F', N'60 kg', N'82 bpm', N'18 breaths/min', 2, CAST(N'2026-07-31T11:22:58.2297151' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`PatientVitals` OFF
SET IDENTITY_INSERT `dbo`.`Payments` ON 

INSERT `dbo`.`Payments` (`Id`, `AppointmentId`, `DoctorId`, `BranchId`, `AppointmentDate`, `AppointmentTime`, `HospitalId`, `PatientId`, `Amount`, `PaymentMode`, `Status`, `TransactionId`, `PaymentDate`, `CreatedAt`) VALUES (1, 2, 2, 1, CAST(N'2026-07-30T00:00:00.0000000' AS DATETIME), CAST(N'15:00:00' AS Time), 1, 2, CAST(700.00 AS Decimal(18, 2)), N'Card', N'Paid', N'PAT-1785403319443', CAST(N'2026-07-30T09:21:59.1226969' AS DATETIME), CAST(N'2026-07-30T09:21:58.5487357' AS DATETIME))
INSERT `dbo`.`Payments` (`Id`, `AppointmentId`, `DoctorId`, `BranchId`, `AppointmentDate`, `AppointmentTime`, `HospitalId`, `PatientId`, `Amount`, `PaymentMode`, `Status`, `TransactionId`, `PaymentDate`, `CreatedAt`) VALUES (2, 3, 1, 2, CAST(N'2026-07-30T00:00:00.0000000' AS DATETIME), CAST(N'16:15:00' AS Time), 1, 2, CAST(500.00 AS Decimal(18, 2)), N'UPI', N'Paid', N'PAT-1785407677306', CAST(N'2026-07-30T10:34:36.7045948' AS DATETIME), CAST(N'2026-07-30T10:34:36.5550956' AS DATETIME))
INSERT `dbo`.`Payments` (`Id`, `AppointmentId`, `DoctorId`, `BranchId`, `AppointmentDate`, `AppointmentTime`, `HospitalId`, `PatientId`, `Amount`, `PaymentMode`, `Status`, `TransactionId`, `PaymentDate`, `CreatedAt`) VALUES (3, 4, 3, 3, CAST(N'2026-07-30T00:00:00.0000000' AS DATETIME), CAST(N'17:00:00' AS Time), 2, 3, CAST(400.00 AS Decimal(18, 2)), N'Card', N'Paid', N'PAT-1785410519439', CAST(N'2026-07-30T11:21:58.8468964' AS DATETIME), CAST(N'2026-07-30T11:21:58.6789238' AS DATETIME))
INSERT `dbo`.`Payments` (`Id`, `AppointmentId`, `DoctorId`, `BranchId`, `AppointmentDate`, `AppointmentTime`, `HospitalId`, `PatientId`, `Amount`, `PaymentMode`, `Status`, `TransactionId`, `PaymentDate`, `CreatedAt`) VALUES (4, 6, 3, 3, CAST(N'2026-07-31T00:00:00.0000000' AS DATETIME), CAST(N'14:00:00' AS Time), 2, 4, CAST(400.00 AS Decimal(18, 2)), N'UPI', N'Paid', N'PAT-1785481816432', CAST(N'2026-07-31T07:10:18.4658776' AS DATETIME), CAST(N'2026-07-31T07:10:18.0613468' AS DATETIME))
INSERT `dbo`.`Payments` (`Id`, `AppointmentId`, `DoctorId`, `BranchId`, `AppointmentDate`, `AppointmentTime`, `HospitalId`, `PatientId`, `Amount`, `PaymentMode`, `Status`, `TransactionId`, `PaymentDate`, `CreatedAt`) VALUES (5, 7, 3, 3, CAST(N'2026-07-31T00:00:00.0000000' AS DATETIME), CAST(N'14:30:00' AS Time), 2, 5, CAST(400.00 AS Decimal(18, 2)), N'Card', N'Paid', N'PAT-1785482062476', CAST(N'2026-07-31T07:14:24.4387236' AS DATETIME), CAST(N'2026-07-31T07:14:24.2825300' AS DATETIME))
INSERT `dbo`.`Payments` (`Id`, `AppointmentId`, `DoctorId`, `BranchId`, `AppointmentDate`, `AppointmentTime`, `HospitalId`, `PatientId`, `Amount`, `PaymentMode`, `Status`, `TransactionId`, `PaymentDate`, `CreatedAt`) VALUES (6, 8, 3, 3, CAST(N'2026-07-31T00:00:00.0000000' AS DATETIME), CAST(N'15:00:00' AS Time), 2, 7, CAST(400.00 AS Decimal(18, 2)), N'UPI', N'Paid', N'PAT-1785482410495', CAST(N'2026-07-31T07:20:12.4650803' AS DATETIME), CAST(N'2026-07-31T07:20:12.3010339' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Payments` OFF
SET IDENTITY_INSERT `dbo`.`PrescriptionItems` ON 

INSERT `dbo`.`PrescriptionItems` (`Id`, `PrescriptionId`, `MedicineName`, `Dosage`, `Frequency`, `Duration`, `Notes`) VALUES (1, 1, N'Pand-D', N'1 Tablet', N'1-0-0', N'5  Days', N'Before food')
INSERT `dbo`.`PrescriptionItems` (`Id`, `PrescriptionId`, `MedicineName`, `Dosage`, `Frequency`, `Duration`, `Notes`) VALUES (2, 2, N'Dolo', N'1 Tablet', N'1-1-1', N'5 Days', N'After food')
INSERT `dbo`.`PrescriptionItems` (`Id`, `PrescriptionId`, `MedicineName`, `Dosage`, `Frequency`, `Duration`, `Notes`) VALUES (3, 3, N'DOT', N'1 Tablet', N'1-0-1', N'3 Days', N'After food')
SET IDENTITY_INSERT `dbo`.`PrescriptionItems` OFF
SET IDENTITY_INSERT `dbo`.`Prescriptions` ON 

INSERT `dbo`.`Prescriptions` (`Id`, `AppointmentId`, `PatientId`, `Diagnosis`, `Instructions`, `FollowUpDate`, `Status`, `HospitalId`, `CreatedAt`, `IsPrinted`, `PrintedAt`, `PrintedByUserId`) VALUES (1, 3, 2, N'Cardiac Enzyme Test, 2D Echocardiogram', N'Take medicines after food and complete the full course.', CAST(N'2026-08-08T04:30:00.0000000' AS DATETIME), N'Completed', 1, CAST(N'2026-07-30T10:53:19.8840999' AS DATETIME), 0, NULL, NULL)
INSERT `dbo`.`Prescriptions` (`Id`, `AppointmentId`, `PatientId`, `Diagnosis`, `Instructions`, `FollowUpDate`, `Status`, `HospitalId`, `CreatedAt`, `IsPrinted`, `PrintedAt`, `PrintedByUserId`) VALUES (2, 4, 3, N'Blood Glucose Test', N'Take medicines after food and complete the full course.', CAST(N'2026-08-08T04:30:00.0000000' AS DATETIME), N'Completed', 2, CAST(N'2026-07-31T03:55:44.2129252' AS DATETIME), 0, NULL, NULL)
INSERT `dbo`.`Prescriptions` (`Id`, `AppointmentId`, `PatientId`, `Diagnosis`, `Instructions`, `FollowUpDate`, `Status`, `HospitalId`, `CreatedAt`, `IsPrinted`, `PrintedAt`, `PrintedByUserId`) VALUES (3, 8, 7, N'', N'Take medicines after food and complete the full course.', CAST(N'2026-08-08T04:30:00.0000000' AS DATETIME), N'Completed', 2, CAST(N'2026-07-31T11:40:47.6741281' AS DATETIME), 0, NULL, NULL)
SET IDENTITY_INSERT `dbo`.`Prescriptions` OFF
SET IDENTITY_INSERT `dbo`.`Receptionists` ON 

INSERT `dbo`.`Receptionists` (`Id`, `Name`, `Email`, `Phone`, `PasswordHash`, `IsActive`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (1, N'Devi', N'pilladurgaprasad6966@gmail.com', N'9699595736', N'$2a$11$r/4pNCWHysb6FBJ/s42wfOE3.8iDEvhK7Sm4hH833oBuTGMTS2cue', 1, 1, 2, CAST(N'2026-07-30T07:25:00.9042491' AS DATETIME))
INSERT `dbo`.`Receptionists` (`Id`, `Name`, `Email`, `Phone`, `PasswordHash`, `IsActive`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (2, N'Karuna Mutyala', N'alphajyothihanu@gmail.com', N'7473847637', N'$2a$11$Zr5SiunzP/Wwkb/TIFZ6p.qtIJi3YhB2lij1sOIAVdTR.wZafUq12', 1, 2, 3, CAST(N'2026-07-30T11:19:02.0583199' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Receptionists` OFF
SET IDENTITY_INSERT `dbo`.`RolePermissions` ON 

INSERT `dbo`.`RolePermissions` (`Id`, `RoleName`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`) VALUES (1, N'Admin', 1, 1, 1, 1)
SET IDENTITY_INSERT `dbo`.`RolePermissions` OFF
SET IDENTITY_INSERT `dbo`.`Schedules` ON 

INSERT `dbo`.`Schedules` (`Id`, `DoctorId`, `StartDate`, `EndDate`, `Days`, `WorkStart`, `WorkEnd`, `BreakStart`, `BreakEnd`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (1, 1, CAST(N'2026-07-31T00:00:00.0000000' AS DATETIME), CAST(N'2026-08-30T00:00:00.0000000' AS DATETIME), N'Monday,Tuesday,Wednesday,Thursday,Friday', CAST(N'09:00:00' AS Time), CAST(N'18:00:00' AS Time), CAST(N'13:00:00' AS Time), CAST(N'14:00:00' AS Time), 1, 1, CAST(N'2026-07-30T07:34:38.1865278' AS DATETIME))
INSERT `dbo`.`Schedules` (`Id`, `DoctorId`, `StartDate`, `EndDate`, `Days`, `WorkStart`, `WorkEnd`, `BreakStart`, `BreakEnd`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (2, 2, CAST(N'2026-07-30T00:00:00.0000000' AS DATETIME), CAST(N'2026-08-29T00:00:00.0000000' AS DATETIME), N'Monday,Tuesday,Wednesday,Thursday,Friday', CAST(N'09:00:00' AS Time), CAST(N'18:00:00' AS Time), CAST(N'13:00:00' AS Time), CAST(N'14:00:00' AS Time), 1, 1, CAST(N'2026-07-30T07:34:44.7475775' AS DATETIME))
INSERT `dbo`.`Schedules` (`Id`, `DoctorId`, `StartDate`, `EndDate`, `Days`, `WorkStart`, `WorkEnd`, `BreakStart`, `BreakEnd`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (3, 3, CAST(N'2026-07-30T00:00:00.0000000' AS DATETIME), CAST(N'2026-08-29T00:00:00.0000000' AS DATETIME), N'Monday,Tuesday,Wednesday,Thursday,Friday', CAST(N'09:00:00' AS Time), CAST(N'18:00:00' AS Time), CAST(N'13:00:00' AS Time), CAST(N'14:00:00' AS Time), 2, 3, CAST(N'2026-07-30T11:18:32.3589669' AS DATETIME))
INSERT `dbo`.`Schedules` (`Id`, `DoctorId`, `StartDate`, `EndDate`, `Days`, `WorkStart`, `WorkEnd`, `BreakStart`, `BreakEnd`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (4, 1, CAST(N'2026-08-01T00:00:00.0000000' AS DATETIME), CAST(N'2026-09-02T00:00:00.0000000' AS DATETIME), N'Monday,Tuesday,Wednesday,Thursday,Friday', CAST(N'09:00:00' AS Time), CAST(N'21:00:00' AS Time), CAST(N'13:00:00' AS Time), CAST(N'14:00:00' AS Time), 1, 2, CAST(N'2026-07-31T10:41:45.1297132' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Schedules` OFF
SET IDENTITY_INSERT `dbo`.`ScheduleSettings` ON 

INSERT `dbo`.`ScheduleSettings` (`Id`, `SlotDuration`, `ClinicOpen`, `ClinicClose`, `HospitalId`, `CreatedAt`) VALUES (1, 15, CAST(N'09:00:00' AS Time), CAST(N'18:00:00' AS Time), 1, CAST(N'2026-07-30T07:34:28.7443672' AS DATETIME))
INSERT `dbo`.`ScheduleSettings` (`Id`, `SlotDuration`, `ClinicOpen`, `ClinicClose`, `HospitalId`, `CreatedAt`) VALUES (2, 15, CAST(N'09:00:00' AS Time), CAST(N'09:00:00' AS Time), 1, CAST(N'2026-07-31T10:41:05.6318376' AS DATETIME))
INSERT `dbo`.`ScheduleSettings` (`Id`, `SlotDuration`, `ClinicOpen`, `ClinicClose`, `HospitalId`, `CreatedAt`) VALUES (3, 15, CAST(N'09:00:00' AS Time), CAST(N'21:00:00' AS Time), 1, CAST(N'2026-07-31T10:41:25.6057149' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`ScheduleSettings` OFF
SET IDENTITY_INSERT `dbo`.`Staffs` ON 

INSERT `dbo`.`Staffs` (`Id`, `UserId`, `Role`, `IsActive`, `HospitalId`, `BranchId`, `CreatedAt`) VALUES (1, 11, N'Nurse', 1, 1, 2, CAST(N'2026-07-31T05:18:21.8113039' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`Staffs` OFF
SET IDENTITY_INSERT `dbo`.`UserPermissions` ON 

INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (15, 3, 1, N'Dashboard', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2941470' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (16, 3, 1, N'Appointments', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2945817' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (17, 3, 1, N'Patients', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2947581' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (18, 3, 1, N'Billing', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2949242' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (19, 3, 1, N'Reports', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2950833' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (20, 3, 1, N'Schedule', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2952372' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (21, 3, 1, N'Prescriptions', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2954379' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (22, 4, 1, N'Dashboard', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2941469' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (23, 4, 1, N'Appointments', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2945816' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (24, 4, 1, N'Patients', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2947582' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (25, 4, 1, N'Billing', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2949242' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (26, 4, 1, N'Reports', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2950833' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (27, 4, 1, N'Schedule', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2952374' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (28, 4, 1, N'Prescriptions', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:53:45.2955070' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (36, 5, 1, N'Dashboard', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:56:07.6336993' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (37, 5, 1, N'Appointments', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:56:07.6340250' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (38, 5, 1, N'Patients', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:56:07.6341357' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (39, 5, 1, N'Billing', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:56:07.6341891' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (40, 5, 1, N'Reports', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:56:07.6342398' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (41, 5, 1, N'Schedule', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:56:07.6343548' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (42, 5, 1, N'Prescriptions', 1, 1, 1, 1, 2, CAST(N'2026-07-30T12:56:07.6344242' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (43, 11, 1, N'Dashboard', 1, 1, 1, 1, 2, CAST(N'2026-07-31T06:44:05.3458574' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (44, 11, 1, N'Appointments', 1, 1, 1, 1, 2, CAST(N'2026-07-31T06:44:05.3513998' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (45, 11, 1, N'Patients', 1, 1, 1, 1, 2, CAST(N'2026-07-31T06:44:05.3521112' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (46, 11, 1, N'Billing', 1, 1, 1, 1, 2, CAST(N'2026-07-31T06:44:05.3522014' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (47, 11, 1, N'Reports', 1, 1, 1, 1, 2, CAST(N'2026-07-31T06:44:05.3522693' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (48, 11, 1, N'Schedule', 1, 1, 1, 1, 2, CAST(N'2026-07-31T06:44:05.3523267' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (49, 11, 1, N'Prescriptions', 1, 1, 1, 1, 2, CAST(N'2026-07-31T06:44:05.3523695' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (50, 11, 1, N'Vitals', 1, 1, 1, 1, 2, CAST(N'2026-07-31T06:44:05.3524083' AS DATETIME))
INSERT `dbo`.`UserPermissions` (`Id`, `UserId`, `HospitalId`, `Module`, `CanView`, `CanCreate`, `CanEdit`, `CanDelete`, `AssignedByUserId`, `UpdatedAt`) VALUES (51, 11, 1, N'NursingNotes', 1, 1, 1, 1, 2, CAST(N'2026-07-31T06:44:05.3524504' AS DATETIME))
SET IDENTITY_INSERT `dbo`.`UserPermissions` OFF
SET IDENTITY_INSERT `dbo`.`Users` ON 

INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (1, N'Super Admin', N'0000000000', N'superadmin@gmail.com', N'$2a$11$GL4maQ4nl5eRSGV2XlKOAuwq6EoycJxP83iKWwED994vfZdjXc.XS', N'SuperAdmin', NULL, NULL, NULL, 1, CAST(N'2026-07-30T05:56:00.4603191' AS DATETIME), 0)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (2, N'Pilla Durga Prasad', N'', N'pilla.durgaprasad666@gmail.com', N'$2a$11$hKOl9rxL.oH0HpJFekznteu.ZWaDo1tT.7UnmYhC9xxzilDvYVS/m', N'Admin', NULL, 1, NULL, 1, CAST(N'2026-07-30T06:16:09.9357495' AS DATETIME), 1)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (3, N'Durga Prasad', N'9785845635', N'durgaprasad.pilla4321@gmail.com', N'$2a$11$w1aW6p9fi9JSkxUM7nQO.OUJtL54zvYwNA.9mNc9l2hKQQnhpeUeK', N'Doctor', 1, 1, 2, 1, CAST(N'2026-07-30T06:34:57.3375913' AS DATETIME), 1)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (4, N'Laxmi', N'9585684745', N'pilladurgaprasad669@gmail.com', N'$2a$11$3BtJr0a9JNcE16AFCQjtYe35xZ8xUA1N9NtvH0zKBWO.cRGWcSZc6', N'Doctor', 2, 1, 2, 1, CAST(N'2026-07-30T07:21:47.0735682' AS DATETIME), 1)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (5, N'Devi', N'9699595736', N'pilladurgaprasad6966@gmail.com', N'$2a$11$S4BKaKDLQv2utUS0/I7NXOFyGoHJg3sox/OTI60b5wkch3vNNstgG', N'Receptionist', NULL, 1, 2, 1, CAST(N'2026-07-30T07:25:00.9544946' AS DATETIME), 1)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (6, N'Jyothi Mutyala', N'9323238523', N'jyothi@gmail.com', N'$2a$11$Acy1ZNIAE3b445.i2QzT5.n.KIZYrrlkB1Hmu3yBURyI12hJZZrfy', N'Patient', NULL, 1, NULL, 1, CAST(N'2026-07-30T09:19:00.2554820' AS DATETIME), 0)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (7, N'Jyothi Mutyala', N'', N'jyothimutyala543@gmail.com', N'$2a$11$M1V.VCVEKUcQqD/CGXJWneVCHFjMlDfuITvzpIgyEBlj.ZFWN7f4e', N'Admin', NULL, 2, NULL, 1, CAST(N'2026-07-30T11:11:17.7750875' AS DATETIME), 0)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (8, N'Jyothi', N'9177881856', N'jyothimutyala544@gmail.com', N'$2a$11$eDFYEIqHEmtenEqjs0hTK.b2q3dcr8sX1HEE3YeMQif08OjqZ0YuC', N'Doctor', 3, 2, 3, 1, CAST(N'2026-07-30T11:18:09.1295940' AS DATETIME), 0)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (9, N'Karuna Mutyala', N'7473847637', N'alphajyothihanu@gmail.com', N'$2a$11$w5/NHKscg5pzQfODVuQSA.yi8CUbsigjM.4ODwJuBdagnUSCP1ZiS', N'Receptionist', NULL, 2, 3, 1, CAST(N'2026-07-30T11:19:02.0586160' AS DATETIME), 0)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (10, N'Karuna Mutyala', N'7293462837', N'karuna@gmail.com', N'$2a$11$BJUiNT.UFJQAk0eOy0QMIuW9HJDjc.ejC8hSVnH8VI1weyhPz7EiS', N'Patient', NULL, 2, NULL, 1, CAST(N'2026-07-30T11:20:57.4558016' AS DATETIME), 0)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (11, N'Pilla', N'9967856745', N'pilla.durgaprasad667@gmail.com', N'$2a$11$MvMwbsYWhQW2ZtUFWHocWudsz.dfzvYLvBj1edJWuWlEGTj3QqiD6', N'Nurse', NULL, 1, 2, 1, CAST(N'2026-07-31T05:18:21.4400710' AS DATETIME), 1)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (12, N'Anusha Sharma', N'9884763465', N'anusha@gmail.com', N'$2a$11$SJbsGhs8TPEATKaVSFBquuj4dAglhHs3PQnN6WJxDD1lcQYJd9SGi', N'Patient', NULL, 2, NULL, 1, CAST(N'2026-07-31T07:07:26.5072216' AS DATETIME), 0)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (13, N'Vasanth Reddy', N'7837236623', N'vasanth@gmail.com', N'$2a$11$6j5MQbCs9VQJu/K9cKVUFuY0XKoq2d3B3joX8Inis4TsUkZEUQpbK', N'Patient', NULL, 2, NULL, 1, CAST(N'2026-07-31T07:12:34.8400901' AS DATETIME), 0)
INSERT `dbo`.`Users` (`Id`, `Name`, `MobileNumber`, `Email`, `PasswordHash`, `Role`, `DoctorId`, `HospitalId`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`) VALUES (14, N'Lakshmi Prasanthi', N'8977665564', N'lakshmi@gmail.com', N'$2a$11$C/SUW2PQWYACsvMqIHuy..xNM9jIPoeSZjwrJ193M0VWFG8yTUFKq', N'Patient', NULL, 2, NULL, 1, CAST(N'2026-07-31T07:19:02.9919770' AS DATETIME), 0)
SET IDENTITY_INSERT `dbo`.`Users` OFF
/****** Object:  Index `IX_AppointmentDocuments_AppointmentId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_AppointmentDocuments_AppointmentId` ON `dbo`.`AppointmentDocuments`
(
	`AppointmentId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Appointments_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Appointments_BranchId` ON `dbo`.`Appointments`
(
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Appointments_Date`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Appointments_Date` ON `dbo`.`Appointments`
(
	`Date` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Appointments_DoctorId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Appointments_DoctorId` ON `dbo`.`Appointments`
(
	`DoctorId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Appointments_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Appointments_HospitalId` ON `dbo`.`Appointments`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Appointments_PatientId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Appointments_PatientId` ON `dbo`.`Appointments`
(
	`PatientId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_AuditLogs_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_AuditLogs_BranchId` ON `dbo`.`AuditLogs`
(
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_AuditLogs_ClinicId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_AuditLogs_ClinicId` ON `dbo`.`AuditLogs`
(
	`ClinicId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Billings_AppointmentId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Billings_AppointmentId` ON `dbo`.`Billings`
(
	`AppointmentId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Billings_DoctorId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Billings_DoctorId` ON `dbo`.`Billings`
(
	`DoctorId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Billings_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Billings_HospitalId` ON `dbo`.`Billings`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Billings_PatientId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Billings_PatientId` ON `dbo`.`Billings`
(
	`PatientId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Branches_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Branches_HospitalId` ON `dbo`.`Branches`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Cities_DistrictId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Cities_DistrictId` ON `dbo`.`Cities`
(
	`DistrictId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Consultations_AppointmentId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Consultations_AppointmentId` ON `dbo`.`Consultations`
(
	`AppointmentId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Consultations_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Consultations_HospitalId` ON `dbo`.`Consultations`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Consultations_PatientId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Consultations_PatientId` ON `dbo`.`Consultations`
(
	`PatientId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Districts_StateId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Districts_StateId` ON `dbo`.`Districts`
(
	`StateId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_DoctorBranches_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_DoctorBranches_BranchId` ON `dbo`.`DoctorBranches`
(
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_DoctorBranches_DoctorId_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX `IX_DoctorBranches_DoctorId_BranchId` ON `dbo`.`DoctorBranches`
(
	`DoctorId` ASC,
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_DoctorDiagnoses_DoctorId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_DoctorDiagnoses_DoctorId` ON `dbo`.`DoctorDiagnoses`
(
	`DoctorId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_DoctorDiagnoses_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_DoctorDiagnoses_HospitalId` ON `dbo`.`DoctorDiagnoses`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Doctors_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Doctors_BranchId` ON `dbo`.`Doctors`
(
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Doctors_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Doctors_HospitalId` ON `dbo`.`Doctors`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Holidays_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Holidays_HospitalId` ON `dbo`.`Holidays`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_MedicalHistories_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_MedicalHistories_HospitalId` ON `dbo`.`MedicalHistories`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_MedicalHistories_PatientId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_MedicalHistories_PatientId` ON `dbo`.`MedicalHistories`
(
	`PatientId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Notifications_PatientId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Notifications_PatientId` ON `dbo`.`Notifications`
(
	`PatientId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Patients_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Patients_HospitalId` ON `dbo`.`Patients`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_PatientVitals_AppointmentId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_PatientVitals_AppointmentId` ON `dbo`.`PatientVitals`
(
	`AppointmentId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_PatientVitals_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_PatientVitals_HospitalId` ON `dbo`.`PatientVitals`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_PatientVitals_PatientId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_PatientVitals_PatientId` ON `dbo`.`PatientVitals`
(
	`PatientId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Payments_AppointmentId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Payments_AppointmentId` ON `dbo`.`Payments`
(
	`AppointmentId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Payments_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Payments_BranchId` ON `dbo`.`Payments`
(
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Payments_DoctorId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Payments_DoctorId` ON `dbo`.`Payments`
(
	`DoctorId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Payments_PatientId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Payments_PatientId` ON `dbo`.`Payments`
(
	`PatientId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_PrescriptionItems_PrescriptionId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_PrescriptionItems_PrescriptionId` ON `dbo`.`PrescriptionItems`
(
	`PrescriptionId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_PrescriptionLabTests_PrescriptionId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_PrescriptionLabTests_PrescriptionId` ON `dbo`.`PrescriptionLabTests`
(
	`PrescriptionId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Prescriptions_AppointmentId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Prescriptions_AppointmentId` ON `dbo`.`Prescriptions`
(
	`AppointmentId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Prescriptions_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Prescriptions_HospitalId` ON `dbo`.`Prescriptions`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Prescriptions_PatientId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Prescriptions_PatientId` ON `dbo`.`Prescriptions`
(
	`PatientId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Receptionists_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Receptionists_BranchId` ON `dbo`.`Receptionists`
(
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Receptionists_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Receptionists_HospitalId` ON `dbo`.`Receptionists`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Schedules_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Schedules_BranchId` ON `dbo`.`Schedules`
(
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Schedules_DoctorId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Schedules_DoctorId` ON `dbo`.`Schedules`
(
	`DoctorId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Schedules_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Schedules_HospitalId` ON `dbo`.`Schedules`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_ScheduleSettings_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_ScheduleSettings_HospitalId` ON `dbo`.`ScheduleSettings`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Staffs_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Staffs_BranchId` ON `dbo`.`Staffs`
(
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Staffs_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Staffs_HospitalId` ON `dbo`.`Staffs`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Staffs_UserId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Staffs_UserId` ON `dbo`.`Staffs`
(
	`UserId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
SET ANSI_PADDING ON
/****** Object:  Index `IX_SuperAdmins_Email`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX `IX_SuperAdmins_Email` ON `dbo`.`SuperAdmins`
(
	`Email` ASC
)
WHERE (`Email` IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_UserPermissions_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_UserPermissions_HospitalId` ON `dbo`.`UserPermissions`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
SET ANSI_PADDING ON
/****** Object:  Index `IX_UserPermissions_UserId_Module`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX `IX_UserPermissions_UserId_Module` ON `dbo`.`UserPermissions`
(
	`UserId` ASC,
	`Module` ASC
)
WHERE (`UserId` IS NOT NULL AND `Module` IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Users_BranchId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Users_BranchId` ON `dbo`.`Users`
(
	`BranchId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
/****** Object:  Index `IX_Users_HospitalId`    Script Date: 7/31/2026 5:43:28 PM ******/
CREATE NONCLUSTERED INDEX `IX_Users_HospitalId` ON `dbo`.`Users`
(
	`HospitalId` ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON `PRIMARY`
ALTER TABLE `dbo`.`Billings` ADD  DEFAULT (N'OP') FOR `BillingType`
ALTER TABLE `dbo`.`Billings` ADD  DEFAULT ((0.0)) FOR `SubTotal`
ALTER TABLE `dbo`.`Billings` ADD  DEFAULT ((0.0)) FOR `GstPercentage`
ALTER TABLE `dbo`.`Billings` ADD  DEFAULT ((0.0)) FOR `GstAmount`
ALTER TABLE `dbo`.`Prescriptions` ADD  DEFAULT (CONVERT(`BOOLEAN`,(0))) FOR `IsPrinted`
ALTER TABLE `dbo`.`AppointmentDocuments`  WITH CHECK ADD  CONSTRAINT `FK_AppointmentDocuments_Appointments_AppointmentId` FOREIGN KEY(`AppointmentId`)
REFERENCES `dbo`.`Appointments` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`AppointmentDocuments` CHECK CONSTRAINT `FK_AppointmentDocuments_Appointments_AppointmentId`
ALTER TABLE `dbo`.`Appointments`  WITH CHECK ADD  CONSTRAINT `FK_Appointments_Branches_BranchId` FOREIGN KEY(`BranchId`)
REFERENCES `dbo`.`Branches` (`Id`)
ALTER TABLE `dbo`.`Appointments` CHECK CONSTRAINT `FK_Appointments_Branches_BranchId`
ALTER TABLE `dbo`.`Appointments`  WITH CHECK ADD  CONSTRAINT `FK_Appointments_Doctors_DoctorId` FOREIGN KEY(`DoctorId`)
REFERENCES `dbo`.`Doctors` (`Id`)
ALTER TABLE `dbo`.`Appointments` CHECK CONSTRAINT `FK_Appointments_Doctors_DoctorId`
ALTER TABLE `dbo`.`Appointments`  WITH CHECK ADD  CONSTRAINT `FK_Appointments_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`Appointments` CHECK CONSTRAINT `FK_Appointments_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Appointments`  WITH CHECK ADD  CONSTRAINT `FK_Appointments_Patients_PatientId` FOREIGN KEY(`PatientId`)
REFERENCES `dbo`.`Patients` (`Id`)
ALTER TABLE `dbo`.`Appointments` CHECK CONSTRAINT `FK_Appointments_Patients_PatientId`
ALTER TABLE `dbo`.`AuditLogs`  WITH CHECK ADD  CONSTRAINT `FK_AuditLogs_Branches_BranchId` FOREIGN KEY(`BranchId`)
REFERENCES `dbo`.`Branches` (`Id`)
ALTER TABLE `dbo`.`AuditLogs` CHECK CONSTRAINT `FK_AuditLogs_Branches_BranchId`
ALTER TABLE `dbo`.`AuditLogs`  WITH CHECK ADD  CONSTRAINT `FK_AuditLogs_Hospitals_ClinicId` FOREIGN KEY(`ClinicId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`AuditLogs` CHECK CONSTRAINT `FK_AuditLogs_Hospitals_ClinicId`
ALTER TABLE `dbo`.`Billings`  WITH CHECK ADD  CONSTRAINT `FK_Billings_Appointments_AppointmentId` FOREIGN KEY(`AppointmentId`)
REFERENCES `dbo`.`Appointments` (`Id`)
ALTER TABLE `dbo`.`Billings` CHECK CONSTRAINT `FK_Billings_Appointments_AppointmentId`
ALTER TABLE `dbo`.`Billings`  WITH CHECK ADD  CONSTRAINT `FK_Billings_Doctors_DoctorId` FOREIGN KEY(`DoctorId`)
REFERENCES `dbo`.`Doctors` (`Id`)
ALTER TABLE `dbo`.`Billings` CHECK CONSTRAINT `FK_Billings_Doctors_DoctorId`
ALTER TABLE `dbo`.`Billings`  WITH CHECK ADD  CONSTRAINT `FK_Billings_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Billings` CHECK CONSTRAINT `FK_Billings_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Billings`  WITH CHECK ADD  CONSTRAINT `FK_Billings_Patients_PatientId` FOREIGN KEY(`PatientId`)
REFERENCES `dbo`.`Patients` (`Id`)
ALTER TABLE `dbo`.`Billings` CHECK CONSTRAINT `FK_Billings_Patients_PatientId`
ALTER TABLE `dbo`.`Branches`  WITH CHECK ADD  CONSTRAINT `FK_Branches_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`Branches` CHECK CONSTRAINT `FK_Branches_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Cities`  WITH CHECK ADD  CONSTRAINT `FK_Cities_Districts_DistrictId` FOREIGN KEY(`DistrictId`)
REFERENCES `dbo`.`Districts` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Cities` CHECK CONSTRAINT `FK_Cities_Districts_DistrictId`
ALTER TABLE `dbo`.`Consultations`  WITH CHECK ADD  CONSTRAINT `FK_Consultations_Appointments_AppointmentId` FOREIGN KEY(`AppointmentId`)
REFERENCES `dbo`.`Appointments` (`Id`)
ALTER TABLE `dbo`.`Consultations` CHECK CONSTRAINT `FK_Consultations_Appointments_AppointmentId`
ALTER TABLE `dbo`.`Consultations`  WITH CHECK ADD  CONSTRAINT `FK_Consultations_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Consultations` CHECK CONSTRAINT `FK_Consultations_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Consultations`  WITH CHECK ADD  CONSTRAINT `FK_Consultations_Patients_PatientId` FOREIGN KEY(`PatientId`)
REFERENCES `dbo`.`Patients` (`Id`)
ALTER TABLE `dbo`.`Consultations` CHECK CONSTRAINT `FK_Consultations_Patients_PatientId`
ALTER TABLE `dbo`.`Districts`  WITH CHECK ADD  CONSTRAINT `FK_Districts_States_StateId` FOREIGN KEY(`StateId`)
REFERENCES `dbo`.`States` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Districts` CHECK CONSTRAINT `FK_Districts_States_StateId`
ALTER TABLE `dbo`.`DoctorBranches`  WITH CHECK ADD  CONSTRAINT `FK_DoctorBranches_Branches_BranchId` FOREIGN KEY(`BranchId`)
REFERENCES `dbo`.`Branches` (`Id`)
ALTER TABLE `dbo`.`DoctorBranches` CHECK CONSTRAINT `FK_DoctorBranches_Branches_BranchId`
ALTER TABLE `dbo`.`DoctorBranches`  WITH CHECK ADD  CONSTRAINT `FK_DoctorBranches_Doctors_DoctorId` FOREIGN KEY(`DoctorId`)
REFERENCES `dbo`.`Doctors` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`DoctorBranches` CHECK CONSTRAINT `FK_DoctorBranches_Doctors_DoctorId`
ALTER TABLE `dbo`.`DoctorDiagnoses`  WITH CHECK ADD  CONSTRAINT `FK_DoctorDiagnoses_Doctors_DoctorId` FOREIGN KEY(`DoctorId`)
REFERENCES `dbo`.`Doctors` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`DoctorDiagnoses` CHECK CONSTRAINT `FK_DoctorDiagnoses_Doctors_DoctorId`
ALTER TABLE `dbo`.`DoctorDiagnoses`  WITH CHECK ADD  CONSTRAINT `FK_DoctorDiagnoses_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`DoctorDiagnoses` CHECK CONSTRAINT `FK_DoctorDiagnoses_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Doctors`  WITH CHECK ADD  CONSTRAINT `FK_Doctors_Branches_BranchId` FOREIGN KEY(`BranchId`)
REFERENCES `dbo`.`Branches` (`Id`)
ALTER TABLE `dbo`.`Doctors` CHECK CONSTRAINT `FK_Doctors_Branches_BranchId`
ALTER TABLE `dbo`.`Doctors`  WITH CHECK ADD  CONSTRAINT `FK_Doctors_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`Doctors` CHECK CONSTRAINT `FK_Doctors_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Holidays`  WITH CHECK ADD  CONSTRAINT `FK_Holidays_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Holidays` CHECK CONSTRAINT `FK_Holidays_Hospitals_HospitalId`
ALTER TABLE `dbo`.`MedicalHistories`  WITH CHECK ADD  CONSTRAINT `FK_MedicalHistories_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`MedicalHistories` CHECK CONSTRAINT `FK_MedicalHistories_Hospitals_HospitalId`
ALTER TABLE `dbo`.`MedicalHistories`  WITH CHECK ADD  CONSTRAINT `FK_MedicalHistories_Patients_PatientId` FOREIGN KEY(`PatientId`)
REFERENCES `dbo`.`Patients` (`Id`)
ALTER TABLE `dbo`.`MedicalHistories` CHECK CONSTRAINT `FK_MedicalHistories_Patients_PatientId`
ALTER TABLE `dbo`.`Notifications`  WITH CHECK ADD  CONSTRAINT `FK_Notifications_Patients_PatientId` FOREIGN KEY(`PatientId`)
REFERENCES `dbo`.`Patients` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Notifications` CHECK CONSTRAINT `FK_Notifications_Patients_PatientId`
ALTER TABLE `dbo`.`Patients`  WITH CHECK ADD  CONSTRAINT `FK_Patients_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`Patients` CHECK CONSTRAINT `FK_Patients_Hospitals_HospitalId`
ALTER TABLE `dbo`.`PatientVitals`  WITH CHECK ADD  CONSTRAINT `FK_PatientVitals_Appointments_AppointmentId` FOREIGN KEY(`AppointmentId`)
REFERENCES `dbo`.`Appointments` (`Id`)
ALTER TABLE `dbo`.`PatientVitals` CHECK CONSTRAINT `FK_PatientVitals_Appointments_AppointmentId`
ALTER TABLE `dbo`.`PatientVitals`  WITH CHECK ADD  CONSTRAINT `FK_PatientVitals_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`PatientVitals` CHECK CONSTRAINT `FK_PatientVitals_Hospitals_HospitalId`
ALTER TABLE `dbo`.`PatientVitals`  WITH CHECK ADD  CONSTRAINT `FK_PatientVitals_Patients_PatientId` FOREIGN KEY(`PatientId`)
REFERENCES `dbo`.`Patients` (`Id`)
ALTER TABLE `dbo`.`PatientVitals` CHECK CONSTRAINT `FK_PatientVitals_Patients_PatientId`
ALTER TABLE `dbo`.`Payments`  WITH CHECK ADD  CONSTRAINT `FK_Payments_Appointments_AppointmentId` FOREIGN KEY(`AppointmentId`)
REFERENCES `dbo`.`Appointments` (`Id`)
ALTER TABLE `dbo`.`Payments` CHECK CONSTRAINT `FK_Payments_Appointments_AppointmentId`
ALTER TABLE `dbo`.`Payments`  WITH CHECK ADD  CONSTRAINT `FK_Payments_Branches_BranchId` FOREIGN KEY(`BranchId`)
REFERENCES `dbo`.`Branches` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Payments` CHECK CONSTRAINT `FK_Payments_Branches_BranchId`
ALTER TABLE `dbo`.`Payments`  WITH CHECK ADD  CONSTRAINT `FK_Payments_Doctors_DoctorId` FOREIGN KEY(`DoctorId`)
REFERENCES `dbo`.`Doctors` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Payments` CHECK CONSTRAINT `FK_Payments_Doctors_DoctorId`
ALTER TABLE `dbo`.`Payments`  WITH CHECK ADD  CONSTRAINT `FK_Payments_Patients_PatientId` FOREIGN KEY(`PatientId`)
REFERENCES `dbo`.`Patients` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Payments` CHECK CONSTRAINT `FK_Payments_Patients_PatientId`
ALTER TABLE `dbo`.`PrescriptionItems`  WITH CHECK ADD  CONSTRAINT `FK_PrescriptionItems_Prescriptions_PrescriptionId` FOREIGN KEY(`PrescriptionId`)
REFERENCES `dbo`.`Prescriptions` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`PrescriptionItems` CHECK CONSTRAINT `FK_PrescriptionItems_Prescriptions_PrescriptionId`
ALTER TABLE `dbo`.`PrescriptionLabTests`  WITH CHECK ADD  CONSTRAINT `FK_PrescriptionLabTests_Prescriptions_PrescriptionId` FOREIGN KEY(`PrescriptionId`)
REFERENCES `dbo`.`Prescriptions` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`PrescriptionLabTests` CHECK CONSTRAINT `FK_PrescriptionLabTests_Prescriptions_PrescriptionId`
ALTER TABLE `dbo`.`Prescriptions`  WITH CHECK ADD  CONSTRAINT `FK_Prescriptions_Appointments_AppointmentId` FOREIGN KEY(`AppointmentId`)
REFERENCES `dbo`.`Appointments` (`Id`)
ALTER TABLE `dbo`.`Prescriptions` CHECK CONSTRAINT `FK_Prescriptions_Appointments_AppointmentId`
ALTER TABLE `dbo`.`Prescriptions`  WITH CHECK ADD  CONSTRAINT `FK_Prescriptions_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`Prescriptions` CHECK CONSTRAINT `FK_Prescriptions_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Prescriptions`  WITH CHECK ADD  CONSTRAINT `FK_Prescriptions_Patients_PatientId` FOREIGN KEY(`PatientId`)
REFERENCES `dbo`.`Patients` (`Id`)
ALTER TABLE `dbo`.`Prescriptions` CHECK CONSTRAINT `FK_Prescriptions_Patients_PatientId`
ALTER TABLE `dbo`.`Receptionists`  WITH CHECK ADD  CONSTRAINT `FK_Receptionists_Branches_BranchId` FOREIGN KEY(`BranchId`)
REFERENCES `dbo`.`Branches` (`Id`)
ALTER TABLE `dbo`.`Receptionists` CHECK CONSTRAINT `FK_Receptionists_Branches_BranchId`
ALTER TABLE `dbo`.`Receptionists`  WITH CHECK ADD  CONSTRAINT `FK_Receptionists_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`Receptionists` CHECK CONSTRAINT `FK_Receptionists_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Schedules`  WITH CHECK ADD  CONSTRAINT `FK_Schedules_Branches_BranchId` FOREIGN KEY(`BranchId`)
REFERENCES `dbo`.`Branches` (`Id`)
ALTER TABLE `dbo`.`Schedules` CHECK CONSTRAINT `FK_Schedules_Branches_BranchId`
ALTER TABLE `dbo`.`Schedules`  WITH CHECK ADD  CONSTRAINT `FK_Schedules_Doctors_DoctorId` FOREIGN KEY(`DoctorId`)
REFERENCES `dbo`.`Doctors` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Schedules` CHECK CONSTRAINT `FK_Schedules_Doctors_DoctorId`
ALTER TABLE `dbo`.`Schedules`  WITH CHECK ADD  CONSTRAINT `FK_Schedules_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Schedules` CHECK CONSTRAINT `FK_Schedules_Hospitals_HospitalId`
ALTER TABLE `dbo`.`ScheduleSettings`  WITH CHECK ADD  CONSTRAINT `FK_ScheduleSettings_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`ScheduleSettings` CHECK CONSTRAINT `FK_ScheduleSettings_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Staffs`  WITH CHECK ADD  CONSTRAINT `FK_Staffs_Branches_BranchId` FOREIGN KEY(`BranchId`)
REFERENCES `dbo`.`Branches` (`Id`)
ALTER TABLE `dbo`.`Staffs` CHECK CONSTRAINT `FK_Staffs_Branches_BranchId`
ALTER TABLE `dbo`.`Staffs`  WITH CHECK ADD  CONSTRAINT `FK_Staffs_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`Staffs` CHECK CONSTRAINT `FK_Staffs_Hospitals_HospitalId`
ALTER TABLE `dbo`.`Staffs`  WITH CHECK ADD  CONSTRAINT `FK_Staffs_Users_UserId` FOREIGN KEY(`UserId`)
REFERENCES `dbo`.`Users` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`Staffs` CHECK CONSTRAINT `FK_Staffs_Users_UserId`
ALTER TABLE `dbo`.`UserPermissions`  WITH CHECK ADD  CONSTRAINT `FK_UserPermissions_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`UserPermissions` CHECK CONSTRAINT `FK_UserPermissions_Hospitals_HospitalId`
ALTER TABLE `dbo`.`UserPermissions`  WITH CHECK ADD  CONSTRAINT `FK_UserPermissions_Users_UserId` FOREIGN KEY(`UserId`)
REFERENCES `dbo`.`Users` (`Id`)
ON DELETE CASCADE
ALTER TABLE `dbo`.`UserPermissions` CHECK CONSTRAINT `FK_UserPermissions_Users_UserId`
ALTER TABLE `dbo`.`Users`  WITH CHECK ADD  CONSTRAINT `FK_Users_Branches_BranchId` FOREIGN KEY(`BranchId`)
REFERENCES `dbo`.`Branches` (`Id`)
ALTER TABLE `dbo`.`Users` CHECK CONSTRAINT `FK_Users_Branches_BranchId`
ALTER TABLE `dbo`.`Users`  WITH CHECK ADD  CONSTRAINT `FK_Users_Hospitals_HospitalId` FOREIGN KEY(`HospitalId`)
REFERENCES `dbo`.`Hospitals` (`Id`)
ALTER TABLE `dbo`.`Users` CHECK CONSTRAINT `FK_Users_Hospitals_HospitalId`
ALTER DATABASE `ClinicalManagementSystemDB` SET  READ_WRITE 