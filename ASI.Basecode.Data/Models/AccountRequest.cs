using System;

namespace ASI.Basecode.Data.Models
{
    public class AccountRequest
    {
        public int RequestId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Course { get; set; }
        public int YearLevel { get; set; }
        public string Section { get; set; }
        public string MobileNumber { get; set; }
        public string Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string ReviewNotes { get; set; }
        public int? CreatedUserId { get; set; }
    }
}
