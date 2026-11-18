CMS BACKEND UPDATE — SELF-SERVICE ONBOARDING + DUAL PAYMENT GATEWAYS

Start here:
1. Read SELF_SERVICE_ONBOARDING_IMPLEMENTATION.md.
2. Read API_CHANGELOG.md for new and modified APIs.
3. Read PAYMENT_GATEWAYS_IMPLEMENTATION_AND_API_GUIDE.md for both payment domains, payloads, merchant secrets, refunds and settlement reconciliation.
4. Configure JWT, SMTP, CMS Owner Razorpay secrets and each hospital's Razorpay Test/Live secrets outside source control.
5. Back up your development database.
6. From this folder run:
   dotnet ef database update --project .\Adminclinic.csproj
   dotnet build .\Adminclinic.csproj
7. Test all sandbox scenarios before connecting the frontend or deploying.

Implemented in this package:
- Admin self-registration, email OTP and restricted prepayment onboarding.
- Authenticated Initial CMS subscription purchase and CMS Owner Razorpay order/verification/webhook flow.
- Verified-payment-only clinic creation and Main Branch creation.
- Existing CMS Renewal and Upgrade paths retained.
- Hospital-scoped Razorpay patient invoice orders, checkout verification and signed webhook processing.
- Invoice payment allocation and partial payments for Razorpay and manual Cash/UPI/Card collection.
- Hospital and CMS subscription refund ledgers and refund webhook status processing.
- Separate hospital and CMS settlement import/reconciliation ledgers.
- Separate hospital patient-collection and CMS subscription-revenue reports.
- Legacy online appointment success endpoint hardened to reject client-submitted online success.

Important configuration:
Hospital credentials are read from `Payments:Hospitals:{HospitalId}:Razorpay:{Test|Live}:KeyId`, `KeySecret` and `WebhookSecret`. Use environment variables or a secret manager; do not commit actual credentials. CMS Owner credentials remain in the existing CMS Owner configuration path. See the payment implementation guide for examples.

Settlement limitation:
Settlement reconciliation supports payment-level import from trusted provider reconciliation exports and automatic payout-level sync from Razorpay GET /v1/settlements. Provider account onboarding, KYC approval and settlement destination remain managed by Razorpay; CMS records the review workflow and gates activation.

Build verification:
The environment that prepared this package did not have the .NET SDK installed, so `dotnet build` and database migration execution could not be run here. Validate against a restored test database and run the commands above before merging/deploying.

LATEST PAYMENT BACKEND ADDITIONS (2026-11-10)
- Hospital and CMS Razorpay dispute/chargeback webhook lifecycle tracking and read APIs.
- Automatic settlement payout synchronization from Razorpay GET /v1/settlements; payment-level settlement allocation import remains available separately.
- Hospital and CMS Owner merchant onboarding/KYC status workflow records.
- Checkout activation now requires SuperAdmin-recorded provider approval, provider merchant account ID, and configured credentials.
- New migration: 20261110170000_AddPaymentDisputesSettlementSyncAndMerchantOnboarding.
- Razorpay KYC/merchant approval, frontend Checkout, and actual bank account onboarding remain external/frontend responsibilities; final dispute adjudication remains Razorpay-controlled.
- Dispute evidence support: upload PDF/JPG/PNG documents to Razorpay, contest a dispute for full/partial amount, or accept an open dispute through authorized backend APIs.
- Optional scheduled settlement payout sync is included and disabled by default. Enable with Payments__SettlementSync__Enabled=true; configure IntervalHours and LookbackDays as documented in PAYMENT_GATEWAYS_IMPLEMENTATION_AND_API_GUIDE.md.
