# Luồng kiểm thử tổng thể Backend — 12/10/2026

Phạm vi đã có: API contract, Identity (đăng ký/đăng nhập/Google/OTP/phiên đăng nhập), RBAC, hàng đợi email và worker File/SMTP, cấu hình startup và migrations. Organization, xe/tài xế, tenant, OCR, Booking và thanh toán chưa có code, chưa nằm trong luồng nghiệm thu này.

## 1. Chạy bộ kiểm thử tự động trước

Từ thư mục `TransConnect-Backend`, dùng .NET SDK/runtime 10:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Test-Core.ps1 -DotnetPath C:/Users/PC/.dotnet/dotnet.exe -NoRestore
```

Đường dẫn trên là SDK 10 của máy hiện tại. Máy khác dùng `-DotnetPath dotnet` khi `dotnet --list-sdks` có SDK 10. Bỏ `-NoRestore` nếu chưa có cache NuGet. Script build toàn solution rồi chạy toàn bộ test.

Chạy riêng bộ hồi quy trong terminal đã đặt `DOTNET_ROOT`/`DOTNET_ROOT_X64` trỏ đúng runtime 10:

```powershell
dotnet test TransConnect.Tests/TransConnect.Tests.csproj --no-restore --filter FullyQualifiedName~IdentityRegressionTests
```

| Nhóm | Đã kiểm tra tự động |
| --- | --- |
| API | Validation tiếng Việt, JSON hỏng/body rỗng, 401/403/404/405/415/500 và envelope |
| Hồ sơ | `/auth/me` đọc DB, ẩn secret, từ chối tài khoản mất/khóa/xóa |
| Phiên | Login sai/đúng, logout, refresh hash/rotation/replay, hai refresh đồng thời |
| OTP | Purpose, hash, hạn dùng, vô hiệu mã cũ, số lần thử/xin; hai request đồng thời dùng mã/nhập sai/xin mã |
| Reset | Thu hồi refresh token; lỗi thu hồi phiên rollback cả mật khẩu và OTP; nhập sai vẫn tăng bộ đếm |
| Mật khẩu | Đăng ký/reset từ chối 5 ký tự, chấp nhận 6; đăng nhập được bằng mật khẩu mới |
| RBAC | Xóa mềm rồi tạo lại với ID mới; không nhận lại liên kết cũ; tạo/đổi tên trùng đồng thời trả 409; seeder chạy lại |
| Email | Đăng ký → render template → lưu queue, encode tên trong HTML; không chạy worker/SMTP |
| Cấu hình/CSDL | Options sai bị từ chối; sinh SQL migrations và model khớp snapshot |

Test dùng SQLite tạm, không sửa database phát triển. Test sinh SQL không có nghĩa migration hay locking đã được nghiệm thu trên SQL Server.

## 2. Chuẩn bị kiểm thử thủ công trên SQL Server

1. Dùng database phát triển riêng. Nếu chưa có `TransConnect.Host/appsettings.Development.json`, tạo từ `.example.json`; không ghi đè file đang dùng.
2. Cấu hình `ConnectionStrings:Default` cho database test và `Email:Provider = File`. Nếu có biến môi trường override, kiểm tra cả biến đó. Không đưa credential/token thật vào báo cáo test.
3. Chạy từ Backend:

   ```powershell
   dotnet run --project TransConnect.Host --launch-profile https
   ```

   Máy hiện tại nếu `dotnet` mặc định còn là SDK 8/9, thay bằng `C:/Users/PC/.dotnet/dotnet.exe`.

4. Development tự migrate **Identity và Notification**, seed role/permission/admin. Mở `https://localhost:7218/swagger`; HTTP profile là `http://localhost:5267`. Chứng chỉ development phải được máy/client tin cậy.
5. Migration mới `20261012120436_ActiveRolePermissionUniqueIndexes` phải có trong lịch sử migration; hai index RBAC có filter `[DeletedAt] IS NULL`. Lệnh áp dụng riêng khi cần:

   ```powershell
   dotnet ef database update --project TransConnect.Modules.Identity --startup-project TransConnect.Host --context IdentityDbContext
   ```

   Lệnh này ghi database. Trong phiên sửa code chỉ sinh migration và test script, chưa chạy lệnh cập nhật database.

