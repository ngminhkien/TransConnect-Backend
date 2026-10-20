namespace TransConnect.Modules.Operator.Dto.Responses;

public sealed record OrganizationResponse(Guid Id, string Name, string RegistrationNumber, string OwnerEmail, string ReviewStatus,
    string? ReviewNote, int EmployeeLimit, int? DriverLimit, string? LegalEntityType = null, string? RegisteredAddress = null,
    string? ContactPhone = null, string? ContactEmail = null, bool IsProfileComplete = false, Guid? CreatedByUserId = null);
