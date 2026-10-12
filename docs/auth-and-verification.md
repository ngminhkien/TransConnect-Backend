# Xác thực & phân quyền (module Identity)

Tài liệu này mô tả các luồng đăng nhập, xoay vòng token, xác thực email và đặt lại mật khẩu —
kèm lý do đằng sau từng quyết định bảo mật, để người sửa sau không vô tình gỡ mất chúng.

## Tổng quan

- **Không dùng ASP.NET Core Identity.** RBAC (Role - Permission) tự xây trong module Identity.
- Access token là **JWT** mang sẵn claim role + permission → không query DB mỗi request.
- Refresh token lưu **SHA-256** trong DB, có **xoay vòng và phát hiện tái sử dụng**. Client vẫn nhận token ngẫu nhiên 64 byte ở dạng Base64.

## Phân quyền

Permission được nhét thẳng vào JWT lúc phát token. `RequirePermissionAttribute`
(`TransConnect.Common/Auth/`) đọc claim, không query DB.

**Đánh đổi cần biết**: thu hồi quyền trong DB **không có hiệu lực ngay** với token đang sống —
chỉ áp dụng từ lần refresh/đăng nhập kế tiếp. Access token mặc định sống 60 phút
(`Jwt:AccessTokenMinutes`). Muốn thu hồi tức thì thì phải thêm blacklist theo `JwtId`, đổi lại
mỗi request phải tra cache/DB.

## Refresh token: xoay vòng + phát hiện tái sử dụng

Mỗi lần refresh, token cũ bị đánh dấu `IsUsed` bằng UPDATE có điều kiện `!IsUsed && !IsRevoked && ExpiresAt > now`; tạo token mới trong cùng transaction. Nếu tạo token mới thất bại, việc dùng token cũ cũng rollback. User không tồn tại, bị xóa mềm hoặc bị khóa trả 401.

**Nếu một token đã `IsUsed` bị trình lên lần nữa** → coi như bị đánh cắp → **revoke toàn bộ
refresh token còn hiệu lực của user đó** (`RevokeAllTokensAsync`), buộc đăng nhập lại trên mọi
thiết bị.

Replay có thể do bị đánh cắp hoặc do client gửi refresh đồng thời/retry sau khi mất response. Chính sách hiện tại vẫn thu hồi toàn bộ refresh token của user; client phải gộp các yêu cầu refresh đồng thời vào một yêu cầu chung. Chỉ từ chối token cũ không vô hiệu được token mới đã cấp cho bên giữ bản sao.

> Đừng "tối ưu" bước này thành chỉ từ chối token bị tái sử dụng. Đó là toàn bộ giá trị của cơ chế.

**Giới hạn**: revoke refresh token **không** làm access token đã phát mất hiệu lực — kẻ tấn công
vẫn dùng được tối đa hết thời hạn access token (mặc định 60 phút).

## Migration hash refresh token (11/10/2026)

Cột `identity.RefreshTokens.Token` giữ kiểu/index hiện có, nhưng lưu `sha256:` + 64 ký tự hex. SHA-256 phù hợp với token có 512 bit ngẫu nhiên; mật khẩu/OTP vẫn dùng BCrypt. Token hash lấy từ DB không thể dùng trực tiếp làm bearer token.

Migration dữ liệu `20261011000000_HashRefreshTokens` dùng SQL Server `HASHBYTES('SHA2_256', CONVERT(varchar(500), Token))`. Token cũ là Base64 ASCII nên kết quả tương ứng UTF-8 phía .NET; không cần đổi token client đang giữ. Không có fallback đọc plaintext trong code mới.

Trình tự triển khai:

1. Dừng các instance code cũ, sao lưu database theo quy trình môi trường.
2. Chạy `dotnet ef database update --project TransConnect.Modules.Identity --startup-project TransConnect.Host --context IdentityDbContext` bằng SDK/runtime .NET 10 và EF CLI tương ứng.
3. Khởi động code mới, thử login/refresh/logout. Development tự migrate khi khởi động; Production cần bước migrate riêng.

Không chạy lẫn code cũ/mới trên cùng database khi chuyển đổi. Migration không thể đảo hash về plaintext; `Down` chủ động từ chối. Nếu buộc quay lại code cũ, cần kế hoạch khôi phục DB hoặc thu hồi toàn bộ phiên và đăng nhập lại, không gọi downgrade migration một cách tự động.

Trong phiên 11/10 đã kiểm tra sinh SQL bằng provider SQL Server và không lệch model/snapshot; chưa chạy migration trên DB thực tế. Test rotation/concurrency chạy bằng SQLite, không thay thế nghiệm thu SQL Server.

## Cấu hình được kiểm tra khi khởi động

