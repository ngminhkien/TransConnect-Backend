using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public sealed class UpdateAccountProfileRequest
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = "";
    [StringLength(20), Phone]
    public string? PhoneNumber { get; set; }
}
