using System;
using System.ComponentModel.DataAnnotations;

namespace Tenant.Api.Models
{
    public class SharedLink
    {
        public int Id { get; set; }
        public string ShareToken { get; set; } = string.Empty;
        public int EntryId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;
    }
    
    public class ShareLinkRequest
    {
        [Range(1, int.MaxValue)]
        public int EntryId { get; set; }

        [Range(1, 365)]
        public int ExpiryDays { get; set; } = 30; // Default 30 days
    }
    
    public class ShareLinkResponse
    {
        public string ShareToken { get; set; } = string.Empty;
        public string ShareUrl { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
    }
}
