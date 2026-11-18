using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

// Change #19: Reconcile provider settlement evidence with local hospital ledger.
// No billing/payment/refund financial state is changed here.
public sealed class HospitalSettlementReconciliationService
{
    private readonly AppDbContext _db;
    private readonly HospitalRazorpayGatewayService _gateway;
    private readonly IConfiguration _configuration;
    private readonly HospitalSettlementReconciliationLockService _lockService;

    public HospitalSettlementReconciliationService(
        AppDbContext db,
        HospitalRazorpayGatewayService gateway,
        IConfiguration configuration,
        HospitalSettlementReconciliationLockService lockService)
    {
        _db = db;
        _gateway = gateway;
        _configuration = configuration;
        _lockService = lockService;
    }

    public sealed record Result(
        long SettlementId,
        string GatewaySettlementId,
        string Status,
        int MatchedPayments,
        int MatchedRefunds,
        int UnmatchedTransactions,
        decimal Difference,
        string? Notes);

    // Manual requests and background workers share the same database-backed lock.
    // Keep the lock for the gateway fetch AND the database transaction.
    public Task<Result> ReconcileAsync(
        int hospitalId,
        long settlementRecordId,
        CancellationToken cancellationToken = default)
    {
        if (hospitalId <= 0) throw new ArgumentOutOfRangeException(nameof(hospitalId));
        if (settlementRecordId <= 0) throw new ArgumentOutOfRangeException(nameof(settlementRecordId));
        if (!_configuration.GetValue<bool>("Payments:HospitalSettlementReconciliation:GatewayEvidenceVerified"))
            throw new InvalidOperationException("Gateway settlement evidence is not yet verified.");

        return _lockService.ExecuteAsync(
            hospitalId, settlementRecordId,
            token => ReconcileUnderLockAsync(hospitalId, settlementRecordId, token),
            cancellationToken);
    }

    private async Task<Result> ReconcileUnderLockAsync(
        int hospitalId,
        long settlementRecordId,
        CancellationToken cancellationToken)
    {
        // Service-level guard protects both HTTP and background callers.
        if (!_configuration.GetValue<bool>("Payments:HospitalSettlementReconciliation:GatewayEvidenceVerified"))
            throw new InvalidOperationException("Gateway settlement evidence is not yet verified.");

        if (hospitalId <= 0 || settlementRecordId <= 0)
            throw new ArgumentOutOfRangeException(nameof(settlementRecordId));

        var settlement = await _db.HospitalGatewaySettlements
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == settlementRecordId &&
                x.HospitalId == hospitalId, cancellationToken)
            ?? throw new KeyNotFoundException("Hospital settlement not found.");

        // Re-check eligibility after acquiring the database application lock.
        // This makes repeated manual requests and competing workers idempotent.
        if (string.Equals(settlement.ReconciliationStatus, "Matched", StringComparison.OrdinalIgnoreCase))
            return new Result(settlement.Id, settlement.SettlementId, "Matched",
                settlement.MatchedPaymentCount, settlement.MatchedRefundCount,
                settlement.UnmatchedTransactionCount, settlement.SettlementDifference,
                settlement.ReconciliationNotes);

