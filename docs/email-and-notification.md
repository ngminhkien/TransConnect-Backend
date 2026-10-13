# Hệ thống gửi email (module Notification)

Giai đoạn 1 thêm template organization-invitation (.html/.txt nhúng DLL) cho Owner/nhân viên/tài xế. Token ngẫu nhiên được gửi qua email, bảng lời mời chỉ lưu hash; API create trả id/expiresAt. HTML dùng cơ chế encode hiện có. Queue thuộc Notification còn invitation thuộc Operator: nếu enqueue xong nhưng transaction Operator rollback, email có thể tồn tại nhưng token không dùng được; gửi lại lời mời để phục hồi. Không đánh dấu email queued/sent là bằng chứng quan hệ thành viên/cộng tác đã được xác nhận.

Tài liệu này mô tả cách email được gửi trong TransConnect: kiến trúc, lý do chọn thiết kế đó,
cách thêm một loại email mới, và những giới hạn đã biết.

## Nguyên tắc cốt lõi

**Module nghiệp vụ không bao giờ gửi email trực tiếp.** Chúng chỉ gọi
`IEmailQueue.EnqueueAsync(...)` để xếp email vào hàng đợi; một background worker gửi thật.

```
Identity/Wallet/...            Common                    Modules.Notification
─────────────────────         ────────                  ──────────────────────
AuthService  ──gọi──►  IEmailQueue (interface)  ◄──implement── EmailQueue
                                                                    │ ghi 1 row
                                                                    ▼
                                                     notification.EmailMessages (outbox)
                                                                    │ quét mỗi 15s
                                                                    ▼
                                                          EmailDispatchWorker
                                                                    │
                                                                    ▼
                                                     IEmailProvider (File | Smtp)
```

Contract nằm ở `TransConnect.Common/Notifications/` nên module nghiệp vụ **không cần reference
module Notification** — đúng quy tắc trong [base-libraries-guidelines.md](base-libraries-guidelines.md).

### Vì sao vòng qua hàng đợi thay vì gọi thẳng SMTP

| Vấn đề khi gọi thẳng | Hàng đợi giải quyết thế nào |
|---|---|
| Request phải chờ SMTP 1–3 giây | Enqueue chỉ là 1 lệnh INSERT, trả về ngay |
| SMTP chết → đăng ký cũng chết theo | Đăng ký vẫn thành công, email gửi sau |
| Gửi hụt là mất luôn email | Worker tự thử lại với giãn cách tăng dần |
| Không biết đã gửi gì cho ai | Bảng outbox chính là nhật ký gửi |

Riêng với **email đặt lại mật khẩu**, mất email đồng nghĩa người dùng không vào được tài khoản
— đây là lý do quan trọng nhất để giữ cơ chế retry.

## Vòng đời một email

`EmailMessage.Status` đi theo đúng đường sau:

```
Pending ──worker bốc──► Sending ──gửi OK──► Sent
   ▲                        │
   │                        └──gửi hụt──► Pending (đặt NextAttemptAt)
   │                                          │
   └──────thu hồi row kẹt──────────           └──quá MaxAttempts──► Failed
```

- **`Pending`** — đang chờ tới lượt (kể cả email đã gửi hụt và đang chờ retry).
- **`Sending`** — worker đã bốc; trạng thái này để lượt quét sau không bốc lại.
- **`Sent`** — xong.
- **`Failed`** — đã thử đủ `MaxAttempts`, không tự thử lại nữa, cần can thiệp tay.

**Thu hồi row kẹt**: nếu app crash giữa lúc gửi, row nằm lại ở `Sending` vĩnh viễn. Worker trả
những row `Sending` quá `StuckAfterMinutes` về `Pending` để được thử lại.

**Backoff**: `BackoffBaseSeconds * 2^(số lần đã thử - 1)`, chặn trên bởi `BackoffMaxSeconds`
→ mặc định 30s, 60s, 120s, 240s...

## Cấu hình

Toàn bộ nằm ở section `Email`. Phần nhạy cảm (`Smtp.User`, `Smtp.Password`) để trong
`appsettings.Development.json` — file này **không commit**.

