using System;

namespace ASI.Basecode.Data.Models
{
    /// <summary>
    /// Read projection for a member joined to its linked account.
    /// </summary>
    public class MemberRecord
    {
        public int MemberId { get; set; }

        public int UserId { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }

        public string StudentNumber { get; set; }

        public string Course { get; set; }

        public int YearLevel { get; set; }

        public string Section { get; set; }

        public string MobileNumber { get; set; }

        public string MembershipType { get; set; }

        public string Status { get; set; }

        public int CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
