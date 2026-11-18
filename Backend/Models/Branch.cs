using System.Text.Json.Serialization;

namespace AuthDemo.Models;

public class Branch
{

    public int? Id { get; set; }


    public int HospitalId { get; set; }


    [JsonIgnore]

    public Hospital? Hospital { get; set; }


    public string Name { get; set; } = string.Empty;


    public string? Phone { get; set; }


    public string? Email { get; set; }


    public string? Address { get; set; }


    public string? City { get; set; }


    public string? State { get; set; }


    public string? District { get; set; }


    public string? Country { get; set; }


    public string? PostalCode { get; set; }


    public bool IsActive { get; set; } = true;


    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}