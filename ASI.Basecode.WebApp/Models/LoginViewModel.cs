using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// Login View Model
    /// </summary>
    public class LoginViewModel
    {
        /// <summary>Email address</summary>
        [JsonPropertyName("email")]
        [Required, EmailAddress, StringLength(255)]
        public string Email { get; set; }
        /// <summary>パスワード</summary>
        [JsonPropertyName("password")]
        [Required(ErrorMessage = "Password is required."), StringLength(128), DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
