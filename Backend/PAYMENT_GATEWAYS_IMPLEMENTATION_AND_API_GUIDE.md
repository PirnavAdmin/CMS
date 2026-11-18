# CMS dual Razorpay payment implementation and API guide

This backend keeps two financially separate payment domains:

1. **CMS subscription gateway** — hospital Admins pay the CMS platform for Initial, Renewal and Upgrade purchases. These collections belong to the CMS merchant.
2. **Hospital patient gateway** — patients pay a hospital's own invoices. These collections belong to that hospital's Razorpay merchant.

Never use the CMS Owner key/secret for patient invoices or a hospital key/secret for subscription purchases.

## Implemented in this package

- Hospital-scoped Razorpay order creation from a Billing invoice.
- Hospital-scoped checkout signature validation and independent Razorpay payment fetch before marking a payment captured.
- Signed hospital webhook endpoint with a durable inbox and event-ID idempotency.
- Invoice allocation using `BillingPaymentTransaction` plus a gateway transaction ledger.
- Full and partial invoice payments. Invoice status becomes `Pending`, `PartiallyPaid` or `Paid` based on paid allocations less completed refunds.
- Hospital-specific partial/full refunds, refund webhook status updates and refund ledger.
- Hospital settlement import/reconciliation with gross, provider fee, tax and net amounts.
- Hospital revenue report restricted to the authenticated hospital.
- CMS subscription refund endpoint and refund webhook processing.
- CMS settlement import/reconciliation and a separate CMS subscription revenue report.
- Manual Cash/UPI/Card invoice payments now accept a partial amount as well.
- Legacy appointment `POST /api/payment/create` and `POST /api/payment/success` are Cash-only legacy appointment payment endpoints. Online appointment fees must first be generated as a Billing invoice and then paid through the hospital order/verify/webhook flow. The frontend must be migrated accordingly.

## Hospital merchant secret configuration

The `HospitalMerchantSetting` row stores merchant display settings and a credential reference only. Secrets are not returned by APIs and are not accepted from a public browser form. The gateway service reads each hospital's credentials from backend secret configuration using this exact key structure:

```json
{
  "Payments": {
    "Hospitals": {
      "12": {
        "Razorpay": {
          "Test": {
            "KeyId": "rzp_test_HOSPITAL_12_KEY",
            "KeySecret": "STORE_IN_SECRET_MANAGER",
            "WebhookSecret": "STORE_IN_SECRET_MANAGER"
          },
          "Live": {
            "KeyId": "rzp_live_HOSPITAL_12_KEY",
            "KeySecret": "STORE_IN_SECRET_MANAGER",
            "WebhookSecret": "STORE_IN_SECRET_MANAGER"
          }
        }
      }
    }
  }
}
```

`12` is an example HospitalId only. Do not commit real values to `appsettings.json` or source control. Use environment variables, .NET user-secrets for local development, or a managed secret vault in production. Example environment variable names:

- `Payments__Hospitals__12__Razorpay__Test__KeyId`
- `Payments__Hospitals__12__Razorpay__Test__KeySecret`
- `Payments__Hospitals__12__Razorpay__Test__WebhookSecret`

Before setting `IsEnabled=true`, `PUT /api/hospital/payment-settings` checks that the selected mode's three credentials are present. The API never returns the secret values.

The CMS Owner credentials remain under the existing `Payments:CmsOwner:Razorpay:{Test|Live}:...` configuration and its existing settings controller. The CMS Owner settings API now checks `KeyId`, `KeySecret` and `WebhookSecret` before enabling the central merchant. Set `Payments:CmsOwner:WebhookMode` to the mode receiving the CMS webhook.

## New hospital APIs

All hospital APIs below require JWT authentication except the Razorpay webhook. `HospitalId` is taken from the authenticated user's token; clients cannot select another hospital's merchant.

| Method | Endpoint | Access | Purpose |
|---|---|---|---|
| GET | `/api/hospital/payment-settings` | Hospital Admin | Read own merchant display settings and whether backend credentials are configured |
| PUT | `/api/hospital/payment-settings` | Hospital Admin | Enable/disable own merchant and select Test/Live mode; secrets stay in backend configuration |
| POST | `/api/hospital-payments/orders` | Billing-authorized hospital user | Create Razorpay order for an invoice; optional partial amount |
| GET | `/api/hospital-payments/orders/{localOrderId}` | Authenticated same hospital | Poll local payment order status |
| POST | `/api/hospital-payments/verify` | Authenticated same hospital | Verify checkout signature and independently confirm captured payment, order, currency and amount |
| POST | `/api/hospital-payments/webhooks/razorpay/{mode}` | Public gateway callback | Verify raw-body webhook signature using the merchant secret for the hospital mapped to the stored order; process capture/refund events idempotently |
| POST | `/api/hospital-payments/transactions/{transactionId}/refunds` | Hospital Admin | Request full or partial Razorpay refund |
| POST | `/api/hospital-payments/settlements/reconcile` | Hospital Admin | Import settlement rows and reconcile them to this hospital's captured payment IDs |
| GET | `/api/hospital-payments/reports/revenue` | Hospital Admin | Read only this hospital's collection, refund and settlement summary |