```json
"Email": {
  "Provider": "File",
  "FileDropPath": "",
  "From":  { "Address": "", "DisplayName": "TransConnect" },
  "Smtp":  { "Host": "smtp.gmail.com", "Port": 587, "User": "", "Password": "", "UseStartTls": true },
  "Worker": {
    "PollSeconds": 15, "BatchSize": 20, "MaxAttempts": 5,
    "BackoffBaseSeconds": 30, "BackoffMaxSeconds": 3600, "StuckAfterMinutes": 10
  },
  "Branding": {
    "AppName": "TransConnect", "PrimaryColor": "#0F62FE", "LogoUrl": "",
    "CompanyName": "OpenArt", "CompanyAddress": "", "SupportEmail": "", "WebsiteUrl": ""
  }
}
```

### Provider

| Giá trị | Hành vi |
|---|---|
| `"File"` (mặc định) | **Không gửi thật**, ghi file `.html` vào `TransConnect.Host/sent-emails/`. Mở bằng trình duyệt để xem. |
| `"Smtp"` | Gửi thật qua MailKit. |

Luôn để `"File"` khi phát triển: tránh spam hộp thư thật và tránh đốt hạn mức gửi của Gmail.

### Gmail

- `Smtp.Password` là **App Password** (16 ký tự), không phải mật khẩu tài khoản. Phải bật 2FA
  mới tạo được App Password.
- Hạn mức khoảng **500 email/ngày**. Đủ cho đồ án và cho email giao dịch.
- Gmail **cấm** dùng SMTP để gửi thư quảng cáo hàng loạt. Nếu sau này có nhu cầu đó, phải
  chuyển sang dịch vụ chuyên dụng (Brevo/Resend/SES) — chỉ cần viết thêm một `IEmailProvider`.

### Branding

`AppName` là tên **sản phẩm** hiển thị cho người dùng (TransConnect). `CompanyName` là tên
**công ty** ở chân email (OpenArt).

Mọi giá trị trong `Branding` tự động dùng được trong template dưới dạng placeholder
(`{{AppName}}`, `{{PrimaryColor}}`...). **Không hard-code branding vào template** — thêm logo
hay đổi màu chỉ nên là sửa config, không phải sửa code.

Chưa cấu hình `LogoUrl` thì header tự hiện `AppName` bằng chữ, email vẫn tử tế và không có ô
ảnh lỗi.

## Template

Đặt tại `TransConnect.Modules.Notification/Templates/`, nhúng vào DLL dạng Embedded Resource
(khai trong `.csproj`) nên không cần copy thư mục khi deploy.

```
Templates/
├── _Layout.html              khung chung: header, footer, màu thương hiệu
├── email-verification.html   phần ruột (HTML)
├── email-verification.txt    bản plain-text
└── ...
```

Mỗi email cần **đủ cặp `.html` + `.txt`**. Bản plain-text là bắt buộc: email chỉ có HTML bị bộ
lọc spam trừ điểm.

### Cú pháp

- Tiêu đề khai ngay trong file HTML: `<!--subject: Mã xác thực {{AppName}}-->`
- Dòng xem trước trong hộp thư: `<!--preheader: ...-->`
- Placeholder: `{{TenKhoa}}`, không phân biệt hoa thường.
- Placeholder không có giá trị sẽ thành chuỗi rỗng — không bao giờ lọt `{{Xxx}}` ra email thật.

### Quy tắc bắt buộc khi sửa HTML email

Email client **không** render như trình duyệt. Ba ràng buộc dưới đây là bắt buộc:

1. **Bố cục bằng `<table>`** — Outlook render bằng engine của Word, flexbox/grid không chạy.
2. **CSS phải inline** trên từng thẻ — thẻ `<style>` bị nhiều client lược bỏ.
3. **Rộng tối đa 600px.**

Ngoài ra: ảnh phải có `alt`, và đừng nhồi nội dung quan trọng vào ảnh vì nhiều client chặn ảnh
mặc định.

> Khi cần layout mới, nên lấy nền từ boilerplate email đã được kiểm chứng (vd Cerberus) thay vì
> tự viết từ đầu — đây là chỗ rất dễ mất thời gian vô ích.

### Bảo mật: HTML-encode

`EmbeddedTemplateRenderer` **tự HTML-encode mọi giá trị placeholder do bên gọi truyền vào**
(`FullName` là dữ liệu người dùng nhập). Chỉ các khối HTML do hệ thống tự dựng
(`HeaderBrand`, `FooterMeta`, `Body`) mới được để nguyên.

Nếu sau này thêm placeholder chứa HTML có chủ đích, phải xử lý riêng — **đừng tắt encode cho
toàn bộ**.

## Thêm một loại email mới

