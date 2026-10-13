namespace TransConnect.Modules.Operator.Options;

public sealed class OperatorOptions
{
    public const string SectionName = "Operator";
    // Operational defaults, not subscription pricing. Override per environment/organization.
    public int DefaultEmployeeLimit { get; set; } = 3;
    public int InvitationHours { get; set; } = 48;
    public int? DefaultDriverLimit { get; set; }
}