- Jwt: key tối thiểu 32 byte UTF-8 cho HS256, issuer/audience không rỗng, thời hạn dương.
- Verification: CodeLength 1–9 (giới hạn thuật toán Int32), thời hạn/số lần thử/số lần xin mã dương; mặc định vẫn 6 chữ số.
- Email: chỉ File/Smtp; worker có giá trị dương, backoff tối đa không nhỏ hơn ban đầu. Smtp cần host, port 1–65535, user/password và địa chỉ người gửi hợp lệ. File không yêu cầu SMTP credential.
- Google ClientId rỗng vẫn tắt chức năng Google; không cản startup.

Validate chạy trước migration/seeding Development. Lỗi chỉ nêu tên cấu hình, không đưa secret vào thông báo.

## Mã OTP dùng chung

Xác thực email và đặt lại mật khẩu dùng **chung một cơ chế**: bảng `VerificationCodes` phân biệt
bằng cột `Purpose` (`EmailVerification` | `PasswordReset`), phục vụ bởi `IVerificationCodeService`.

Cấu hình ở section `Verification`:

```json
"Verification": { "CodeLength": 6, "ExpiryMinutes": 10, "MaxAttempts": 5, "MaxRequestsPerHour": 3 }
```

### Vì sao OTP mà không phải link đặt lại mật khẩu

Hệ thống có cả web React lẫn app Flutter. Dùng link thì muốn mở được trong app native phải cấu
hình Universal Links (iOS) / App Links (Android) — khai báo domain, verify... khá tốn công.
**Mã OTP dùng chung một luồng cho cả web lẫn app, không cần cấu hình gì thêm.**

### Các ràng buộc bảo mật — KHÔNG được gỡ

Mã 6 chữ số chỉ có 1 triệu khả năng, làm sai là thành lỗ hổng dò mã. Mọi ràng buộc dưới đây là
bắt buộc, không phải tùy chọn:

| Ràng buộc | Cài đặt | Vì sao |
|---|---|---|
| Sinh mã bằng `RandomNumberGenerator` | `VerificationCodeService.GenerateCode` | `Random` đoán được nếu biết seed |
| Lưu **hash**, không lưu mã gốc | BCrypt qua `IPasswordHasher` | DB rò rỉ thì mã cũng không dùng ngay được |
| Hash **chậm** (BCrypt), không phải SHA256 | — | 1 triệu khả năng, hash nhanh là dò hết trong vài giây |
| Hết hạn ngắn (10 phút) | `ExpiryMinutes` | Entropy thấp, sống càng lâu càng dễ bị dò |
| Sai quá 5 lần → **huỷ mã** | `MaxAttempts` | Đây chính là thứ chặn brute-force |
| Xin mã mới → **vô hiệu hoá mã cũ** | `IssueAsync` | Nhiều mã cùng sống làm tăng xác suất đoán trúng |
| Giới hạn 3 lần xin mã/giờ | `MaxRequestsPerHour` | Chặn spam hộp thư nạn nhân và đốt hạn mức Gmail |
| Dùng 1 lần | `IsUsed` | — |

### Chống dò tài khoản (account enumeration)

`forgot-password` và `resend-verification` **luôn trả về thành công**, kể cả khi email không tồn
tại. Nếu phản hồi khác nhau giữa "có" và "không có", endpoint này thành công cụ dò xem email nào
đã đăng ký.

Hệ quả kéo theo — dễ bỏ sót khi sửa sau này:

- Lỗi vượt giới hạn số lần xin mã (`TooManyRequestsException`) bị **nuốt có chủ đích** trong
  `ForgotPasswordAsync`/`ResendVerificationAsync`. Trả 429 sẽ tiết lộ email đó có tồn tại.
- `reset-password`/`verify-email` với email không tồn tại trả **đúng cùng thông báo** với mã sai
  (*"Mã xác thực không đúng hoặc đã hết hạn"*), không phải 404.

## Luồng xác thực email

Người dùng đăng ký → nhận mã 6 số qua email → gọi `verify-email` → `User.EmailVerifiedAt` được set.

**Cố tình KHÔNG chặn đăng nhập khi chưa xác thực.** Người dùng vẫn đăng nhập và dùng app bình
thường; `AuthResponse.EmailVerified` để client hiển thị nhắc nhở. Khi có module ví, hãy bắt buộc
đã xác thực trước các thao tác nhạy cảm (rút tiền).

**Vì sao vẫn cần xác thực dù không chặn**: nếu người dùng gõ nhầm email lúc đăng ký, họ sẽ không
bao giờ nhận được mã đặt lại mật khẩu → mất tài khoản vĩnh viễn. Xác thực email là điều kiện tiên
quyết để chức năng quên mật khẩu hoạt động được.

**Đăng nhập Google được đánh dấu đã xác thực tự động** — Google đã xác minh email rồi, bắt xác
minh lại là thừa.

