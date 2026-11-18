# API Change Summary

## New

- `POST /api/auth/admin/register` — create a self-service Admin account and send registration OTP.
- `POST /api/auth/admin/verify-otp` — verify registration email OTP.
- `POST /api/auth/admin/resend-otp` — resend registration OTP.
- `GET /api/superadmin/admin-onboarding` — Super Admin read-only list of Admin onboarding and latest payment status.
- `GET /api/superadmin/admin-onboarding/{adminId}` — Super Admin read-only detail and purchase history.

## Modified

- `POST /api/Auth/login` — unverified Admin accounts cannot log in; existing accounts remain verified by migration default.
- `POST /api/subscriptions/checkout` — now authenticated; creates Initial purchase linked to logged-in Admin; identity values are server-derived.
- `GET /api/subscriptions/payment-methods` — reports actual CMS Owner gateway configuration without exposing secrets.
- `POST /api/cms/subscription-payments/create-order/{purchaseId}` — supports Initial as well as Renewal/Upgrade; reuses existing unpaid gateway order for retry.
- `POST /api/cms/subscription-payments/verify` — supports Initial payment and links verified payment to existing Admin.
- `GET /api/cms/subscription-payments/{purchaseId}/status` — supports Initial purchase before HospitalId assignment.
- `POST /api/clinics` — requires verified CMS gateway payment and creates Main Branch in the same transaction.
- `POST /api/cms/subscription-payments/webhook` — reconciliation also marks linked Initial purchase as ready for clinic onboarding.

## Preserved

- Existing Renewal checkout and activation APIs.
- Existing Upgrade checkout and activation APIs.
- Existing password-reset OTP endpoints, separated from Admin registration OTP by OTP purpose.
- Existing Super Admin pending-admin endpoints for historical/legacy purchases only; new self-registration purchases are linked to their Admin and do not enter that manual creation queue.

## Compatibility note

The Initial checkout API has a deliberate breaking change: it now requires an authenticated, email-verified Admin token. Frontend checkout must register, verify OTP, log in, then call authenticated checkout.

## Additional new APIs — dual payment gateways

### Hospital patient collections

- `POST /api/hospital-payments/orders` — create a hospital-merchant Razorpay order for a hospital-owned invoice; supports optional partial amount and idempotency key.
- `GET /api/hospital-payments/orders/{localOrderId}` — read same-hospital payment order status.
- `POST /api/hospital-payments/verify` — verify checkout signature and independently fetch the Razorpay payment before allocating it to the invoice.
- `POST /api/hospital-payments/webhooks/razorpay/{mode}` — signed hospital webhook for captured payments and refund outcomes; `{mode}` is `Test` or `Live`.
- `POST /api/hospital-payments/transactions/{transactionId}/refunds` — Hospital Admin full/partial refund.
- `POST /api/hospital-payments/settlements/reconcile` — Hospital Admin imports provider settlement rows and reconciles gross/fees/tax/net by payment ID.
- `GET /api/hospital-payments/reports/revenue` — Hospital Admin report scoped to the authenticated HospitalId; `mode=Live|Test|All` separates sandbox from live collections.

### CMS subscription collections

- `POST /api/cms/subscription-payments/refunds/{purchaseId}` — Super Admin full/partial refund for a verified CMS subscription payment.
- `POST /api/cms/payments/reports/settlements/reconcile` — Super Admin imports CMS Owner settlement rows.
- `GET /api/cms/payments/reports/subscriptions` — Super Admin central subscription collections/refunds/settlements report; excludes patient collections and supports `mode=Live|Test|All`.

## Additional modified APIs

- `GET /api/hospital/payment-settings` — `credentialConfigured` checks actual backend Test/Live credential configuration.
- `GET /api/cms-owner/payment-settings` and `PUT /api/cms-owner/payment-settings` — now validate CMS Owner Test/Live KeyId, KeySecret and WebhookSecret before enabling the central merchant.
- `PUT /api/hospital/payment-settings` — refuses to enable a mode until KeyId, KeySecret and WebhookSecret are configured for that HospitalId/mode.
- `PATCH /api/Billing/{billId}/payment` — accepts optional `amount` for partial manual Cash/UPI/Card allocations; response now includes `outstandingAmount` and can return `PartiallyPaid`.
- `GET /api/Billing/{id}` — adds `PaidAmount`, `RefundedAmount` and `OutstandingAmount`.
- `GET /api/Billing/{id}` — adds `PaidAmount`, `RefundedAmount` and `OutstandingAmount`.
- `GET /api/Billing/{billId}/payments` — returns the existing ledger, including Razorpay allocations mirrored into `BillingPaymentTransactions`.
- `POST /api/payment/create` — legacy appointment payment creation is Cash-only; online fees must be billed as an invoice and paid through `/api/hospital-payments`.
- `POST /api/payment/success` — legacy endpoint accepts Cash only; it rejects client-submitted online payment success because it does not verify a gateway signature. Online invoice collections use `/api/hospital-payments`.
- `POST /api/cms/subscription-payments/webhook` — additionally reconciles CMS `refund.processed` and `refund.failed` events.