6. Chuẩn bị tài khoản A (người dùng mới), tài khoản B (test giới hạn OTP), và admin của database test. Thông tin tài khoản seed nằm trong `IdentitySeeder`; dùng credential của môi trường đang test. Tạo email/tên role/permission riêng cho mỗi lượt để không vướng hạn mức và dữ liệu cũ.

## 3. Quy ước khi gọi API

- Base URL: `https://localhost:7218`, không thêm `/api` hoặc `/api/v1`.
- POST/PUT body JSON, `Content-Type: application/json`.
- Endpoint bảo vệ: `Authorization: Bearer <accessToken>`. Trong Swagger chọn Authorize và nhập `Bearer ...`.
- Thành công hiện dùng HTTP **200**, `succeeded: true`; lỗi có `succeeded: false`, `errorCode`, `message`; lỗi field có `errors` tiếng Việt.
- Ghi lại `userId`, access token và refresh token từ `data` trong công cụ test cục bộ. Mỗi lần refresh thành công phải thay cả cặp token.

## 4. Luồng chính: đăng ký → xác thực → đăng nhập → hồ sơ

| Bước | Request / thao tác | Kết quả mong đợi |
| --- | --- | --- |
| M-01 | `POST /auth/register` với body bên dưới | 200, có token và userId; `emailVerified=false`; role User nếu seed đã chạy |
| M-02 | Gửi lại cùng email | 409 CONFLICT |
| M-03 | `POST /auth/login`, email A và mật khẩu đã đăng ký, trước xác thực | 200; hệ thống hiện cho đăng nhập khi chưa xác thực |
| M-04 | `GET /auth/me` bằng token A | Hồ sơ/roles/permissions từ DB; không có passwordHash, GoogleId, refresh/access token |
| M-05 | Đợi worker, mở HTML mới của A tại `TransConnect.Host/sent-emails/` (hoặc FileDropPath đã cấu hình) | Đúng người nhận, mã OTP; queue chuyển Pending → Sent; không gửi email thật |
| M-06 | `POST /auth/verify-email` với email A và OTP trong file | 200; gọi `/auth/me` hoặc login lại thấy `emailVerified=true` |
| M-07 | Login với mật khẩu sai | 401 UNAUTHORIZED, không phát token |

```json
{
  "email": "qa-a-20261012@example.com",
  "password": "123456",
  "fullName": "QA A",
  "phoneNumber": "0900000001"
}
```

Body verify-email:

```json
{ "email": "qa-a-20261012@example.com", "code": "<OTP mới nhất>" }
```

Sau khi đã xác thực, gọi verify-email lần nữa trả 200 theo hành vi idempotent; dùng tài khoản chưa xác thực để kiểm tra OTP sai/hết hạn. `/auth/me` phản ánh DB hiện tại nhưng không tự thay claim trong JWT cũ.

## 5. OTP và gửi lại: dùng tài khoản B chưa xác thực

| Bước | Thao tác | Kết quả mong đợi |
| --- | --- | --- |
| M-08 | Đăng ký B, lưu OTP đầu; `POST /auth/resend-verification` với `{ "email": "<B>" }` | 200; có email/mã mới; OTP đầu trả 401, mã mới xác thực được |
| M-09 | Với tài khoản test khác, nhập sai `MaxAttempts` lần rồi nhập mã đúng cũ | Đều 401; mã đã vô hiệu, bộ đếm không bị mất |
| M-10 | Xin mã quá `MaxRequestsPerHour` | Resend/forgot vẫn 200 nhưng không thêm OTP/email vượt hạn mức; đăng ký đã tính một lượt EmailVerification |
| M-11 | Resend/forgot cho email không tồn tại | Cùng response thành công; không có email mới |
| M-12 | Chờ quá ExpiryMinutes rồi dùng mã | 401; không xác thực/đổi mật khẩu |
| M-13 | Dùng OTP xác thực email cho reset, hoặc ngược lại | 401; hai purpose độc lập |

Mặc định CodeLength=6, ExpiryMinutes=10, MaxAttempts=5, MaxRequestsPerHour=3; lấy giá trị cấu hình thực tế làm chuẩn. Để test hết hạn nhanh có thể đặt ExpiryMinutes=1 trong môi trường test và khởi động lại, sau đó khôi phục cấu hình.

