# TransConnect Backend

Backend đồ án tốt nghiệp — nền tảng kết nối vận tải (xe khách tuyến cố định + xe tải linh hoạt), xây theo kiến trúc **modular monolith**: nhiều module nghiệp vụ độc lập chạy chung 1 process, nhưng ranh giới rõ ràng để tách microservice sau này nếu cần.

## Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| Runtime / Framework | .NET 10, ASP.NET Core Web API |
| ORM | Entity Framework Core 10 (Code-First + Migrations) |
| Database | SQL Server (mỗi module 1 schema riêng trong cùng 1 database) |
| Authentication | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`) |
| Authorization | RBAC (Role - Permission) tự xây trong module Identity, **không** dùng ASP.NET Core Identity |
| Đăng nhập Google | `Google.Apis.Auth` — chưa cấu hình, endpoint đang tắt (xem `docs/auth-and-verification.md`) |
| Password & OTP hashing | BCrypt.Net-Next |
| Gửi email | MailKit (SMTP), mẫu outbox + background worker |
| Logging | Serilog (`Serilog.AspNetCore`) |
| API docs | Swagger UI (Swashbuckle) — chỉ bật ở `Development` |
| CORS | Named policy, whitelist origin qua section `Cors` |

Version NuGet được **pin cố định** (không dùng wildcard) để mọi máy restore ra cùng một version.

## Kiến trúc

```
TransConnect.Base           <- không phụ thuộc project nào khác
TransConnect.Common         -> Base
TransConnect.Data           -> Base
TransConnect.Modules.Xxx    -> Base, Common, Data
TransConnect.Host           -> Base, Common, Data, tất cả Modules.Xxx
```

Mỗi module tự sở hữu Entities, DbContext, Migration và 1 SQL schema riêng. Module **không** được
reference thẳng Entity/DbContext của module khác — trao đổi qua interface đặt trong `Common`.

## Cấu trúc thư mục

```
TransConnect.Host/                 # composition root (Program.cs, appsettings, DI wiring)
TransConnect.Base/                 # Entity/Exception base, không phụ thuộc gì
TransConnect.Common/               # ApiResponse, exception handler, ICurrentUser, contract dùng chung
TransConnect.Data/                 # Generic repository (soft-delete), AppDbContextBase
TransConnect.Modules.Identity/     # Auth, RBAC, OTP xác thực email / đặt lại mật khẩu
TransConnect.Modules.Notification/ # Gửi email: outbox + worker + template
docs/
```

## Yêu cầu môi trường

- .NET SDK 10
- SQL Server local (đã test với named instance, vd `.\MINHKIEN1`)
- EF Core CLI khớp runtime: `dotnet tool install --global dotnet-ef` (hoặc `dotnet tool update --global dotnet-ef`)

## Setup lần đầu

**1. Tạo `TransConnect.Host/appsettings.Development.json`.**

Copy từ file mẫu có sẵn trong repo rồi sửa cho máy bạn:

```bash
cp TransConnect.Host/appsettings.Development.example.json TransConnect.Host/appsettings.Development.json
```

Thường **chỉ cần sửa đúng một dòng** — connection string trỏ tới SQL Server trên máy bạn:

```json
"ConnectionStrings": {
  "Default": "Server=.\\TEN_INSTANCE;Database=TransConnectDb;User Id=sa;Password=***;TrustServerCertificate=True"
}
```

SQL Server dạng **named instance** trên máy local phải có tiền tố `.\` (vd `.\MINHKIEN1`) —
thiếu tiền tố này SQL Client hiểu nhầm thành hostname mạng và báo lỗi Named Pipes error 40.

### Cấu hình nằm ở đâu

Repo này là **private**, nên cấu hình dùng chung được commit thẳng để cả nhóm chạy được ngay
sau khi clone, không phải hỏi nhau từng giá trị.

| File | Chứa gì | Commit? |
|---|---|---|
| `appsettings.json` | **Mọi cấu hình dùng chung**: `Jwt`, `Email` (kể cả SMTP), `Branding`, `Verification`, `Serilog` | ✅ |
| `appsettings.Development.json` | **Chỉ thứ khác nhau giữa các máy**: connection string, và các override tuỳ chọn | ❌ (đã có trong `.gitignore`) |

ASP.NET Core nạp `appsettings.json` trước, rồi `appsettings.Development.json` **đè lên**. Nghĩa là:

- Khoá nào **không khai** trong `appsettings.Development.json` → tự lấy từ `appsettings.json`.
- Khoá nào **có khai** → giá trị trong `.Development` thắng.

Đây là hành vi mặc định của framework, không phải cấu hình thêm. Vì vậy **đừng chép cả khối
`Email`/`Jwt` sang `.Development`** — chỉ khai đúng khoá bạn cần đổi.

Ví dụ muốn gửi email thật bằng tài khoản Gmail của riêng mình, chỉ cần:

```json
{
  "Email": {
    "Provider": "Smtp",
    "Smtp": { "User": "toi@gmail.com", "Password": "<app password cua toi>" }
  }
}
```

Các khoá còn lại của `Email` (`Worker`, `Branding`, `Host`, `Port`...) vẫn lấy từ `appsettings.json`.

> **Lưu ý khi thay đổi giá trị đã commit**: git giữ lại toàn bộ lịch sử. Đổi `Jwt:Key` hay App
> Password ở commit sau **không xoá được giá trị cũ khỏi history** — ai clone repo vẫn đọc được.
> Nếu về sau repo chuyển sang public, hoặc một thành viên rời nhóm, phải **tạo giá trị mới**
> (đổi App Password trong Google Account, sinh lại `Jwt:Key`) chứ không chỉ sửa file.

**2. Build & chạy:**

```bash
dotnet build TransConnect.sln
dotnet run --project TransConnect.Host
```

Ở `Development`, app tự động migrate database + seed dữ liệu mặc định khi start.

**3. Mở Swagger UI** tại `https://localhost:<port>/swagger`.

