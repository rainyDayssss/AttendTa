namespace ASI.Basecode.Data.Models
{
    public partial class Member
    {
        public int MemberId { get; set; }

        public int? UserId { get; set; }

        public string StudentNumber { get; set; }

        public string FullName { get; set; }

        public string CourseYear { get; set; }

        public string Section { get; set; }

        public string ContactDetails { get; set; }

        public string MembershipType { get; set; }

        public string Status { get; set; }
    }
}
