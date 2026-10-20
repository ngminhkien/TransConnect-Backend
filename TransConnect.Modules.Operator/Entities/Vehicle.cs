using TransConnect.Base.Entities;

namespace TransConnect.Modules.Operator.Entities;

// DOM-VEH-001: vehicle identity survives changes of operating organization.
public class Vehicle : BaseEntity
{
    public string RegistrationPlate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? VehicleKind { get; set; }
    public int? SeatCount { get; set; }
    public decimal? PermittedPayloadKg { get; set; }
}