        if (settlement.RequiresManualReview ||
            string.Equals(settlement.ReconciliationStatus, "Review", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Settlement requires manual review; do not automatically retry it without an explicit review/reset workflow.");

        if (!string.Equals(settlement.ReconciliationStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Settlement is not pending reconciliation.");

        if (settlement.Mode != "Test" && settlement.Mode != "Live")
            throw new InvalidOperationException("Invalid settlement merchant mode.");
        if (string.IsNullOrWhiteSpace(settlement.SettlementId))
            throw new InvalidOperationException("Settlement has no gateway ID.");

        // Do not mark a payout matched unless the provider reports it as processed.
        if (!string.Equals(settlement.Status, "processed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only processed Razorpay settlements can be reconciled.");

        // Settlement-level credential pin: a specific credential must be explicitly
        // configured for this historical payout. Never fall back to today's active key.
        // Configuration example (use a secret/config provider, not a hard-coded ID):
        // Payments:HospitalSettlementReconciliation:CredentialPins:12:Live:setl_abc = 42
        var pinKey = $"Payments:HospitalSettlementReconciliation:CredentialPins:{hospitalId}:{settlement.Mode}:{settlement.SettlementId}";
        if (!int.TryParse(_configuration[pinKey], out var pinnedCredentialId) || pinnedCredentialId <= 0)
            throw new InvalidOperationException(
                "No verified historical Razorpay credential pin exists for this hospital settlement.");

        // The gateway itself also fails closed unless its evidence contract is verified.
        var evidence = await _gateway.FetchSettlementTransactionsAsync(
            hospitalId, settlement.Mode, settlement.SettlementId, settlement.SettledAt,
            credentialId: pinnedCredentialId, cancellationToken: cancellationToken);

        // The report is fetched under the settlement-specific SQL application lock.
        // Do not accept a truncated or empty report as successful evidence.
        // FetchSettlementTransactionsAsync must itself exhaust provider pagination.
        // Empty evidence is not proof that an amount-bearing settlement is reconciled.
        if (evidence.Count == 0)
            throw new InvalidOperationException("No transaction-level settlement evidence was returned.");

        var paymentIds = evidence
            .Where(x => x.EntityType.Equals("payment", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.EntityId).Distinct(StringComparer.Ordinal).ToArray();
        var refundIds = evidence
            .Where(x => x.EntityType.Equals("refund", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.EntityId).Distinct(StringComparer.Ordinal).ToArray();

        var paymentRows = await _db.HospitalPaymentTransactions
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId && x.Mode == settlement.Mode &&
                paymentIds.Contains(x.GatewayPaymentId))
            .ToListAsync(cancellationToken);
        var refundRows = await _db.HospitalPaymentRefunds
            .AsNoTracking()
            .Include(x => x.Transaction)
            .Where(x => x.HospitalId == hospitalId &&
                refundIds.Contains(x.GatewayRefundId))
            .ToListAsync(cancellationToken);

        // A duplicated local gateway ID is ambiguous: never silently choose a row.
        if (paymentRows.GroupBy(x => x.GatewayPaymentId, StringComparer.Ordinal).Any(g => g.Count() > 1) ||
            refundRows.GroupBy(x => x.GatewayRefundId, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Duplicate local gateway IDs require manual investigation.");

        var payments = paymentRows.ToDictionary(x => x.GatewayPaymentId, StringComparer.Ordinal);
        var refunds = refundRows.ToDictionary(x => x.GatewayRefundId, StringComparer.Ordinal);

        var matchedPayments = new List<(HospitalPaymentTransaction Payment,
            HospitalRazorpayGatewayService.HospitalSettlementTransactionEvidence Evidence)>();
        var matchedRefunds = new List<(HospitalPaymentRefund Refund,
            HospitalRazorpayGatewayService.HospitalSettlementTransactionEvidence Evidence)>();
        var problems = new List<string>();
        decimal providerNet = 0m;
        var seenEntries = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in evidence)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line.EntityId) ||
                !seenEntries.Add($"{line.EntityType}:{line.EntityId}"))
            {
                problems.Add("Duplicate or missing gateway transaction identity.");
                continue;
            }
            if (!string.Equals(line.SettlementId, settlement.SettlementId, StringComparison.Ordinal))
            {
                problems.Add("Settlement identity mismatch.");
                continue;
            }
            if (!string.Equals(line.Currency, "INR", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"Unsupported currency for {line.EntityId}.");
                continue;
            }
            if (line.AmountPaise < 0 || line.DebitPaise < 0 || line.CreditPaise < 0 ||
                (line.DebitPaise > 0 && line.CreditPaise > 0) ||
                (line.DebitPaise == 0 && line.CreditPaise == 0))
            {
                problems.Add($"Invalid debit/credit amounts for {line.EntityId}.");
                continue;
            }
            if (line.FeePaise < 0 || line.TaxPaise < 0)
            {
                problems.Add($"Invalid charges for {line.EntityId}.");
                continue;
            }
            // Razorpay report uses nonnegative amount plus debit/credit direction.
            // Unsupported rows remain Review rather than being silently ignored.
            var gross = line.AmountPaise / 100m;
            var fees = line.FeePaise / 100m;
            var tax = line.TaxPaise / 100m;
            providerNet += (line.CreditPaise - line.DebitPaise) / 100m;

            if (line.EntityType.Equals("payment", StringComparison.OrdinalIgnoreCase))
            {
                if (!payments.TryGetValue(line.EntityId, out var payment) ||
                    !string.Equals(payment.Status, "Captured", StringComparison.OrdinalIgnoreCase) ||
                    gross <= 0m || line.DebitPaise != 0 || line.CreditPaise <= 0 || payment.Amount != gross ||
                    (!string.IsNullOrWhiteSpace(payment.GatewaySettlementId) &&
                     !string.Equals(payment.GatewaySettlementId, settlement.SettlementId, StringComparison.Ordinal)))
                {
                    problems.Add($"Unmatched payment {line.EntityId}.");
                    continue;
                }
                matchedPayments.Add((payment, line));
            }
            else if (line.EntityType.Equals("refund", StringComparison.OrdinalIgnoreCase))
            {
                if (!refunds.TryGetValue(line.EntityId, out var refund) ||
                    refund.Transaction == null ||
                    refund.Transaction.HospitalId != hospitalId ||
                    refund.Transaction.Mode != settlement.Mode ||
                    !string.Equals(refund.Status, "Processed", StringComparison.OrdinalIgnoreCase) ||
                    gross != refund.Amount || line.DebitPaise <= 0 || line.CreditPaise != 0 ||
                    (!string.IsNullOrWhiteSpace(refund.GatewaySettlementId) &&
                     !string.Equals(refund.GatewaySettlementId, settlement.SettlementId, StringComparison.Ordinal)))
                {
                    problems.Add($"Unmatched refund {line.EntityId}.");
                    continue;
                }
                matchedRefunds.Add((refund, line));
            }
            else
            {
                problems.Add($"Unsupported settlement entry type {line.EntityType}.");
            }
        }

        // Settlement summary semantics can differ across Razorpay accounts.
        // Do not assume gross/fee/tax fields are directly additive to the net payout.
        // Require a zero difference before automatic matching.
        // Gateway settlement summary amount is assumed to be net payout INR.
        // Do not enable the evidence flag until this assumption is verified.
        var difference = decimal.Round(providerNet - settlement.Amount, 2);
        if (difference != 0m)
            problems.Add($"Provider line net differs from payout by INR {difference:0.00}.");

        // We only persist per-payment settlement records if the full evidence agrees.
        // Unsupported adjustment entries and refunds require manual investigation.
        // A settlement summary alone cannot establish that all local transactions
        // were included; only gateway-provided membership is authoritative.
        // Do not claim a complete match if local-ledger coverage is unknown.
        // A verified settlement membership report is required to establish scope.
        var isMatched = problems.Count == 0;
        var now = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var current = await _db.HospitalGatewaySettlements
            .SingleAsync(x => x.Id == settlementRecordId &&
                x.HospitalId == hospitalId && x.Mode == settlement.Mode &&
                x.SettlementId == settlement.SettlementId, cancellationToken);

        // Guard again inside the serializable transaction. A separate code path
        // must not overwrite a settlement already finalized by an administrator.
        if (!string.Equals(current.ReconciliationStatus, "Pending", StringComparison.OrdinalIgnoreCase) ||
            current.RequiresManualReview)
            throw new InvalidOperationException("Settlement eligibility changed before reconciliation was committed.");

        current.ReconciliationAttemptCount++;
        current.LastReconciliationAttemptAtUtc = now;
        current.GatewayEvidenceFetchedAtUtc = now;
        current.GatewayTransactionCount = evidence.Count;
        current.MatchedPaymentCount = matchedPayments.Count;
        current.MatchedRefundCount = matchedRefunds.Count;
        current.MatchedPaymentAmount = matchedPayments.Sum(x => x.Payment.Amount);
        current.MatchedRefundAmount = matchedRefunds.Sum(x => x.Refund.Amount);
        current.ExpectedNetAmount = providerNet;
        current.SettlementDifference = difference;
        current.UnmatchedGatewayTransactionCount = problems.Count;
        // This is not a full local-ledger audit; do not represent zero as audited.
        // Zero here means not measured, NOT proof that every local ledger entry
        // was included. Full local coverage needs a separately defined audit.
        current.UnmatchedLocalTransactionCount = 0;
        current.UnmatchedTransactionCount = problems.Count;
        current.RequiresManualReview = !isMatched;
        current.ReconciliationStatus = isMatched ? "Matched" : "Review";
        current.ReconciledAtUtc = isMatched ? now : null;
        current.UpdatedAtUtc = now;
        var problemSummary = string.Join(" ", problems.Take(8));
        current.ReconciliationNotes = problems.Count == 0 ? null :
            problemSummary[..Math.Min(1000, problemSummary.Length)];

        if (isMatched)
        {
            foreach (var (payment, line) in matchedPayments)
            {
                var record = await _db.HospitalSettlementRecords.SingleOrDefaultAsync(x =>
                    x.HospitalId == hospitalId &&
                    x.SettlementId == settlement.SettlementId &&
                    x.HospitalPaymentTransactionId == payment.Id, cancellationToken);
                if (record == null)
                {
                    record = new HospitalSettlementRecord
                    {
                        HospitalId = hospitalId,
                        HospitalPaymentTransactionId = payment.Id,
                        SettlementId = settlement.SettlementId,
                        CreatedAt = now
                    };
                    _db.HospitalSettlementRecords.Add(record);
                }
                record.Mode = settlement.Mode;
                record.GatewayPaymentId = payment.GatewayPaymentId;
                record.GatewaySettlementTransactionId = line.GatewayTransactionId;
                record.GatewayTransactionType = line.EntityType;
                record.Currency = "INR";
                record.GrossAmount = line.AmountPaise / 100m;
                record.FeeAmount = line.FeePaise / 100m;
                record.TaxAmount = line.TaxPaise / 100m;
                record.NetAmount = record.GrossAmount - record.FeeAmount - record.TaxAmount;
                record.ExpectedGrossAmount = payment.Amount;
                record.ExpectedNetAmount = record.NetAmount;
                record.GrossDifference = record.GrossAmount - payment.Amount;
                record.NetDifference = 0m;
                record.SettledAt = settlement.SettledAt;
                record.ReconciliationStatus = "Matched";
                record.ReconciledAtUtc = now;
                record.LastReconciliationAttemptAtUtc = now;
                record.ReconciliationAttemptCount++;
                record.RequiresManualReview = false;
                record.ReconciliationNotes = null;
                record.UpdatedAtUtc = now;

                var localPayment = await _db.HospitalPaymentTransactions.SingleAsync(
                    x => x.Id == payment.Id && x.HospitalId == hospitalId &&
                         x.Mode == settlement.Mode, cancellationToken);
                localPayment.GatewaySettlementId = settlement.SettlementId;
                localPayment.SettlementFeeAmount = record.FeeAmount;
                localPayment.SettlementTaxAmount = record.TaxAmount;
                localPayment.SettlementNetAmount = record.NetAmount;
                localPayment.SettlementReconciliationStatus = "Matched";
                localPayment.SettlementReconciledAtUtc = now;
                localPayment.GatewaySettledAtUtc = settlement.SettledAt;
                localPayment.SettlementReconciliationNotes = null;
            }

            foreach (var (refund, line) in matchedRefunds)
            {
                var localRefund = await _db.HospitalPaymentRefunds.SingleAsync(
                    x => x.Id == refund.Id && x.HospitalId == hospitalId,
                    cancellationToken);
                localRefund.GatewaySettlementId = settlement.SettlementId;
                localRefund.SettlementAdjustmentAmount = -line.DebitPaise / 100m;
                localRefund.SettlementFeeAmount = line.FeePaise / 100m;
                localRefund.SettlementTaxAmount = line.TaxPaise / 100m;
                localRefund.SettlementReconciliationStatus = "Matched";
                localRefund.SettlementReconciledAtUtc = now;
                localRefund.GatewaySettledAtUtc = settlement.SettledAt;
                localRefund.SettlementReconciliationNotes = null;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new Result(current.Id, current.SettlementId,
            current.ReconciliationStatus, current.MatchedPaymentCount,
            current.MatchedRefundCount, current.UnmatchedTransactionCount,
            current.SettlementDifference, current.ReconciliationNotes);
    }
}
