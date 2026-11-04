using System.ComponentModel.DataAnnotations;
namespace ASI.Basecode.Services.ServiceModels
{
    public class AccountRequestViewModel
    {
        [Required, StringLength(50)] public string StudentNumber { get; set; }
        [Required, StringLength(255)] public string FullName { get; set; }
        [Required, EmailAddress, StringLength(255)] public string Email { get; set; }
        [Required, StringLength(100)] public string Course { get; set; }
        [Range(1, int.MaxValue)] public int YearLevel { get; set; }
        [Required, StringLength(50)] public string Section { get; set; }
        [StringLength(30)] public string MobileNumber { get; set; }
        [Required, StringLength(128, MinimumLength = 8), DataType(DataType.Password)] public string Password { get; set; }
        [Required, StringLength(128), Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; }
    }
}
