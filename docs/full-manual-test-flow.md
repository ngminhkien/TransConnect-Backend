# Luồng test đầy đủ — Identity, Operator và Compliance demo

Đối chiếu controller/service/DTO ngày 15/10/2026. Tài liệu này mô tả API đã có để test thủ công từ đầu; các yêu cầu nghiệp vụ đang thiết kế vẫn giữ trạng thái riêng. Dữ liệu mẫu chỉ dành cho database phát triển.

## 1. Bộ tài liệu và cách dùng

- File này: thứ tự thực hiện, tài khoản cần dùng, điểm dừng nhập OTP/token, kết quả và ca lỗi.
- [Request mẫu REST Client](http/full-flow.http): mở trong VS Code với REST Client, sửa biến ở đầu file và chọn từng `Send Request`.
- [Postman collection](http/TransConnect-full-flow.postman_collection.json): Import vào Postman; collection variables chứa dữ liệu giả lập, token/OTP ban đầu để trống. Không cần environment riêng.
- [Response DTO mẫu](http/response-examples.json): cấu trúc đầy đủ của các response, bao gồm envelope. UUID/thời gian chỉ minh họa, không phải ID có sẵn trong DB.
- [Nghiệm thu core và đồng thời](phase-1-foundation-test-flow.md), [cài AIStor và demo](compliance-demo-setup.md).

**Gọi từng request theo hướng dẫn. Không chạy toàn bộ collection bằng Runner ngay**: có bước cần nhập OTP/token từ email, nhánh lỗi, refresh/replay và cleanup. Thay `runId` mỗi lượt chạy mới để email, mã đăng ký và tên role/permission không bị trùng. Collection tự lưu ID/token của response thành công; trong REST Client cần chép ID/token vào các biến đầu file. Không export collection đã điền token thật lên Git.

## 2. Chuẩn bị môi trường

1. Dùng SQL Server database phát triển đúng `ConnectionStrings:Default`, .NET 10. File local `appsettings.Development.json` có JWT key riêng và `Email:Provider=File`. Giữ cấu hình local đang có.
2. Có thể test Identity/Operator khi `ComplianceDemo:Enabled=false`; không cần Docker/AIStor lúc này.
3. Để test Compliance: làm theo [setup](compliance-demo-setup.md), đặt license, chạy AIStor, tạo bucket private `transconnect-demo-unscanned`, cấp access key ứng dụng và bật ComplianceDemo trong Development. Tạm dừng container ClamAV để giảm RAM.
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

Thực hiện folder **01 Identity setup** trong collection.

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

Thực hiện folder **02 Organizations**.

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

Thực hiện folder **03 Members and quotas**. Mặc định EmployeeLimit=3 tính Owner + active members + pending invitations còn hạn.

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

Thực hiện folder **04 Drivers**.

1. Với JWT Driver Operator đã verify: `POST /drivers` `{"fullName":"Tài xế QA"}`; 200 Pending, lưu driverId. `GET /drivers/me` thấy đúng account; một account chỉ một hồ sơ Driver.
2. Admin `GET /drivers/pending`; `POST /drivers/{driverId}/review` dùng ReviewRequest `{"approved":true,"note":"Đã kiểm tra hồ sơ tài xế QA"}`. Driver không tự review -> 403.
3. Owner/Manager/Admin `POST /organizations/{orgId}/drivers/{driverId}/invitations` **không body**. Lưu invitation `id` và lấy mã từ email. Driver `POST /drivers/invitations/accept` với `{"token":"<token-driver>"}` -> engagement AcceptedAt, lưu engagementId.
4. Dùng Owner B mời cùng Driver vào B, Driver accept -> cộng tác cả A và B. Driver không có membership gọi list xe A -> 403; cộng tác không cấp quyền nhân viên.
5. Owner/Manager/Admin `GET /organizations/{orgId}/drivers`: thấy engagement và lịch sử. Driver hoặc quản lý phù hợp `DELETE /organizations/{orgId}/drivers/{engagementId}` -> kết thúc, không xóa hồ sơ Driver.
6. Nhánh mời trước đăng ký: `POST /organizations/{orgId}/driver-invitations` với `{"email":"qa-driver-new-20261015a@example.com"}`. Sau đó người nhận register Operator -> verify -> submit Driver -> Admin review -> accept bằng **/drivers/invitations/accept**. Chưa có hồ sơ/Approved -> 409, token chưa bị consume; token Driver gửi sang accept Member -> 401.
7. Admin `PUT /organizations/{orgId}/driver-limit` với `{"limit":1}`; engagement chưa kết thúc và pending invitation còn hạn tính quota. Vượt -> 409. `{"limit":null}` bỏ giới hạn. Driver quota không cộng vào EmployeeLimit.

