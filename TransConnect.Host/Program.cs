using TransConnect.Base.Exceptions;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using TransConnect.Common.Auth;
using TransConnect.Common.ExceptionHandling;
using TransConnect.Common.Notifications;
using TransConnect.Common.Options;
using TransConnect.Common.Response;
using TransConnect.Host.Options;
using TransConnect.Modules.Identity;
using TransConnect.Modules.Identity.Controllers;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using TransConnect.Modules.Operator;
using TransConnect.Modules.Operator.Persistence;
using TransConnect.Modules.Notification;
using TransConnect.Modules.Notification.Persistence;
using TransConnect.Modules.Compliance;
using TransConnect.Modules.Compliance.Persistence;

const string CorsPolicyName = "TransConnectClients";

Console.OutputEncoding = Encoding.UTF8;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Đang khởi động TransConnect.Host...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Modules — mỗi module tự đăng ký DbContext/Services của mình qua 1 extension method.
    builder.Services.AddIdentityModule(builder.Configuration);
    builder.Services.AddNotificationModule(builder.Configuration);
    builder.Services.AddOperatorModule(builder.Configuration);
    builder.Services.AddComplianceModule(builder.Configuration, builder.Environment);

    // Cross-cutting (TransConnect.Common)
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddScoped<TransConnect.Common.Audit.IAuditContext, TransConnect.Common.Audit.HttpAuditContext>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddOptions<TransConnect.Common.Maintenance.MaintenanceOptions>()
        .Bind(builder.Configuration.GetSection("Maintenance"))
        .Validate(o => o.PollSeconds > 0 && o.BatchSize > 0 && o.AuditRetentionMonths > 0,
            "Chu kỳ, kích thước lô và thời hạn lưu audit phải lớn hơn 0.")
        .ValidateOnStart();
    builder.Services.AddHostedService<TransConnect.Common.Maintenance.MaintenanceWorker>();
    builder.Services.AddExceptionHandler<AppExceptionHandler>();
    builder.Services.AddProblemDetails();

    // CORS — client dự kiến: web React và Flutter Web. App Flutter native không bị CORS
    // chi phối nên không cần khai báo gì thêm ở đây.
    var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
        ?? new CorsOptions();

    builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
    {
        if (corsOptions.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                // Cần cho trường hợp về sau chuyển refresh token sang HttpOnly cookie.
                // AllowCredentials chỉ hợp lệ khi origin được liệt kê tường minh (không dùng "*").
                .AllowCredentials();
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Dev chưa cấu hình origin: mở hết để khỏi chặn lúc frontend đổi port liên tục.
            // Production mà không cấu hình thì policy rỗng => chặn hết, đúng hướng fail-safe.
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    }));

    // Controllers nằm trong từng module assembly, Host chỉ discover qua AddApplicationPart.
    var mvc = builder.Services.AddControllers()
        .AddVietnameseApiValidation()
        .AddApplicationPart(typeof(AuthController).Assembly)
        .AddApplicationPart(typeof(TransConnect.Modules.Operator.Controllers.OrganizationsController).Assembly);

    // Remove automatic discovery too: demo routes must not exist when disabled.
    var complianceAssembly = typeof(TransConnect.Modules.Compliance.Controllers.DemoDocumentsController).Assembly;
    var complianceDemoEnabled = builder.Configuration.GetValue<bool>("ComplianceDemo:Enabled");
    mvc.ConfigureApplicationPartManager(manager =>
    {
        foreach (var part in manager.ApplicationParts.Where(p => p.Name == complianceAssembly.GetName().Name).ToArray())
            manager.ApplicationParts.Remove(part);
        if (complianceDemoEnabled && builder.Environment.IsDevelopment())
            manager.ApplicationParts.Add(new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(complianceAssembly));
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "TransConnect API", Version = "v1" });

        var jwtScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Nhập: Bearer {access_token}"
        };
        options.AddSecurityDefinition("Bearer", jwtScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { jwtScheme, [] }
        });
    });

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("Thiếu cấu hình 'Jwt' trong appsettings.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            // Mặc định JwtBearer trả 401/403 với body RỖNG, trong khi mọi response khác của
            // API đều là ApiResponse. Ghi đè để client chỉ phải parse 1 định dạng duy nhất.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    if (context.Principal is null || !await context.HttpContext.RequestServices
                        .GetRequiredService<LiveAccountClaims>()
                        .RefreshAsync(context.Principal, context.HttpContext.RequestAborted))
                        context.Fail("Tài khoản không còn khả dụng.");
                },
                OnChallenge = context =>
                {
                    // Chặn handler mặc định, nếu không nó sẽ ghi đè response mình vừa viết.
                    context.HandleResponse();

                    return ApiResponseWriter.WriteFailureAsync(
                        context.HttpContext,
                        StatusCodes.Status401Unauthorized,
                        AppErrorCodes.Unauthorized,
                        "Bạn cần đăng nhập để dùng chức năng này.");
                },
                OnForbidden = context => ApiResponseWriter.WriteFailureAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    AppErrorCodes.Forbidden,
                    "Bạn không có quyền thực hiện thao tác này.")
            };
        });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    // Fail before Development migrations/seeding, not at the first auth/email request.
    app.Services.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate();

    // UseExceptionHandler phải đứng ĐẦU pipeline: middleware đăng ký trước nó (request
    // logging, https redirect...) mà ném exception thì sẽ không được handler bắt.
    app.UseExceptionHandler();
    app.UseApiStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        // Mỗi module có DbContext + migration riêng nên phải migrate từng cái một.
        using var scope = app.Services.CreateScope();

        var identityDbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await identityDbContext.Database.MigrateAsync();
        await IdentitySeeder.SeedAsync(identityDbContext, passwordHasher);

        var notificationDbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await notificationDbContext.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<OperatorDbContext>().Database.MigrateAsync();
        if (complianceDemoEnabled)
            await scope.ServiceProvider.GetRequiredService<ComplianceDbContext>().Database.MigrateAsync();

        // Chỉ có ở Development: xếp hàng 1 email mẫu để kiểm tra template và cấu hình SMTP
        // mà không phải chạy qua luồng đăng ký. Hữu ích nhất khi vừa điền App Password mới.
        app.MapPost("/dev/test-email", async (
            string to,
            IEmailQueue emailQueue,
            CancellationToken cancellationToken) =>
        {
            var messageId = await emailQueue.EnqueueAsync(new EmailRequest
            {
                ToEmail = to,
                ToName = "Người dùng thử",
                TemplateCode = EmailTemplateCodes.EmailVerification,
                Placeholders = new Dictionary<string, string>
                {
                    ["FullName"] = "Người dùng thử",
                    ["OtpCode"] = "123456",
                    ["ExpiresMinutes"] = "10"
                }
            }, cancellationToken);

            return Results.Ok(ApiResponse<object>.Success(
                new { messageId },
                "Đã xếp hàng. Worker sẽ gửi trong khoảng 15 giây."));
        });
    }

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    // UseCors phải nằm trước UseAuthentication/UseAuthorization, nếu không request bị chặn
    // bởi auth sẽ trả về thiếu header CORS và trình duyệt báo lỗi CORS thay vì lỗi 401 thật.
    app.UseCors(CorsPolicyName);

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException do tooling EF Core (dotnet ef migrations/database update) chủ động
    // ném ra để dừng host sau khi lấy xong DI container — không phải lỗi thật, không log Fatal.
    Log.Fatal(ex, "TransConnect.Host dừng đột ngột lúc khởi động.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Cho phép kiểm thử HTTP bằng WebApplicationFactory mà không chạy database Development.
public partial class Program { }
