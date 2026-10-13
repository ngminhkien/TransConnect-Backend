using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record SuspendVehicleRequest
{
    public bool IsSuspended { get; init; }
}
