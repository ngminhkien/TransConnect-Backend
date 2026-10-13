# Luồng kiểm thử giai đoạn 1 — Nền tảng VieGo

Phạm vi ngày 13/10/2026: Identity theo nhóm Customer/Operator, Admin nội bộ, tổ chức và xét duyệt thủ công, Owner/nhân viên/lời mời/quota/audit, hồ sơ tài xế và sự đồng ý cộng tác, phương tiện và lịch sử khai thác; giữ các luồng core API/OTP/phiên/RBAC/email bên dưới. OCR, kho hồ sơ riêng, chuyến/đơn hàng và thanh toán chưa triển khai.

Nguồn: [câu trả lời Q-01–09](../../TransConnect-Docs/09-decisions/Answer-DEC-PRODUCT-20261012.txt) và [quyết định giai đoạn 1](../../TransConnect-Docs/09-decisions/phase-1-foundation-2026-10-13.md). Giai đoạn 1 ở đây là thứ tự mới trong file trả lời, không phải tên Phase 1/Phase 2 của roadmap cũ.

**Thay đổi khi dùng các request core bên dưới:** thêm `accountGroup: "Customer"` hoặc `"Operator"` vào register/login/google/forgot/reset/verify/resend. Nếu bỏ trống, mặc định Customer để giữ client cũ; không tự dò nhóm từ email. Admin login/OTP dùng `"InternalAdmin"`; đăng ký công khai nhóm này trả 400. Driver dùng Operator. SĐT hiện là thông tin liên hệ, chưa phải định danh đăng nhập. Response có `accountId` (cùng giá trị `userId` được giữ để tương thích) và `accountGroup`.

## 1. Chạy bộ kiểm thử tự động trước

Các test mới nằm trong `FoundationTests`: phân nhóm tài khoản/OTP/reset, Owner/nhân viên/quota/audit, tenant, lời mời hết hạn/dùng lại/thay thế, tài xế nhiều tổ chức, mời trước đăng ký, lịch sử xe, logout chéo tài khoản và adapter chặn gỡ liên kết khi còn nghĩa vụ. Kết quả cuối cùng ghi tại ma trận truy vết.

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

**Migration của giai đoạn này:** Identity `20261013102847_AccountGroups`; Operator `InitOperatorFoundation` và `SeparateDriverQuota`. Development sẽ áp cả ba context. Lượt sửa này chỉ sinh/test SQL, chưa cập nhật DB thực tế. AccountGroups giữ ID/mật khẩu, chuyển người có role Admin còn hiệu lực sang InternalAdmin, còn lại Customer, chuẩn hóa email và thu hồi refresh token cũ. Nếu phát hiện trùng sau chuẩn hóa, migration dừng để xử lý dữ liệu. Login lại với đúng nhóm; rollback bị chặn nếu đã có email/GoogleId trùng giữa nhóm.

Cấu hình tùy chọn trong môi trường test:

```json
{
  "Operator": {
    "DefaultEmployeeLimit": 3,
    "DefaultDriverLimit": null,
    "InvitationHours": 48
  }
}
```

Giá trị 3/48 là mặc định kỹ thuật có thể đổi; không phải chính sách gói giá đã chốt. DriverLimit null nghĩa là chưa giới hạn, tách khỏi nhân viên. API Admin có thể đổi quota từng tổ chức. Cấu hình âm/0 bị từ chối ở startup.

1. Dùng database phát triển riêng. Nếu chưa có `TransConnect.Host/appsettings.Development.json`, tạo từ `.example.json`; không ghi đè file đang dùng.
2. Cấu hình `ConnectionStrings:Default` cho database test và `Email:Provider = File`. Nếu có biến môi trường override, kiểm tra cả biến đó. Không đưa credential/token thật vào báo cáo test.
3. Chạy từ Backend:

   ```powershell
   dotnet run --project TransConnect.Host --launch-profile https
   ```

   Máy hiện tại nếu `dotnet` mặc định còn là SDK 8/9, thay bằng `C:/Users/PC/.dotnet/dotnet.exe`.

