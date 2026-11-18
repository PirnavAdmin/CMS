
using System;

namespace AuthDemo.Models
{
    public class HospitalPaymentRecoveryAttempt
    {
        public long Id { get; set; }

        public int HospitalId { get; set; }

        public string PaymentSource { get; set; } = string.Empty;

        public string GatewayPaymentId { get; set; } = string.Empty;

        public long? HospitalPaymentOrderId { get; set; }

        public long? HospitalPaymentQrId { get; set; }

        public int AttemptNumber { get; set; }

        public string Status { get; set; } = "Pending";

        public string? ErrorMessage { get; set; }

        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAtUtc { get; set; }

        public DateTime? NextRetryAtUtc { get; set; }
    }
}
