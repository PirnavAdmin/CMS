using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class CreateLabTestOrderDto
{
    [Required]
    public string TestName { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string? Priority { get; set; } = "Routine";
}