4. Development tự migrate **Identity, Notification và Operator**, seed role/permission/admin. Mở `https://localhost:7218/swagger`; HTTP profile là `http://localhost:5267`. Chứng chỉ development phải được máy/client tin cậy.
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

Xem kết quả tự động cập nhật và các giới hạn tại [ma trận truy vết](../../TransConnect-Docs/08-testing/traceability.md). Worker nhiều instance, SQL Server thực tế, SMTP/Google thật vẫn cần nghiệm thu riêng. Operations/Booking chưa triển khai không được đánh dấu pass từ bộ test nền tảng.

## 12. Tài khoản theo nhóm — TC-FOUNDATION-001, 008

1. Register Customer với email A và mật khẩu 123456; register Operator với cùng email A, mật khẩu 654321. Hai accountId khác nhau; phoneNumber có thể trùng vì chưa dùng đăng nhập.
2. Lặp lại email trong cùng nhóm, kể cả đổi chữ hoa/thường → 409. Public register InternalAdmin → 400.
3. Login A/123456/Customer → 200; A/123456/Operator → 401; A/654321/Operator → 200.
4. GET /auth/me và refresh giữ đúng accountId/accountGroup. Không tự chuyển nhóm khi refresh.
5. Forgot-password cho Operator; dùng mã đó để reset Customer → 401; reset Operator → 200. Mật khẩu Customer không đổi. Tương tự kiểm tra verify/resend không xác thực tài khoản khác nhóm.
6. Dùng JWT Customer logout với refresh token Operator → 200 nhưng refresh Operator vẫn hoạt động. Logout đúng tài khoản → refresh bị từ chối.
7. Admin seed phải login với accountGroup InternalAdmin; public Google không được tạo/đăng nhập InternalAdmin.

## 13. Tổ chức → xét duyệt → Owner — TC-FOUNDATION-002, 005

Chuẩn bị hai tổ chức A/B với hai email Owner khác nhau. Thay ID và token trong ví dụ bằng dữ liệu trả về. Tất cả route không có /api.

1. Chưa cần tài khoản, POST /organizations:

```json
{
  "name": "Nhà xe QA A",
  "registrationNumber": "QA-ORG-A-20261013",
  "ownerEmail": "owner-a@example.com"
}
```

Kỳ vọng 200, lưu id, reviewStatus Pending. Lặp mã đăng ký → 409. Hồ sơ đăng ký không tạo tài khoản Owner.

2. Login Admin. GET /organizations → danh sách hồ sơ (hiện tối đa 200). POST /organizations/{id}/review với `{"approved":true,"note":"Căn cứ đã kiểm tra hồ sơ QA"}` → Approved, email mời Owner được enqueue. Từ chối dùng approved=false → Rejected, không có lời mời. Review lại hồ sơ đã xử lý → 409.
3. Người nhận register/login Operator bằng đúng ownerEmail, verify email. Đọc mã lời mời trong email File. POST /organizations/invitations/accept với `{"token":"<mã lời mời>"}` → membership OrganizationOwner. Sai nhóm/chưa verify → 403; sai người/hết hạn/đã dùng/đã thu hồi → 401.
4. Owner GET /organizations/{id} → 200. Tài khoản của B đọc A → 403. GET /organizations chỉ hiện tổ chức có membership hoạt động của chính mình.
5. Mã Owner hết hạn: Admin POST /organizations/{id}/owner-invitation để gửi lại; mã cũ không dùng được. Tổ chức đã có Owner → 409.
6. Thử accept lời mời tổ chức khác bằng tài khoản đã có membership → 409; không chuyển tổ chức bằng lời mời mới. Việc cộng tác tài xế nhiều bên dùng luồng riêng ở mục 15.

## 14. Nhân viên, quota và audit — TC-FOUNDATION-002