## 6. Refresh → replay → logout

1. Login A lấy `R1`; gửi `POST /auth/refresh` body `{ "refreshToken": "<R1>" }` → 200, nhận `R2` khác R1.
2. Dùng R1 lại → 401 và thu hồi toàn bộ refresh token của A. Dùng R2 tiếp → 401. Đây là chính sách chống replay hiện tại.
3. Login lại lấy phiên mới. Gọi `POST /auth/logout` với access token và body refresh token của phiên → 200; refresh bằng token đó → 401.
4. Logout thiếu access token → 401. Access token đã phát vẫn sống tới hết hạn; logout/reset không phải blacklist JWT tức thì.
5. Kiểm tra DB: `RefreshTokens.Token` bắt đầu bằng `sha256:`, không bằng token client đang giữ; token đã dùng có IsUsed, token đã thu hồi có IsRevoked.

Không chạy bước replay giữa các bước test cần giữ phiên A; login lại sau mỗi kịch bản phá phiên.

## 7. Quên mật khẩu → reset → kiểm tra phiên cũ

1. Login A hai lần để có hai refresh token đang hoạt động.
2. `POST /auth/forgot-password` với `{ "email": "<A>" }` → 200; đọc mã **password-reset** mới nhất từ email File.
3. `POST /auth/reset-password` với body bên dưới nhưng NewPassword có 5 ký tự → 400, field `newPassword`; OTP chưa bị dùng.
4. Gửi lại cùng OTP, NewPassword có 6 ký tự → 200. Login bằng mật khẩu cũ → 401; mật khẩu mới → 200, emailVerified=true.
5. Thử refresh cả hai token trước reset → 401. Dùng lại OTP để reset lần nữa → 401.
6. Nhập OTP sai phải vẫn tăng AttemptCount trong DB. Luồng lỗi giữa các lần ghi được giả lập tự động trong `TC_IAM_008`; không ngắt database đang sử dụng chỉ để mô phỏng lỗi.

```json
{ "email": "qa-a-20261012@example.com", "code": "<OTP reset>", "newPassword": "654321" }
```

## 8. RBAC: quản trị → cấp quyền → thu hồi → xóa/tạo lại

| Bước | Thao tác | Kết quả mong đợi |
| --- | --- | --- |
| M-14 | A gọi `GET /roles` / `GET /permissions` | 403; không token thì 401 |
| M-15 | Login admin, `GET /roles`, `GET /permissions` | 200, danh sách hiện có |
| M-16 | Admin `POST /roles`, `{ "name": "QAReader", "description": "QA" }`; `POST /permissions`, `{ "code": "qa.read", "description": "QA" }` | 200, lưu roleId/permissionId; lặp tên/code đang hoạt động → 409 |
| M-17 | `GET /roles/{roleId}`; `PUT /roles/{roleId}` với name/description mới | Đọc/cập nhật thành công; đổi sang tên role đang hoạt động khác → 409 |
| M-18 | `POST /roles/{roleId}/permissions/{permissionId}`; `POST /roles/{roleId}/users/{userIdA}` | 200; `/auth/me` của A thấy role/quyền mới |
| M-19 | Gán permission có sẵn `roles.manage` vào QAReader rồi login lại A | A gọi `/roles` được 200; `qa.read` tự tạo chưa có endpoint nghiệp vụ để kiểm tra quyền này |
| M-20 | Admin `DELETE /roles/{roleId}/permissions/{permissionIdRolesManage}` | A login/refresh lấy JWT mới rồi gọi `/roles` → 403; JWT cũ vẫn giữ claim đến hết hạn |
| M-21 | `DELETE /roles/{roleId}/users/{userIdA}` | `/auth/me` không còn role; token mới không còn quyền của role |
| M-22 | Admin xóa permission QA và role QA qua `DELETE /permissions/{id}`, `DELETE /roles/{id}` rồi tạo lại cùng code/name | 200, ID mới; liên kết cũ không tự chuyển sang ID mới; DB giữ DeletedAt của bản cũ |
| M-23 | GET role có GUID không tồn tại hoặc đã xóa | 404 |

Chỉ xóa role/permission QA khi test thủ công. Khôi phục seeder sau khi xóa mặc định đã có test tự động trên DB tạm. Không dùng role Admin thật làm dữ liệu thử.

