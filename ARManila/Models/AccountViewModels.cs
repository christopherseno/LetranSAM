using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ARManila.Models
{
    public class ProjectCodeDTO
    {
        [JsonProperty("projectCode")]
        public string ProjectCode { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
    }
    public class QneGlAccountDto
    {
        [JsonProperty("glAccountCode")]
        public string GlAccountCode { get; set; }
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("parentId")]
        public string ParentId { get; set; }
        [JsonProperty("parent")]
        public string Parent { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
        [JsonProperty("drcr")]
        public string DrCr { get; set; }
        [JsonProperty("account")]
        public string Account { get; set; }
        [JsonProperty("specialAccount")]
        public string SpecialAccount { get; set; }
        [JsonProperty("currency")]
        public string Currency { get; set; }
        [JsonProperty("bankAccountNo")]
        public string BankAccountNo { get; set; }
        [JsonProperty("isCreditCard")]
        public string IsCreditCard { get; set; }
        [JsonProperty("isActive")]
        public string IsActive { get; set; }
        [JsonProperty("isRevaluation")]
        public string IsRevaluation { get; set; }
        [JsonProperty("isSubAccount")]
        public string IsSubAccount { get; set; }
    }
    public class BearerToken
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public string ExpiresIn { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty(".issued")]
        public string Issued { get; set; }

        [JsonProperty(".expires")]
        public string Expires { get; set; }
    }
    public class ExternalLoginConfirmationViewModel
    {
        [Required]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }

    public class ExternalLoginListViewModel
    {
        public string ReturnUrl { get; set; }
    }

    public class SendCodeViewModel
    {
        public string SelectedProvider { get; set; }
        public ICollection<System.Web.Mvc.SelectListItem> Providers { get; set; }
        public string ReturnUrl { get; set; }
        public bool RememberMe { get; set; }
    }

    public class VerifyCodeViewModel
    {
        [Required]
        public string Provider { get; set; }

        [Required]
        [Display(Name = "Code")]
        public string Code { get; set; }
        public string ReturnUrl { get; set; }

        [Display(Name = "Remember this browser?")]
        public bool RememberBrowser { get; set; }

        public bool RememberMe { get; set; }
    }

    public class ForgotViewModel
    {
        [Required]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }

    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Username")]        
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        public string Code { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }
}
