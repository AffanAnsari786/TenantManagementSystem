using System;

namespace Tenant.Api.Models
{
    public class Record
    {
        public int Id { get; set; }
        public DateTime RentPeriod { get; set; }
        public decimal Amount { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? TenantSign { get; set; }
    }
}