Owner có thể submit hồ sơ Driver trên chính account Operator của mình, được Admin duyệt rồi được mời/accept như flow trên. Hiện chưa có API tự nhận cộng tác bỏ qua lời mời. Người vừa member vừa Driver được tính ở cả hai quota.

## 8. Xe: gán -> tạm ngừng -> kết thúc -> sang B

Thực hiện folder **05 Vehicles**, sau các bước đọc/quản lý Driver nếu không muốn thay quota giữa flow.

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

Thực hiện folder **06 Compliance demo**; cần AIStor thật, `Enabled=true`, Development. Bất kỳ account đang hoạt động có thể test hồ sơ demo của chính mình; ví dụ dùng Owner.

1. Chuẩn bị một PNG/JPEG/PDF **giả lập** <=8 MiB, chọn vào multipart `File`; `SyntheticDataConfirmed=true`. Trong `.http`, sửa `sampleFile` sang đường dẫn file local. Trong Postman, chọn file ở Body/form-data. Đây không phải JSON base64.
2. `POST /compliance/demo/documents` -> PendingSimulation, NOT_SCANNED; lưu documentId. List/get chỉ thấy file của account hiện tại. Get/download bằng Manager/Customer khác -> 404.
3. Trước mô phỏng download -> 403. Mô phỏng:

```json
{ "scenario": "Success" }
```

4. Kết quả DemoNeedsReview, SIMULATED_CLEAN và OCR text cố định, `processingMode=SIMULATED`, `verification=NOT_VERIFIED`. Không gọi ClamAV/OCR thật, không chuyển file sang clean bucket.
5. Chủ file `POST /compliance/demo/documents/{id}/review` với `{"approved":true}` -> DemoApproved, vẫn NOT_VERIFIED. Tạo file Success riêng nếu muốn test `{"approved":false}` -> DemoRejected, download bị chặn.
6. Download trước/sau DemoApproved -> attachment `.bin`, octet-stream, no-store, `X-Document-Mode: DEMO-UNSCANNED`; SHA-256/size đối chiếu metadata. Đổi file trên storage -> 409 khi hash/size lệch.
7. Upload file mới cho mỗi nhánh bên dưới; collection tự lưu ID riêng nếu dùng từng upload tương ứng:

| scenario | status | scanResult | Download |
|---|---|---|---|
| Infected | DemoBlocked | SIMULATED_INFECTED | 403 |
| ScannerUnavailable | DemoFailed | SIMULATED_UNAVAILABLE | 403 |
| OcrFailed | DemoFailed | SIMULATED_CLEAN | 403 |

8. Scenario ngoài danh mục -> 400. Review khi chưa Success -> 409; bỏ approved -> 400. Simulate cùng scenario đã chạy -> trả kết quả cũ; đổi scenario trên cùng document -> 409. Hai cập nhật stale revision -> 409 cho bản thua.
9. DELETE -> xóa S3 object, soft-delete metadata và xóa OCR mẫu; get -> 404. Delete storage lỗi -> 503, status Deleting, retry delete sau khi storage phục hồi.
10. Tắt AIStor để test upload lỗi -> 503, không giả thành công; bật lại rồi dọn UploadFailed/Uploading qua API. Tắt ComplianceDemo/restart -> routes 404. Bật trên Production/Staging -> startup từ chối. Không thực hiện ca tắt dịch vụ trong lúc người khác đang test.

## 10. RBAC: role/permission độc lập membership tổ chức

Thực hiện folder **07 RBAC**. Dùng Customer A để tránh ảnh hưởng membership đang test.

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

Thực hiện folder **08 Sessions and reset**.

1. Customer login để có refresh R1. POST /auth/refresh body `{"refreshToken":"<R1>"}` -> 200, nhận access và R2 mới, phải thay cả hai. Collection tự cập nhật customerToken/customerRefresh.
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

JSON mẫu của **mọi request DTO public** nằm trong REST Client/Postman, kể cả request không có body. Quy tắc cần nhớ:

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

[response-examples.json](http/response-examples.json) có đủ AuthResponse, CurrentUserResponse, RoleResponse, PermissionResponse, OrganizationResponse, MemberResponse, InvitationResponse, DriverResponse, EngagementResponse, VehicleResponse, AuditResponse, DemoDocumentResponse, envelope rỗng, validation/error, list và mô tả download. DemoDownloadResponse là kết quả service nội bộ; controller chuyển Content sang binary và FileName sang attachment, không trả base64 JSON cho endpoint download.

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

Test tự động gần nhất 89/89 pass, xem [traceability](../../TransConnect-Docs/08-testing/traceability.md). Tài liệu/request mẫu không phải bằng chứng đã test SQL Server/AIStor/SMTP/Google/R2 thật. Luồng production scan/OCR, hồ sơ pháp lý và Trip/Booking vẫn thuộc các mốc sau.
