using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public sealed class SetAccountStatusRequest
{
    public bool IsActive { get; set; }
    [Required, StringLength(1000)]
    public string Reason { get; set; } = "";
}
