# Test hiện hành — Identity, Operator, Compliance demo và refactor Notification

Luồng controller/service/DTO đối chiếu ngày 15/10; cập nhật refactor và tổ chức tài liệu ngày 17/10/2026. Tài liệu này mô tả API đã có để test thủ công từ đầu; các yêu cầu nghiệp vụ đang thiết kế vẫn giữ trạng thái riêng. Dữ liệu mẫu chỉ dành cho database phát triển.

## 0. Lượt test đang chờ xác nhận

Đây là **file test thủ công duy nhất**. Cập nhật trực tiếp theo yêu cầu mới nhất, không tạo test flow theo ngày. Chủ dự án chưa xác nhận đã test xong; chỉ xóa khi có xác nhận và đã ghi kết quả ngắn vào [tracker](../../TransConnect-Docs/00-project/progress-tracker.md#test-evidence).

**Phần mới 17/10:** NotificationMessage/EmailMessage, enum chuyển Enums. Build Release và 94/94 test local đã pass; chưa SMTP/SQL Server/storage live. Refactor không đổi bảng hoặc yêu cầu migration mới; R-15 và chính sách bắt buộc verify trước login chưa được triển khai.

```powershell
dotnet build TransConnect.sln --configuration Release
dotnet test TransConnect.Tests/TransConnect.Tests.csproj --configuration Release --no-build --no-restore
```

Máy nhiều SDK: chọn executable .NET 10 thực tế và DOTNET_ROOT phù hợp hoặc dùng scripts/Test-Core.ps1. Kiểm tra NotificationPersistenceTests (5 ca) và luồng register → queue → worker File → verify; trạng thái vẫn 0/1/2/3, audit/soft-delete hoạt động. Không yêu cầu body tự bị xóa vì R-15 chưa code.

**Xác nhận của bạn:**
- Ngày / môi trường:
- Phạm vi đã test:
- Kết quả / lỗi còn lại:
- Đã test xong và cho phép xóa file này: CHƯA XÁC NHẬN.

## 1. Cách dùng

Thực hiện từng bước dưới đây bằng Swagger hoặc công cụ HTTP. [125 request đầy đủ](#request-samples) và [response mẫu](#response-samples) nằm ngay trong file này. Có thể chép riêng khối HTTP ra file tạm local để dùng REST Client; không tạo thêm bản hướng dẫn test được quản lý trong repo.

Gửi từng request; có bước cần nhập OTP/token từ email, nhánh lỗi, refresh/replay và cleanup. Thay runId mỗi lượt để dữ liệu không trùng. Tự chép ID/token từ response thành công vào biến local; không ghi token/credential thật vào tài liệu.

## 2. Chuẩn bị môi trường

1. Dùng SQL Server database phát triển đúng `ConnectionStrings:Default`, .NET 10. File local `appsettings.Development.json` có JWT key riêng và `Email:Provider=File`. Giữ cấu hình local đang có.
2. Có thể test Identity/Operator khi `ComplianceDemo:Enabled=false`; không cần Docker/AIStor lúc này.
3. Để test Compliance: làm theo [setup](reference/compliance-demo-setup.md), đặt license, chạy AIStor, tạo bucket private `transconnect-demo-unscanned`, cấp access key ứng dụng và bật ComplianceDemo trong Development. Tạm dừng container ClamAV để giảm RAM.
4. Từ thư mục Backend:

```powershell
& C:/Users/PC/.dotnet/dotnet.exe restore TransConnect.sln
& C:/Users/PC/.dotnet/dotnet.exe run --project TransConnect.Host --launch-profile http
```

Base URL `http://localhost:5267`; Swagger `/swagger`. Có thể dùng `https://localhost:7218` với profile `https` và chứng chỉ local tin cậy. Không thêm `/api` hoặc version. Host Development migrate Identity/Notification/Operator và Compliance khi bật demo; xác nhận database phát triển trước khi chạy.

Body JSON dùng `Content-Type: application/json`. Endpoint cần đăng nhập dùng `Authorization: Bearer <accessToken>`. Trong Swagger phải đổi Authorize khi chuyển Customer/Owner/Manager/Driver/Admin. API hiện trả **200** cho create thành công; upload demo cũng 200. Download thành công là binary, lỗi vẫn theo envelope JSON.

## 3. Tài khoản và biến cần giữ

| Tên | Nhóm | Dữ liệu mẫu / mục đích |
|---|---|---|
| Customer A | Customer | `qa-customer-{{runId}}@example.com`, test auth/RBAC |
| Owner A | Operator | `qa-owner-{{runId}}@example.com`, chủ tổ chức A |
| Manager A | Operator | `qa-manager-{{runId}}@example.com`, nhân viên tổ chức A |
| Dispatcher A | Operator | `qa-dispatcher-{{runId}}@example.com`, nhân viên hạn chế quyền |
| Driver | Operator | `qa-driver-{{runId}}@example.com`, tài xế cộng tác |
| Owner B | Operator | `qa-owner-b-{{runId}}@example.com`, tổ chức B / kiểm tra chéo |
| Admin | InternalAdmin | Dùng admin của database test; không đăng ký công khai |

Mật khẩu giả lập mẫu `Demo123456`, reset mẫu `Demo654321`. Admin email/password lấy từ cấu hình/seed của DB test (xem `IdentitySeeder` nếu là DB mới), không suy ra credential của DB đang dùng. Điền `adminEmail/adminPassword` cục bộ.

Lưu biến riêng: `customerToken/customerRefresh/customerId`, `ownerToken/ownerId`, `managerToken/managerId`, `dispatcherToken`, `driverToken/driverId`, `ownerBToken`, `adminToken`, `orgId/orgBId`, `managerMemberId`, `invitationId`, `engagementId`, `vehicleId/linkId`, `roleId/permissionId/rolesManagePermissionId`, `documentId` và ID từng nhánh demo. `driverId` lấy từ `/drivers`, không dùng AccountId thay thế. `linkId` khác `vehicleId`; `memberId` khác AccountId. ID lời mời qua email có thể khác ID engagement sau accept.

## 4. Auth: đăng ký -> xác thực -> đăng nhập

Thực hiện nhóm request **01 Identity setup** trong phụ lục request.

1. Register Customer, Owner, Manager, Dispatcher, Driver và Owner B theo DTO tương ứng; 200, `data.accountId` và `userId` cùng giá trị, token riêng từng account. Mỗi user mới có role User, Driver không phải nhóm/role đăng ký riêng.
2. Đọc email **email-verification** đúng người nhận trong `TransConnect.Host/sent-emails/`. Worker gửi theo chu kỳ; dùng OTP mới nhất. Email khác nhau/nhóm khác nhau không dùng chung OTP.
3. Điền `ownerOtp/managerOtp/dispatcherOtp/driverOtp/ownerBOtp/customerOtp`. Gọi `verify-email` tương ứng; 200, `data={}`. `/auth/me` xác nhận `emailVerified=true`.
4. Login mỗi account để lưu token mới. Login admin với `accountGroup=InternalAdmin`.
5. `/auth/me` có accountGroup/accountId/userId/email/fullName/phoneNumber/avatarUrl/emailVerified/roles/permissions, không passwordHash/GoogleId/token.

DTO đăng ký đầy đủ:

```json
{
  "accountGroup": "Operator",
  "email": "qa-owner-20261015a@example.com",
  "password": "Demo123456",
  "fullName": "Chủ nhà xe QA",
  "phoneNumber": "0900000001"
}
```

DTO login:

```json
{ "accountGroup": "Operator", "email": "qa-owner-20261015a@example.com", "password": "Demo123456" }
```

DTO xác thực/resend:

```json
{ "accountGroup": "Operator", "email": "qa-owner-20261015a@example.com", "code": "<OTP-mới-nhất>" }
```

```json
{ "accountGroup": "Operator", "email": "qa-owner-20261015a@example.com" }
```

Ca lỗi: register 5 ký tự -> 400; thiếu/email sai -> 400; trùng email cùng nhóm -> 409; login sai password/nhóm -> 401; verify sai/hết hạn/dùng lại OTP -> 401. Register cùng email **khác Customer/Operator** được accountId khác. Bỏ accountGroup mặc định Customer; không tự dò nhóm từ email. Chưa verify vẫn login được, nhưng Operator accept/quản trị yêu cầu email đã verify. Register InternalAdmin hoặc group Driver -> 400. OTP giới hạn dùng/xin mã xem `Verification` của môi trường, không spam để đo.

Google là nhánh tùy chọn: DTO `{"accountGroup":"Customer","idToken":"<Google-ID-token-thật-đúng-audience>"}`. Chỉ test khi cấu hình Google ClientId; token giả không phải ca thành công. InternalAdmin không được Google login.

## 5. Tổ chức A/B và Owner

Thực hiện nhóm request **02 Organizations**.

1. `POST /organizations` **không cần JWT**, nộp A và B, `ownerEmail` trùng email Operator tương ứng. Lưu orgId/orgBId. DTO hiện chỉ gồm ba trường bên dưới; Address/ContactPhone/OrganizationType chưa được triển khai trong luồng này.

```json
{ "name": "Nhà xe QA A", "registrationNumber": "QA-20261015A-A", "ownerEmail": "qa-owner-20261015a@example.com" }
```

2. Admin `GET /organizations` thấy hồ sơ Pending. Admin `POST /organizations/{id}/review`:

```json
{ "approved": true, "note": "Đã kiểm tra hồ sơ giả lập QA" }
```

3. Review Approved tự gửi lời mời Owner. **Token nằm trong email, không trả plaintext trong response/API**. Điền `ownerInvitationToken/ownerBInvitationToken` từ email đúng người nhận. Sau commit, Owner tương ứng `POST /organizations/invitations/accept`:

```json
{ "token": "<token-lời-mời-owner-từ-email>" }
```

4. 200, Member role OrganizationOwner. Owner `GET /organizations` và `/organizations/{orgId}` thấy A. Owner A đọc B -> 403. Hồ sơ Pending có thể chưa được Owner đọc trước accept; Admin dùng để theo dõi.
5. Admin `POST /organizations/{id}/owner-invitation` (body rỗng) chỉ để gửi lại trước khi Owner accept; mã cũ bị vô hiệu. Sau có Owner -> 409. Review lại hồ sơ đã review -> 409.
6. Nhánh Rejected dùng **hồ sơ thứ ba** với registrationNumber riêng và `{"approved":false,"note":"Thiếu hồ sơ QA"}`; không dùng nhánh này cho A/B đang chạy flow.

## 6. Nhân viên, quota và quyền theo tổ chức

Thực hiện nhóm request **03 Members and quotas**. Mặc định EmployeeLimit=3 tính Owner + active members + pending invitations còn hạn.

1. Owner A mời Manager và Dispatcher:

```json
{ "email": "qa-manager-20261015a@example.com", "role": "Manager" }
```

```json
{ "email": "qa-dispatcher-20261015a@example.com", "role": "Dispatcher" }
```

2. API trả InvitationResponse `{id,expiresAt}`. Lưu invitation ID khi cần revoke; lấy token từ email, rồi **đổi sang JWT người nhận** và accept bằng AcceptInvitationRequest. Lưu memberId từ response accept.
3. Owner/Admin `GET /organizations/{orgId}/members`. Manager/Dispatcher gọi endpoint này -> 403 trong code hiện tại; danh sách nhân viên là quyền Owner. Manager được quản lý xe/tài xế; Dispatcher được đọc xe.
4. Owner đổi Manager thành Dispatcher:

```json
{ "role": "Dispatcher", "isActive": true }
```

Manager cũ bị chặn các thao tác manage ngay khi request mới đọc membership DB, không cần chờ JWT hết hạn. Đổi lại role Manager để tiếp tục.
5. Vô hiệu nhân viên bằng `{"role":"Manager","isActive":false}`; quyền org mất. Bật lại phải còn chỗ quota. Không dùng request này để đổi/vô hiệu Owner -> 409.
6. Mời nhân viên thứ tư khi quota 3 đã đủ -> 409. Admin `PUT /organizations/{orgId}/employee-limit` với `{"limit":4}`, mời lại -> 200. Owner không tự sửa quota -> 403. Limit thấp hơn occupancy -> 409.
7. Owner/Admin `DELETE /organizations/{orgId}/invitations/{invitationId}` (không body) hủy lời mời còn pending; accept mã bị hủy -> 401. Lời mời đã accept -> 409 khi revoke. Có thể dùng lời mời thứ tư để kiểm thử.
8. Một account không nhận membership tổ chức thứ hai, kể cả membership cũ đã inactive -> 409. Engagement Driver là quan hệ riêng, không bị giới hạn theo membership này.
9. Owner/Admin `GET /organizations/{orgId}/audit`: thấy member.invite/member.accept/member.update/revoke...; không token plaintext.

## 7. Driver: hồ sơ -> review -> mời -> accept -> nhiều tổ chức

Thực hiện nhóm request **04 Drivers**.

1. Với JWT Driver Operator đã verify: `POST /drivers` `{"fullName":"Tài xế QA"}`; 200 Pending, lưu driverId. `GET /drivers/me` thấy đúng account; một account chỉ một hồ sơ Driver.
2. Admin `GET /drivers/pending`; `POST /drivers/{driverId}/review` dùng ReviewRequest `{"approved":true,"note":"Đã kiểm tra hồ sơ tài xế QA"}`. Driver không tự review -> 403.
3. Owner/Manager/Admin `POST /organizations/{orgId}/drivers/{driverId}/invitations` **không body**. Lưu invitation `id` và lấy mã từ email. Driver `POST /drivers/invitations/accept` với `{"token":"<token-driver>"}` -> engagement AcceptedAt, lưu engagementId.
4. Dùng Owner B mời cùng Driver vào B, Driver accept -> cộng tác cả A và B. Driver không có membership gọi list xe A -> 403; cộng tác không cấp quyền nhân viên.
5. Owner/Manager/Admin `GET /organizations/{orgId}/drivers`: thấy engagement và lịch sử. Driver hoặc quản lý phù hợp `DELETE /organizations/{orgId}/drivers/{engagementId}` -> kết thúc, không xóa hồ sơ Driver.
6. Nhánh mời trước đăng ký: `POST /organizations/{orgId}/driver-invitations` với `{"email":"qa-driver-new-20261015a@example.com"}`. Sau đó người nhận register Operator -> verify -> submit Driver -> Admin review -> accept bằng **/drivers/invitations/accept**. Chưa có hồ sơ/Approved -> 409, token chưa bị consume; token Driver gửi sang accept Member -> 401.
7. Admin `PUT /organizations/{orgId}/driver-limit` với `{"limit":1}`; engagement chưa kết thúc và pending invitation còn hạn tính quota. Vượt -> 409. `{"limit":null}` bỏ giới hạn. Driver quota không cộng vào EmployeeLimit.

Owner có thể submit hồ sơ Driver trên chính account Operator của mình, được Admin duyệt rồi được mời/accept như flow trên. Hiện chưa có API tự nhận cộng tác bỏ qua lời mời. Người vừa member vừa Driver được tính ở cả hai quota.

## 8. Xe: gán -> tạm ngừng -> kết thúc -> sang B

Thực hiện nhóm request **05 Vehicles**, sau các bước đọc/quản lý Driver nếu không muốn thay quota giữa flow.

1. Admin `POST /organizations/{orgId}/vehicles`:

```json
{ "registrationPlate": "51A-123.45", "description": "Xe QA", "verificationReference": "Hồ sơ giả lập QA đã kiểm tra quyền khai thác" }
```

Lưu vehicleId và linkId. Đổi biển số mỗi lượt nếu lịch sử trước còn liên kết. Operator tự gán quyền khai thác -> 403.
2. Owner/Manager/Dispatcher `GET /organizations/{orgId}/vehicles` -> 200; account thuộc B không có quyền A -> 403.
3. Owner/Manager `PUT /organizations/{orgId}/vehicles/{linkId}/suspension` `{"isSuspended":true}`. B gán cùng biển số lúc A tạm ngừng vẫn -> 409. Dispatcher tạm ngừng xe -> 403. Khôi phục `{"isSuspended":false}`.
4. Owner/Manager/Admin `DELETE /organizations/{orgId}/vehicles/{linkId}` không body -> EndedAt. **EndReason/EndedBy chưa có trong API hiện tại**, không thêm body để kỳ vọng lưu hai trường đó.
5. Admin gán cùng biển số cho B -> vehicleId cũ, linkId mới; lịch sử A còn nguyên.

Guard nghĩa vụ vận tải đã có nhưng adapter Foundation hiện trả không có nghĩa vụ vì chưa có Trip/Booking. Ca “còn nghĩa vụ -> 409” dùng adapter thử nghiệm trong automated tests, chưa thể tạo qua API vận tải thật.

## 9. Compliance demo: upload -> mô phỏng -> review -> download/delete

Thực hiện nhóm request **06 Compliance demo**; cần AIStor thật, `Enabled=true`, Development. Bất kỳ account đang hoạt động có thể test hồ sơ demo của chính mình; ví dụ dùng Owner.

1. Chuẩn bị một PNG/JPEG/PDF **giả lập** <=8 MiB, chọn vào multipart `File`; `SyntheticDataConfirmed=true`. Khi dùng mẫu HTTP trong phụ lục, sửa `sampleFile` sang đường dẫn file local. Nếu dùng công cụ Postman, chọn file ở Body/form-data. Đây không phải JSON base64.
2. `POST /compliance/demo/documents` -> PendingSimulation, NOT_SCANNED; lưu documentId. List/get chỉ thấy file của account hiện tại. Get/download bằng Manager/Customer khác -> 404.
3. Trước mô phỏng download -> 403. Mô phỏng:

```json
{ "scenario": "Success" }
```

4. Kết quả DemoNeedsReview, SIMULATED_CLEAN và OCR text cố định, `processingMode=SIMULATED`, `verification=NOT_VERIFIED`. Không gọi ClamAV/OCR thật, không chuyển file sang clean bucket.
5. Chủ file `POST /compliance/demo/documents/{id}/review` với `{"approved":true}` -> DemoApproved, vẫn NOT_VERIFIED. Tạo file Success riêng nếu muốn test `{"approved":false}` -> DemoRejected, download bị chặn.
6. Download trước/sau DemoApproved -> attachment `.bin`, octet-stream, no-store, `X-Document-Mode: DEMO-UNSCANNED`; SHA-256/size đối chiếu metadata. Đổi file trên storage -> 409 khi hash/size lệch.
7. Upload file mới cho mỗi nhánh bên dưới; bộ request tự lưu ID riêng nếu dùng từng upload tương ứng:

| scenario | status | scanResult | Download |
|---|---|---|---|
| Infected | DemoBlocked | SIMULATED_INFECTED | 403 |
| ScannerUnavailable | DemoFailed | SIMULATED_UNAVAILABLE | 403 |
| OcrFailed | DemoFailed | SIMULATED_CLEAN | 403 |

8. Scenario ngoài danh mục -> 400. Review khi chưa Success -> 409; bỏ approved -> 400. Simulate cùng scenario đã chạy -> trả kết quả cũ; đổi scenario trên cùng document -> 409. Hai cập nhật stale revision -> 409 cho bản thua.
9. DELETE -> xóa S3 object, soft-delete metadata và xóa OCR mẫu; get -> 404. Delete storage lỗi -> 503, status Deleting, retry delete sau khi storage phục hồi.
10. Tắt AIStor để test upload lỗi -> 503, không giả thành công; bật lại rồi dọn UploadFailed/Uploading qua API. Tắt ComplianceDemo/restart -> routes 404. Bật trên Production/Staging -> startup từ chối. Không thực hiện ca tắt dịch vụ trong lúc người khác đang test.

## 10. RBAC: role/permission độc lập membership tổ chức

Thực hiện nhóm request **07 RBAC**. Dùng Customer A để tránh ảnh hưởng membership đang test.

1. Customer gọi GET /roles -> 403. Admin GET /roles, /permissions -> 200.
2. Admin tạo role và permission:

```json
{ "name": "QAReader-20261015a", "description": "Vai trò QA" }
```

```json
{ "code": "qa.read.20261015a", "description": "Quyền QA" }
```

3. Lưu roleId/permissionId; GET /roles/{roleId}; PUT cùng id bằng UpdateRoleRequest `{"name":"QAReaderUpdated-20261015a","description":"Đã cập nhật QA"}`. Trùng name/code đang hoạt động -> 409.
4. POST /roles/{roleId}/permissions/{permissionId} và /roles/{roleId}/users/{customerId}, không body. Customer `/auth/me` thấy role/permission DB mới.
5. Trong GET /permissions, tìm ID có code `roles.manage` và điền rolesManagePermissionId. Gán quyền này vào role QA, login Customer lại để JWT mới có quyền; GET /roles -> 200. Quyền `qa.read.*` tự tạo chưa có endpoint dùng nó.
6. Admin DELETE /roles/{roleId}/permissions/{rolesManagePermissionId}. Customer login lại -> GET /roles 403; JWT đã cấp trước đó giữ claim đến khi hết hạn. Không dùng RBAC claim để kiểm tra quyền member của Operator.
7. Bỏ permission QA khỏi role, bỏ user khỏi role; DELETE permission và role QA. Tạo lại cùng tên/code -> ID mới, không nhận lại liên kết cũ. Chỉ cleanup role/permission QA.

## 11. Refresh, logout, reset — làm cuối để không mất token các flow trước

Thực hiện nhóm request **08 Sessions and reset**.

1. Customer login để có refresh R1. POST /auth/refresh body `{"refreshToken":"<R1>"}` -> 200, nhận access và R2 mới, phải thay cả hai. Tự cập nhật customerToken/customerRefresh từ response thành công.
2. Lưu R1 vào `replayedRefresh` **trước refresh** nếu muốn thử replay. Gửi R1 lần nữa -> 401 và thu hồi refresh của account; R2 cũng -> 401. Login lại để tiếp tục.
3. POST /auth/logout cần JWT Customer và DTO RefreshTokenRequest đúng account -> 200; refresh token đó -> 401. Access JWT vẫn sống đến hết hạn. Gửi refresh của Owner dưới JWT Customer không được thu hồi phiên Owner; kiểm tra Owner vẫn refresh được bằng token riêng.
4. Login lại Customer hai lần, giữ refresh của hai phiên. POST /auth/forgot-password `{"accountGroup":"Customer","email":"qa-customer-20261015a@example.com"}` -> 200 kể cả email không tồn tại. Đọc OTP **password-reset** từ email, điền resetOtp.
5. Reset DTO đầy đủ:

```json
{ "accountGroup": "Customer", "email": "qa-customer-20261015a@example.com", "code": "<OTP-reset>", "newPassword": "Demo654321" }
```

6. 5 ký tự -> 400 (OTP chưa dùng); từ 6 ký tự -> 200, login mật khẩu mới được, cũ -> 401; email được verify, refresh các phiên trước reset -> 401. OTP cũ dùng lại -> 401.
7. Resend/forgot trả cùng thông điệp ở email không tồn tại/giới hạn xin mã; không kết luận không gửi email là lỗi nếu đang cooldown. Sau reset, sửa `testPassword` hoặc dùng request login resetPassword để tiếp tục đúng mật khẩu.

## 12. Email và kiểm thử đồng thời

`POST /dev/test-email?to=qa-notify-{{runId}}@example.com`, không body, chỉ Development. Đây là endpoint hỗ trợ test và không cần JWT. Kiểm tra file HTML/TXT, tên người dùng được HTML encode, trạng thái queue/retry. SMTP/Google thật là nghiệm thu riêng. Nội dung OTP/email là dữ liệu nhạy cảm local, không commit cùng kết quả test.

Với hai client song song và dữ liệu mới mỗi lần: register cùng email/group (200/409); hai refresh cùng R1 (một thành công, một 401/replay có thể thu hồi token kế tiếp); hai reset cùng OTP (200/401); hai accept cùng invitation (200/401); hai lời mời khi còn một chỗ (200/409); hai tổ chức gán cùng xe (200/409); hai simulation khác nhau trên bản pending (một kết quả thắng, còn lại 409). Xác nhận DB sau commit. SQLite automated tests không thay nghiệm thu locking SQL Server thật.

## 13. DTO request: trường và giới hạn

JSON mẫu của **mọi request DTO public** nằm trong phụ lục request/response trong file này, kể cả request không có body. Quy tắc cần nhớ:

| DTO | Trường / giới hạn |
|---|---|
| RegisterRequest | accountGroup Customer/Operator; email <=256; password 6–100; fullName <=200; phoneNumber tùy chọn <=20 |
| LoginRequest | accountGroup Customer/Operator/InternalAdmin, email, password |
| GoogleLoginRequest | accountGroup, idToken; InternalAdmin bị service từ chối |
| RefreshTokenRequest | refreshToken bắt buộc; dùng cho refresh/logout |
| VerifyEmailRequest | accountGroup, email <=256, code <=10 |
| ResendVerificationRequest / ForgotPasswordRequest | accountGroup, email <=256 |
| ResetPasswordRequest | accountGroup, email <=256, code <=10, newPassword 6–100 |
| CreateRoleRequest / UpdateRoleRequest | name <=100; description tùy chọn <=500 |
| CreatePermissionRequest | code <=150; description tùy chọn <=500 |
| SubmitOrganizationRequest | name <=200, registrationNumber <=100, ownerEmail <=256 |
| ReviewRequest (Organization/Driver) | approved bool, note bắt buộc <=2000 |
| InviteMemberRequest | email <=256, role Manager/Dispatcher |
| AcceptInvitationRequest (Member/Driver) | token bắt buộc <=128 |
| UpdateMemberRequest | role Manager/Dispatcher, isActive bool |
| EmployeeLimitRequest | limit 1–100000 |
| DriverLimitRequest | limit null hoặc 1–100000 |
| SubmitDriverRequest | fullName <=200 |
| InviteDriverRequest | email <=256 |
| LinkVehicleRequest | registrationPlate 4–20 ký tự chữ/số/dấu cách/chấm/gạch; description <=1000; verificationReference bắt buộc <=1000 |
| SuspendVehicleRequest | isSuspended bool |
| UploadDemoDocumentRequest | multipart File PNG/JPEG/PDF <=8 MiB; SyntheticDataConfirmed=true |
| SimulateDocumentRequest | scenario Success/Infected/ScannerUnavailable/OcrFailed |
| ReviewDemoDocumentRequest | approved bool bắt buộc, thiếu/null -> 400 |

Bool không nullable của một số DTO có thể mặc định false khi bỏ field; gửi đầy đủ field mẫu để tránh vô tình reject/vô hiệu. Các hạn chế quyền/nghiệp vụ ở service vẫn áp dụng ngoài validation field.

## 14. Response và báo cáo kết quả

[response-examples.json](#response-samples) có đủ AuthResponse, CurrentUserResponse, RoleResponse, PermissionResponse, OrganizationResponse, MemberResponse, InvitationResponse, DriverResponse, EngagementResponse, VehicleResponse, AuditResponse, DemoDocumentResponse, envelope rỗng, validation/error, list và mô tả download. DemoDownloadResponse là kết quả service nội bộ; controller chuyển Content sang binary và FileName sang attachment, không trả base64 JSON cho endpoint download.

Mỗi lần test ghi runId, commit, baseUrl, DB test, actor, endpoint, expected/actual status, request đã che bí mật và ID liên quan. Không ghi token, OTP, license hoặc ảnh giấy tờ vào báo cáo Git. Có thể dùng bảng:

| Ca | Actor | API | Mong đợi | Thực tế | Kết luận |
|---|---|---|---|---|---|
| MANUAL-AUTH-001 | Customer | register/login/me | 200, đúng nhóm | Chưa chạy | Pending |
| MANUAL-ORG-001 | Admin/Owner | nộp/review/accept | Approved + Owner | Chưa chạy | Pending |
| MANUAL-MEMBER-001 | Owner/Manager | mời/accept/đổi quyền | quota/quyền đúng | Chưa chạy | Pending |
| MANUAL-DRIVER-001 | Driver/Admin | hồ sơ/review/cộng tác A/B | không cấp quyền member | Chưa chạy | Pending |
| MANUAL-VEHICLE-001 | Admin/Manager | gán/tạm ngừng/kết thúc | lịch sử giữ đúng | Chưa chạy | Pending |
| MANUAL-DEMO-001 | Owner | upload/simulate/review/download | SIMULATED/NOT_VERIFIED | Chưa chạy | Pending |
| MANUAL-RBAC-001 | Admin/Customer | gán/thu hồi/xóa tạo lại | token mới đúng quyền | Chưa chạy | Pending |
| MANUAL-SESSION-001 | Customer | refresh/logout/reset | rotation/revoke đúng | Chưa chạy | Pending |

Test tự động gần nhất 94/94 pass, xem [traceability](../../TransConnect-Docs/00-project/progress-tracker.md#test-evidence). Tài liệu/request mẫu không phải bằng chứng đã test SQL Server/AIStor/SMTP/Google/R2 thật. Luồng production scan/OCR, hồ sơ pháp lý và Trip/Booking vẫn thuộc các mốc sau.


<a id="sql-concurrency"></a>
## 15. Nghiệm thu bổ sung trên SQL Server



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


### Đồng thời ở Operator


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

### Worker, cấu hình và Google


- **File worker:** `POST /dev/test-email?to=qa@example.com` ở Development → 200 với messageId; sau chu kỳ PollSeconds có HTML và trạng thái Sent. Endpoint không tồn tại ngoài Development. Mã `123456` trong email mẫu không phải OTP được lưu cho tài khoản.
- **Retry/Failed:** trên instance/database test riêng, cấu hình SMTP thử nghiệm hợp lệ về hình thức nhưng không kết nối được; enqueue email → AttemptCount tăng, NextAttemptAt theo backoff; đủ MaxAttempts → Failed. Khôi phục provider File sau test. Đây là kiểm thử thủ công, chưa nằm trong suite tự động.
- **SMTP thật:** chỉ dùng hộp thư test và credential của môi trường; kiểm tra thư đến/spam, người gửi, nội dung. Không coi File provider là bằng chứng SMTP hoạt động.
- **Startup:** cấu hình Jwt key quá ngắn, Verification.CodeLength=10, hoặc thông số Email không hợp lệ phải làm startup thất bại trước migrate/seed; khôi phục từng cấu hình sau test. Google ClientId rỗng không cản startup.
- **Google:** khi ClientId rỗng, `POST /auth/google` với `{ "idToken": "invalid" }` → 401. Token sai khi có ClientId cũng bị từ chối. Google đăng nhập thật cần ID token của client đúng audience; kiểm thử riêng khi đã tích hợp frontend, chưa được chứng minh bằng suite hiện tại.
- **Khóa/xóa tài khoản:** chưa có UsersController; chỉ thay IsActive/DeletedAt bằng fixture hoặc công cụ DB trên user test. `/auth/me` và refresh phải 401; khôi phục dữ liệu sau thử nghiệm.
- **CORS:** Development rỗng cho origin bất kỳ; khi khai whitelist chỉ origin đó được header CORS. Production rỗng không cho origin. Dùng trình duyệt/OPTIONS để kiểm tra; app native không chịu cơ chế CORS.
- **API lỗi:** email sai/mật khẩu ngắn → 400; `/api/auth/me` → 404; `GET /auth/login` → 405; POST login với `text/plain` → 415. Response lỗi phải đúng envelope và không lộ stack trace.


<a id="request-samples"></a>
## 16. Phụ lục — 125 request mẫu

Thay các biến @ bằng dữ liệu local. Mẫu HTTP là nguồn duy nhất; bản Postman trùng 125 request đã được bỏ để tránh phải đồng bộ hai bộ.

```http
# Đọc các bước trong current-test.md trước khi gửi; đây là khối mẫu, chỉ dùng dữ liệu test.
# Chọn từng request; OTP/invitation/IDs cần điền từ response và email local.
# Không commit token/credentials sau khi điền.

@baseUrl = http://localhost:5267
@runId = 20261015a
@testPassword = Demo123456
@resetPassword = Demo654321
@adminEmail =
@adminPassword =
@sampleFile = ./synthetic-example.png
@customerToken =
@customerRefresh =
@customerId =
@ownerToken =
@ownerRefresh =
@ownerId =
@ownerBToken =
@ownerBId =
@managerToken =
@managerId =
@dispatcherToken =
@dispatcherId =
@driverToken =
@driverAccountId =
@driverId =
@adminToken =
@customerOtp =
@ownerOtp =
@ownerBOtp =
@managerOtp =
@dispatcherOtp =
@driverOtp =
@ownerInvitationToken =
@ownerBInvitationToken =
@managerInvitationToken =
@dispatcherInvitationToken =
@driverInvitationToken =
@driverBInvitationToken =
@newDriverInvitationToken =
@resetOtp =
@googleIdToken =
@replayedRefresh =
@orgId =
@orgBId =
@managerMemberId =
@dispatcherMemberId =
@invitationId =
@engagementId =
@engagementBId =
@vehicleId =
@linkId =
@roleId =
@permissionId =
@rolesManagePermissionId =
@documentId =
@infectedDocumentId =
@unavailableDocumentId =
@ocrFailedDocumentId =
@rejectedDocumentId =

### 01 Identity setup / Register customer
# Expected HTTP 200; RegisterRequest; email mới cho mỗi runId. Đọc OTP từ email File sau request này.
# Save from data: {"customerToken": "accessToken", "customerId": "accountId", "customerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/register
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com",
  "password": "{{testPassword}}",
  "fullName": "QA customer",
  "phoneNumber": "0900000001"
}

### 01 Identity setup / Register owner
# Expected HTTP 200; RegisterRequest; email mới cho mỗi runId. Đọc OTP từ email File sau request này.
# Save from data: {"ownerToken": "accessToken", "ownerId": "accountId", "ownerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/register
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-owner-{{runId}}@example.com",
  "password": "{{testPassword}}",
  "fullName": "QA owner",
  "phoneNumber": "0900000001"
}

### 01 Identity setup / Register manager
# Expected HTTP 200; RegisterRequest; email mới cho mỗi runId. Đọc OTP từ email File sau request này.
# Save from data: {"managerToken": "accessToken", "managerId": "accountId"}
POST {{baseUrl}}/auth/register
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-manager-{{runId}}@example.com",
  "password": "{{testPassword}}",
  "fullName": "QA manager",
  "phoneNumber": "0900000001"
}

### 01 Identity setup / Register dispatcher
# Expected HTTP 200; RegisterRequest; email mới cho mỗi runId. Đọc OTP từ email File sau request này.
# Save from data: {"dispatcherToken": "accessToken", "dispatcherId": "accountId"}
POST {{baseUrl}}/auth/register
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-dispatcher-{{runId}}@example.com",
  "password": "{{testPassword}}",
  "fullName": "QA dispatcher",
  "phoneNumber": "0900000001"
}

### 01 Identity setup / Register driver
# Expected HTTP 200; RegisterRequest; email mới cho mỗi runId. Đọc OTP từ email File sau request này.
# Save from data: {"driverToken": "accessToken", "driverAccountId": "accountId"}
POST {{baseUrl}}/auth/register
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-driver-{{runId}}@example.com",
  "password": "{{testPassword}}",
  "fullName": "QA driver",
  "phoneNumber": "0900000001"
}

### 01 Identity setup / Register ownerB
# Expected HTTP 200; RegisterRequest; email mới cho mỗi runId. Đọc OTP từ email File sau request này.
# Save from data: {"ownerBToken": "accessToken", "ownerBId": "accountId"}
POST {{baseUrl}}/auth/register
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-owner-b-{{runId}}@example.com",
  "password": "{{testPassword}}",
  "fullName": "QA ownerB",
  "phoneNumber": "0900000001"
}

### 01 Identity setup / Verify customer
# Expected HTTP 200; VerifyEmailRequest; điền OTP email-verification của đúng account trước khi gửi.
POST {{baseUrl}}/auth/verify-email
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com",
  "code": "{{customerOtp}}"
}

### 01 Identity setup / Verify owner
# Expected HTTP 200; VerifyEmailRequest; điền OTP email-verification của đúng account trước khi gửi.
POST {{baseUrl}}/auth/verify-email
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-owner-{{runId}}@example.com",
  "code": "{{ownerOtp}}"
}

### 01 Identity setup / Verify manager
# Expected HTTP 200; VerifyEmailRequest; điền OTP email-verification của đúng account trước khi gửi.
POST {{baseUrl}}/auth/verify-email
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-manager-{{runId}}@example.com",
  "code": "{{managerOtp}}"
}

### 01 Identity setup / Verify dispatcher
# Expected HTTP 200; VerifyEmailRequest; điền OTP email-verification của đúng account trước khi gửi.
POST {{baseUrl}}/auth/verify-email
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-dispatcher-{{runId}}@example.com",
  "code": "{{dispatcherOtp}}"
}

### 01 Identity setup / Verify driver
# Expected HTTP 200; VerifyEmailRequest; điền OTP email-verification của đúng account trước khi gửi.
POST {{baseUrl}}/auth/verify-email
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-driver-{{runId}}@example.com",
  "code": "{{driverOtp}}"
}

### 01 Identity setup / Verify ownerB
# Expected HTTP 200; VerifyEmailRequest; điền OTP email-verification của đúng account trước khi gửi.
POST {{baseUrl}}/auth/verify-email
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-owner-b-{{runId}}@example.com",
  "code": "{{ownerBOtp}}"
}

### 01 Identity setup / Login customer
# Expected HTTP 200; LoginRequest; lưu token riêng của từng actor.
# Save from data: {"customerToken": "accessToken", "customerId": "accountId", "customerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 01 Identity setup / Login owner
# Expected HTTP 200; LoginRequest; lưu token riêng của từng actor.
# Save from data: {"ownerToken": "accessToken", "ownerId": "accountId", "ownerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-owner-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 01 Identity setup / Login manager
# Expected HTTP 200; LoginRequest; lưu token riêng của từng actor.
# Save from data: {"managerToken": "accessToken", "managerId": "accountId"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-manager-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 01 Identity setup / Login dispatcher
# Expected HTTP 200; LoginRequest; lưu token riêng của từng actor.
# Save from data: {"dispatcherToken": "accessToken", "dispatcherId": "accountId"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-dispatcher-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 01 Identity setup / Login driver
# Expected HTTP 200; LoginRequest; lưu token riêng của từng actor.
# Save from data: {"driverToken": "accessToken", "driverAccountId": "accountId"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-driver-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 01 Identity setup / Login ownerB
# Expected HTTP 200; LoginRequest; lưu token riêng của từng actor.
# Save from data: {"ownerBToken": "accessToken", "ownerBId": "accountId"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-owner-b-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 01 Identity setup / Login admin
# Expected HTTP 200; Điền admin của DB test, không register InternalAdmin.
# Save from data: {"adminToken": "accessToken"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "InternalAdmin",
  "email": "{{adminEmail}}",
  "password": "{{adminPassword}}"
}

### 01 Identity setup / Me customer
# Expected HTTP 200; CurrentUserResponse; quyền/roles đọc DB.
GET {{baseUrl}}/auth/me
Authorization: Bearer {{customerToken}}

### 01 Identity setup / Me owner
# Expected HTTP 200;
GET {{baseUrl}}/auth/me
Authorization: Bearer {{ownerToken}}

### 01 Identity setup / Resend owner verification
# Expected HTTP 200; ResendVerificationRequest; nhánh tùy chọn trước verify, mã cũ bị vô hiệu khi cấp mới.
POST {{baseUrl}}/auth/resend-verification
Content-Type: application/json

{
  "accountGroup": "Operator",
  "email": "qa-owner-{{runId}}@example.com"
}

### 01 Identity setup / Google optional
# Expected HTTP 200; GoogleLoginRequest; chỉ gửi khi đã cấu hình ClientId và có Google ID token hợp lệ.
POST {{baseUrl}}/auth/google
Content-Type: application/json

{
  "accountGroup": "Customer",
  "idToken": "{{googleIdToken}}"
}

### 02 Organizations / Submit organization A
# Expected HTTP 200; SubmitOrganizationRequest; endpoint anonymous.
# Save from data: {"orgId": "id"}
POST {{baseUrl}}/organizations
Content-Type: application/json

{
  "name": "Nhà xe QA A",
  "registrationNumber": "QA-{{runId}}-A",
  "ownerEmail": "qa-owner-{{runId}}@example.com"
}

### 02 Organizations / Submit organization B
# Expected HTTP 200; SubmitOrganizationRequest; endpoint anonymous.
# Save from data: {"orgBId": "id"}
POST {{baseUrl}}/organizations
Content-Type: application/json

{
  "name": "Nhà xe QA B",
  "registrationNumber": "QA-{{runId}}-B",
  "ownerEmail": "qa-owner-b-{{runId}}@example.com"
}

### 02 Organizations / Admin list organizations
# Expected HTTP 200;
GET {{baseUrl}}/organizations
Authorization: Bearer {{adminToken}}

### 02 Organizations / Approve organization A
# Expected HTTP 200; ReviewRequest; tự gửi lời mời Owner, lấy token trong email.
POST {{baseUrl}}/organizations/{{orgId}}/review
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "approved": true,
  "note": "Đã kiểm tra hồ sơ giả lập QA"
}

### 02 Organizations / Approve organization B
# Expected HTTP 200; ReviewRequest; tự gửi lời mời Owner, lấy token trong email.
POST {{baseUrl}}/organizations/{{orgBId}}/review
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "approved": true,
  "note": "Đã kiểm tra hồ sơ giả lập QA"
}

### 02 Organizations / Reissue owner invitation optional
# Expected HTTP 200; Không body; chỉ chạy trước Owner accept. Sau có Owner trả 409. Điền token mới từ email.
POST {{baseUrl}}/organizations/{{orgId}}/owner-invitation
Authorization: Bearer {{adminToken}}

### 02 Organizations / Accept owner A
# Expected HTTP 200; AcceptInvitationRequest; JWT đúng người nhận, đã verify.
POST {{baseUrl}}/organizations/invitations/accept
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "token": "{{ownerInvitationToken}}"
}

### 02 Organizations / Accept owner B
# Expected HTTP 200; AcceptInvitationRequest; JWT đúng người nhận, đã verify.
POST {{baseUrl}}/organizations/invitations/accept
Authorization: Bearer {{ownerBToken}}
Content-Type: application/json

{
  "token": "{{ownerBInvitationToken}}"
}

### 02 Organizations / Owner list organizations
# Expected HTTP 200;
GET {{baseUrl}}/organizations
Authorization: Bearer {{ownerToken}}

### 02 Organizations / Owner get A
# Expected HTTP 200;
GET {{baseUrl}}/organizations/{{orgId}}
Authorization: Bearer {{ownerToken}}

### 02 Organizations / Cross organization forbidden
# Expected HTTP 403;
GET {{baseUrl}}/organizations/{{orgBId}}
Authorization: Bearer {{ownerToken}}

### 03 Members and quotas / Invite manager
# Expected HTTP 200; InviteMemberRequest; đọc token email đúng người nhận.
# Save from data: {"invitationId": "id"}
POST {{baseUrl}}/organizations/{{orgId}}/invitations
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "email": "qa-manager-{{runId}}@example.com",
  "role": "Manager"
}

### 03 Members and quotas / Accept manager
# Expected HTTP 200; AcceptInvitationRequest; JWT người nhận đã verify.
# Save from data: {"managerMemberId": "id"}
POST {{baseUrl}}/organizations/invitations/accept
Authorization: Bearer {{managerToken}}
Content-Type: application/json

{
  "token": "{{managerInvitationToken}}"
}

### 03 Members and quotas / Invite dispatcher
# Expected HTTP 200; InviteMemberRequest; đọc token email đúng người nhận.
# Save from data: {"invitationId": "id"}
POST {{baseUrl}}/organizations/{{orgId}}/invitations
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "email": "qa-dispatcher-{{runId}}@example.com",
  "role": "Dispatcher"
}

### 03 Members and quotas / Accept dispatcher
# Expected HTTP 200; AcceptInvitationRequest; JWT người nhận đã verify.
# Save from data: {"dispatcherMemberId": "id"}
POST {{baseUrl}}/organizations/invitations/accept
Authorization: Bearer {{dispatcherToken}}
Content-Type: application/json

{
  "token": "{{dispatcherInvitationToken}}"
}

### 03 Members and quotas / Owner list members
# Expected HTTP 200;
GET {{baseUrl}}/organizations/{{orgId}}/members
Authorization: Bearer {{ownerToken}}

### 03 Members and quotas / Manager member-list forbidden
# Expected HTTP 403;
GET {{baseUrl}}/organizations/{{orgId}}/members
Authorization: Bearer {{managerToken}}

### 03 Members and quotas / Demote manager
# Expected HTTP 200; UpdateMemberRequest; kiểm tra quyền giữa từng bước.
PUT {{baseUrl}}/organizations/{{orgId}}/members/{{managerMemberId}}
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "role": "Dispatcher",
  "isActive": true
}

### 03 Members and quotas / Restore manager
# Expected HTTP 200; UpdateMemberRequest; kiểm tra quyền giữa từng bước.
PUT {{baseUrl}}/organizations/{{orgId}}/members/{{managerMemberId}}
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "role": "Manager",
  "isActive": true
}

### 03 Members and quotas / Deactivate manager
# Expected HTTP 200; UpdateMemberRequest; kiểm tra quyền giữa từng bước.
PUT {{baseUrl}}/organizations/{{orgId}}/members/{{managerMemberId}}
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "role": "Manager",
  "isActive": false
}

### 03 Members and quotas / Reactivate manager
# Expected HTTP 200; UpdateMemberRequest; kiểm tra quyền giữa từng bước.
PUT {{baseUrl}}/organizations/{{orgId}}/members/{{managerMemberId}}
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "role": "Manager",
  "isActive": true
}

### 03 Members and quotas / Set employee limit
# Expected HTTP 200; EmployeeLimitRequest; trước đó kiểm tra lời mời thứ tư trả 409 ở quota mặc định 3.
PUT {{baseUrl}}/organizations/{{orgId}}/employee-limit
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "limit": 4
}

### 03 Members and quotas / Invite spare employee
# Expected HTTP 200;
# Save from data: {"invitationId": "id"}
POST {{baseUrl}}/organizations/{{orgId}}/invitations
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "email": "qa-spare-{{runId}}@example.com",
  "role": "Dispatcher"
}

### 03 Members and quotas / Revoke spare invitation
# Expected HTTP 200; Không body; không dùng invitation đã accept.
DELETE {{baseUrl}}/organizations/{{orgId}}/invitations/{{invitationId}}
Authorization: Bearer {{ownerToken}}

### 03 Members and quotas / Organization audit
# Expected HTTP 200;
GET {{baseUrl}}/organizations/{{orgId}}/audit
Authorization: Bearer {{ownerToken}}

### 04 Drivers / Submit driver
# Expected HTTP 200; SubmitDriverRequest; driverId khác accountId.
# Save from data: {"driverId": "id"}
POST {{baseUrl}}/drivers
Authorization: Bearer {{driverToken}}
Content-Type: application/json

{
  "fullName": "Tài xế QA"
}

### 04 Drivers / Driver me
# Expected HTTP 200;
GET {{baseUrl}}/drivers/me
Authorization: Bearer {{driverToken}}

### 04 Drivers / Admin pending drivers
# Expected HTTP 200;
GET {{baseUrl}}/drivers/pending
Authorization: Bearer {{adminToken}}

### 04 Drivers / Approve driver
# Expected HTTP 200; ReviewRequest, không có OCR/giấy tờ thật.
POST {{baseUrl}}/drivers/{{driverId}}/review
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "approved": true,
  "note": "Đã kiểm tra tài xế giả lập QA"
}

### 04 Drivers / Invite driver to A
# Expected HTTP 200; Không body. Lấy token từ email sau mỗi lời mời.
POST {{baseUrl}}/organizations/{{orgId}}/drivers/{{driverId}}/invitations
Authorization: Bearer {{ownerToken}}

### 04 Drivers / Driver accept A
# Expected HTTP 200; AcceptInvitationRequest; lưu ID engagement trả về.
# Save from data: {"engagementId": "id"}
POST {{baseUrl}}/drivers/invitations/accept
Authorization: Bearer {{driverToken}}
Content-Type: application/json

{
  "token": "{{driverInvitationToken}}"
}

### 04 Drivers / Invite driver to B
# Expected HTTP 200; Không body. Lấy token từ email sau mỗi lời mời.
POST {{baseUrl}}/organizations/{{orgBId}}/drivers/{{driverId}}/invitations
Authorization: Bearer {{ownerBToken}}

### 04 Drivers / Driver accept B
# Expected HTTP 200; AcceptInvitationRequest; lưu ID engagement trả về.
# Save from data: {"engagementBId": "id"}
POST {{baseUrl}}/drivers/invitations/accept
Authorization: Bearer {{driverToken}}
Content-Type: application/json

{
  "token": "{{driverBInvitationToken}}"
}

### 04 Drivers / Organization drivers
# Expected HTTP 200;
GET {{baseUrl}}/organizations/{{orgId}}/drivers
Authorization: Bearer {{managerToken}}

### 04 Drivers / Driver lacks member permission
# Expected HTTP 403;
GET {{baseUrl}}/organizations/{{orgId}}/vehicles
Authorization: Bearer {{driverToken}}

### 04 Drivers / Set driver limit
# Expected HTTP 200; DriverLimitRequest; active engagement ở A chiếm 1 chỗ.
PUT {{baseUrl}}/organizations/{{orgId}}/driver-limit
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "limit": 1
}

### 04 Drivers / Remove driver limit
# Expected HTTP 200;
PUT {{baseUrl}}/organizations/{{orgId}}/driver-limit
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "limit": null
}

### 04 Drivers / Invite unregistered driver optional
# Expected HTTP 200; InviteDriverRequest; người nhận phải register/verify/submit/review trước accept; lưu token vào newDriverInvitationToken. Dùng JWT người nhận cho accept.
POST {{baseUrl}}/organizations/{{orgId}}/driver-invitations
Authorization: Bearer {{managerToken}}
Content-Type: application/json

{
  "email": "qa-driver-new-{{runId}}@example.com"
}

### 04 Drivers / End driver A
# Expected HTTP 200; Không body; chạy sau kiểm tra cộng tác A/B, giữ lịch sử.
DELETE {{baseUrl}}/organizations/{{orgId}}/drivers/{{engagementId}}
Authorization: Bearer {{driverToken}}

### 05 Vehicles / Admin link vehicle A
# Expected HTTP 200; LinkVehicleRequest; đổi biển số nếu DB còn link từ lượt trước.
# Save from data: {"vehicleId": "vehicleId", "linkId": "linkId"}
POST {{baseUrl}}/organizations/{{orgId}}/vehicles
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "registrationPlate": "51A-123.45",
  "description": "Xe QA",
  "verificationReference": "Hồ sơ giả lập QA đã được kiểm tra"
}

### 05 Vehicles / List vehicles A
# Expected HTTP 200;
GET {{baseUrl}}/organizations/{{orgId}}/vehicles
Authorization: Bearer {{managerToken}}

### 05 Vehicles / Suspend vehicle
# Expected HTTP 200; SuspendVehicleRequest; liên kết vẫn chiếm xe.
PUT {{baseUrl}}/organizations/{{orgId}}/vehicles/{{linkId}}/suspension
Authorization: Bearer {{managerToken}}
Content-Type: application/json

{
  "isSuspended": true
}

### 05 Vehicles / B cannot link suspended vehicle
# Expected HTTP 409;
POST {{baseUrl}}/organizations/{{orgBId}}/vehicles
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "registrationPlate": "51A-123.45",
  "description": "Xe QA",
  "verificationReference": "Hồ sơ giả lập QA đã được kiểm tra"
}

### 05 Vehicles / Resume vehicle
# Expected HTTP 200;
PUT {{baseUrl}}/organizations/{{orgId}}/vehicles/{{linkId}}/suspension
Authorization: Bearer {{managerToken}}
Content-Type: application/json

{
  "isSuspended": false
}

### 05 Vehicles / End vehicle link A
# Expected HTTP 200; Không body. EndReason/EndedBy chưa triển khai.
DELETE {{baseUrl}}/organizations/{{orgId}}/vehicles/{{linkId}}
Authorization: Bearer {{managerToken}}

### 05 Vehicles / Admin link same vehicle B
# Expected HTTP 200; Cùng vehicleId, linkId mới. Lịch sử A giữ nguyên.
POST {{baseUrl}}/organizations/{{orgBId}}/vehicles
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "registrationPlate": "51A-123.45",
  "description": "Xe QA",
  "verificationReference": "Hồ sơ giả lập QA đã được kiểm tra"
}

### 06 Compliance demo / Upload demo success
# Expected HTTP 200; UploadDemoDocumentRequest; chọn file giả lập; storage thật, không quét/OCR thật.
# Save from data: {"documentId": "id"}
POST {{baseUrl}}/compliance/demo/documents
Authorization: Bearer {{ownerToken}}
Content-Type: multipart/form-data; boundary=TransConnectDemoBoundary

--TransConnectDemoBoundary
Content-Disposition: form-data; name="SyntheticDataConfirmed"

true
--TransConnectDemoBoundary
Content-Disposition: form-data; name="File"; filename="synthetic-example.png"
Content-Type: application/octet-stream

< {{sampleFile}}
--TransConnectDemoBoundary--

### 06 Compliance demo / List my demo documents
# Expected HTTP 200;
GET {{baseUrl}}/compliance/demo/documents
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Get my demo document
# Expected HTTP 200;
GET {{baseUrl}}/compliance/demo/documents/{{documentId}}
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Download before simulation forbidden
# Expected HTTP 403;
GET {{baseUrl}}/compliance/demo/documents/{{documentId}}/download
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Simulate success
# Expected HTTP 200; SimulateDocumentRequest; SIMULATED_CLEAN / NOT_VERIFIED.
POST {{baseUrl}}/compliance/demo/documents/{{documentId}}/simulate
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "scenario": "Success"
}

### 06 Compliance demo / Approve demo
# Expected HTTP 200; ReviewDemoDocumentRequest; tự duyệt chỉ để trình diễn.
POST {{baseUrl}}/compliance/demo/documents/{{documentId}}/review
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "approved": true
}

### 06 Compliance demo / Download demo attachment
# Expected HTTP 200; Binary .bin; không có JSON data, không preview inline.
GET {{baseUrl}}/compliance/demo/documents/{{documentId}}/download
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Other account cannot download
# Expected HTTP 404;
GET {{baseUrl}}/compliance/demo/documents/{{documentId}}/download
Authorization: Bearer {{managerToken}}

### 06 Compliance demo / Upload demo Infected
# Expected HTTP 200;
# Save from data: {"infectedDocumentId": "id"}
POST {{baseUrl}}/compliance/demo/documents
Authorization: Bearer {{ownerToken}}
Content-Type: multipart/form-data; boundary=TransConnectDemoBoundary

--TransConnectDemoBoundary
Content-Disposition: form-data; name="SyntheticDataConfirmed"

true
--TransConnectDemoBoundary
Content-Disposition: form-data; name="File"; filename="synthetic-example.png"
Content-Type: application/octet-stream

< {{sampleFile}}
--TransConnectDemoBoundary--

### 06 Compliance demo / Simulate Infected
# Expected HTTP 200;
POST {{baseUrl}}/compliance/demo/documents/{{infectedDocumentId}}/simulate
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "scenario": "Infected"
}

### 06 Compliance demo / Download blocked Infected
# Expected HTTP 403;
GET {{baseUrl}}/compliance/demo/documents/{{infectedDocumentId}}/download
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Upload demo ScannerUnavailable
# Expected HTTP 200;
# Save from data: {"unavailableDocumentId": "id"}
POST {{baseUrl}}/compliance/demo/documents
Authorization: Bearer {{ownerToken}}
Content-Type: multipart/form-data; boundary=TransConnectDemoBoundary

--TransConnectDemoBoundary
Content-Disposition: form-data; name="SyntheticDataConfirmed"

true
--TransConnectDemoBoundary
Content-Disposition: form-data; name="File"; filename="synthetic-example.png"
Content-Type: application/octet-stream

< {{sampleFile}}
--TransConnectDemoBoundary--

### 06 Compliance demo / Simulate ScannerUnavailable
# Expected HTTP 200;
POST {{baseUrl}}/compliance/demo/documents/{{unavailableDocumentId}}/simulate
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "scenario": "ScannerUnavailable"
}

### 06 Compliance demo / Download blocked ScannerUnavailable
# Expected HTTP 403;
GET {{baseUrl}}/compliance/demo/documents/{{unavailableDocumentId}}/download
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Upload demo OcrFailed
# Expected HTTP 200;
# Save from data: {"ocrFailedDocumentId": "id"}
POST {{baseUrl}}/compliance/demo/documents
Authorization: Bearer {{ownerToken}}
Content-Type: multipart/form-data; boundary=TransConnectDemoBoundary

--TransConnectDemoBoundary
Content-Disposition: form-data; name="SyntheticDataConfirmed"

true
--TransConnectDemoBoundary
Content-Disposition: form-data; name="File"; filename="synthetic-example.png"
Content-Type: application/octet-stream

< {{sampleFile}}
--TransConnectDemoBoundary--

### 06 Compliance demo / Simulate OcrFailed
# Expected HTTP 200;
POST {{baseUrl}}/compliance/demo/documents/{{ocrFailedDocumentId}}/simulate
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "scenario": "OcrFailed"
}

### 06 Compliance demo / Download blocked OcrFailed
# Expected HTTP 403;
GET {{baseUrl}}/compliance/demo/documents/{{ocrFailedDocumentId}}/download
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Upload rejected demo
# Expected HTTP 200;
# Save from data: {"rejectedDocumentId": "id"}
POST {{baseUrl}}/compliance/demo/documents
Authorization: Bearer {{ownerToken}}
Content-Type: multipart/form-data; boundary=TransConnectDemoBoundary

--TransConnectDemoBoundary
Content-Disposition: form-data; name="SyntheticDataConfirmed"

true
--TransConnectDemoBoundary
Content-Disposition: form-data; name="File"; filename="synthetic-example.png"
Content-Type: application/octet-stream

< {{sampleFile}}
--TransConnectDemoBoundary--

### 06 Compliance demo / Simulate before reject
# Expected HTTP 200;
POST {{baseUrl}}/compliance/demo/documents/{{rejectedDocumentId}}/simulate
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "scenario": "Success"
}

### 06 Compliance demo / Reject demo
# Expected HTTP 200;
POST {{baseUrl}}/compliance/demo/documents/{{rejectedDocumentId}}/review
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "approved": false
}

### 06 Compliance demo / Rejected download forbidden
# Expected HTTP 403;
GET {{baseUrl}}/compliance/demo/documents/{{rejectedDocumentId}}/download
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Delete documentId
# Expected HTTP 200;
DELETE {{baseUrl}}/compliance/demo/documents/{{documentId}}
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Delete infectedDocumentId
# Expected HTTP 200;
DELETE {{baseUrl}}/compliance/demo/documents/{{infectedDocumentId}}
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Delete unavailableDocumentId
# Expected HTTP 200;
DELETE {{baseUrl}}/compliance/demo/documents/{{unavailableDocumentId}}
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Delete ocrFailedDocumentId
# Expected HTTP 200;
DELETE {{baseUrl}}/compliance/demo/documents/{{ocrFailedDocumentId}}
Authorization: Bearer {{ownerToken}}

### 06 Compliance demo / Delete rejectedDocumentId
# Expected HTTP 200;
DELETE {{baseUrl}}/compliance/demo/documents/{{rejectedDocumentId}}
Authorization: Bearer {{ownerToken}}

### 07 RBAC / Customer roles forbidden
# Expected HTTP 403;
GET {{baseUrl}}/roles
Authorization: Bearer {{customerToken}}

### 07 RBAC / Admin list roles
# Expected HTTP 200;
GET {{baseUrl}}/roles
Authorization: Bearer {{adminToken}}

### 07 RBAC / Admin list permissions
# Expected HTTP 200; Điền rolesManagePermissionId từ item có code roles.manage.
GET {{baseUrl}}/permissions
Authorization: Bearer {{adminToken}}

### 07 RBAC / Create QA role
# Expected HTTP 200; CreateRoleRequest
# Save from data: {"roleId": "id"}
POST {{baseUrl}}/roles
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "name": "QAReader-{{runId}}",
  "description": "Vai trò QA"
}

### 07 RBAC / Create QA permission
# Expected HTTP 200; CreatePermissionRequest
# Save from data: {"permissionId": "id"}
POST {{baseUrl}}/permissions
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "code": "qa.read.{{runId}}",
  "description": "Quyền QA"
}

### 07 RBAC / Get QA role
# Expected HTTP 200;
GET {{baseUrl}}/roles/{{roleId}}
Authorization: Bearer {{adminToken}}

### 07 RBAC / Update QA role
# Expected HTTP 200; UpdateRoleRequest
PUT {{baseUrl}}/roles/{{roleId}}
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "name": "QAReaderUpdated-{{runId}}",
  "description": "Đã cập nhật QA"
}

### 07 RBAC / Assign QA permission
# Expected HTTP 200;
POST {{baseUrl}}/roles/{{roleId}}/permissions/{{permissionId}}
Authorization: Bearer {{adminToken}}

### 07 RBAC / Assign QA role to customer
# Expected HTTP 200;
POST {{baseUrl}}/roles/{{roleId}}/users/{{customerId}}
Authorization: Bearer {{adminToken}}

### 07 RBAC / Customer me after DB permission change
# Expected HTTP 200;
GET {{baseUrl}}/auth/me
Authorization: Bearer {{customerToken}}

### 07 RBAC / Grant roles manage to QA role
# Expected HTTP 200;
POST {{baseUrl}}/roles/{{roleId}}/permissions/{{rolesManagePermissionId}}
Authorization: Bearer {{adminToken}}

### 07 RBAC / Relogin customer for claims
# Expected HTTP 200;
# Save from data: {"customerToken": "accessToken", "customerId": "accountId", "customerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 07 RBAC / Customer can manage roles
# Expected HTTP 200;
GET {{baseUrl}}/roles
Authorization: Bearer {{customerToken}}

### 07 RBAC / Revoke roles manage
# Expected HTTP 200;
DELETE {{baseUrl}}/roles/{{roleId}}/permissions/{{rolesManagePermissionId}}
Authorization: Bearer {{adminToken}}

### 07 RBAC / Relogin after revoke
# Expected HTTP 200;
# Save from data: {"customerToken": "accessToken", "customerId": "accountId", "customerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 07 RBAC / Customer roles forbidden again
# Expected HTTP 403;
GET {{baseUrl}}/roles
Authorization: Bearer {{customerToken}}

### 07 RBAC / Remove QA permission from role
# Expected HTTP 200;
DELETE {{baseUrl}}/roles/{{roleId}}/permissions/{{permissionId}}
Authorization: Bearer {{adminToken}}

### 07 RBAC / Remove QA role from customer
# Expected HTTP 200;
DELETE {{baseUrl}}/roles/{{roleId}}/users/{{customerId}}
Authorization: Bearer {{adminToken}}

### 07 RBAC / Delete QA permission
# Expected HTTP 200;
DELETE {{baseUrl}}/permissions/{{permissionId}}
Authorization: Bearer {{adminToken}}

### 07 RBAC / Delete QA role
# Expected HTTP 200;
DELETE {{baseUrl}}/roles/{{roleId}}
Authorization: Bearer {{adminToken}}

### 08 Sessions and reset / Fresh customer session
# Expected HTTP 200; Copy customerRefresh vào replayedRefresh trước bước refresh nếu thử replay.
# Save from data: {"customerToken": "accessToken", "customerId": "accountId", "customerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 08 Sessions and reset / Rotate customer refresh
# Expected HTTP 200; RefreshTokenRequest; thay cả access/refresh bằng response mới.
# Save from data: {"customerToken": "accessToken", "customerId": "accountId", "customerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/refresh
Content-Type: application/json

{
  "refreshToken": "{{customerRefresh}}"
}

### 08 Sessions and reset / Replay old refresh optional
# Expected HTTP 401; Nhánh tùy chọn: thu hồi refresh account, login lại trước các bước sau.
POST {{baseUrl}}/auth/refresh
Content-Type: application/json

{
  "refreshToken": "{{replayedRefresh}}"
}

### 08 Sessions and reset / Login before logout
# Expected HTTP 200;
# Save from data: {"customerToken": "accessToken", "customerId": "accountId", "customerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com",
  "password": "{{testPassword}}"
}

### 08 Sessions and reset / Logout customer
# Expected HTTP 200; Cần JWT; RefreshTokenRequest.
POST {{baseUrl}}/auth/logout
Authorization: Bearer {{customerToken}}
Content-Type: application/json

{
  "refreshToken": "{{customerRefresh}}"
}

### 08 Sessions and reset / Refresh logged out token
# Expected HTTP 401;
POST {{baseUrl}}/auth/refresh
Content-Type: application/json

{
  "refreshToken": "{{customerRefresh}}"
}

### 08 Sessions and reset / Forgot customer password
# Expected HTTP 200; ForgotPasswordRequest; nhập resetOtp từ email password-reset.
POST {{baseUrl}}/auth/forgot-password
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com"
}

### 08 Sessions and reset / Reset customer password
# Expected HTTP 200; ResetPasswordRequest; revoke toàn bộ refresh cũ.
POST {{baseUrl}}/auth/reset-password
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com",
  "code": "{{resetOtp}}",
  "newPassword": "{{resetPassword}}"
}

### 08 Sessions and reset / Login with reset password
# Expected HTTP 200;
# Save from data: {"customerToken": "accessToken", "customerId": "accountId", "customerRefresh": "refreshToken"}
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "accountGroup": "Customer",
  "email": "qa-customer-{{runId}}@example.com",
  "password": "{{resetPassword}}"
}

### 08 Sessions and reset / Test email development only
# Expected HTTP 200; Không body; File provider, không SMTP thật.
POST {{baseUrl}}/dev/test-email?to=qa-notify-{{runId}}@example.com
```

<a id="response-samples"></a>
## 17. Phụ lục — Response mẫu

```json
{
  "_notice": "Illustrative DTO shapes only. Replace IDs/tokens/size/hash/time with actual responses; hash of all zeros is a placeholder. Success message may be null or localized. Lists return arrays, not pagination envelopes.",
  "AuthResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "accessToken": "<access-token>",
      "accessTokenExpiresAt": "2026-10-15T10:15:00Z",
      "refreshToken": "<refresh-token>",
      "accountGroup": "Operator",
      "accountId": "11111111-1111-4111-8111-111111111111",
      "userId": "11111111-1111-4111-8111-111111111111",
      "email": "qa-owner-20261015a@example.com",
      "fullName": "Chủ nhà xe QA",
      "emailVerified": true,
      "roles": [
        "User"
      ]
    },
    "errors": null
  },
  "CurrentUserResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "accountGroup": "Operator",
      "accountId": "11111111-1111-4111-8111-111111111111",
      "userId": "11111111-1111-4111-8111-111111111111",
      "email": "qa-owner-20261015a@example.com",
      "fullName": "Chủ nhà xe QA",
      "phoneNumber": "0900000001",
      "avatarUrl": null,
      "emailVerified": true,
      "roles": [
        "User"
      ],
      "permissions": []
    },
    "errors": null
  },
  "RoleResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111",
      "name": "QAReader-20261015a",
      "description": "Vai trò QA",
      "permissions": [
        "qa.read.20261015a"
      ]
    },
    "errors": null
  },
  "PermissionResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111",
      "code": "qa.read.20261015a",
      "description": "Quyền QA"
    },
    "errors": null
  },
  "OrganizationResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111",
      "name": "Nhà xe QA A",
      "registrationNumber": "QA-20261015A-A",
      "ownerEmail": "qa-owner-20261015a@example.com",
      "reviewStatus": "Approved",
      "reviewNote": "Đã kiểm tra QA",
      "employeeLimit": 3,
      "driverLimit": null
    },
    "errors": null
  },
  "MemberResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111",
      "accountId": "22222222-2222-4222-8222-222222222222",
      "role": "Manager",
      "isActive": true
    },
    "errors": null
  },
  "InvitationResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111",
      "expiresAt": "2026-10-17T10:00:00Z"
    },
    "errors": null
  },
  "DriverResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111",
      "accountId": "22222222-2222-4222-8222-222222222222",
      "fullName": "Tài xế QA",
      "reviewStatus": "Approved",
      "reviewNote": "Đã kiểm tra tài xế QA"
    },
    "errors": null
  },
  "EngagementResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111",
      "organizationId": "22222222-2222-4222-8222-222222222222",
      "driverId": "11111111-1111-4111-8111-111111111111",
      "expiresAt": "2026-10-17T10:00:00Z",
      "acceptedAt": "2026-10-15T10:00:00Z",
      "endedAt": null,
      "revokedAt": null
    },
    "errors": null
  },
  "VehicleResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "linkId": "11111111-1111-4111-8111-111111111111",
      "vehicleId": "22222222-2222-4222-8222-222222222222",
      "registrationPlate": "51A12345",
      "description": "Xe QA",
      "isSuspended": false,
      "endedAt": null
    },
    "errors": null
  },
  "AuditResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111",
      "actorAccountId": "22222222-2222-4222-8222-222222222222",
      "subjectId": "11111111-1111-4111-8111-111111111111",
      "action": "member.accept",
      "details": "Manager",
      "createdAt": "2026-10-15T10:00:00Z"
    },
    "errors": null
  },
  "DemoDocumentResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111",
      "status": "DemoNeedsReview",
      "contentType": "image/png",
      "size": 1234,
      "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
      "scenario": "Success",
      "scanResult": "SIMULATED_CLEAN",
      "ocrText": "[OCR MÔ PHỎNG] Họ tên: NGUYỄN VĂN DEMO. Mã hồ sơ: DEMO-0001. Nội dung cố định, không trích xuất từ file.",
      "createdAt": "2026-10-15T10:00:00Z",
      "simulatedAt": "2026-10-15T10:00:00Z",
      "reviewedAt": null,
      "isDemo": true,
      "processingMode": "SIMULATED",
      "verification": "NOT_VERIFIED",
      "notice": "DEMO: chưa quét mã độc, chưa OCR thật; chỉ dùng dữ liệu giả lập."
    },
    "errors": null
  },
  "ListResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": [
      {
        "id": "11111111-1111-4111-8111-111111111111",
        "name": "Nhà xe QA A",
        "registrationNumber": "QA-20261015A-A",
        "ownerEmail": "qa-owner-20261015a@example.com",
        "reviewStatus": "Approved",
        "reviewNote": "Đã kiểm tra QA",
        "employeeLimit": 3,
        "driverLimit": null
      }
    ],
    "errors": null
  },
  "EmptySuccessResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {},
    "errors": null
  },
  "DemoDeleteResponse": {
    "succeeded": true,
    "message": null,
    "errorCode": null,
    "data": {
      "id": "11111111-1111-4111-8111-111111111111"
    },
    "errors": null
  },
  "ValidationErrorResponse": {
    "succeeded": false,
    "message": "Dữ liệu không hợp lệ.",
    "errorCode": "VALIDATION_ERROR",
    "data": null,
    "errors": {
      "email": [
        "Email không đúng định dạng."
      ]
    }
  },
  "ForbiddenResponse": {
    "succeeded": false,
    "message": "Bạn không có quyền thực hiện thao tác này.",
    "errorCode": "FORBIDDEN",
    "data": null,
    "errors": null
  },
  "StorageUnavailableResponse": {
    "succeeded": false,
    "message": "Kho file chưa sẵn sàng. Kiểm tra kết nối và quyền bucket.",
    "errorCode": "SERVICE_UNAVAILABLE",
    "data": null,
    "errors": null
  },
  "DemoDownloadHTTP": {
    "status": 200,
    "headers": {
      "Content-Type": "application/octet-stream",
      "Content-Disposition": "attachment; filename=demo-unscanned-<document-id>.bin",
      "Cache-Control": "no-store",
      "X-Content-Type-Options": "nosniff",
      "X-Document-Mode": "DEMO-UNSCANNED"
    },
    "body": "<binary file bytes; not a JSON envelope>",
    "internalServiceDto": "DemoDownloadResponse(Content: byte[], FileName: string)"
  }
}
```