## New tables and migrations

- Hospital: `HospitalPaymentOrders`, `HospitalPaymentTransactions`, `HospitalPaymentWebhookEvents`, `HospitalPaymentRefunds`, `HospitalSettlementRecords`.
- CMS: `CmsSubscriptionRefunds`, `CmsSubscriptionSettlementRecords`.
- Migrations: `20261110150000_AddHospitalPatientPayments` and `20261110153000_AddCmsRefundSettlementReconciliation`.

Read `PAYMENT_GATEWAYS_IMPLEMENTATION_AND_API_GUIDE.md` for request examples, credential configuration, webhook setup, settlement import format and limitations.

## 2026-11-10 — Payment dispute, settlement sync and merchant onboarding extensions

### New APIs
- `GET /api/hospital/payment-onboarding` — Admin views the authenticated hospital's KYC/onboarding state.
- `POST /api/hospital/payment-onboarding/submit` — Admin submits the hospital merchant profile for platform review (does not submit KYC to Razorpay).
- `GET /api/platform/payment-onboarding/hospitals` — SuperAdmin lists hospital merchant onboarding states.
- `PUT /api/platform/payment-onboarding/hospitals/{hospitalId}` — SuperAdmin records external Razorpay review status and merchant account ID; approval requires credentials and provider account ID.
- `GET /api/platform/payment-onboarding/cms-owner` — SuperAdmin views CMS Owner onboarding state.
- `PUT /api/platform/payment-onboarding/cms-owner` — SuperAdmin records CMS Owner external KYC review state.
- `POST /api/hospital-payments/settlements/sync?from={utc}&to={utc}` — fetches settlement payout records from the selected hospital's Razorpay account and upserts them.
- `GET /api/hospital-payments/disputes` — Admin lists hospital disputes/chargebacks.
- `POST /api/hospital-payments/disputes/{disputeId}/documents` — upload PDF/JPG/PNG evidence to Razorpay for a hospital dispute.
- `POST /api/hospital-payments/disputes/{disputeId}/contest` — submit a full/partial dispute contest with uploaded evidence.
- `POST /api/hospital-payments/disputes/{disputeId}/accept` — accept an open hospital dispute (irreversible at Razorpay).
- `POST /api/cms/payments/reports/settlements/sync?mode=Test|Live&from={utc}&to={utc}` — fetches CMS Owner settlement payout records.
- `GET /api/cms/payments/reports/disputes?mode=Test|Live|All` — SuperAdmin lists CMS subscription disputes/chargebacks.
- `POST /api/cms/payments/reports/disputes/{disputeId}/documents` — upload PDF/JPG/PNG evidence to Razorpay for a CMS subscription dispute.
- `POST /api/cms/payments/reports/disputes/{disputeId}/contest` — submit a full/partial CMS dispute contest with uploaded evidence.
- `POST /api/cms/payments/reports/disputes/{disputeId}/accept` — accept an open CMS dispute (irreversible at Razorpay).

### New persistence
- `HospitalPaymentDisputes`, `CmsSubscriptionDisputes` — dispute ID, payment mapping, amount, reason, evidence deadline, status, payload and lifecycle timestamps.
- `HospitalGatewaySettlements`, `CmsGatewaySettlements` — settlement payout ID, mode, amount, fees, tax, UTR, provider status and sync timestamps.
- Added KYC/onboarding status, provider merchant account ID, notes, submitted and reviewed timestamps to both merchant settings tables.

### Webhook handling
- Hospital and CMS webhook handlers now process `payment.dispute.created`, `payment.dispute.under_review`, `payment.dispute.action_required`, `payment.dispute.won`, `payment.dispute.lost`, and `payment.dispute.closed`.
- Dispute records are upserted idempotently by Razorpay dispute ID, include uploaded evidence document references, support contest/accept actions, and are included in revenue reporting.

### Merchant activation rules
- A merchant cannot be enabled for checkout until SuperAdmin has recorded `KycStatus=Approved`, a provider merchant account ID is present, and the selected Test/Live credentials are configured.
- This application records the onboarding workflow only. Razorpay KYC, account approval, supported methods and bank settlement destination remain external provider actions.
- Added optional `RazorpaySettlementSyncBackgroundService` for scheduled payout-level sync across approved hospital merchants and CMS Owner. Disabled by default; configure `Payments:SettlementSync:Enabled`, `IntervalHours`, and `LookbackDays`.