1. Thêm hằng số vào `EmailTemplateCodes` (`TransConnect.Common/Notifications/`). Giá trị phải
   khớp tên file template.
2. Tạo `Templates/<ten-template>.html` + `.txt`, khai `<!--subject: ...-->` trong file HTML.
3. Ở module nghiệp vụ, inject `IEmailQueue` rồi gọi:

```csharp
await emailQueue.EnqueueAsync(new EmailRequest
{
    ToEmail = user.Email,
    ToName = user.FullName,
    TemplateCode = EmailTemplateCodes.TenTemplate,
    Placeholders = new Dictionary<string, string> { ["Khoa"] = "giá trị" }
}, cancellationToken);
```

4. Test bằng `Provider: "File"` rồi mở file HTML sinh ra để kiểm tra.

Không cần đụng vào worker, provider hay `Program.cs`.

## Giới hạn đã biết

Từ 11/10/2026: cấu hình Email được validate khi startup (provider File/Smtp, tham số worker dương, backoff hợp lệ; SMTP yêu cầu host/port/credential/địa chỉ người gửi). Test `TC_EMAIL_001` kiểm chứng register → render HTML an toàn → hàng đợi DB; không chạy gửi SMTP/worker. Các giới hạn bên dưới vẫn còn.

**Enqueue không cùng transaction với nghiệp vụ.** Hàng đợi nằm ở `NotificationDbContext`, khác
với DbContext của module gọi, nên hai bên không chung transaction.

→ **Luôn gọi `EnqueueAsync` SAU khi nghiệp vụ đã lưu thành công.** Enqueue hỏng thì chỉ mất
email (người dùng xin gửi lại được); còn enqueue trước mà nghiệp vụ rollback thì email báo
"giao dịch thành công" cho một giao dịch không tồn tại — tệ hơn nhiều.

**Chỉ chạy đúng với 1 instance.** Worker chưa khoá row khi bốc, nên chạy nhiều instance sẽ có
khả năng 2 worker cùng gửi 1 email. Khi nào cần scale ngang thì bổ sung `UPDLOCK, READPAST`
vào câu bốc row.

**Chưa có gửi theo lịch.** Cột `ScheduledAt` đã có sẵn và worker đã lọc theo nó, nên khi cần chỉ
việc truyền `EmailRequest.ScheduledAt` — không phải migrate bảng hay sửa chỗ gọi.

**Chưa tách email quảng cáo.** Hiện chỉ có email giao dịch. Nếu sau này gửi thư quảng cáo, phải
tách hẳn (thêm `Category`, danh sách opt-out, link unsubscribe): người dùng bấm "Spam" trên thư
quảng cáo sẽ làm mất uy tín domain, kéo theo email **đặt lại mật khẩu** cũng rơi vào hộp thư rác.

## Việc để dành (chưa làm)

### Email thông báo nạp/rút tiền

Template **đã viết sẵn**: `transaction-notice.html` + `.txt`, mã template
`EmailTemplateCodes.TransactionNotice`. Placeholder đang dùng: `FullName`, `TransactionType`,
`Amount`, `TransactionTime`, `BalanceAfter`, `TransactionId`.

Chưa nối vào đâu vì **module ví/thanh toán chưa tồn tại**. Khi có, chỉ cần inject `IEmailQueue`
vào service xử lý giao dịch và gọi `EnqueueAsync` — không phải sửa gì trong module Notification.

Hai điều phải nhớ lúc làm:

1. **Gọi `EnqueueAsync` SAU khi giao dịch đã lưu thành công** (xem mục "Giới hạn đã biết").
   Enqueue trước mà giao dịch rollback thì người dùng nhận email báo nạp tiền thành công cho một
   giao dịch không tồn tại.
2. Kiểm tra lại danh sách placeholder trong template có khớp với dữ liệu module ví thật sự có
   không — bộ hiện tại chỉ là dự kiến, sửa template thoải mái.

### Gửi theo lịch

Cột `ScheduledAt` và bộ lọc trong worker đã sẵn sàng, chỉ cần truyền `EmailRequest.ScheduledAt`.
Chưa có nhu cầu cụ thể nên chưa làm gì thêm.

## Test nhanh

Ở môi trường `Development` có endpoint xếp hàng 1 email mẫu, dùng để kiểm tra template và App
Password mà không phải chạy qua luồng đăng ký:

```
POST /dev/test-email?to=abc@example.com
```