## Đăng nhập Google — ĐANG TẠM DỪNG

> **Trạng thái**: code backend đã xong nhưng **chưa cấu hình**, tạm dừng để làm cùng lúc với
> frontend Flutter/React. Đọc hết mục này trước khi bật lại.

### Endpoint đang đóng an toàn (fail-closed)

`Google:ClientId` chưa cấu hình, nên `GoogleLoginAsync` **từ chối thẳng** mọi yêu cầu:

```json
{ "succeeded": false, "message": "Đăng nhập Google chưa được cấu hình trên máy chủ.", "errorCode": "UNAUTHORIZED" }
```

**Đừng gỡ chốt chặn này để "cho chạy tạm".** Trước đây code bỏ qua kiểm tra claim `aud` khi
`ClientId` rỗng — tức chỉ xác minh "token do Google ký" mà không xác minh "ký cho ứng dụng nào".

Cách khai thác nếu bỏ kiểm tra `aud`: kẻ tấn công tạo một ứng dụng Google OAuth của riêng hắn, dụ
nạn nhân đăng nhập vào đó, lấy được ID token của nạn nhân — token này do Google ký thật, chứa
email thật. Gửi token đó tới `POST /auth/google` là **đăng nhập được vào tài khoản nạn nhân**.

Vì vậy quy tắc là: **có `ClientId` thì kiểm tra `aud`, không có thì từ chối** — không bao giờ có
trạng thái thứ ba là "chạy mà không kiểm tra".

### Việc cần làm khi quay lại

**1. Tạo OAuth client trong Google Cloud Console** (APIs & Services → Credentials):

| Loại client | Dùng cho | Trường phải điền |
|---|---|---|
| Web application | React | **Authorized JavaScript origins**: `http://localhost:5173`, domain thật (https) |
| Android | Flutter Android | Package name + SHA-1 fingerprint |
| iOS | Flutter iOS | Bundle ID |

**Authorized redirect URIs: để trống.** Luồng hiện tại là client tự đăng nhập rồi gửi ID token
cho backend — Google không redirect đi đâu cả, nên không có URL nào để khai. Redirect URI chỉ cần
cho luồng authorization code (backend tự redirect trình duyệt), luồng đó mình **không** dùng.

Lấy SHA-1 của debug keystore (Android):

```bash
keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android
```

Khi build bản phát hành phải thêm SHA-1 của release keystore nữa.

**2. Đổi `GoogleOptions.ClientId` (string) → `ClientIds` (mảng).** Google cấp client ID riêng cho
từng nền tảng, backend phải chấp nhận cả danh sách:

```json
"Google": {
  "ClientIds": [
    "xxx.apps.googleusercontent.com",
    "yyy.apps.googleusercontent.com"
  ]
}
```

**3. Giữ nguyên hành vi fail-closed** khi đổi sang mảng — danh sách rỗng thì vẫn phải từ chối, đừng
để lọt trạng thái "chạy mà không kiểm tra `aud`".

**4. Mẹo cho Flutter**: package `google_sign_in` cho phép truyền `serverClientId` = **Web client
ID**. Khi đó ID token trả về có `aud` là Web client ID chứ không phải client ID của Android/iOS —
backend chỉ cần biết mỗi Web client ID. Vẫn phải tạo client Android/iOS trong Console để SDK chạy
được.

### Những thứ KHÔNG cần

- **`ClientSecret`**: không cần, và không nên tạo. Backend không bao giờ gọi sang Google — nó chỉ
  xác minh chữ ký bằng khoá **công khai** của Google. ClientSecret chỉ dùng cho luồng
  authorization code.
- **Authorized redirect URIs**: xem giải thích ở trên.

Client ID **không phải bí mật** (frontend nào cũng lộ nó ra), để trong `appsettings.json` là bình
thường — khác hẳn App Password của Gmail.

## Luồng quên mật khẩu

```
forgot-password (email)  →  sinh OTP + gửi mail  →  luôn trả 200
reset-password (email + code + mật khẩu mới)
   → kiểm tra OTP → đổi mật khẩu → REVOKE TOÀN BỘ refresh token
```

**Đổi mật khẩu xong phải revoke hết refresh token.** Nếu không, kẻ đã chiếm được tài khoản vẫn
giữ nguyên phiên đăng nhập dù nạn nhân vừa đổi mật khẩu — reset mật khẩu mà không đá thiết bị lạ
ra thì gần như vô nghĩa.

Từ 12/10/2026, tiêu thụ OTP, đổi mật khẩu/xác thực email và thu hồi refresh token nằm trong cùng transaction Identity. Lỗi ở bước ghi dữ liệu rollback toàn bộ; mã nhập sai vẫn commit AttemptCount trước khi trả 401.