## 9. Request đồng thời trên SQL Server

Dùng hai client/request song song với cùng dữ liệu, database test riêng; lặp lại vài lần bằng dữ liệu mới:

| Kịch bản | Kỳ vọng |
| --- | --- |
| Hai reset cùng OTP hợp lệ | Một 200, một 401; mật khẩu khớp request thắng; không còn refresh token cũ |
| Hai lần nhập sai đồng thời, còn hai lượt | AttemptCount tăng đủ hai; chạm ngưỡng thì mã bị vô hiệu |
| Hai forgot/resend cùng purpose khi còn một lượt | Cả hai HTTP 200 để chống dò email; DB chỉ thêm một mã/email, chỉ một mã còn hiệu lực |
| Hai refresh cùng token | Một 200, một 401; replay thu hồi cả token kế tiếp, cần login lại |
| Hai tạo cùng role/permission hoặc hai đổi role thành cùng tên | Một 200, một 409; chỉ một bản ghi đang hoạt động giữ tên/code |
| Reset và refresh cùng user | Không deadlock do thứ tự khóa User → token; sau reset commit, refresh token cấp trước commit đó đã bị thu hồi |

Đếm trong DB bằng truy vấn chỉ đọc, không dựa riêng vào số response 200 của forgot/resend. Test SQLite đồng thời đã tự động hóa nhưng không thay thế phần này.

## 10. Email worker, cấu hình và Google

- **File worker:** `POST /dev/test-email?to=qa@example.com` ở Development → 200 với messageId; sau chu kỳ PollSeconds có HTML và trạng thái Sent. Endpoint không tồn tại ngoài Development. Mã `123456` trong email mẫu không phải OTP được lưu cho tài khoản.
- **Retry/Failed:** trên instance/database test riêng, cấu hình SMTP thử nghiệm hợp lệ về hình thức nhưng không kết nối được; enqueue email → AttemptCount tăng, NextAttemptAt theo backoff; đủ MaxAttempts → Failed. Khôi phục provider File sau test. Đây là kiểm thử thủ công, chưa nằm trong suite tự động.
- **SMTP thật:** chỉ dùng hộp thư test và credential của môi trường; kiểm tra thư đến/spam, người gửi, nội dung. Không coi File provider là bằng chứng SMTP hoạt động.
- **Startup:** cấu hình Jwt key quá ngắn, Verification.CodeLength=10, hoặc thông số Email không hợp lệ phải làm startup thất bại trước migrate/seed; khôi phục từng cấu hình sau test. Google ClientId rỗng không cản startup.
- **Google:** khi ClientId rỗng, `POST /auth/google` với `{ "idToken": "invalid" }` → 401. Token sai khi có ClientId cũng bị từ chối. Google đăng nhập thật cần ID token của client đúng audience; kiểm thử riêng khi đã tích hợp frontend, chưa được chứng minh bằng suite hiện tại.
- **Khóa/xóa tài khoản:** chưa có UsersController; chỉ thay IsActive/DeletedAt bằng fixture hoặc công cụ DB trên user test. `/auth/me` và refresh phải 401; khôi phục dữ liệu sau thử nghiệm.
- **CORS:** Development rỗng cho origin bất kỳ; khi khai whitelist chỉ origin đó được header CORS. Production rỗng không cho origin. Dùng trình duyệt/OPTIONS để kiểm tra; app native không chịu cơ chế CORS.
- **API lỗi:** email sai/mật khẩu ngắn → 400; `/api/auth/me` → 404; `GET /auth/login` → 405; POST login với `text/plain` → 415. Response lỗi phải đúng envelope và không lộ stack trace.

## 11. Ghi nhận kết quả

Lưu ngày, phiên bản code, database/môi trường, cấu hình hạn mức, bước test, HTTP thực tế, trạng thái DB và kết quả pass/fail. Không lưu password, OTP, bearer token hay connection string vào báo cáo chia sẻ.

Xem kết quả tự động cập nhật và các giới hạn tại [ma trận truy vết](../../TransConnect-Docs/08-testing/traceability.md). Worker nhiều instance, SQL Server thực tế, SMTP/Google thật vẫn cần nghiệm thu riêng. Các module vận tải chưa triển khai không được đánh dấu pass từ bộ test core.
