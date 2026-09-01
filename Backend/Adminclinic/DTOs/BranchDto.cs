namespace AuthDemo.DTOs
{
    public class BranchDto
    {
        public string Name { get; set; } = string.Empty;

        public int HospitalId { get; set; }

        public string? Phone { get; set; }

        public string? Email { get; set; }

        public string? Address { get; set; }

        public string? City { get; set; }

        public string? District { get; set; }

        public string? State { get; set; }

        public string? Country { get; set; }

        public string? PostalCode { get; set; }
    }
}