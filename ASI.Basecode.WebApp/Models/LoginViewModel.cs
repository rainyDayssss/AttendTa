using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// Email sign-in view model.
    /// </summary>
    public class LoginViewModel
    {
        /// <summary>
        /// Gets or sets the account email, which is the login identifier.
        /// </summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the local URL to return to after a successful sign-in.
        /// </summary>
        public string ReturnUrl { get; set; }
    }
}
