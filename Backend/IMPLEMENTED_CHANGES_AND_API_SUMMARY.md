# CMS backend — implemented changes and API summary

## Payment domains

### A. CMS subscription collections
- Continue to use CMS Owner Razorpay credentials for Initial/Renewal/Upgrade subscription purchases.
- Add Super Admin-only partial/full subscription refunds.
- Process signed CMS refund webhook events (`refund.processed`, `refund.failed`).
- Import CMS Owner settlement rows and calculate gross, provider fees, fee tax and net settlement.
- Provide a CMS-only revenue report; hospital patient collections are excluded.

### B. Hospital patient collections
- Each hospital uses its own Razorpay Test/Live merchant credentials.
- Create a Razorpay order only for an invoice belonging to the authenticated HospitalId.
- Support full or partial payment amounts; backend recomputes outstanding amount and reserves active pending order amounts to reduce duplicate/overpayment risk.
- Verify checkout HMAC and independently fetch payment from Razorpay before allocation.
- Process signed hospital webhooks for captured payments and refund outcomes; persist webhook event IDs and raw payloads for idempotency/retry.
- Mirror successful gateway allocations into the existing `BillingPaymentTransactions` ledger and add a hospital gateway transaction ledger.
- Add partial/full Razorpay refunds and settlement import/reconciliation.
- Provide a hospital-only revenue report.

## Invoice/payment behavior changes
- `PATCH /api/Billing/{billId}/payment` accepts optional `amount`; if omitted, the remaining balance is recorded. It returns `outstandingAmount` and sets `PartiallyPaid` until fully paid.
- `GET /api/Billing/{id}` returns `PaidAmount`, `RefundedAmount` and `OutstandingAmount`.
- Bills with any payment allocation cannot be edited or deleted through the existing update/delete endpoints; use an adjustment/refund workflow.
- Legacy `POST /api/payment/create` and `POST /api/payment/success` are Cash-only. They do not verify a Razorpay signature. Online appointment fees must be represented by a Billing invoice and paid through the hospital payment APIs.
- Existing manual Cash/UPI/Card collection remains available to authorized billing staff; these are staff-recorded offline/manual transactions, not a gateway callback.

## New APIs

| Method | Endpoint | Access | Purpose |
|---|---|---|---|
| POST | `/api/hospital-payments/orders` | Billing-authorized hospital staff | Create hospital-scoped Razorpay invoice order; optional partial amount and idempotency key |
| GET | `/api/hospital-payments/orders/{localOrderId}` | Same hospital billing staff | Poll order status |
| POST | `/api/hospital-payments/verify` | Same hospital billing staff | Verify signature and provider-captured payment; allocate invoice payment |
| POST | `/api/hospital-payments/webhooks/razorpay/{mode}` | Razorpay public callback | Verify signed webhook with the merchant mapped to stored order; process captured/refund events |
| POST | `/api/hospital-payments/transactions/{transactionId}/refunds` | Hospital Admin | Request full/partial patient payment refund |
| POST | `/api/hospital-payments/settlements/reconcile` | Hospital Admin | Import and match settlement export rows |
| GET | `/api/hospital-payments/reports/revenue` | Hospital Admin | Hospital-only collections/refunds/settlements report; `mode=Live|Test|All` |
| POST | `/api/cms/subscription-payments/refunds/{purchaseId}` | Super Admin | CMS subscription refund |
| POST | `/api/cms/payments/reports/settlements/reconcile` | Super Admin | Import CMS Owner settlement export rows |
| GET | `/api/cms/payments/reports/subscriptions` | Super Admin | CMS subscription revenue/refund/settlement report; `mode=Live|Test|All` |

## Existing APIs modified

| Endpoint | Change |
|---|---|
| `GET /api/hospital/payment-settings` | `credentialConfigured` reflects real backend configuration for the selected mode |
| `PUT /api/hospital/payment-settings` | Cannot enable a merchant unless its Test/Live KeyId, KeySecret and WebhookSecret exist in backend secret configuration |
| `GET /api/cms-owner/payment-settings` | `credentialConfigured` reflects CMS Owner backend configuration |
| `PUT /api/cms-owner/payment-settings` | Cannot enable central merchant unless CMS Owner KeyId, KeySecret and WebhookSecret exist |
| `PATCH /api/Billing/{billId}/payment` | Optional partial amount; response includes outstanding balance and `PartiallyPaid` status |
| `GET /api/Billing/{id}` | Adds paid/refunded/outstanding amounts |
| `POST /api/payment/create` | Cash-only legacy appointment path |
| `POST /api/payment/success` | Cash-only; rejects unverified online success |
| `POST /api/cms/subscription-payments/webhook` | Adds CMS refund webhook event processing to existing captured-payment reconciliation |