| Bước | Request / thao tác | Kỳ vọng |
| --- | --- | --- |
| P1-M01 | Owner POST /organizations/{id}/invitations, `{"email":"staff@example.com","role":"Manager"}` | Lời mời email, trả id/expiresAt; không trả token trong API |
| P1-M02 | Nhân viên đăng ký Operator, verify email, accept tại /organizations/invitations/accept | Member riêng, không dùng chung tài khoản Owner |
| P1-M03 | Owner GET /organizations/{id}/members | Danh sách id/accountId/role/isActive của tổ chức |
| P1-M04 | Owner PUT /organizations/{id}/members/{memberId}, `{"role":"Dispatcher","isActive":false}` | JWT cũ của nhân viên vẫn xác thực nhưng API dữ liệu tổ chức trả 403 ngay |
| P1-M05 | Gửi yêu cầu sửa memberId của B qua route A | 404; không sửa dữ liệu B |
| P1-M06 | Manager/Dispatcher tự mời hoặc sửa quyền nhân viên | 403; không được tự nâng thành Owner |
| P1-M07 | Owner sửa/vô hiệu hóa membership Owner qua API nhân viên | 409; chuyển Owner chưa có quy trình |
| P1-M08 | Owner + một active member + một pending invite, limit=3; mời thêm | 409; hết hạn/thu hồi lời mời giải phóng quota |
| P1-M09 | DELETE /organizations/{id}/invitations/{invitationId} | Thu hồi mã pending; mã cũ accept → 401 |
| P1-M10 | Admin PUT /organizations/{id}/employee-limit, `{"limit":10}` | Đổi quota có audit; hạ dưới occupancy → 409 |
| P1-M11 | Owner/Admin GET /organizations/{id}/audit | Có actor/subject/action/details/time; không có API sửa/xóa audit |

Gửi lại lời mời cùng email trước accept vô hiệu token cũ và giữ một chỗ quota. Role nhân viên chỉ Manager/Dispatcher. Sửa role nhân viên không dùng /roles toàn hệ thống. Audit hiện trả tối đa 200 bản ghi mới nhất.

## 15. Tài xế và sự đồng ý cộng tác — TC-FOUNDATION-003, 009, 010

1. Tài xế register/login Operator, verify email; POST /drivers `{"fullName":"Tài xế QA"}` → hồ sơ Pending, riêng với accountId. GET /drivers/me đọc hồ sơ của mình. Một AccountId chỉ có một Driver.
2. Admin GET /drivers/pending; POST /drivers/{driverId}/review `{"approved":true,"note":"Đã kiểm tra hồ sơ QA"}` → Approved. Chưa duyệt không được accept cộng tác.
3. Owner/Manager/Admin POST /organizations/{orgId}/drivers/{driverId}/invitations → email mời, chưa trở thành quan hệ đã chấp nhận.
4. Driver POST /drivers/invitations/accept `{"token":"<mã>"}` → AcceptedAt có giá trị. Sai tài khoản/hết hạn/dùng lại → 401. Có thể lặp tại tổ chức B để cộng tác cả A/B.
5. Driver không có membership gọi /organizations/{orgId}/vehicles hoặc /members → 403; liên kết tài xế không cấp quyền quản trị.
6. Tổ chức GET /organizations/{orgId}/drivers để xem liên kết/lịch sử. Driver hoặc quản lý phù hợp DELETE /organizations/{orgId}/drivers/{engagementId} để kết thúc; lịch sử còn nguyên. Pending → RevokedAt; đã accept → EndedAt.
7. Admin PUT /organizations/{orgId}/driver-limit với `{"limit":1}`; một liên kết hoặc pending invitation còn hiệu lực chiếm một chỗ, lời mời tiếp theo → 409. `{"limit":null}` bỏ giới hạn tài xế. Không cộng vào EmployeeLimit.
8. **Mời trước đăng ký:** Owner/Manager/Admin POST /organizations/{orgId}/driver-invitations `{"email":"new-driver@example.com"}`. Người nhận đăng ký Operator, verify, nộp Driver và được Admin duyệt, rồi accept ở bước 4. Chưa có hồ sơ hoặc chưa duyệt → 409, token chưa bị tiêu thụ. Token tài xế gửi sang /organizations/invitations/accept → 401, không cấp membership.

