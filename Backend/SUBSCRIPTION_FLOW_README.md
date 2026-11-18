# Subscription flow changes (no payment gateway)

This package is an updated **backend source project**, not a verified release. It has not been compiled here (dotnet SDK unavailable). Keep a backup of the original project and database.

## Flow
1. Public plans -> checkout (Pending; no money collected).
2. SuperAdmin manually marks payment Paid in Development only via /api/subscriptions/payments/confirm/{id}.
3. SuperAdmin lists pending Admin creations and creates one Admin for paid purchase.
4. Admin sets password through existing forgot-password OTP flow (OTP hardening remains pending).
5. Admin logs in, POST /api/clinics atomically creates clinic, links purchase, and activates plan.
6. Admin POST /api/Auth/refresh-token receives HospitalId claim.
7. Lab APIs require active plan including Lab; reports saved outside wwwroot.
8. Renewal checkout -> manual confirmation in Development -> activation.

## Files updated
Program.cs; Controllers/{AuthController,ClinicController,SubscriptionController,SubscriptionPaymentTestingController,SuperAdminPendingAdminController,LabController}.cs; Helpers/JwtHelper.cs; Models/SubscriptionPurchase.cs; Data/AppDbContext.cs; new migration; protected-report migration script.

## IMPORTANT DATABASE WARNING
The original ZIP has no SubscriptionPurchases model/table. A new EF migration is included, but the original database may have changed independently. Startup auto-migration is commented out to avoid unreviewed schema changes. The EF model snapshot was not regenerated. Reconcile the new migration and snapshot using the EF CLI/PMC before applying to a real database. If SubscriptionPurchases already exists, do not run the create-table migration unchanged. This project is not ready to run against an existing DB without this step.

## Known limitations
No payment gateway or verified payments. Manual confirmation endpoints only work in ASPNETCORE_ENVIRONMENT=Development and require SuperAdmin JWT. Password OTP flow is inherited and needs security hardening. All cross-tenant endpoints have not been audited. Existing old subscription request workflow remains. Existing records require data migration to fill purchased snapshots. Protected reports already uploaded to wwwroot must be migrated. No frontend is present in the uploaded ZIP.
