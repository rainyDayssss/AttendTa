using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.ServiceModels
{
    public class MemberViewModel
    {
        public void Normalize()
        {
            FullName = ProfileOptions.NormalizeName(FullName);
            StudentNumber = StudentNumber?.Trim();
            Course = ProfileOptions.NormalizeCourse(Course);
            Section = ProfileOptions.NormalizeSection(Section);
            MobileNumber = string.IsNullOrWhiteSpace(MobileNumber) ? null : MobileNumber.Trim();
            MembershipType = string.IsNullOrWhiteSpace(MembershipType) ? null : MembershipType.Trim();
            StudentNumberCorrectionReason = string.IsNullOrWhiteSpace(StudentNumberCorrectionReason)
                ? null : StudentNumberCorrectionReason.Trim();
        }

        public int MemberId { get; set; }
        public string EditVersion { get; set; }

        // Display-only. The linked users.user_id, read from the member record.
        public int UserId { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(255, ErrorMessage = "Full name cannot exceed 255 characters.")]
        public string FullName { get; set; }

        public string Email { get; set; }

        [Required(ErrorMessage = "Student number is required.")]
        [RegularExpression(ProfileOptions.StudentNumberPattern, ErrorMessage = "Enter an 8-digit student number.")]
        public string StudentNumber { get; set; }

        [StringLength(500, ErrorMessage = "The correction reason cannot exceed 500 characters.")]
        public string StudentNumberCorrectionReason { get; set; }

        [Required(ErrorMessage = "Course is required.")]
        [RegularExpression(ProfileOptions.CoursePattern, ErrorMessage = "Choose BSCS or BSIT.")]
        public string Course { get; set; }

        [Range(ProfileOptions.MinYearLevel, ProfileOptions.MaxYearLevel, ErrorMessage = "Choose a year level from 1 to 4.")]
        public int YearLevel { get; set; }

        [Required(ErrorMessage = "Section is required.")]
        [RegularExpression(ProfileOptions.SectionPattern, ErrorMessage = "Choose a section from A to C.")]
        public string Section { get; set; }

        [RegularExpression(ProfileOptions.MobileNumberPattern, ErrorMessage = "Enter an 11-digit mobile number starting with 09.")]
        public string MobileNumber { get; set; }

        [StringLength(100)]
        public string MembershipType { get; set; }

        // Display-only. Member edits deliberately preserve the persisted status.
        public string Status { get; set; }
    }
}