Hồ sơ đang xét duyệt thủ công; chưa có upload/OCR hay phát hiện nghi trùng danh tính tự động. Không tự hợp nhất người có thông tin giống nhau. Mã lời mời mặc định có hạn 48 giờ; nếu quá hạn lúc chờ duyệt thì gửi lại.

## 16. Phương tiện và lịch sử khai thác — TC-FOUNDATION-004, 007

1. Sau khi kiểm tra quyền khai thác thực tế, Admin POST /organizations/{orgId}/vehicles:

```json
{
  "registrationPlate": "51A-123.45",
  "description": "Xe kiểm thử",
  "verificationReference": "Mã hồ sơ/căn cứ đã được người xét duyệt kiểm tra"
}
```

Trả linkId/vehicleId; biển số chuẩn hóa bỏ dấu cách, dấu chấm/gạch, viết hoa. API không tự xác minh giấy tờ chỉ từ chuỗi này. Operator tự gọi bước gán quyền khai thác → 403.

2. Owner/Manager/Dispatcher GET /organizations/{orgId}/vehicles. Tổ chức khác không có quyền đọc qua route A.
3. Owner/Manager PUT /organizations/{orgId}/vehicles/{linkId}/suspension `{"isSuspended":true}` → tạm ngừng. Liên kết vẫn còn; B thêm cùng xe → 409. isSuspended=false khôi phục liên kết chưa kết thúc.
4. DELETE /organizations/{orgId}/vehicles/{linkId} → kết thúc liên kết, không xóa Vehicle hay lịch sử. Có nghĩa vụ chưa giải quyết → 409.
5. Admin kiểm tra quyền khai thác B rồi thêm cùng biển số tại B → cùng vehicleId, linkId mới. GET danh sách A vẫn thấy liên kết cũ với EndedAt; không chuyển dữ liệu lịch sử A sang B.

**Giới hạn tích hợp:** giai đoạn 1 chưa có Trip/Booking. Adapter FoundationTransportObligations xác định không có nghĩa vụ vì chưa có nghiệp vụ tạo chúng; test TC-FOUNDATION-007 dùng adapter giả có nghĩa vụ để chứng minh chặn gỡ xe/tài xế. Phải thay adapter và phối hợp khóa với Operations/Booking trước khi bật giai đoạn 2. Chưa thể nghiệm thu “không phân công xe đang tạm ngừng” khi API phân công chưa tồn tại.

## 17. Kiểm thử đồng thời bổ sung trên SQL Server

| Kịch bản | Kỳ vọng |
| --- | --- |
| Hai đăng ký cùng email, cùng nhóm | Một 200, một 409; khác nhóm thì hai AccountId |
| Hai accept cùng mã Owner/nhân viên/tài xế | Một thành công, một 401; một membership/engagement |
| Hai lời mời nhân viên khi còn một chỗ | Một 200, một 409; không vượt quota |
| Hai lời mời tài xế khi còn một chỗ DriverLimit | Một 200, một 409; EmployeeLimit không đổi |
| Hai tổ chức gán cùng biển số | Một thành công; một 409; một liên kết còn hiệu lực |
| Vô hiệu member đồng thời thao tác quản trị | Sau transaction vô hiệu commit, request mới bị 403 |
| Hai lời mời từ hai tổ chức được cùng tài khoản accept | Một membership duy nhất; request còn lại 409 |

Không lấy test SQLite tuần tự làm bằng chứng các kịch bản này đã pass SQL Server. Ghi SQL Server/version/database, kết quả response và truy vấn xác nhận sau commit.
