namespace TransConnect.Common.Options;

// Nằm ở Common (không phải trong module Identity) vì cả TokenService (phát JWT)
// lẫn Program.cs của Host (validate JWT) đều cần cùng 1 kiểu cấu hình này.
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}
