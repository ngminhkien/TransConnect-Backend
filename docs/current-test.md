# Test toàn giai đoạn nền tảng người dùng — 1 BE và 3 FE

Cập nhật **20/10/2026** theo code Backend và ba frontend hiện có. Đây là file test thủ công duy nhất, đã thay toàn bộ hướng dẫn cũ và các phần bổ sung chồng nhau. Ưu tiên chức năng cơ bản; không đặt tiêu chí hoàn thiện UI/UX trong lượt này.

**Trạng thái:** đã có code và kiểm thử tự động; chưa xác nhận nghiệm thu chạy thật với SQL Server, SMTP, Cloudinary, S3 và trình duyệt/thiết bị. Kết quả thực hiện và lịch sử giữ tại [progress-tracker](../../TransConnect-Docs/00-project/progress-tracker.md#test-evidence).

## 1. Những phần đã làm

| Phần | Chức năng đã triển khai | Nơi có thể test |
|---|---|---|
| Tài khoản | Đăng ký Customer/Operator, đăng nhập theo nhóm, xác thực/gửi lại OTP email, quên/reset mật khẩu, refresh/logout, đọc phiên | Customer app, Driver app, web và Swagger; Google qua API khi có cấu hình OAuth |
| Hồ sơ tài khoản | Sửa tên/SĐT, đổi mật khẩu hiện tại, upload avatar Cloudinary | Customer/Driver phần tài khoản, Partner web, Swagger |
| Quản trị tài khoản | Danh sách/phân trang, chi tiết, khóa/mở có lý do, lịch sử; quyền cao hơn khi quản InternalAdmin | Admin web và Swagger |
| RBAC | CRUD role/permission, gán/gỡ role và permission, audit; quyền DB có hiệu lực ngay trên request tiếp theo | Swagger; kiểm tra hiệu lực trên FE |
| Tổ chức | Operator nộp hồ sơ có xác thực, theo dõi, sửa/nộp lại khi Rejected, lịch sử; duyệt tự gán Applicant làm Owner | Partner/Admin web, Swagger |
| Hồ sơ tổ chức | Loại chủ thể, địa chỉ đăng ký, số/email liên hệ, trạng thái đủ thông tin | Partner web, Swagger |
| Nhân sự | Mời Manager/Dispatcher, nhận/thu hồi lời mời, đổi vai trò/bật tắt membership, quota, audit | Partner web, Swagger |
| Tài xế | Nộp/duyệt/từ chối/nộp lại, lịch sử, thông tin liên hệ; cộng tác nhiều tổ chức, mời/nhận/kết thúc cộng tác | Driver app, Partner/Admin web, Swagger |
| Quyền xem tài xế | Owner xem liên hệ hiện hành khi đang cộng tác; sau kết thúc chỉ xem snapshot đã lưu | Partner web, Swagger |
| Xe | Admin xác nhận liên kết, Owner quản profile/tạm ngưng/kết thúc có lý do; một tổ chức khai thác hiện hành | Admin/Partner web, Swagger |
| Giấy demo | DriverLicense và VehicleRegistration giả, nhiều version, mô phỏng xử lý/duyệt, download private, xóa | Driver app, Partner web, Swagger |
| File | Avatar theo môi trường/nhóm tài khoản; giấy demo theo chủ thể/loại/scope trong S3 private | FE upload + kiểm tra Media Library/bucket |
| Notification/maintenance | Queue email, retry/lease, kiểm tra nguồn OTP/lời mời, dọn body/metadata; audit retention và rà tham chiếu tài khoản | Thao tác auth/lời mời + mailbox, DB và log |
| Nền FE | Khôi phục phiên, refresh tuần tự, chống phản hồi phiên cũ, multipart/binary, loading/error, guard theo nhóm/quyền | Cả ba frontend |

Không nằm trong phạm vi test chức năng hoàn chỉnh: Trip, tìm chuyến, Booking, thanh toán, đánh giá, bản đồ/thu nhập tài xế; bộ giấy thật, ClamAV/OCR thật và lifecycle storage production; đổi email/xóa tài khoản/đặt mật khẩu đầu tiên cho Google-only. Các màn hình giới thiệu dịch vụ chưa có API không được tính là chức năng đã hoàn thành.

## 2. Điều kiện trước khi test

| Hạng mục | Cần chuẩn bị | Hiện trạng đã ghi nhận |
|---|---|---|
| SQL Server + .NET 10 | Database Development, connection string/JWT local, apply migration | Đã xác nhận MINHKIEN1 Running và TCP 1433 mở; probe ODBC/.NET vẫn lỗi TLS trong phiên agent, chưa apply migration mới |
| SMTP | Hộp thư test có thể đọc OTP và token lời mời | Chưa nghiệm thu gửi/nhận thật; không dựa vào file thư đã bị dọn sau gửi |
| Cloudinary | CloudName, ApiKey, ApiSecret phía BE | Chưa cấu hình ở lần kiểm tra gần nhất; avatar cần cấu hình để test thành công |
| S3 demo | Storage/bucket private hoạt động, ComplianceDemo bật trong Development | Cờ demo đã bật ở lần kiểm tra gần nhất; chưa nghiệm thu storage thật |
| Google | ClientId và Google ID token hợp lệ | Tùy chọn; FE Google chưa phải luồng hoàn thiện |

Nhập secret trong `TransConnect.Host/appsettings.Development.json` bị Git ignore hoặc biến môi trường; không ghi secret vào file này/FE/chat. Mẫu tại [appsettings.Development.example.json](../TransConnect.Host/appsettings.Development.example.json). Hướng dẫn [email](reference/email-and-notification.md), [storage và Cloudinary](reference/compliance-demo-setup.md).

### Chạy một BE và ba FE bằng bốn terminal PowerShell

Chạy BE trước; khi terminal báo `Now listening on: http://localhost:5267` mới test FE. Ba FE dùng chung BE và database. Management là **một FE chứa cả Admin và Partner**, Customer và Driver là hai FE Flutter riêng. Các lệnh dưới chạy Flutter Web bằng Chrome; chưa cần emulator.

**Chuẩn bị lần đầu:** cài .NET SDK 10, Node >=22.14, Flutter tương thích repo và Chrome; bật SQL Server, cấu hình JWT/DB/SMTP/Cloudinary/S3 local. Trước lần cập nhật DB, dừng Host/worker cũ, kiểm tra đúng database Development rồi chạy trong thư mục Backend:

```powershell
Set-Location 'C:\Users\PC\Desktop\CNTTB\doantotnghiep\TransConnect-Backend'
$env:DOTNET_ROOT = 'C:\Users\PC\.dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& "$env:DOTNET_ROOT\dotnet.exe" restore .\TransConnect.sln
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Update-DevelopmentDatabase.ps1 -DotnetPath C:\Users\PC\.dotnet\dotnet.exe -ProbeOnly
# Chỉ chạy dòng tiếp theo khi probe thành công:
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Update-DevelopmentDatabase.ps1 -DotnetPath C:\Users\PC\.dotnet\dotnet.exe -IncludeComplianceDemo
```

Helper kiểm tra dữ liệu trước migration; lỗi giữa các module thì kiểm tra migration history trước khi chạy lại. Migration gần nhất gồm `20261019164634_MinimalProfilesAndEngagementSnapshots`, `20261019164639_ProfileDocumentDemoVersions`, `20261020111920_ApplicantOnboardingAndReviewHistory`; áp dụng cả migration còn thiếu trước đó. Host Development cũng tự migrate khi khởi động. Nếu probe lỗi TLS/SSPI, phần DB chưa sẵn sàng; chạy ba FE không thay thế bước sửa kết nối này.

**Terminal 1 — Backend:**

```powershell
Set-Location 'C:\Users\PC\Desktop\CNTTB\doantotnghiep\TransConnect-Backend'
$env:DOTNET_ROOT = 'C:\Users\PC\.dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& "$env:DOTNET_ROOT\dotnet.exe" run --project .\TransConnect.Host --launch-profile http
```

API `http://localhost:5267`; Swagger `http://localhost:5267/swagger`. Đường dẫn SDK trên là SDK 10 đã tìm thấy ở máy này; tránh gọi nhầm dotnet 9 trong PATH.

**Terminal 2 — Management web (Admin + Partner):**

```powershell
Set-Location 'C:\Users\PC\Desktop\CNTTB\doantotnghiep\TransConnect-Frontend\transconnect-management-web'
$env:VITE_API_BASE_URL = 'http://localhost:5267'
npm.cmd ci
npm.cmd run dev -- --host localhost --port 5173 --strictPort
```

Mở `http://localhost:5173/login`; chọn InternalAdmin cho Admin, Operator cho Partner. `npm.cmd ci` chỉ cần lần đầu hoặc khi dependency/lockfile đổi.

**Terminal 3 — Customer Flutter:**

```powershell
Set-Location 'C:\Users\PC\Desktop\CNTTB\doantotnghiep\TransConnect-Frontend\transconnect-customer-app'
flutter pub get
flutter run -d chrome --web-hostname localhost --web-port 5174 --dart-define=API_BASE_URL=http://localhost:5267
```

Customer mở tại `http://localhost:5174` trong Chrome do Flutter khởi động.

**Terminal 4 — Driver Flutter:**

```powershell
Set-Location 'C:\Users\PC\Desktop\CNTTB\doantotnghiep\TransConnect-Frontend\transconnect-driver-app'
flutter pub get
flutter run -d chrome --web-hostname localhost --web-port 5175 --dart-define=API_BASE_URL=http://localhost:5267
```

Driver mở tại `http://localhost:5175`. `flutter pub get` chỉ cần lần đầu hoặc khi dependency đổi. Giữ cả bốn terminal chạy; Ctrl+C ở terminal tương ứng để dừng.

Nếu BE cấu hình `Cors:AllowedOrigins` không rỗng, danh sách phải chứa `http://localhost:5173`, `http://localhost:5174`, `http://localhost:5175`. Development với danh sách rỗng hiện cho phép origin để test. Không đổi localhost thành 127.0.0.1 tùy ý vì origin và session storage khác nhau.

**Storage giấy demo:** nếu dùng AIStor Compose của repo và đã có `.env`, `minio.license`, bucket/key đúng [hướng dẫn setup](reference/compliance-demo-setup.md), chạy một lần trước BE:

```powershell
Set-Location 'C:\Users\PC\Desktop\CNTTB\doantotnghiep\TransConnect-Backend'
docker compose --env-file .\infra\document-demo\.env -f .\infra\document-demo\compose.yaml up -d
```

Container chạy nền nên không cần giữ terminal thứ năm. Cloudinary là dịch vụ ngoài, không cần chạy process local. Chưa cấu hình Cloudinary chỉ chặn avatar; tắt ComplianceDemo nếu chưa test giấy. Test đầy đủ giấy bắt buộc bật demo Development và storage hoạt động.

**Tùy chọn Android:** thay lệnh Flutter trong terminal app cần thử bằng `flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5267` khi emulator đã mở. Thiết bị thật dùng địa chỉ LAN có thể truy cập BE; không dùng localhost trên điện thoại để trỏ về máy tính.

## 3. Dữ liệu và quy ước

Chuẩn bị tài khoản giả: một Customer; Operator A/B làm Applicant/Owner của hai tổ chức; Operator M làm Manager, D làm Dispatcher; Operator T làm Driver; một InternalAdmin có đủ quyền và một admin hạn chế quyền để thử từ chối. Dùng mailbox test có thể nhận thư. **Owner và Driver dùng hai tài khoản Operator riêng.** Không đăng ký InternalAdmin công khai; dùng tài khoản đã seed của DB test.

Dùng MST demo 10 hoặc 13 chữ số chưa tồn tại, ví dụ `0123456789`; đổi dữ liệu mỗi lượt để tránh trùng. Lưu riêng AccountId, OrganizationId, DriverId, VehicleId, MemberId, EngagementId, LinkId và FileId từ response; không dùng lẫn các loại ID. Token/OTP lấy từ response hoặc email đúng người nhận, chỉ lưu local.

Request JSON: `Content-Type: application/json`; endpoint bảo vệ: `Authorization: Bearer <accessToken>`. Đổi token khi đổi vai trò trong Swagger. Thành công thường HTTP 200, dữ liệu trong `data`; response JSON có `succeeded`, `message`, `errorCode`, `data`, `errors`. Download thành công trả binary, lỗi vẫn JSON. Phân trang trả `{items,pageNumber,pageSize,totalRecords,totalPages}`.

## 4. Luồng test cơ bản theo thứ tự

### 4.1 Tài khoản và phiên — TC-IAM / TC-CLOSURE-004–005, 007

| Bước | API/thao tác | Kỳ vọng |
|---|---|---|
| 1 | POST `/auth/register` với `{"accountGroup":"Customer","email":"<mailbox>","password":"Demo123456","fullName":"Khách demo","phoneNumber":"0900000000"}`; lặp lại với Operator | Đăng ký đúng nhóm; không cho đăng ký InternalAdmin. Dữ liệu thiếu/sai trả lỗi field |
| 2 | POST `/auth/login` với `{"accountGroup":"Customer","email":"<mailbox>","password":"Demo123456"}`; GET `/auth/me` | Lưu token từ response, /me đúng account/nhóm. Sai mật khẩu bị từ chối. Không bắt buộc email verified trước login; nghiệp vụ Operator vẫn có gate |
| 3 | POST `/auth/resend-verification` với email/accountGroup; POST `/auth/verify-email` thêm `code` từ mailbox | Verified; mã sai/hết hạn/đã dùng không dùng được; cấp mã mới làm mã cũ mất hiệu lực |
| 4 | PUT `/auth/me` với `{"fullName":"Tên đã sửa","phoneNumber":"0900000001"}` | GET /me và FE cập nhật. Gửi thêm email/accountGroup/isActive/avatarUrl không sửa được các trường đó; không tự đổi Driver.FullName |
| 5 | POST `/auth/me/avatar`, multipart `file`: PNG/JPEG/WebP tối đa 5 MiB | URL Cloudinary lưu vào account và hiển thị lại sau đăng nhập; thiếu cấu hình/provider lỗi 503, file sai 400, request quá lớn có thể 413; thất bại giữ URL cũ |
| 6 | POST `/auth/change-password` với `{"currentPassword":"Demo123456","newPassword":"Demo654321"}` | Mật khẩu đổi, refresh cũ bị thu hồi, FE về đăng nhập. Mật khẩu hiện tại sai: 400 errors.currentPassword và giữ phiên. Google-only: 409 |
| 7 | POST `/auth/forgot-password` với email/accountGroup; POST `/auth/reset-password` thêm `code`, `newPassword` | Reset bằng OTP, revoke refresh cũ; mật khẩu mới đăng nhập được, mật khẩu cũ không được |
| 8 | POST `/auth/refresh` với `{"refreshToken":"<token>"}` rồi thử lại token cũ | Rotation trả cặp mới; replay bị từ chối và thu hồi refresh account. Đăng nhập lại để tiếp tục |
| 9 | POST `/auth/logout` với refreshToken và Bearer; thử refresh token đó | Logout thành công, refresh bị từ chối; đóng/mở FE không tự khôi phục phiên đã logout |
| 10 | Tùy chọn POST `/auth/google` với request theo Swagger và ID token thật của môi trường test | Chỉ Customer/Operator; không cấp InternalAdmin; không giả định thành công khi chưa cấu hình Google |

Mật khẩu dài 6–100 ký tự. Thử lỗi mạng, email trùng trong cùng nhóm, truy cập /me không token. Logout thu hồi refresh; không coi logout đơn thuần là cơ chế vô hiệu mọi access token ngay như khóa account.

### 4.2 Tổ chức Applicant → Owner — TC-CLOSURE-001, 009–010

Dùng Operator A đã verified; Admin duyệt bằng tài khoản khác.

| Bước | API/body | Kỳ vọng |
|---|---|---|
| 1 | POST `/organizations` với `{"name":"Nhà xe demo A","registrationNumber":"0123456789"}` | Pending; CreatedByUserId và OwnerEmail lấy từ account server. Anonymous 401; MST sai 10/13 chữ số bị từ chối |
| 2 | GET `/organizations/search?pageNumber=1&pageSize=20`; GET `/organizations/{id}` | Applicant thấy hồ sơ của mình; không đọc/sửa hộ bằng cách đoán ID/email |
| 3 | Admin POST `/organizations/{id}/review` với `{"approved":false,"note":"Cần sửa tên demo"}` | Rejected, có người duyệt/thời gian/note; note bắt buộc khi duyệt hoặc từ chối |
| 4 | Applicant POST `/organizations/{id}/resubmit` với name/registrationNumber đã sửa | Trở về Pending, giữ OrganizationId; chỉ Rejected được nộp lại |
| 5 | GET `/organizations/{id}/reviews`; Admin review `{"approved":true,"note":"Đủ dữ liệu demo"}` | Lịch sử được giữ; tự tạo Owner cho Applicant trong transaction, không cần mời chính Applicant |
| 6 | GET `/operator/context` | Có membership/role/capabilities/quota phù hợp; account Driver không trở thành Owner và ngược lại |
| 7 | Owner PUT `/organizations/{id}/profile` theo mẫu bên dưới | Thông tin lưu lại, isProfileComplete cập nhật; không tự sửa MST/email Owner/review status |

```json
{"legalEntityType":"Enterprise","registeredAddress":"Địa chỉ tổ chức demo","contactPhone":"0900000002","contactEmail":"contact@example.com"}
```

LegalEntityType nhận Enterprise/Cooperative/HouseholdBusiness. Lặp lại với Operator B để test cách ly tổ chức.

Nhánh Admin nộp thay/legacy: POST `/organizations` bằng Admin với thêm `ownerEmail`; CreatedByUserId NULL. Khi duyệt gửi lời mời Owner, người nhận Operator verified dùng POST `/organizations/invitations/accept` body `{"token":"<token email>"}`. POST `/organizations/{id}/owner-invitation` gửi lại khi chưa có Owner; token cũ không còn dùng được. Admin có thể resubmit hồ sơ legacy Rejected. Không áp dụng nhánh lời mời này cho Applicant tự nộp.

### 4.3 Nhân sự, lời mời và quota — TC-FOUNDATION / TC-CLOSURE-008

| Thao tác | API/body | Kỳ vọng |
|---|---|---|
| Owner mời nhân viên | POST `/organizations/{id}/invitations`, `{"email":"<mailbox M>","role":"Manager"}`; lặp với Dispatcher | Email đúng người nhận, lời mời chiếm quota theo trạng thái; role khác bị từ chối |
| Người nhận xem/nhận | GET `/operator/invitations`; POST `/organizations/invitations/accept`, `{"token":"<email token>"}` | Đúng email/nhóm và verified mới nhận; không gán membership sang tổ chức khác trái quy tắc |
| Owner xem/quản | GET `/organizations/{id}/members/search`; PUT `/organizations/{id}/members/{memberId}`, `{"role":"Dispatcher","isActive":false}` | Vai trò/trạng thái đổi, quyền request tiếp theo cập nhật; bật lại bằng isActive=true khi hợp lệ |
| Xem/thu hồi lời mời | GET `/organizations/{id}/invitations`; DELETE `/organizations/{id}/invitations/{invitationId}` | Token đã thu hồi/hết hạn không nhận được; list không lộ raw token/hash |
| Admin đổi quota | PUT `/organizations/{id}/employee-limit`, `{"limit":4}`; PUT `/organizations/{id}/driver-limit`, `{"limit":2}` hoặc `{"limit":null}` | Quota/counter đúng trong context; vượt quota bị chặn, không tạo một phần dữ liệu |
| Kiểm tra quyền/audit | GET `/organizations/{id}/audit/search` | Owner/Admin theo quyền; Manager/Dispatcher không được thao tác quản trị chỉ dành Owner; nhà xe khác bị từ chối |

Employee quota mặc định 3; thử đủ quota và lời mời đang chờ, sau đó thu hồi để kiểm tra giải phóng chỗ. Context tổ chức/membership không hoạt động không trả capabilities hoạt động. Lời mời/cộng tác cá nhân hiện giới hạn 200; không coi là phân trang đầy đủ.

### 4.4 Tài xế, cộng tác và snapshot — TC-CLOSURE-002 / TC-FOUNDATION-016

| Bước | API/body | Kỳ vọng |
|---|---|---|
| 1 | Operator T verified POST `/drivers`, `{"fullName":"Tài xế demo"}`; GET `/drivers/me` | Pending; Owner không đăng ký Driver trên cùng account |
| 2 | Admin GET `/drivers/search?status=Pending`; GET `/drivers/{driverId}`; POST `/drivers/{driverId}/review` với approved=false và note | Rejected, Driver app hiển thị lý do |
| 3 | Driver POST `/drivers/me/resubmit`, `{"fullName":"Tài xế demo đã sửa"}`; Admin duyệt approved=true có note | Giữ DriverId và lịch sử GET `/drivers/{driverId}/reviews`; chỉ Approved dùng chức năng cần duyệt |
| 4 | Driver PUT `/drivers/me/profile`, `{"contactPhone":"0900000003","contactAddress":"Địa chỉ tài xế demo"}` | Lưu liên hệ; không tự đổi thông tin tài khoản |
| 5 | Owner POST `/organizations/{orgId}/drivers/{driverId}/invitations` không body; hoặc `/organizations/{orgId}/driver-invitations` với email | Gửi lời mời; email chưa có Driver phải hoàn tất đăng ký/verified/duyệt trước khi nhận |
| 6 | Driver POST `/drivers/invitations/accept`, `{"token":"<email token>"}`; GET `/drivers/me/collaborations` | Có engagement; nhận lời mời từ A và B được, không tự trở thành nhân viên |
| 7 | Owner GET `/organizations/{orgId}/drivers/search`; GET `/organizations/{orgId}/drivers/{engagementId}/profile` | Owner xem liên hệ hiện hành; Manager/Dispatcher/nhà xe khác bị từ chối dữ liệu riêng |
| 8 | DELETE `/organizations/{orgId}/drivers/{engagementId}` theo quyền hiện hành; Driver sửa liên hệ rồi Owner đọc lại profile | Kết thúc giữ snapshot tên/SĐT/địa chỉ lúc kết thúc; isHistorical=true. Gọi lại không đổi snapshot/audit |

Snapshot legacy không có dữ liệu phải báo snapshotAvailable=false, không lấy liên hệ mới để điền. Owner bị vô hiệu membership mất quyền cả snapshot. Kết thúc cộng tác ở A không xóa cộng tác B. Nghĩa vụ vận tải thực tế chưa được kiểm chứng vì chưa có Trip.

### 4.5 Xe — TC-FOUNDATION-012, 015

1. Admin POST `/organizations/{orgId}/vehicles`:

```json
{"registrationPlate":"51A-12345","description":"Xe demo","verificationReference":"Căn cứ xác nhận demo"}
```

2. GET `/organizations/{orgId}/vehicles`, lưu vehicleId và linkId. Owner không tự xác nhận liên kết mới thay Admin.
3. Owner PUT `/organizations/{orgId}/vehicles/{linkId}/profile`:

```json
{"vehicleKind":"Passenger","seatCount":16,"permittedPayloadKg":null}
```

Thử Freight với `{"vehicleKind":"Freight","seatCount":null,"permittedPayloadKg":2500.5}`. Thiếu số chỗ/tải trọng, loại sai hoặc nhập cả hai bị 400. Số chỗ gồm lái xe, tải trọng đơn vị kg.
4. PUT `/organizations/{orgId}/vehicles/{linkId}/suspension`, `{"isSuspended":true}` rồi false: tạm ngưng vẫn giữ liên kết, tổ chức B chưa nhận được xe.
5. DELETE `/organizations/{orgId}/vehicles/{linkId}` bắt buộc body `{"reason":"Kết thúc hợp đồng demo"}`: giữ EndedAt/EndedBy/EndReason, gọi lại không sửa lý do/thời điểm đầu tiên. Thiếu/trắng/lý do quá 1000 ký tự bị 400.
6. Sau khi A kết thúc, Admin liên kết xe cho B. A không sửa profile/giấy xe hiện hành; B không đọc giấy thuộc scope A. Xe còn nghĩa vụ phải bị chặn theo contract; nhánh nghĩa vụ Trip thật chưa có dữ liệu để nghiệm thu.

### 4.6 Giấy giả và folder — TC-PROFILE-DOC-001–009 / TC-COMPLIANCE-DEMO / TC-CLOSURE-007

Chỉ dùng file tự tạo. Demo bật trong Development; không thay thế thẩm định pháp lý hoặc tự duyệt Driver/Organization.

1. Driver dùng `Driver/{driverId}/DriverLicense`; Owner dùng `Vehicle/{vehicleId}/VehicleRegistration`. POST `/compliance/demo/profile-documents/{subjectType}/{subjectId}/{documentType}`, multipart `File` PDF/JPEG/PNG tối đa 8 MiB và `SyntheticDataConfirmed=true`. App chọn ảnh có thể giới hạn thấp hơn; dùng Swagger để thử PDF/giới hạn BE.
2. Response có versionNumber và `file.id`; giữ FileId này cho bước sau. GET cùng URL để đọc version. Không trả bucket/ObjectKey; file ban đầu PendingSimulation chưa download được.
3. POST `/compliance/demo/profile-documents/versions/{fileId}/simulate`, `{"scenario":"Success"}` → DemoNeedsReview. POST cùng prefix `/review`, `{"approved":true}` → DemoApproved. Chọn false → DemoRejected. Đây là mô phỏng bởi người quản file, không phải Admin xác minh thật.
4. GET cùng prefix `/download`: binary attachment `.bin`, no-store/nosniff và X-Document-Mode=DEMO-UNSCANNED. Vẫn SIMULATED/NOT_VERIFIED; review status Operator không đổi.
5. Với file mới, thử Infected/ScannerUnavailable/OcrFailed; không download được. Sai scenario, thiếu xác nhận, chữ ký file sai, kích thước vượt giới hạn phải bị chặn.
6. Upload bản 2, xóa bằng DELETE `/compliance/demo/profile-documents/versions/{fileId}`, upload tiếp phải bản 3; không tái dùng số. Storage lỗi có thể giữ UploadFailed/Deleting để đối soát hoặc thử xóa lại; không báo thành công giả.
7. Driver quản GPLX của mình. Owner đang cộng tác chỉ đọc, không simulate/review/delete GPLX Driver. Kết thúc cộng tác mất quyền đọc ngay; Manager/Dispatcher/tổ chức khác bị chặn. Admin không mặc nhiên có quyền đọc giấy private.
8. Giấy xe theo scope tổ chức: A mất quyền sau khi gỡ; B không thấy file scope A, upload của B bắt đầu header/version riêng. Snapshot tài xế không chứa file/OCR.
9. Demo độc lập `/compliance/demo/documents`: POST multipart, GET list/detail, POST `/{id}/simulate`, POST `/{id}/review`, GET `/{id}/download`, DELETE `/{id}` theo cùng body. Owner account-only; không dùng endpoint này truy cập file gắn hồ sơ. List/version demo tối đa 100 bản mới nhất.
10. Tắt demo: route demo không xuất hiện; không bật được demo ngoài Development.

Kiểm tra thư mục lưu file mới:

| Loại | Vị trí kỳ vọng |
|---|---|
| Avatar Customer | `transconnect/development/avatars/customer/{AccountId}` |
| Avatar Operator | `transconnect/development/avatars/operator/{AccountId}` |
| Giấy hồ sơ S3 | `demo/profiles/{SubjectType}/{SubjectId}/{DocumentType}/{OrganizationId hoặc personal}/{FileId}/original` |
| Demo độc lập S3 | `demo/accounts/{AccountId}/{FileId}/original` |

Cloudinary RootFolder mặc định transconnect/development; đổi theo môi trường. UseDynamicFolders=true gửi asset_folder; fixed folder mode đặt false. Upload avatar lại giữ public ID theo account. ObjectKey cũ tiếp tục đọc/xóa được; không tự di chuyển/xóa file cũ khi đổi folder.

### 4.7 Quản trị tài khoản và RBAC — TC-CLOSURE-003, 006 / TC-IAM

1. InternalAdmin có users.manage GET `/accounts?pageNumber=1&pageSize=20&search=demo`, GET `/accounts/{id}`. Quản target InternalAdmin cần thêm admins.manage.
2. PUT `/accounts/{id}/status`, `{"isActive":false,"reason":"Khóa để test"}`. GET `/accounts/{id}/audit`: có lý do/trạng thái trước và sau/thời gian/requestId, không trả hash/token/raw JSON nội bộ. ActorAccountId lưu trong audit DB, chưa trả trong DTO này.
3. Dùng JWT cũ của account bị khóa gọi API bảo vệ → 401 ngay request tiếp theo; refresh cũng bị từ chối. Admin khác mở bằng isActive=true có reason; refresh cũ không được phục hồi. Tự khóa trả 409; admin thiếu quyền quản InternalAdmin trả 403.
4. Với quyền roles.manage: GET/POST `/roles`, GET/PUT/DELETE `/roles/{roleId}`. POST/PUT body theo Swagger. Với permissions.manage: GET/POST `/permissions`, DELETE `/permissions/{permissionId}`.
5. POST/DELETE `/roles/{roleId}/permissions/{permissionId}` và `/roles/{roleId}/users/{userId}`. Gỡ quyền rồi dùng **cùng JWT** gọi endpoint cần quyền → 403, không cần đăng nhập lại. /auth/me phản ánh DB.
6. Xóa mềm rồi tạo lại role/permission cùng tên/code không khôi phục liên kết cũ. Thay đổi RBAC phải có audit cùng transaction, chỉ field allowlist; không chứa secret/OTP/token. Test tên/code trùng và thao tác không đủ quyền.

### 4.8 Phân trang, context và cách ly dữ liệu — TC-CLOSURE-008

Test `/organizations/search`, `/drivers/search`, `/organizations/{id}/members/search`, `/organizations/{id}/drivers/search`, `/organizations/{id}/audit/search` và `/accounts`. Dùng pageNumber=1/pageSize=20 rồi trang tiếp; trang rỗng trả items rỗng, count theo tenant/quyền. PageSize ngoài 1–100 bị 400. Organization/Driver có search/status theo contract; không mặc định mọi list đều hỗ trợ toàn bộ bộ lọc. Đổi orgId sang tổ chức không có quyền phải bị từ chối, không lộ tên/liên hệ/count. Endpoint array cũ giữ tương thích.

## 5. Kịch bản nghiệm thu toàn giai đoạn qua FE

Thực hiện theo thứ tự bên dưới sau khi bốn terminal sẵn sàng. Các mục API tại phần 4 là hướng dẫn đối chiếu/lỗi biên của cùng kịch bản, không phải thay cho bước thử trên FE. Chưa có UI thì dùng Swagger và ghi rõ phương thức test. Không đánh dấu toàn giai đoạn pass chỉ vì đăng nhập thành công.

### 5.1 Chuẩn bị các phiên và dữ liệu

- Customer: Chrome của app cổng 5174, account C thuộc Customer.
- Driver: Chrome của app cổng 5175, account T thuộc Operator và không phải Owner.
- Management: mở cổng 5173 bằng các browser profile riêng cho Admin, Owner A, Owner B và nhân viên M/D. Không đổi account trong cùng phiên khi đang cần quan sát song song. Đổi account có chủ đích để test session ở cuối.
- Tạo mailbox test riêng cho mỗi người nhận; chuẩn bị ảnh avatar PNG/JPEG, ảnh/PDF giấy giả, MST và biển số chưa dùng. Account/email có thể tái sử dụng khi trạng thái phù hợp, nhưng luồng nộp mới cần hồ sơ mới.
- Mỗi dòng dưới ghi Pass/Fail/Blocked vào kết quả cuối file. Blocked vì thiếu SMTP/storage/DB chưa phải lỗi UI; vẫn ghi bước và thông báo nhận được.

### 5.2 Customer — tài khoản và quản lý phiên

| ID | Thao tác trên Customer FE | Kết quả cần thấy |
|---|---|---|
| E2E-C01 | Mở app → đăng ký C, thử thiếu tên/email sai/mật khẩu ngắn rồi nhập hợp lệ | Validation rõ, đăng ký thành công với nhóm Customer |
| E2E-C02 | Xác thực email bằng mã mailbox; thử sai mã, gửi lại và dùng mã mới | Sai mã có lỗi; mã hợp lệ xác thực được, không dùng lại mã cũ |
| E2E-C03 | Logout rồi login sai/đúng mật khẩu; tải lại trang và đóng/mở app | Sai bị từ chối; đúng giữ danh tính và phiên, không lẫn Driver đang mở |
| E2E-C04 | Tài khoản → sửa tên/SĐT → lưu → mở lại; đổi avatar và hủy chọn ảnh một lần | Dữ liệu lưu từ BE, avatar hiện sau reload, hủy chọn không upload |
| E2E-C05 | Đổi mật khẩu với mật khẩu hiện tại sai rồi đúng | Sai giữ phiên; đúng đưa về đăng nhập, mật khẩu cũ thất bại, mới thành công |
| E2E-C06 | Quên mật khẩu → đọc mã mailbox → reset → login mới | Reset hoàn tất, refresh cũ bị thu hồi; không in OTP/token trong log/UI |
| E2E-C07 | Mở các tab dịch vụ/đơn hàng | Thể hiện phạm vi chưa có đặt chuyến; không coi dữ liệu giới thiệu là booking thật |

### 5.3 Partner và Admin — hoàn thành hồ sơ tổ chức

| ID | Người/FE và thao tác | Kết quả cần thấy |
|---|---|---|
| E2E-O01 | Management: Operator A đăng ký/login/xác thực → /partner | Có trang nộp/theo dõi hồ sơ đúng account |
| E2E-O02 | A nhập tên tổ chức/MST sai rồi đúng và nộp | Chặn MST sai; hồ sơ Pending xuất hiện, Applicant là A |
| E2E-O03 | Admin → Tổ chức → tìm A → mở chi tiết → từ chối có lý do | A reload thấy Rejected và lý do |
| E2E-O04 | A sửa hồ sơ và nộp lại; Admin xem lịch sử rồi duyệt có note | Giữ ID/lịch sử, A trở thành OrganizationOwner mà không nhận lời mời cho chính mình |
| E2E-O05 | A mở workspace → Thông tin tổ chức → điền loại/địa chỉ/SĐT/email → lưu | Profile đủ thông tin, đóng/mở vẫn còn; MST/quyết định duyệt không bị đổi |
| E2E-O06 | Lặp nộp/duyệt với B ở phiên riêng | B chỉ thấy workspace/dữ liệu được phép của B; A không đọc chi tiết riêng của B |
| E2E-O07 | Swagger: Admin nộp hộ một hồ sơ legacy; Admin web sửa/nộp lại/gửi lại invitation khi phù hợp; người nhận dùng token email ở Partner | Nhánh legacy tạo Owner qua lời mời; tách rõ với nhánh Applicant |

### 5.4 Nhân viên, vai trò và quota

| ID | Thao tác | Kết quả cần thấy |
|---|---|---|
| E2E-M01 | Owner A → Nhân viên / lời mời → mời M làm Manager, D làm Dispatcher | Lời mời hiện trạng thái; mailbox đúng người nhận có token |
| E2E-M02 | M/D đăng ký hoặc login Operator, verified → Partner → nhập token nhận lời mời | Membership đúng tổ chức/role, không có các thao tác chỉ Owner |
| E2E-M03 | Owner đổi M sang Dispatcher, vô hiệu rồi kích hoạt lại; M tải lại/thao tác | Quyền thay đổi theo BE; vô hiệu mất truy cập, bật lại theo membership |
| E2E-M04 | Owner mời thêm người rồi thu hồi; người nhận thử token cũ | Không nhận được lời mời đã thu hồi; quota hiển thị phản ánh chỗ được giải phóng |
| E2E-M05 | Thử đủ quota nhân viên/Driver; đổi limit qua Swagger bằng Admin rồi reload Partner | Vượt quota bị chặn; counter/limit trên Partner khớp. **Chưa có form FE sửa quota** |
| E2E-M06 | Đọc audit tổ chức qua Swagger sau các thao tác | Actor/action/subject/thời gian/requestId hợp lệ; **chưa có màn audit tổ chức riêng trên web** |

### 5.5 Driver và cộng tác nhiều tổ chức

| ID | Người/FE và thao tác | Kết quả cần thấy |
|---|---|---|
| E2E-D01 | Driver app: T đăng ký Operator/xác thực → nộp tên tài xế | Pending, không tự cấp chức năng cần Approved |
| E2E-D02 | Admin web → Tài xế → tìm hồ sơ Pending → từ chối có note | Driver reload thấy Rejected và lý do |
| E2E-D03 | Driver sửa tên/nộp lại; Admin duyệt | DriverId giữ nguyên, lịch sử đủ lần nộp/duyệt; Driver thấy Approved |
| E2E-D04 | Driver → “Liên hệ, cộng tác và giấy tờ demo” → lưu SĐT/địa chỉ | Dữ liệu phục hồi sau đóng/mở; không tự đổi tên/SĐT của User |
| E2E-D05 | Owner A → Tài xế → mời T bằng email; Driver xem lời mời và nhập token mailbox | T nhận được cộng tác A, danh sách cả hai phía phản ánh BE |
| E2E-D06 | Owner B mời T; T nhận tiếp | Hai cộng tác cùng tồn tại, không biến T thành nhân viên hai tổ chức |
| E2E-D07 | Owner A xem liên hệ T; Manager/Dispatcher thử cùng nội dung | Owner xem được hiện hành; người không có quyền bị chặn/không có nút |
| E2E-D08 | Sau khi test giấy GPLX ở mục 5.7, kết thúc cộng tác A; Driver sửa liên hệ; A đọc lịch sử | A chỉ thấy snapshot khi kết thúc và không đọc GPLX; B còn cộng tác vẫn xem dữ liệu theo quyền |
| E2E-D09 | Driver sửa profile account/avatar/đổi hoặc reset password, như Customer | Đúng nhóm Operator, phiên và tài khoản độc lập Customer. Không dùng account Owner làm Driver |

### 5.6 Xe và quyền khai thác

| ID | Người/FE và thao tác | Kết quả cần thấy |
|---|---|---|
| E2E-V01 | Admin → chi tiết tổ chức A Approved → xác nhận liên kết xe, nhập biển số/căn cứ | Xe hiện ở workspace A; Owner không có quyền tự xác nhận link mới |
| E2E-V02 | Owner A → Phương tiện → sửa Passenger/số chỗ hoặc Freight/tải trọng | Dữ liệu đúng loại; thiếu/sai field bị chặn, lưu và reload không mất |
| E2E-V03 | Owner tạm ngưng xe; Admin thử liên kết cùng xe cho B | B chưa nhận được xe; tạm ngưng không phải kết thúc liên kết |
| E2E-V04 | Owner bỏ tạm ngưng; upload giấy xe ở mục 5.7; kết thúc xe có lý do | Lịch sử/lý do còn, thiếu lý do bị chặn; thao tác lặp không đổi mốc đầu |
| E2E-V05 | Admin xác nhận xe cho B; Owner B mở phương tiện/giấy demo | B quản được xe nhưng không thấy giấy scope A; A mất quyền quản hiện hành |

### 5.7 File — avatar, GPLX giả và đăng ký xe giả

| ID | Thao tác FE và kiểm tra ngoài FE | Kết quả cần thấy |
|---|---|---|
| E2E-F01 | Customer/Driver/Partner upload avatar; kiểm tra Cloudinary Media Library | Folder đúng customer/operator dưới RootFolder; upload lại giữ ID account, URL BE trả dùng được |
| E2E-F02 | Avatar sai định dạng/quá lớn hoặc provider không khả dụng | FE báo lỗi, avatar cũ giữ; không hiển thị thành công giả |
| E2E-F03 | Driver đã Approved → giấy GPLX giả → xác nhận dữ liệu giả → upload ảnh | Version mới PendingSimulation; list sau reload đúng, không tự approve Driver |
| E2E-F04 | Driver mô phỏng Success → duyệt demo → xem ảnh; upload thêm rồi xóa bản mới | Nội dung xem từ download private, version tăng; file cũ giữ, vẫn NOT_VERIFIED |
| E2E-F05 | Owner đang cộng tác xem GPLX từ trang tài xế; thử thao tác ghi bằng Swagger | Owner chỉ đọc, không simulate/review/delete GPLX. Sau kết thúc ở E2E-D08 phải mất đọc |
| E2E-F06 | Owner → Phương tiện → Giấy đăng ký xe demo → upload/simulate/review/download/delete | Hoạt động theo scope tổ chức và quyền Owner; Manager/Dispatcher không được quản file |
| E2E-F07 | Sau chuyển xe A → B, B upload giấy mới; kiểm tra bucket/DB | Scope mới riêng; đường dẫn đúng bảng folder ở phần 4.6, không lộ key/bucket ra API |
| E2E-F08 | Swagger: PDF, giới hạn 8 MiB, Infected/ScannerUnavailable/OcrFailed, demo độc lập và route khi tắt demo | Đúng kết quả phần 4.6; không yêu cầu mọi case có nút FE |

### 5.8 Admin, khóa tài khoản và quyền có hiệu lực ngay

| ID | Thao tác | Kết quả cần thấy |
|---|---|---|
| E2E-A01 | Admin → Tài khoản → tìm kiếm/phân trang → chi tiết C/T/M | Đúng dữ liệu, loading/error/empty rõ; history trạng thái theo trang |
| E2E-A02 | Admin khóa C có lý do trong lúc Customer đang login; Customer gọi thao tác tiếp | BE trả 401, phiên bị xử lý đúng; refresh không mở khóa được account |
| E2E-A03 | Admin mở C, C login lại | Login được; token refresh cũ vẫn mất hiệu lực, audit có cả hai thay đổi |
| E2E-A04 | Admin hạn chế thiếu admins.manage thử quản InternalAdmin; thử tự khóa | Không đủ quyền bị từ chối; tự khóa 409, không khóa nhầm người khác |
| E2E-A05 | Swagger gán/gỡ permission RBAC; giữ nguyên JWT và mở lại thao tác cần quyền | Quyền mất ngay request tiếp theo, FE không dựa vào role cũ để vượt quyền. **Chưa có màn CRUD role/permission riêng** |
| E2E-A06 | Thử vào URL Admin bằng Operator/Customer hoặc vào Partner bằng sai nhóm | Guard FE và BE chặn đúng; không chỉ ẩn nút mà bỏ kiểm tra server |

### 5.9 Kiểm tra cuối trên cả ba FE

- Refresh trang, đóng/mở app: account/profile/review/membership/cộng tác/file lấy lại đúng từ BE.
- Dừng BE tạm thời: lỗi mạng rõ, không báo danh sách rỗng/thành công giả; khởi động lại có thể thử lại.
- 403 không tự logout; logout/đổi account trong khi đang upload/download/refresh không làm dữ liệu phiên cũ xuất hiện trong phiên mới.
- Kiểm tra token hết hạn/refresh bằng test core/API; FE retry 401 tối đa theo session policy, không tự lặp mutation khi timeout/500.
- Thử màn hình hẹp và hủy chọn ảnh; yêu cầu đủ thao tác, không chấm mức độ đẹp UI.
- Đối chiếu mailbox và dữ liệu DB/storage cho email, audit, folder. Ghi riêng các test SQL concurrency/maintenance ở phần 6, vì thao tác UI thông thường không chứng minh được các điều kiện này.

**Điều kiện kết luận toàn giai đoạn:** các luồng FE áp dụng ở trên, các chức năng BE chưa có UI ở phần 4 và kiểm tra tích hợp ở phần 6 có kết quả ghi nhận. Phần chưa chạy phải đánh dấu Blocked/Chưa thử kèm lý do. Các module ngoài phạm vi ở phần 1 không được tính đã hoàn thành.

## 6. Kiểm tra tích hợp kỹ thuật

<a id="sql-concurrency"></a>
### SQL Server và tranh chấp đồng thời

Dùng DB test và hai request/connection thực tế. Các test SQLite không thay nghiệm thu SQL Server:

- Hai refresh đồng thời: tối đa một successor; replay giữ metadata Replaced/Replay.
- Hai request dùng cùng OTP: chỉ một lần thực hiện; reset/verify thất bại phải rollback đồng bộ. Reset/change-password/login/khóa account dùng thứ tự khóa User nhất quán, không phát refresh sau khi account đã khóa hoặc hash đã đổi (TC-CLOSURE-011).
- Nhận Owner và đăng ký Driver đồng thời trên cùng account: không tạo trạng thái kiêm cả hai; một Owner hoạt động/tổ chức. Hai accept/quota/link xe không vượt giới hạn hoặc tạo trùng quan hệ.
- Hai upload giấy đồng thời: version khác nhau. Kiểm tra migration history/index/CHECK/FK; preflight phát hiện dữ liệu xung đột thì dừng, không tự sửa dữ liệu cũ.
- Kết thúc cộng tác/xe lặp lại giữ snapshot/lý do/actor/thời gian đầu tiên, không thêm audit kết thúc trùng.

### Notification, audit và maintenance — TC-NOTIFICATION / Stage2MaintenanceTests

- Register/reset/lời mời tạo email có SourceType/SourceId/DeliverBefore phù hợp. Đọc OTP/token từ mailbox test. Sent phải dọn BodyHtml/BodyText, có TerminalAt/BodyPurgedAt.
- OTP đã dùng/hết hạn/bị thay hoặc invitation thu hồi/đã xử lý trước lúc gửi: ngừng gửi/retry và dọn body. Email thường hết TTL 24 giờ cũng dừng; LastError không lộ OTP/link/secret.
- Thử SMTP lỗi, max attempts, cancellation, Sending kẹt và hai worker: một lease hiện hành; worker mất lease không ghi đè. Crash sau SMTP có thể gửi trùng, không khẳng định exactly-once.
- Audit giữ 6 tháng lịch; metadata email OTP 7 ngày, loại khác 30 ngày; job batch không dọn email đang Sending. File legacy đúng tên được dọn, file không liên quan giữ nguyên.
- Rà tham chiếu tài khoản chỉ cảnh báo ID mồ côi, không tự sửa/xóa; account khóa/xóa mềm còn tồn tại không bị coi là mồ côi. Demo tắt không truy vấn bảng chưa migrate. Xóa vật lý User qua service/repository bị chặn.

## 7. Bằng chứng tự động hiện có

| Bộ kiểm tra | Kết quả gần nhất đã ghi nhận | Giới hạn |
|---|---|---|
| Backend toàn bộ trước thay folder | 155/155 pass, 0 skip; foundation-closure-final.trx | Không phải full run sau thay folder |
| Backend liên quan folder/avatar/Compliance | 28/28 pass, 0 skip; storage-folders.trx | Đã build code mới; thêm case fixed/dynamic folder, dùng provider giả |
| Management web | Check/format/lint/TypeScript/build pass, 16 test pass | Chưa browser E2E với BE thật |
| Customer app | Analyze sạch, 19 test pass | Chưa native/device E2E |
| Driver app | Analyze sạch, 16 test pass | Chưa native/device E2E |

TRX nằm trong Backend `.codex-build/test-results/`. Lượt viết lại tài liệu này không chạy lại test hoặc apply migration. Dependency warnings và rate limit còn được theo dõi trong tracker. Không suy kết quả tự động thành nghiệm thu SQL/SMTP/Cloudinary/S3 thật.

## 8. Ghi nhận lượt test của bạn

- Ngày, môi trường và phiên bản code:
- Nhóm tài khoản/FE đã thử:
- Các mục/bước đã pass:
- Bước lỗi, request/response đã che token/secret, kết quả mong đợi và thực tế:
- Phần chưa thử vì thiếu cấu hình:
- Kết luận nghiệm thu: **CHƯA XÁC NHẬN**.

Sau mỗi lượt, ghi kết quả xác nhận vào [tracker](../../TransConnect-Docs/00-project/progress-tracker.md). Giữ file này làm hướng dẫn hiện hành và cập nhật tại chỗ khi chức năng thay đổi.
