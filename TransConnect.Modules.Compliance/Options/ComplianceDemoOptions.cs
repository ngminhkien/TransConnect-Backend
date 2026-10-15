namespace TransConnect.Modules.Compliance.Options;

public sealed class ComplianceDemoOptions
{
    public const string SectionName = "ComplianceDemo";
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string Region { get; set; } = "us-east-1";
    public string Bucket { get; set; } = "transconnect-demo-unscanned";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool ForcePathStyle { get; set; } = true;
}