### Create hospital payment order

`POST /api/hospital-payments/orders`

```json
{
  "billingId": 501,
  "amount": 500.00,
  "idempotencyKey": "invoice-501-part-payment-1"
}
```

`amount` is optional. If omitted, the backend charges the full outstanding amount. The backend always recomputes the outstanding amount from the invoice and recorded allocations; a requested amount cannot exceed it.

Example response:

```json
{
  "localOrderId": "server-generated-local-order-id",
  "gatewayOrderId": "order_from_razorpay",
  "amountPaise": 50000,
  "currency": "INR",
  "publicKey": "rzp_test_public_key",
  "status": "Pending",
  "outstandingAmount": 1500.00,
  "mode": "Test"
}
```

Frontend uses the returned `publicKey`, `gatewayOrderId`, `amountPaise` and `currency` to open Razorpay Checkout. It then sends the checkout callback values to the verify endpoint. The frontend must display Processing until backend verification or webhook confirmation is returned.

### Verify hospital payment

`POST /api/hospital-payments/verify`

```json
{
  "localOrderId": "server-generated-local-order-id",
  "razorpayOrderId": "order_from_razorpay",
  "razorpayPaymentId": "pay_from_razorpay",
  "razorpaySignature": "signature_from_checkout"
}
```

A successful browser callback is not sufficient. The backend verifies the HMAC signature and fetches the payment from Razorpay to check `captured`, order ID, INR currency and exact paise amount before allocating it.

### Hospital settlement reconciliation

`POST /api/hospital-payments/settlements/reconcile`

```json
{
  "rows": [
    {
      "gatewayPaymentId": "pay_from_razorpay",
      "settlementId": "settl_from_provider_report",
      "grossAmount": 500.00,
      "feeAmount": 10.00,
      "taxAmount": 1.80,
      "settledAt": "2026-11-10T10:00:00Z"
    }
  ]
}
```

This is an import/reconciliation API, not a live settlement-fetch API. Rows should be sourced from the hospital's Razorpay settlement report/export. The backend checks that the payment belongs to the authenticated hospital and that gross amount matches the captured payment; it calculates net as gross minus fee minus tax. Unknown or mismatched rows are returned as exceptions.

### Hospital refund

`POST /api/hospital-payments/transactions/{transactionId}/refunds`

```json
{
  "amount": 250.00,
  "reason": "Partial invoice refund"
}
```

The backend checks the remaining refundable amount and calls Razorpay using the same hospital merchant mode used by the original order. Refunds can remain `pending` until the signed `refund.processed`/`refund.failed` webhook arrives.

### Hospital revenue report

`GET /api/hospital-payments/reports/revenue?from=2026-11-01&to=2026-11-30&mode=Live`

Returns hospital gateway gross collections, manual collections, processed refunds, net collections, settlement gross, provider fees, fee tax, settled net amount, unsettled gateway gross and pending orders. The report is scoped from the JWT HospitalId. `mode` defaults to `Live`; use `Test` for sandbox or `All` to include both.

## New CMS subscription refund and settlement APIs

These APIs use only CMS Owner credentials and subscription purchase records.

| Method | Endpoint | Access | Purpose |
|---|---|---|---|
| POST | `/api/cms/subscription-payments/refunds/{purchaseId}` | SuperAdmin | Full or partial refund for an independently verified CMS subscription purchase |
| POST | `/api/cms/payments/reports/settlements/reconcile` | SuperAdmin | Import CMS Owner settlement rows against verified subscription payments |
| GET | `/api/cms/payments/reports/subscriptions` | SuperAdmin | CMS subscription revenue, processed refunds and settlement summary |

### CMS refund request

```json
{
  "amount": 2999.00,
  "reason": "Approved subscription refund"
}
```

Only a purchase with `PaymentStatus=Paid`, `IsGatewayPaymentVerified=true`, a gateway payment ID and a saved Test/Live mode can be refunded. The CMS subscription is not automatically cancelled by the refund endpoint; subscription cancellation/revocation remains a separate business decision and must be approved explicitly.

