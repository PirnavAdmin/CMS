using System;
using System.Collections.Generic;

namespace AuthDemo.Controllers;

public sealed class CreateHospitalPaymentOrderDto
{
    public int BillingId { get; set; }
    public decimal? Amount { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class VerifyHospitalPaymentDto
{
    public string LocalOrderId { get; set; } = "";
    public string RazorpayOrderId { get; set; } = "";
    public string RazorpayPaymentId { get; set; } = "";
    public string RazorpaySignature { get; set; } = "";
}

public sealed class CreateHospitalRefundDto
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}

public sealed class SettlementImportRowDto
{
    public string GatewayPaymentId { get; set; } = "";
    public string SettlementId { get; set; } = "";
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public DateTime SettledAt { get; set; }
}

public sealed class SettlementImportDto
{
    public List<SettlementImportRowDto> Rows { get; set; } = new();
}

public sealed class ContestPaymentDisputeDto
{
    public decimal? Amount { get; set; }
    public string Summary { get; set; } = "";
    public string EvidenceType { get; set; } = "billing_proof";
    public List<string> DocumentIds { get; set; } = new();
}
public sealed class CmsSettlementRowDto
{
    public int PurchaseId { get; set; }
    public string GatewayPaymentId { get; set; } = "";
    public string SettlementId { get; set; } = "";
    public string Mode { get; set; } = "Test";
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public DateTime SettledAt { get; set; }
}