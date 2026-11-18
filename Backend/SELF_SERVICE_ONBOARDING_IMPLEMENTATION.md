# CMS Backend Update — Self-Service Admin Onboarding

## Scope of this package

This update implements the self-service Admin onboarding path against the supplied Adminclinic backend source:

1. Admin registration with email OTP verification.
2. Authenticated Initial subscription checkout linked to the registered Admin.
3. Initial Razorpay order creation using the CMS Owner merchant configuration.
4. Backend signature and captured-payment verification for Initial purchases.
5. Webhook reconciliation marks a verified Initial purchase as linked to the already-existing Admin.
6. Clinic creation requires verified CMS gateway payment and creates the Main Branch in the same database transaction.
7. Pre-clinic Admin API access is restricted to onboarding/payment/clinic creation endpoints.
8. Super Admin gets read-only onboarding monitoring endpoints.
9. Existing Renewal and Upgrade checkout/payment paths are retained.

This package is **not yet a complete implementation of the second (hospital patient) payment gateway**. The existing hospital merchant settings and generic PaymentController/PaymentService are present in the supplied source, but they do not implement Razorpay order creation, cryptographic verification and signed hospital webhooks. The current patient `POST /api/payment/success` remains a legacy direct-success endpoint and must not be used as proof of a real gateway payment. Do not enable it for real-money collection until the hospital Razorpay flow is completed and this legacy endpoint is replaced/disabled.

## New APIs

### 1. Admin registration

Base route: `/api/auth/admin`

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| POST | `/register` | Anonymous; rate-limited | Creates an Admin account without HospitalId and sends registration OTP |
| POST | `/verify-otp` | Anonymous; rate-limited | Verifies email OTP and marks Admin email verified |
| POST | `/resend-otp` | Anonymous; rate-limited | Resends OTP for an existing unverified Admin |

`POST /register` request:

```json
{
  "name": "Admin Name",
  "email": "admin@example.com",
  "mobileNumber": "9876543210",
  "password": "Use-A-Strong-Password"
}
```

`POST /verify-otp` request:

```json
{ "email": "admin@example.com", "otp": "123456" }
```

OTP expires after 10 minutes, is stored as a SHA-256 hash for this registration purpose, and is single-use. Registration OTP records are separated from existing password-reset OTP records. The new account is active for authentication but has no HospitalId; a middleware allowlist prevents access to operational APIs until clinic onboarding is completed.

### 2. Super Admin onboarding monitoring

