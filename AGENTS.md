# TransConnect-Backend — hướng dẫn agent

Áp dụng cho toàn bộ repository Backend. Khảo sát core gần nhất: **2026-10-12**. Giữ file này ngắn gọn, dùng đường dẫn tương đối và cập nhật theo bằng chứng trong code/config.

## Nguồn cần đọc

- Hiện trạng chạy: `TransConnect.sln`, các `*.csproj`, `TransConnect.Host/Program.cs`, `appsettings*.json`, `Properties/launchSettings.json` trong Host và code module liên quan.
- Quy ước backend: [README](README.md), [phụ thuộc](docs/base-libraries-guidelines.md), [thư mục/đặt tên](docs/naming-and-folder-conventions.md), [coding](docs/coding-conventions.md), [API](docs/api-conventions.md), [auth/OTP](docs/auth-and-verification.md), [email](docs/email-and-notification.md).
- Nghiệp vụ nằm ở repo cùng cấp [TransConnect-Docs](../TransConnect-Docs/README.md): đọc [AGENTS.md](../TransConnect-Docs/AGENTS.md), `00-project/{glossary,scope,roadmap,progress-tracker}.md`, rồi `01-requirements/`, `02-business-analysis/`, `04-database/`, `05-api/` theo tác vụ. Không coi cây thư mục dự kiến trong README là các file đã tồn tại.
- Tài liệu CSDL hiện có `status: APPROVED`: [database-conventions](../TransConnect-Docs/04-database/database-conventions.md), [data-dictionary](../TransConnect-Docs/04-database/data-dictionary.md), [phase2-erd](../TransConnect-Docs/04-database/erd/phase2-erd.md). Các đặc tả nghiệp vụ/API còn là `DRAFT`; quyết định API nền tảng đã được chủ dự án chốt tại `../TransConnect-Docs/09-decisions/api-baseline.md` (APPROVED). `initial-idea/`, `reference-docs/`, `LuuAnh/` cung cấp bối cảnh, không mặc nhiên là quyết định đã duyệt.
- Phân biệt **hành vi đã triển khai** với **yêu cầu đã duyệt** và **đề xuất**. Không tự đặt luật nghiệp vụ hoặc chuyển tài liệu sang `APPROVED`. Nếu nguồn mâu thuẫn, nêu file và điểm lệch; hỏi khi cần quyết định nghiệp vụ, hoặc ghi `[TBD - Cần xác nhận từ nhóm]`. Không sửa code chỉ để khớp một bản nháp.

## Stack và cấu trúc thực tế

Modular monolith C# / ASP.NET Core Web API, tất cả project target `net10.0`, nullable và implicit usings bật. SQL Server qua EF Core **10.0.10**, migrations Code-First theo module. JWT Bearer + RBAC tự xây (không dùng ASP.NET Core Identity), BCrypt.Net-Next, Google.Apis.Auth, MailKit, Serilog, Swashbuckle; NuGet pin version trong từng `.csproj`.

| Thư mục | Trách nhiệm |
| --- | --- |
| `TransConnect.Host/` | Composition root, DI, JWT, CORS, pipeline, Swagger, cấu hình và khởi động |
| `TransConnect.Base/` | `BaseEntity`, `IAuditable`, exception nghiệp vụ; không reference project khác |
| `TransConnect.Common/` | `ApiResponse`, xử lý lỗi, current user/permission, contract email; reference Base |
| `TransConnect.Data/` | `AppDbContextBase`, generic repository, audit/soft-delete; reference Base |
| `TransConnect.Modules.Identity/` | Controllers, DTO, services, entities, persistence/configurations, seeder, migrations; schema `identity` |
| `TransConnect.Modules.Notification/` | Email queue, providers, worker, embedded templates, persistence, migrations; schema `notification` |
| `TransConnect.Tests/` | xUnit + HTTP test host, SQLite bộ nhớ/file tạm: contract API, auth/refresh đồng thời, OTP/email queue, startup options; SQL migration script |
| `docs/` | Sáu hướng dẫn kỹ thuật backend ở trên |

Module reference Base/Common/Data; Host reference các module. **Không reference chéo module**, Entity hay DbContext; dùng interface trong Common. Common và Data không reference nhau. Mỗi module sở hữu DbContext/schema/migrations, cùng dùng `ConnectionStrings:Default`.

## Luồng đã triển khai

