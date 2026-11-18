using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class UpsertLabTestMasterDto
{
    [Required] public string TestCode { get; set; } = string.Empty;
    [Required] public string TestName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? SampleType { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    [Range(0, double.MaxValue)] public decimal Price { get; set; }
    [Range(0, int.MaxValue)] public int TurnaroundHours { get; set; }
    public string? Instructions { get; set; }
    public int? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateLabResultDto
{
    public string? ResultValue { get; set; }
    public string? ResultUnit { get; set; }
    public string? ReferenceRange { get; set; }
    public string? Remarks { get; set; }
}

public class UpdateLabStatusDto
{
    [Required] public string Status { get; set; } = string.Empty;
}