### CMS settlement import

`POST /api/cms/payments/reports/settlements/reconcile`

```json
{
  "rows": [
    {
      "purchaseId": 1001,
      "gatewayPaymentId": "pay_cms_from_razorpay",
      "settlementId": "settl_cms_from_provider_report",
      "mode": "Test",
      "grossAmount": 2999.00,
      "feeAmount": 59.98,
      "taxAmount": 10.80,
      "settledAt": "2026-11-10T10:00:00Z"
    }
  ]
}
```

Payment-level settlement allocation import ingests provider reconciliation export rows. Payout-level settlement records can be fetched directly from Razorpay using the settlement sync endpoint or optional scheduled sync. Reports default to `mode=Live`; `mode=Test` and `mode=All` are also supported. Unsettled gateway amount is calculated against imported payment-level settlement gross, not payout-level settlement amount.

## Existing API changes

| Existing endpoint | Change |
|---|---|
| `PUT /api/hospital/payment-settings` | Now validates the selected Test/Live KeyId, KeySecret and WebhookSecret are configured before enabling the merchant. |
| `GET /api/hospital/payment-settings` | `credentialConfigured` now reflects actual backend configuration rather than a non-empty reference string. |
| `GET/PUT /api/cms-owner/payment-settings` | CMS Owner setting endpoints now validate actual Test/Live CMS Owner KeyId, KeySecret and WebhookSecret before enabling central payments. |
| `GET /api/Billing/{id}` | Adds `PaidAmount`, `RefundedAmount` and `OutstandingAmount` for the invoice. |
| `PATCH /api/Billing/{billId}/payment` | Supports `amount` for partial manual Cash/UPI/Card allocations; omitted amount collects the remaining balance. Response includes `outstandingAmount`; status can be `PartiallyPaid`. |
| `GET /api/Billing/{billId}/payments` | Continues to return the payment allocation ledger, including Razorpay allocations mirrored into `BillingPaymentTransactions`. |
| `POST /api/payment/success` | Restricted to Cash only because it does not verify any gateway signature. Online payment success must use hospital payment order/verify/webhook APIs. |
| `POST /api/cms/subscription-payments/webhook` | Now processes CMS `refund.processed` and `refund.failed` events in addition to the existing captured-payment reconciliation. |
| Existing CMS subscription checkout/order/verify/status APIs | Retained; CMS Owner credentials and purchase verification remain separate from hospital patient payments. |

## New database tables

Hospital payment domain:

- `HospitalPaymentOrders`
- `HospitalPaymentTransactions`
- `HospitalPaymentWebhookEvents`
- `HospitalPaymentRefunds`
- `HospitalSettlementRecords`

CMS subscription payment domain:

- `CmsSubscriptionRefunds`
- `CmsSubscriptionSettlementRecords`

The migrations added are:

- `20261110150000_AddHospitalPatientPayments`
- `20261110153000_AddCmsRefundSettlementReconciliation`

## Apply migrations and build

From the `Adminclinic` project folder:

```bash
dotnet ef database update --project .\Adminclinic.csproj
dotnet build .\Adminclinic.csproj
dotnet run --project .\Adminclinic.csproj
```

The build could not be run in the packaging environment because the .NET SDK is not installed there. Run these commands against a backup/test database before deploying.

## Production checklist / limits

- Configure one independent merchant account per hospital or an explicitly approved marketplace/sub-merchant setup with Razorpay. Merchant approval, settlement destination, supported payment methods and KYC are provider-controlled.
- Configure distinct Test and Live credentials and webhook signing secrets. Never commit them to the ZIP or source control.
- Configure Razorpay webhooks to the exact `/api/hospital-payments/webhooks/razorpay/Test` or `/Live` URL for each mode. CMS webhook remains separately configured.
- Payout-level settlement sync is available through Razorpay `GET /v1/settlements`; individual payment-level allocation matching still uses trusted provider reconciliation export rows.
- Refund status relies on signed webhook delivery after a provider returns `pending`. Configure retries and monitor failed webhook inbox records.
- The backend has no frontend checkout UI changes in this package; frontend must call these APIs and use Razorpay Checkout.
- Test partial payments, overpayment rejection, duplicate callbacks, cross-hospital invoice access, invalid signatures, wrong merchant mode, partial/full refunds and settlement mismatches in sandbox before live use.
- The migrations are hand-authored and the SDK was unavailable in this packaging environment. Validate them with `dotnet ef migrations list`, `dotnet ef database update` against a restored test DB, and `dotnet build` before merging.

---

## Additional backend endpoints — onboarding, disputes and settlement sync

