# Quy ước API & tích hợp client

Giai đoạn 1 bổ sung /organizations, /drivers và /organizations/{id}/vehicles, cùng envelope tiếng Việt và lỗi nghiệp vụ 403. Auth request thêm accountGroup; accountId là alias userId trong response. ID tổ chức trên URL luôn được service đối chiếu quyền DB, không coi là bằng chứng truy cập. Ví dụ đầy đủ: [phase-1-foundation-test-flow](phase-1-foundation-test-flow.md).

Contract dùng chung: [TransConnect-Docs/05-api/conventions.md](../../TransConnect-Docs/05-api/conventions.md). Quyết định của chủ dự án: [API-BASELINE-001](../../TransConnect-Docs/09-decisions/api-baseline.md).

## URL và xác thực

Dùng `/auth`, `/roles`, `/permissions`, không có `/api` hoặc `/api/v1`. URL cũ trả 404; frontend cần cập nhật base path. Tên Swagger document `v1` không tạo route version.

Gửi `Authorization: Bearer <access_token>`. Giữ `POST /auth/refresh` với JSON refresh token; mỗi lần dùng phải lưu cặp token mới. `GET /auth/me` đọc hồ sơ và quyền hiện tại từ DB, không trả token/hash mật khẩu/Google ID; user bị khóa hoặc soft-delete trả 401. Quyền kiểm tra trên endpoint khác vẫn lấy từ JWT cho tới lần phát token kế tiếp.

## Response và mã lỗi

```json
{ "succeeded": true, "message": null, "errorCode": null, "data": {}, "errors": null }
```

```json
{
  "succeeded": false,
  "message": "Dữ liệu không hợp lệ.",
  "errorCode": "VALIDATION_ERROR",
  "data": null,
  "errors": { "email": ["Email không đúng định dạng."] }
}
```

`errors` ánh xạ tên trường JSON tới mảng thông báo tiếng Việt; null nếu không có chi tiết theo trường. Lỗi JSON có thể có key `$...`, lỗi body tổng thể dùng `body`. Service khi ném `ValidationAppException` dùng tên trường JSON. Mã lỗi là hằng số ổn định để client xử lý; thông báo là tiếng Việt, không dùng làm khóa so sánh.

| HTTP | Mã trong `TransConnect.Base/Exceptions/AppErrorCodes.cs` | Ý nghĩa |
| --- | --- | --- |
| 400 | `VALIDATION_ERROR` | Dữ liệu không hợp lệ |
| 401 | `UNAUTHORIZED` | Cần đăng nhập / xác thực không hợp lệ |
| 403 | `FORBIDDEN` | Không đủ quyền |
| 404 | `NOT_FOUND` | Không tìm thấy tài nguyên |
| 405 | `METHOD_NOT_ALLOWED` | Sai phương thức HTTP |
| 409 | `CONFLICT` | Dữ liệu đã tồn tại / xung đột |
| 415 | `UNSUPPORTED_MEDIA_TYPE` | Định dạng nội dung không được hỗ trợ |
| 429 | `TOO_MANY_REQUESTS` | Quá nhiều yêu cầu |
| 500 / 5xx rỗng | `INTERNAL_ERROR` | Đã xảy ra lỗi hệ thống |
| 4xx rỗng khác | `REQUEST_FAILED` | Không thể xử lý yêu cầu |

Thành công giữ 200 và envelope cho endpoint hiện có. Phân trang khi cần dùng `ApiResponse<PagedResult<T>>`, `data` gồm `items`, `pageNumber`, `pageSize`, `totalRecords`, `totalPages`; danh sách role/permission hiện vẫn là mảng. Chưa thêm traceId/metadata/statusCode vào body. Envelope riêng này không phải ProblemDetails.

## Các đường xử lý lỗi

- `AppExceptionHandler`: đã có và tiếp tục là nơi duy nhất map exception nghiệp vụ sang HTTP; giữ `ValidationAppException.Errors`. Lỗi 500 trả thông báo chung, chi tiết chỉ ghi log.
- `AddVietnameseApiValidation`: cấu hình ModelState 400 cùng envelope, thông báo DTO/model binding tiếng Việt; không đưa chi tiết parser JSON ra client.
- JWT challenge/forbidden và `RequirePermissionAttribute`: trả envelope 401/403.
- `UseApiStatusCodePages`: bổ sung envelope cho lỗi HTTP rỗng (404/405/415...), không ghi đè body đã có. Đặt sau exception handler, trước các middleware xử lý request.
- Ngoài MVC dùng `ApiResponseWriter.WriteFailureAsync`. Không tự try/catch trong controller.
- Swagger, redirect, lỗi proxy/server ngoài pipeline hoặc kết nối đã ngắt không thuộc contract JSON API.

## CORS

Khai `Cors:AllowedOrigins` cho web client; Flutter native không chịu CORS. Danh sách rỗng cho phép mọi origin ở Development, không cho origin ở Production. CORS đứng trước authentication/authorization.

## Kiểm thử

Từ thư mục Backend, với SDK .NET 10:

```powershell
dotnet build TransConnect.sln
dotnet test TransConnect.Tests/TransConnect.Tests.csproj
```

Tests dùng HTTP test host và SQLite trong bộ nhớ, không migrate SQL Server hoặc chạy worker email. Bao phủ validation/binding, mã lỗi/exception, đường dẫn cũ, 401/403, media type và `/auth/me` với quyền DB hiện tại/tài khoản bị khóa hoặc xóa.
