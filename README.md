# TransConnect Backend

**Test từ đầu:** [luồng thủ công đầy đủ + mọi DTO mẫu](docs/full-manual-test-flow.md), [Postman collection](docs/http/TransConnect-full-flow.postman_collection.json), [REST Client](docs/http/full-flow.http). Gồm 125 request theo 8 nhóm Identity → Organization → Member → Driver → Vehicle → Compliance demo → RBAC → phiên/reset. Gửi từng request, nhập OTP/token lời mời tại điểm dừng.

**15/10 — demo hồ sơ nhẹ RAM:** đã tích hợp module Compliance vào .NET, lưu file S3 thật nhưng scan/OCR mô phỏng. Mặc định tắt, chỉ Development; dùng SQL Server/JWT hiện có, không cần Python/Redis/ClamAV. Xem [hướng dẫn từ bước license AIStor đến Swagger](docs/compliance-demo-setup.md). Migration mới chưa apply DB thật; không coi đây là xác minh giấy tờ production.

Luồng nghiệm thu backend hiện có: [giai đoạn 1 — nền tảng](docs/phase-1-foundation-test-flow.md) (tài khoản theo nhóm, tổ chức/nhân viên/tài xế/xe, quota/audit, OTP/phiên/RBAC, email và SQL Server).

Ngày 13/10 thêm module Operator với DbContext/schema/migrations riêng; Host Development migrate Identity, Notification và Operator. Customer/Operator là hai nhóm công khai; Driver dùng Operator; Admin login với accountGroup InternalAdmin. Xem [quyết định và giới hạn triển khai](../TransConnect-Docs/09-decisions/phase-1-foundation-2026-10-13.md). Migration mới chưa được apply vào DB phát triển.

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

Cấu hình connection string trỏ tới SQL Server trên máy bạn và một `Jwt:Key` riêng (ít nhất 32 ký tự). Không dùng lại khóa đã xuất hiện trong Git:

```json
"ConnectionStrings": {
  "Default": "Server=.\\TEN_INSTANCE;Database=TransConnectDb;User Id=sa;Password=***;TrustServerCertificate=True"
}
```

SQL Server dạng **named instance** trên máy local phải có tiền tố `.\` (vd `.\MINHKIEN1`) —
thiếu tiền tố này SQL Client hiểu nhầm thành hostname mạng và báo lỗi Named Pipes error 40.

### Cấu hình nằm ở đâu

Chỉ cấu hình không chứa bí mật được commit. Quy tắc này áp dụng cả repo private.

| File | Chứa gì | Commit? |
|---|---|---|
| `appsettings.json` | Cấu hình chung không chứa bí mật; khóa JWT, tài khoản/mật khẩu SMTP và connection string để trống | ✅ |
| `appsettings.Development.json` | Connection string, khóa JWT riêng và cấu hình SMTP local | ❌ |
| `appsettings.*.example.json` | Mẫu không chứa bí mật thật | ✅ |
| `appsettings.<Environment>.json`, `.env*` | Cấu hình riêng môi trường (trừ file mẫu) | ❌ |

Có thể dùng `dotnet user-secrets` trong Development hoặc biến môi trường khi triển khai. Không commit file bí mật để sửa lỗi thiếu cấu hình. `Jwt:Key` rỗng sẽ bị startup validation từ chối.

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
> Khóa JWT và App Password đã commit phải được thay/thu hồi ở môi trường sử dụng,
> không đợi repo public. Xóa ở bản hiện tại không xóa commit cũ; làm sạch lịch sử cần
> phối hợp các clone/nhánh và cập nhật remote. Không gửi bí mật qua chat hoặc PR.

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

Test project dùng xUnit, HTTP test host và SQLite (bộ nhớ; file tạm cho test đồng thời), không cần SQL Server/SMTP. Bao phủ API contract, auth/refresh/OTP, email queue, startup options và sinh SQL migration. `global.json` yêu cầu SDK 10, cho phép dùng feature band mới hơn trong .NET 10.

Nếu có nhiều bản dotnet, dùng script để chọn đúng cả SDK và runtime của testhost:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Test-Core.ps1 -DotnetPath "$env:USERPROFILE/.dotnet/dotnet.exe"
# Thêm -NoRestore nếu dependencies đã được restore trên máy.
```

Đường dẫn trên dành cho SDK cài trong user profile; thay bằng executable .NET 10 thực tế của máy. Script build toàn solution, chạy test và khôi phục DOTNET_ROOT sau khi hoàn tất. Ngày 11/10/2026 đã build/test bằng SDK 10.0.302; không chạy SQL Server hoặc SMTP thật.

Refresh token hiện lưu hash và rotation có transaction. Trước khi chạy bản này trên database cũ, đọc [migration hash token](docs/auth-and-verification.md); migration dữ liệu không hỗ trợ đảo hash về plaintext. Cấu hình Jwt/Verification/Email được validate trước migrate/seed Development.

## CI trên GitHub Actions

Workflow [Backend CI](.github/workflows/ci.yml) chạy khi push vào `main`, mở/cập nhật Pull Request vào `main`, hoặc chạy thủ công trong tab **Actions**. Runner Ubuntu cài SDK .NET 10, restore dependencies, build solution ở Release và chạy `TransConnect.Tests`; kết quả TRX được lưu trong artifact `backend-test-results` trong 7 ngày.

CI không cần secret, SQL Server hoặc SMTP thật; không chạy Host Development, apply migration hay deploy. Sau khi push workflow lên GitHub, kiểm tra job **Build and test** trong tab Actions. Có thể yêu cầu check này pass trước khi merge bằng branch protection/ruleset của `main`.

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