### Merchant onboarding state

Hospital Admin:
- `GET /api/hospital/payment-onboarding`
- `POST /api/hospital/payment-onboarding/submit`

SuperAdmin:
- `GET /api/platform/payment-onboarding/hospitals`
- `PUT /api/platform/payment-onboarding/hospitals/{hospitalId}`
- `GET /api/platform/payment-onboarding/cms-owner`
- `PUT /api/platform/payment-onboarding/cms-owner`

Review request body:

```json
{
  "status": "Approved",
  "providerMerchantAccountId": "acc_provider_merchant_id",
  "notes": "Provider approval confirmed outside CMS"
}
```

Allowed statuses: `NotStarted`, `Submitted`, `UnderReview`, `Approved`, `Rejected`. Approval requires provider merchant account ID and the selected backend credentials. SuperAdmin must only record `Approved` after independently confirming Razorpay's approval. This workflow does not perform provider KYC or merchant account creation.

### Automatic settlement payout sync

Hospital Admin:
`POST /api/hospital-payments/settlements/sync?from=2026-11-01T00:00:00Z&to=2026-11-11T00:00:00Z`

SuperAdmin:
`POST /api/cms/payments/reports/settlements/sync?mode=Live&from=2026-11-01T00:00:00Z&to=2026-11-11T00:00:00Z`

These endpoints call Razorpay `GET /v1/settlements` with date filters and pagination, then upsert payout-level settlement ID, status, gross payout amount, fees, tax, UTR and provider timestamp. The API limits a single sync range to 93 days. The existing `settlements/reconcile` endpoints still import payment-level settlement allocation rows from a settlement reconciliation export; payout summaries alone cannot identify each individual invoice payment allocation.

### Dispute and chargeback lifecycle

Subscribe the merchant's Razorpay webhook to:
- `payment.dispute.created`
- `payment.dispute.under_review`
- `payment.dispute.action_required`
- `payment.dispute.won`
- `payment.dispute.lost`
- `payment.dispute.closed`

Hospital Admin: `GET /api/hospital-payments/disputes`, `POST /api/hospital-payments/disputes/{disputeId}/documents`, `POST /api/hospital-payments/disputes/{disputeId}/contest`, `POST /api/hospital-payments/disputes/{disputeId}/accept`.
SuperAdmin: `GET /api/cms/payments/reports/disputes?mode=All`, `POST /api/cms/payments/reports/disputes/{disputeId}/documents`, `POST /api/cms/payments/reports/disputes/{disputeId}/contest`, `POST /api/cms/payments/reports/disputes/{disputeId}/accept`.

Webhook processing stores and updates disputes against the original payment. The backend records status, reason, amount, evidence deadline and raw provider payload. Admin/SuperAdmin can upload PDF/JPG/PNG evidence documents through the backend, then submit a full/partial contest or accept an open dispute. Final adjudication remains provider-controlled.

### New database migration

```bash
dotnet ef database update --project .\\Adminclinic.csproj
```

New migration: `20261110170000_AddPaymentDisputesSettlementSyncAndMerchantOnboarding`.

### Optional scheduled settlement sync

The backend also includes `RazorpaySettlementSyncBackgroundService`. It runs once at startup and then at the configured interval when enabled. It syncs payout-level settlement records for all KYC-approved hospital merchants and the CMS Owner merchant, using the configured merchant mode.

`appsettings.json` defaults:

```json
{
  "Payments": {
    "SettlementSync": {
      "Enabled": false,
      "IntervalHours": 24,
      "LookbackDays": 7
    }
  }
}
```

Enable through environment variables/secret configuration, for example `Payments__SettlementSync__Enabled=true`. `IntervalHours` is limited to 1–168 and `LookbackDays` to 1–93. Keep it disabled until each merchant's settlement API access and credentials are configured. The scheduled job syncs settlement payout summaries; it does not replace payment-level settlement allocation reconciliation.

Dispute contest request body (the `documentIds` must be returned by the matching dispute's `/documents` upload endpoint):

```json
{
  "amount": 1500.00,
  "summary": "The consultation service was provided and the attached invoice and service record support the payment.",
  "evidenceType": "proof_of_service",
  "documentIds": ["doc_example_123"]
}
```

`amount` is optional; if omitted, the full dispute amount is contested. At least one uploaded document is required by Razorpay for submission. The `accept` endpoint is irreversible and tells Razorpay that the merchant accepts the dispute/loss. See Razorpay's [Disputes API](https://razorpay.com/docs/api/disputes/) and [Documents API](https://razorpay.com/docs/api/documents/) for provider-side requirements and availability.