- **Auth** (`TransConnect.Modules.Identity/Controllers/AuthController.cs`): `POST /auth/` với `register`, `login`, `google`, `refresh`, `logout`, `forgot-password`, `reset-password`, `verify-email`, `resend-verification`. Đăng ký email/mật khẩu → lưu User → gán role `User` nếu có → thử enqueue OTP → phát token. Chưa xác thực email vẫn được đăng nhập. Google từ chối khi `Google:ClientId` rỗng; luôn giữ kiểm tra audience.
- **Hồ sơ hiện tại**: `GET /auth/me` lấy user ID từ JWT, đọc hồ sơ/roles/permissions trong DB; tài khoản thiếu/đã xóa/khóa trả 401. Không trả hash, Google ID hay token; quyền DB trả về không thay đổi claim JWT đang sống.
- **Phiên đăng nhập**: JWT chứa role/permission; cột RefreshTokens.Token lưu `sha256:` + SHA-256 hex, client nhận Base64 token ngẫu nhiên. Rotation claim bằng UPDATE có điều kiện và tạo token kế tiếp trong cùng transaction; user khóa/xóa/thiếu trả 401. Replay → revoke toàn bộ refresh token của user; logout revoke token được gửi lên. Access token vẫn sống tới hết hạn; quyền mới có hiệu lực ở lần phát token tiếp theo. Client phải gộp refresh đồng thời; xem migration hash trong docs/auth-and-verification.md.
- **OTP**: `VerificationCodeService` dùng RNG mật mã, BCrypt hash trong bảng VerificationCodes, phân biệt purpose, hết hạn/giới hạn thử/giới hạn xin mã, vô hiệu mã cũ. Forgot/resend trả cùng kết quả khi email không tồn tại và khi chạm giới hạn xin mã để tránh dò tài khoản. Reset thành công → đổi mật khẩu, xác thực email, revoke refresh token.
- **RBAC**: `/roles` quản lý role và gán/bỏ permission/user; `/permissions` liệt kê/tạo/xóa permission. Bảo vệ bằng `roles.manage` / `permissions.manage` đọc từ claim. Seeder tạo Admin/User và quyền quản trị; `users.manage` được seed nhưng chưa có UsersController.
- **Email**: nghiệp vụ → `Common/Notifications/IEmailQueue` → render template và lưu `notification.EmailMessages` → `EmailDispatchWorker` → File hoặc SMTP. Trạng thái `Pending → Sending → Sent`, lỗi thì retry/backoff hoặc `Failed`; thu hồi bản ghi Sending bị kẹt. Enqueue **sau khi lưu nghiệp vụ**, khác DbContext nên không chung transaction; worker chưa bảo đảm an toàn nhiều instance. `transaction-notice` mới là template, chưa có module ví.

## Nghiệp vụ đang thiết kế trong Docs

- Phase 2 hướng tới Organization/Fleet/Vehicle/Driver và Compliance: hồ sơ nhà xe → quản lý xe/tài xế → upload giấy tờ private → OCR đề xuất → người dùng xác nhận/sửa → thẩm định → tính eligibility. Định hướng storage: Cloudinary cho ảnh công khai; MinIO/R2/S3 private cho giấy tờ, metadata trong SQL Server. **Các module/tích hợp này chưa có trong Backend.** Xem `02-business-analysis/`, `04-database/`, `initial-idea/Driver-Onboarding-And-Document-Upload-Spec.md`, `LuuAnh/` và `reference-docs/` của Docs; ngưỡng OCR/chính sách trong bản nháp cần được chốt trước khi triển khai.
- Luồng vận tải dự kiến: Organization → tuyến nội bộ → Schedule → Trip → publish Supply lên Network → tìm Journey nhiều chặng / Request–Matching–Offer → Booking/cọc → thực hiện chuyến, hủy/lỡ nối chuyến và hoàn tiền qua Ledger. Các phase sau chưa được triển khai; xem `01-requirements/functional-requirements.md`, `initial-idea/TransConnect-Core-Business-Domain-v3.md` và `initial-idea/TransConnect-Cancellation-Missed-Connection-Disruption-Spec-VN-v3.md`.
- Giữ thuật ngữ: Organization ≠ Fleet; Operational Route ≠ Network Route; Trip ≠ Journey; Request ≠ Booking; Driver ≠ User. Network không sở hữu xe/tài xế; gán xe/tài xế có thể hoãn. Không tự thêm luật từ tài liệu ý tưởng khác phiên bản.
- Microservices/Kafka/Kubernetes trong `ENTERPRISE_MICROSERVICES_ARCHITECTURE_PROPOSAL.md` là đề xuất tương lai. Hangfire trong `../techBase.md` không có package/đăng ký trong code hiện tại; email chạy bằng `BackgroundService`.

