using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// Public membership request fields. The annotations are the single validation surface
    /// shared by the MVC form and the service boundary.
    /// </summary>
    public class AccountRequestViewModel
    {
        [Required(ErrorMessage = "Student number is required.")]
        [RegularExpression(ProfileOptions.StudentNumberPattern, ErrorMessage = "Enter an 8-digit student number.")]
        [Display(Name = "Student number")]
        public string StudentNumber { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(255, ErrorMessage = "Full name cannot exceed 255 characters.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Course is required.")]
        [RegularExpression(ProfileOptions.CoursePattern, ErrorMessage = "Choose BSCS or BSIT.")]
        [Display(Name = "Course")]
        public string Course { get; set; }

        [Required(ErrorMessage = "Year level is required.")]
        [Range(ProfileOptions.MinYearLevel, ProfileOptions.MaxYearLevel, ErrorMessage = "Choose a year level from 1 to 4.")]
        [Display(Name = "Year level")]
        public int? YearLevel { get; set; }

        [Required(ErrorMessage = "Section is required.")]
        [RegularExpression(ProfileOptions.SectionPattern, ErrorMessage = "Choose a section from A to C.")]
        [Display(Name = "Section")]
        public string Section { get; set; }

        [RegularExpression(ProfileOptions.MobileNumberPattern, ErrorMessage = "Enter an 11-digit mobile number starting with 09.")]
        [Display(Name = "Mobile number")]
        public string MobileNumber { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [RegularExpression(ProfileOptions.PasswordPattern, ErrorMessage = "Use at least 8 characters with a letter and a digit.")]
        [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm your password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; }
    }
}
