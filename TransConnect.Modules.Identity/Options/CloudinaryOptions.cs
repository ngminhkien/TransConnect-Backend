namespace TransConnect.Modules.Identity.Options;

public sealed class CloudinaryOptions
{
    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string RootFolder { get; set; } = "transconnect/development";
    public bool UseDynamicFolders { get; set; } = true;
}