## New database tables and migrations

Hospital payment tables:
- `HospitalPaymentOrders`
- `HospitalPaymentTransactions`
- `HospitalPaymentWebhookEvents`
- `HospitalPaymentRefunds`
- `HospitalSettlementRecords`

CMS subscription payment tables:
- `CmsSubscriptionRefunds`
- `CmsSubscriptionSettlementRecords`

Migrations:
- `20261110150000_AddHospitalPatientPayments`
- `20261110153000_AddCmsRefundSettlementReconciliation`

## Secret configuration

Hospital credentials are resolved from backend configuration:
`Payments:Hospitals:{HospitalId}:Razorpay:{Test|Live}:KeyId`, `KeySecret`, `WebhookSecret`.

CMS Owner credentials remain under:
`Payments:CmsOwner:Razorpay:{Test|Live}:KeyId`, `KeySecret`, `WebhookSecret`.

Do not commit real secrets to source control or expose them to the frontend. Use environment variables or a managed secret manager. CMS webhook mode is selected by `Payments:CmsOwner:WebhookMode`; hospital webhook mode is explicit in the route.

## Important limits / verification

- Payment-level settlement allocation reconciliation imports trusted Razorpay reconciliation export rows. Payout-level settlement records can also be fetched automatically from Razorpay `GET /v1/settlements` for date ranges.
- Razorpay merchant KYC, UPI/card/netbanking activation, merchant settlement destinations and production approval must still be completed externally for CMS Owner and each hospital. The backend now records onboarding/review state and blocks checkout until approval is recorded.
- Frontend Razorpay Checkout integration is not included in this backend-only ZIP; frontend must call the new order/verify/status APIs.
- CMS subscription refund does not automatically cancel an active subscription; cancellation/revocation is a separate business decision.
- The packaging environment has no .NET SDK, so `dotnet build` and database migration execution were not possible here. Run them on a restored test database before merging/deployment.

## Additional backend functionality added in the latest package

### Settlement payout synchronization
- Hospital Admin can trigger a date-range sync from that hospital's own Razorpay merchant using `POST /api/hospital-payments/settlements/sync?from={UTC}&to={UTC}`.
- SuperAdmin can sync CMS Owner payouts using `POST /api/cms/payments/reports/settlements/sync?mode=Test|Live&from={UTC}&to={UTC}`.
- Sync calls Razorpay's `GET /v1/settlements` API with pagination and persists payout amount, fees, fee tax, UTR, provider status and settlement timestamp.
- Existing payment-level settlement import/reconciliation APIs remain available to match individual payments to settlement exports. The settlement-list API returns payout-level records, not the payment-level allocation rows.

### Disputes / chargebacks
- Signed hospital and CMS webhook handlers now record Razorpay dispute lifecycle events: created, under review, action required, won, lost and closed.
- Disputes are associated with the originating hospital payment transaction or CMS subscription purchase, updated by dispute ID, and retained with webhook payload/evidence deadline.
- Authorized staff can upload PDF/JPG/PNG evidence to Razorpay, submit a full/partial contest, or accept an open dispute through backend endpoints.
- Hospital Admin: `GET /api/hospital-payments/disputes`, `POST /api/hospital-payments/disputes/{id}/documents`, `/contest`, `/accept`.
- SuperAdmin: `GET /api/cms/payments/reports/disputes?mode=Test|Live|All`, plus corresponding `/documents`, `/contest`, `/accept` endpoints.
- Revenue reports expose open dispute count and lost dispute amount. Final adjudication remains provider-controlled.

### Merchant onboarding and activation workflow
- Hospital Admin: `GET /api/hospital/payment-onboarding` and `POST /api/hospital/payment-onboarding/submit`.
- SuperAdmin: `GET /api/platform/payment-onboarding/hospitals`, `PUT /api/platform/payment-onboarding/hospitals/{hospitalId}`.
- CMS Owner: `GET/PUT /api/platform/payment-onboarding/cms-owner`.
- Both merchant settings tables now store KYC state, provider merchant account ID, notes, submission and review timestamps.
- Checkout activation is blocked unless the merchant is approved, the provider account ID is present, and selected mode credentials are configured.
- The API records the review decision; it cannot submit KYC documents to Razorpay or independently grant provider approval.

### New migration
- `20261110170000_AddPaymentDisputesSettlementSyncAndMerchantOnboarding`
- Optional scheduled settlement payout sync is implemented in `RazorpaySettlementSyncBackgroundService`; it is disabled by default and can be enabled with `Payments__SettlementSync__Enabled=true`. It re-syncs a configurable recent date window at a configurable interval.