## Chạy, build và kiểm tra

Thực hiện từ **thư mục TransConnect-Backend**. Cần .NET SDK/runtime 10 (`global.json` chọn feature band mới trong 10), SQL Server truy cập được; lệnh EF cần `dotnet-ef` tương thích EF Core 10. Ngày 11/10 build và test core đã chạy bằng SDK 10.0.302; chưa chạy Host Development, SQL Server migration hoặc SMTP thật.

1. Nếu chưa có, tạo `TransConnect.Host/appsettings.Development.json` từ `appsettings.Development.example.json`; sửa `ConnectionStrings:Default` cho máy đang dùng. Không ghi đè cấu hình local có sẵn. File Development đã được gitignore; không chép secret vào AGENTS/tài liệu/log.
2. `appsettings.json` chứa cấu hình chung `Jwt`, `Google`, `Cors`, `Email`, `Verification`, `Serilog`; Development override từng khóa. Email hiện mặc định `File`, xuất HTML vào `TransConnect.Host/sent-emails/`.

```powershell
dotnet build TransConnect.sln
dotnet run --project TransConnect.Host
# Profile HTTPS có thật trong launchSettings.json:
dotnet run --project TransConnect.Host --launch-profile https
```

Profile `http`: `http://localhost:5267`; profile `https`: thêm `https://localhost:7218`. Cả hai đặt `Development`. Chỉ Development bật `/swagger`, tự migrate **cả hai DbContext**, seed Identity và có `POST /dev/test-email?to=...`. Vì khởi động có ghi DB, dùng database phát triển phù hợp.

Lệnh migrations có trong README (thay `<TenMigration>` bằng tên thực):

```powershell
dotnet ef migrations add <TenMigration> --project TransConnect.Modules.Identity --startup-project TransConnect.Host --context IdentityDbContext
dotnet ef database update --project TransConnect.Modules.Identity --startup-project TransConnect.Host --context IdentityDbContext
```

Với Notification, đổi project thành `TransConnect.Modules.Notification` và context thành `NotificationDbContext`. Luôn chỉ định `--context`; build lại sau khi thêm migration trước khi dùng `--no-build`.

**Test:** `dotnet test TransConnect.Tests/TransConnect.Tests.csproj` (SDK/runtime .NET 10). `scripts/Test-Core.ps1 -DotnetPath <executable>` build solution/test và đặt DOTNET_ROOT cho testhost; `-NoRestore` khi cache đủ. HTTP contract, auth lifecycle, refresh đồng thời (SQLite file + hai DbContext), OTP, email queue, startup options; sinh SQL migration bằng SQL Server provider nhưng không kết nối DB. Test host dùng Data Protection trong bộ nhớ, tắt worker và không gửi email thật; các host không chạy song song do Serilog bootstrap tĩnh. Chưa có CI/Dockerfile/Compose/script deploy.

**Migration mới:** `20261011000000_HashRefreshTokens` chỉ chuyển dữ liệu token Base64 ASCII sang hash, không đổi schema/snapshot. Phải dừng code cũ khi chuyển đổi; Down từ chối vì hash không đảo ngược. Không tự apply vào DB đang dùng khi chỉ chạy test.

## Quy tắc sửa code và điểm lệch cần nhớ

- Controller mỏng → service → DTO `XxxRequest`/`XxxResponse`; lỗi nghiệp vụ dùng `AppExceptionHandler`. Namespace theo thư mục, interface cạnh implementation, async I/O có CancellationToken. DI scoped cho service/repository/DbContext; không tạo `IUnitOfWork` không generic dùng chung nhiều module.
- Entity audit dùng Guid/UTC/`DeletedAt`; `Remove` của repository là soft-delete, `HardRemove` mới xóa vật lý. Cấu hình EF trong `Persistence/Configurations`; gọi `base.OnModelCreating` sau khi đăng ký entities/configurations. Thay model phải xem xét migration; sửa Base/Common/Data thì build toàn solution.
- API dùng `/auth`, `/roles`, `/permissions`, không có tiền tố/version URL hay alias cũ. Envelope `ApiResponse<T>` gồm `succeeded`, `message`, `errorCode`, `data`, `errors`. `AppErrorCodes` ở Base; thông báo tiếng Việt. Validation DTO/model binding, exception, JWT/permission và lỗi HTTP rỗng được xử lý cùng envelope; xem `docs/api-conventions.md`.
- Email template nhúng trong DLL: đủ `.html` + `.txt`, HTML encode dữ liệu người dùng, layout table/CSS inline/tối đa 600px; sửa template cần build lại.
- Docs: hiện trạng ở `../TransConnect-Docs/00-project/backend-status.md`, truy vết ở `08-testing/traceability.md`. Bản nháp `04-database/backend-alignment-v2.1-draft.md` ghi chênh lệch nullable/default/index/refresh/OTP/Notification với các bản APPROVED; không tự sửa bản đã duyệt hoặc migration cũ. Đăng ký/reset đã cùng dùng PasswordRules tối thiểu 6, tối đa 100 ký tự. SĐT, tenant và FK liên module còn cần đặc tả.
- Jwt/Verification/Email được validate khi startup trước migrate/seed Development; Google chưa cấu hình vẫn tắt. Chưa thu hồi access token tức thì hoặc khóa đăng nhập tự động. CORS rỗng mở mọi origin ở Development nhưng không cho origin ở Production; giữ thứ tự exception handler → status code pages → logging/HTTPS → CORS → authentication → authorization.