Base route: `/api/superadmin/admin-onboarding`

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/superadmin/admin-onboarding` | SuperAdmin | Lists registered Admins and their latest purchase/clinic/payment summary |
| GET | `/api/superadmin/admin-onboarding/{adminId}` | SuperAdmin | Shows one Admin and all linked subscription purchases |

These are monitoring endpoints only. They do not approve or activate customer accounts.

## Existing APIs changed

### 1. Login — `POST /api/Auth/login`

- Existing response fields are retained.
- Admins with unverified email cannot log in; they receive the existing generic invalid-credentials response.
- Existing Admin accounts are marked verified by the migration default, so they are not unexpectedly locked out.
- An Admin without HospitalId receives `onboardingRequired: true`.

### 2. Initial checkout — `POST /api/subscriptions/checkout`

**Breaking behavior change:** this endpoint now requires an Admin Bearer token. It no longer accepts customer identity from an anonymous request.

Request body remains compatible with the existing DTO shape, but `name`, `email`, and `mobileNumber` are ignored. Identity is loaded from the authenticated Admin account.

```json
{
  "subscriptionPlanId": 2,
  "paymentMethod": "UPI"
}
```

It creates one Initial purchase linked to `AdminUserId`. Repeated attempts while a Pending/Failed Initial purchase exists return HTTP 409 with its existing `purchaseId` so the customer can continue that purchase.

### 3. Payment methods — `GET /api/subscriptions/payment-methods`

Now checks the CMS Owner merchant setting and the matching Razorpay mode credentials. It reports `gatewayConfigured`, `mode`, and whether the methods can be used. It never returns secret credentials.

### 4. CMS Razorpay order — `POST /api/cms/subscription-payments/create-order/{purchaseId}`

Now supports `PurchaseType = Initial` for the registered Admin who owns the purchase and has no HospitalId. Renewal and Upgrade ownership checks remain in place.

If the purchase already has an unpaid Razorpay order, the API returns the same order and public key for a safe checkout retry instead of creating another gateway order.

### 5. CMS Razorpay verify — `POST /api/cms/subscription-payments/verify`

Now accepts Initial purchases as well as Renewal/Upgrade. It verifies:

- purchase ownership;
- original order ID and original Test/Live mode;
- Razorpay checkout signature;
- independently fetched captured payment;
- matching order, INR currency and exact amount.

On verified Initial payment, `PaymentStatus=Paid`, `IsGatewayPaymentVerified=true`, `GatewayVerifiedAt` and `AdminCreationStatus=Created` are saved transactionally. The Admin account already exists; no Super Admin account creation is needed.

### 6. Payment status — `GET /api/cms/subscription-payments/{purchaseId}/status`

Now supports an Initial purchase where the Admin and purchase are linked but HospitalId is still null. Renewal/Upgrade continue to require matching HospitalId.

### 7. Clinic creation — `POST /api/clinics`

Now requires an Initial purchase with all gateway verification evidence: Paid status, PaidAt, verified flag/time, original gateway mode, order ID and payment ID. A demo/simulated Paid purchase cannot activate a clinic.

The endpoint creates the Hospital, Main Branch, Admin-Hospital link and ClinicSubscription in the existing database transaction. Subscription validity starts at clinic creation. Main Branch counts toward the purchased branch limit.

### 8. Webhook reconciliation

`POST /api/cms/subscription-payments/webhook` remains publicly reachable and signature-protected. The reconciliation service now also marks a linked Initial purchase as `AdminCreationStatus=Created` when a captured payment event is successfully reconciled.

## Database migration

New migration:

`20261110120000_AddAdminSelfRegistrationVerification`

Adds:

- `Users.EmailVerified` (`bit`, default `true`) — preserves existing accounts as verified.
- `OtpVerifications.Purpose` (`nvarchar(30)`, default `PasswordReset`) — separates registration OTP from password-reset OTP.

Apply it from the backend project directory:

```bash
dotnet ef database update --project .\Adminclinic.csproj
```

Do not run against production before taking a database backup and reviewing the target connection string.

## Required configuration

The delivered `appsettings*.json` files have personal email/JWT values replaced with placeholders. Configure these outside source control using User Secrets or environment variables:

- `Jwt:Key` — random secret of at least 32 UTF-8 bytes.
- SMTP: `Smtp:Host`, `Smtp:Port`, `Smtp:Username`, `Smtp:Password`, `Smtp:FromEmail`, `Smtp:Security`.
- `Payments:CmsOwner:Razorpay:Test:KeyId`
- `Payments:CmsOwner:Razorpay:Test:KeySecret`
- `Payments:CmsOwner:Razorpay:Live:KeyId`
- `Payments:CmsOwner:Razorpay:Live:KeySecret`
- CMS Owner merchant setting row with `Id=1`, enabled state and correct `Mode`.
- `Payments:CmsOwner:WebhookMode` and matching webhook signing secret in the existing configuration expected by `CmsRazorpayWebhookVerifier`.

Never put live secrets in frontend code, ZIP files, Git or `appsettings.json`.

## Recommended test order

1. Apply migration to a development database.
2. Configure test SMTP and CMS Owner Razorpay TEST credentials.
3. Register a new Admin; verify the OTP.
4. Log in and confirm `onboardingRequired=true` and no HospitalId.
5. Confirm operational API calls return 403 before clinic creation.
6. Fetch public plans and payment methods.
7. Create Initial checkout; confirm `AdminUserId` is the logged-in Admin and price/features are server-derived snapshots.
8. Create Razorpay order; close checkout and retry; confirm the same gateway order is returned.
9. Complete a Razorpay TEST payment and call verify; confirm captured status, amount, order and signature checks.
10. Repeat verify; confirm idempotent response and no duplicate activation.
11. Create clinic; confirm Hospital, Main Branch and subscription are committed together.
12. Refresh token; confirm HospitalId is present and branch/staff/operational APIs become available.
13. Test Renewal and Upgrade with an existing clinic account.
14. Test webhook delivery, duplicate event delivery and reconciliation retries.
15. Test that demo/simulated Paid records cannot create a clinic.
16. Check Super Admin onboarding list and details.

## Validation limitation

The execution environment used to prepare this source update does not have the .NET SDK (`dotnet` command is unavailable). Therefore, this package has **not** been compiled or integration-tested here. Run `dotnet build .\Adminclinic.csproj` and the tests above in your Visual Studio/Git Bash environment before merging or deploying. If the build reports an error, share the complete first error and I can correct the source.

## Not included / remaining work

The following requirements from `CMS_Two_Payment_Gateways_Integration_Guide.docx` remain to be implemented in a subsequent verified phase:

- Hospital patient Razorpay order APIs for invoices;
- hospital-specific provider credential resolution from a secure secret store (not merely a client-supplied credential reference);
- hospital payment signature and captured-state verification;
- signed, idempotent hospital webhook inbox and reconciliation;
- patient invoice allocations including partial payments and overpayment protection;
- refunds, chargebacks, settlement records and merchant-specific reconciliation reports;
- Super Admin central subscription revenue reports and separate Hospital Admin patient-collection reports;
- production KYC, live merchant onboarding, HTTPS webhook verification and controlled live settlement tests.

The existing `POST /api/payment/success` is a legacy direct-success operation; it must not be treated as a real Razorpay payment confirmation. Complete the hospital gateway before enabling patient online collections.