## API và kiểm thử

Endpoint dùng trực tiếp `/auth`, `/roles`, `/permissions`, không có tiền tố `/api` hay `/api/v1` và không giữ alias cũ. `GET /auth/me` trả hồ sơ/quyền hiện tại từ DB; `POST /auth/refresh` giữ tên hiện có. Response bổ sung `errors`, thông báo lỗi bằng tiếng Việt; xem [quy chuẩn API](docs/api-conventions.md).

```powershell
dotnet test TransConnect.Tests/TransConnect.Tests.csproj
```

Test project dùng xUnit, HTTP test host và SQLite trong bộ nhớ, không cần SQL Server/SMTP. Cần SDK và runtime .NET 10; nếu máy có nhiều bản .NET, kiểm tra `dotnet --list-sdks` và dùng đúng executable/PATH .NET 10.

## Tài khoản Admin mặc định

- Email: `admin@hotmail.com`
- Password: `123456`

**Đổi ngay sau lần chạy đầu tiên** (xem `TransConnect.Modules.Identity/Persistence/IdentitySeeder.cs`).

## Tài liệu

Đọc trước khi thêm code — đây là quy tắc bắt buộc trong repo này, không phải tham khảo thêm:

| Tài liệu | Nội dung |
|---|---|
| [docs/base-libraries-guidelines.md](docs/base-libraries-guidelines.md) | Hướng phụ thuộc bắt buộc; cái gì thuộc Base/Common/Data |
| [docs/naming-and-folder-conventions.md](docs/naming-and-folder-conventions.md) | Đặt tên, cấu trúc 1 module, khi nào nên/không nên tách module mới |
| [docs/coding-conventions.md](docs/coding-conventions.md) | Controller/Service/Repository/Exception/DTO, DI, async |
| [docs/api-conventions.md](docs/api-conventions.md) | Định dạng response, mã lỗi, CORS, tích hợp client |
| [docs/auth-and-verification.md](docs/auth-and-verification.md) | Đăng nhập, xoay vòng refresh token, OTP, các ràng buộc bảo mật |
| [docs/email-and-notification.md](docs/email-and-notification.md) | Kiến trúc gửi email, template, cách thêm loại email mới |

## Thêm module nghiệp vụ mới

1. Cân nhắc domain có đủ lớn để tách module riêng không (tiêu chí trong `naming-and-folder-conventions.md`) — nghi ngờ thì gộp vào module gần nhất.
2. Tạo project `TransConnect.Modules.<TenModule>`, reference `Base`, `Common`, `Data`; tổ chức thư mục theo đúng khuôn.
3. `<TenModule>DbContext` kế thừa `AppDbContextBase`, đặt schema riêng qua `HasDefaultSchema(...)`.
4. Viết `<TenModule>ModuleExtensions.cs` (`AddXxxModule`), gọi từ `Program.cs`, thêm migrate vào khối `IsDevelopment`.
5. Tạo migration đầu tiên, thêm project vào `TransConnect.sln`.
6. Cần dữ liệu từ module khác → định nghĩa interface trong `Common`, **không** reference thẳng module kia.
7. Cập nhật cây thư mục trong README này.

## Migration

```bash
dotnet ef migrations add <TenMigration> --project TransConnect.Modules.Identity --startup-project TransConnect.Host --context IdentityDbContext
dotnet ef database update --project TransConnect.Modules.Identity --startup-project TransConnect.Host --context IdentityDbContext
```

Đổi project/`--context` theo module tương ứng. Từ khi có nhiều hơn 1 module, **`--context` là bắt
buộc** — thiếu nó EF không biết chọn DbContext nào.

> Sau khi `migrations add`, phải `dotnet build` lại trước khi chạy `dotnet run --no-build`,
> nếu không app sẽ báo `PendingModelChangesWarning` vì binary chưa chứa migration mới.
# TransConnect-Backend
