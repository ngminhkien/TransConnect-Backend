namespace TransConnect.Common.Auth;

// Claim type dùng chung giữa nơi phát JWT (TransConnect.Modules.Identity.Services.TokenService)
// và nơi đọc claim (CurrentUser) — vai trò/email dùng ClaimTypes chuẩn của .NET,
// permission là claim type riêng vì .NET không có sẵn.
public static class AppClaimTypes
{
    public const string Permission = "permission";
}