Đặt lại mật khẩu thành công cũng **đánh dấu email đã xác thực**: nhập đúng mã gửi tới email
nghĩa là đã chứng minh sở hữu email đó.

Tài khoản chỉ có Google (chưa có mật khẩu) vẫn dùng được luồng này để **đặt mật khẩu lần đầu** —
OTP đã chứng minh quyền sở hữu email nên việc này an toàn.

## Danh sách endpoint

| Endpoint | Ghi chú |
|---|---|
| `GET /auth/me` | Hồ sơ và roles/permissions hiện tại từ DB; 401 nếu tài khoản không khả dụng; không phát/trả token |
| `POST /auth/register` | Trả token luôn; gửi mã xác thực nhưng không chặn nếu gửi hỏng |
| `POST /auth/login` | |
| `POST /auth/google` | Chưa cấu hình → đang từ chối mọi yêu cầu; xem mục "Đăng nhập Google" |
| `POST /auth/refresh` | Xoay vòng token + phát hiện tái sử dụng |
| `POST /auth/logout` | Cần đăng nhập |
| `POST /auth/forgot-password` | Luôn trả 200 |
| `POST /auth/reset-password` | Revoke toàn bộ phiên sau khi đổi |
| `POST /auth/verify-email` | |
| `POST /auth/resend-verification` | Luôn trả 200 |

## Việc chưa làm

- **Đăng nhập Google chưa cấu hình** — endpoint đang đóng an toàn nên không có rủi ro, nhưng
  chức năng chưa dùng được. Làm cùng lúc với frontend, xem mục riêng ở trên.
- Đăng ký/reset đã thống nhất tối thiểu 6, tối đa 100 ký tự qua `PasswordRules`, theo yêu cầu chủ dự án ngày 12/10; không thêm quy tắc độ phức tạp.
- Chưa có thu hồi access token tức thì, khóa đăng nhập tự động hoặc tenant. Xem [hiện trạng Docs](../../TransConnect-Docs/00-project/backend-status.md).

## Kiểm thử core

`AuthLifecycleTests`, `RefreshConcurrencyTests`, `VerificationAndEmailTests`, `OptionsValidationTests`, `MigrationTests` bổ sung cho HTTP contract hiện có. Test email dừng tại lưu hàng đợi, không gửi SMTP và không chạy worker. Xem [ma trận truy vết](../../TransConnect-Docs/08-testing/traceability.md).

`IdentityRegressionTests` kiểm tra OTP đồng thời, reset rollback, mật khẩu 6 ký tự, RBAC xóa/tạo lại, ghi trùng đồng thời và seeder. [Luồng test tổng thể](backend-test-flow.md) có request mẫu, thứ tự chạy và kỳ vọng.

## Đồng thời OTP và migration RBAC — 12/10/2026

- Issue/consume mở transaction rồi thực hiện no-op UPDATE User để lấy write lock tại DB trước khi đọc OTP. Cấp mã, kiểm tra hạn mức và vô hiệu mã cũ được tuần tự hóa theo user, kể cả nhiều backend instance. Consume đọc không tracking và claim bằng UPDATE có điều kiện; tăng số lần sai trực tiếp trong DB.
- `ConsumeAndExecuteAsync` sở hữu transaction; callback chỉ ghi cùng IdentityDbContext, không mở transaction lồng nhau hoặc gọi email/network. Callback thành công mới commit mã; callback lỗi rollback. Caller phải bỏ scope DbContext sau lỗi hoặc reload entity trước khi tái sử dụng trạng thái đang tracking.
- Refresh lấy khóa User trước token, cùng thứ tự với reset. SQLite test không thay thế kiểm chứng locking SQL Server.
- Migration `20261012120436_ActiveRolePermissionUniqueIndexes` đổi `IX_Roles_Name` và `IX_Permissions_Code` sang unique có filter `[DeletedAt] IS NULL`. Cần áp dụng migration trước khi dùng khả năng tạo lại tên/code đã xóa. Không thay đổi uniqueness của email hoặc tên bảng.
- Bản tạo lại có ID mới, không kế thừa UserRoles/RolePermissions cũ. Seeder nối tài khoản admin seed với role Admin khi role mặc định được tạo lại; không đặt lại mật khẩu và không phục hồi liên kết của user khác.
- Tạo/đổi tên bị DbUpdateException chỉ chuyển thành 409 khi truy vấn xác nhận tên/code đang bị bản ghi hoạt động khác giữ; lỗi ghi không có xung đột vẫn đi theo lỗi hệ thống.
- Downgrade về index không filter có thể thất bại nếu lịch sử và bản mới trùng tên/code. Phải giải quyết dữ liệu trùng trước khi downgrade, không tự xóa lịch sử trong migration.
