using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public class MemberViewModel
    {
        public int MemberId { get; set; }

        [Required(ErrorMessage = "Student number is required.")]
        public string StudentNumber { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Course/year is required.")]
        public string CourseYear { get; set; }

        [Required(ErrorMessage = "Section is required.")]
        public string Section { get; set; }

        public string ContactDetails { get; set; }

        public string MembershipType { get; set; }

        public string Status { get; set; }
    }
}
