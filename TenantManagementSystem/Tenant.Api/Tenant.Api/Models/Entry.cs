using System;
using System.Collections.Generic;

namespace Tenant.Api.Models
{
    public class Entry
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? UserId { get; set; }
        public ICollection<Record> Records { get; set; } = new List<Record>();
    }
}
