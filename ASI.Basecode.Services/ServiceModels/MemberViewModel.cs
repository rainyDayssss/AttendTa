using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public class MemberViewModel
    {
        public int MemberId { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(255, ErrorMessage = "Full name cannot exceed 255 characters.")]
        public string FullName { get; set; }

        public string Email { get; set; }

        [Required(ErrorMessage = "Student number is required.")]
        [StringLength(50, ErrorMessage = "Student number cannot exceed 50 characters.")]
        public string StudentNumber { get; set; }

        [StringLength(500, ErrorMessage = "The correction reason cannot exceed 500 characters.")]
        public string StudentNumberCorrectionReason { get; set; }

        [Required(ErrorMessage = "Course is required.")]
        [StringLength(100)]
        public string Course { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Year level must be greater than 0.")]
        public int YearLevel { get; set; }

        [Required(ErrorMessage = "Section is required.")]
        [StringLength(50)]
        public string Section { get; set; }

        [StringLength(30)]
        public string MobileNumber { get; set; }

        [StringLength(100)]
        public string MembershipType { get; set; }

        // Display-only. Member edits deliberately preserve the persisted status.
        public string Status { get; set; }
    }
}
