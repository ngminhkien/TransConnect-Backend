using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public sealed class ChangePasswordRequest
{
    [Required, StringLength(PasswordRules.MaximumLength)]
    public string CurrentPassword { get; set; } = "";
    [Required, StringLength(PasswordRules.MaximumLength, MinimumLength = PasswordRules.MinimumLength)]
    public string NewPassword { get; set; } = "";
}