## Bắt buộc duy trì hướng dẫn

- Trước mỗi tác vụ, đọc file này và kiểm tra lại nguồn liên quan; không coi ngày khảo sát là bảo đảm hiện trạng.
- Khi thay stack/package, cấu trúc/module, cấu hình, API/luồng nghiệp vụ, migration workflow hoặc lệnh chạy/build/test, **tự cập nhật mục tương ứng trong AGENTS.md ngay trong cùng tác vụ**, không đợi người dùng nhắc. Giữ phần còn đúng, bỏ phần lỗi thời, không ghi đè toàn bộ hướng dẫn có sẵn.
- Đồng bộ README/`docs/` liên quan; khi sửa Docs, tuân thủ AGENTS của Docs, giữ ID truy vết FR/UC/BR/DOM/API/TC và quy trình phiên bản DRAFT cho thay đổi đặc tả APPROVED. Kiểm tra file truy vết có tồn tại trước khi dẫn link.
- Chỉ bổ sung lệnh có căn cứ trong config/script/project thật; ghi rõ kiểm tra nào đã chạy, kết quả và phần chưa kiểm chứng. Cập nhật ngày khảo sát khi thực sự đối chiếu lại. Đây là quy tắc cho agent trong các phiên làm việc, **không phải tiến trình tự giám sát repository ngoài phiên**.

## Đồng bộ định hướng tài liệu 12/10/2026

### Sửa core theo yêu cầu chủ dự án (sau lượt tài liệu)

- OTP issue/consume lấy write lock User trong transaction trước khi đọc. Consume claim có điều kiện, tăng AttemptCount trong DB. `ConsumeAndExecuteAsync` commit OTP cùng callback Identity (reset/verify-email); nhập sai commit bộ đếm trước 401. Không mở transaction lồng hoặc gọi email trong callback. Sau lỗi phải bỏ scope/reload entity tracking trước khi dùng lại.
- Refresh cũng khóa User trước token để cùng thứ tự với reset. Quyền/access token vẫn theo chính sách cũ.
- Migration `20261012120436_ActiveRolePermissionUniqueIndexes`: unique tên role/code permission chỉ trên DeletedAt NULL. Bản tạo lại có ID mới, không nhận quyền cũ. Seeder khôi phục liên kết admin seed nếu role Admin được tạo lại, giữ mật khẩu hiện có. Ghi xung đột được xác nhận trong DB trả 409.
- Bổ sung `IdentityRegressionTests`, test SQL index/snapshot và [luồng test tổng thể](docs/backend-test-flow.md). Chưa apply migration vào DB phát triển; SQL Server locking/SMTP/Google thật cần nghiệm thu riêng. Kết quả chạy mới nhất xem `../TransConnect-Docs/08-testing/traceability.md`.

### Bối cảnh lượt tài liệu trước đó

Đọc [quyết định sản phẩm](../TransConnect-Docs/09-decisions/transport-platform-direction-2026-10-12.md) và [tác động dữ liệu v2.2 DRAFT](../TransConnect-Docs/04-database/operator-model-v2.2-draft.md): tài khoản chỉ một nhóm; OPERATOR cố định Organization; Driver cộng tác nhiều Organization; xe một đơn vị quản lý; Trip xe tải theo ngày không bắt buộc Schedule. Thuê nguyên xe có hợp đồng/giữ tiền chỉ là ý tưởng. Code RBAC hiện tại chưa được thay đổi để thực thi phân nhóm này; chưa có các module vận tải. Không sửa code theo chi tiết Q-01–09 chưa chốt. Lượt này chỉ cập nhật tài liệu; không chạy build/test hoặc áp dụng migration.
