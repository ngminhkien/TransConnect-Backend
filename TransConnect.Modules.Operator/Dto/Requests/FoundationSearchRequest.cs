using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed class FoundationSearchRequest
{
    [Range(1, 100000)] public int PageNumber { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(200)] public string? Search { get; set; }
    [RegularExpression("^(Pending|Approved|Rejected)$")] public string? Status { get; set; }
}
