namespace AuthDemo.DTOs;

public class UpsertPatientVitalsDto
{
    public string? Symptoms { get; set; }
    public string? BloodPressure { get; set; }
    public string? SugarLevel { get; set; }
    public string? Temperature { get; set; }
    public string? Weight { get; set; }
    public string? PulseRate { get; set; }
    public string? RespiratoryRate { get; set; }
}
