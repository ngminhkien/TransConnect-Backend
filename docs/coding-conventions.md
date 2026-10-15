# Quy tắc viết code (Controller / Service / Repository / Exception)

Mục tiêu: code sạch, thống nhất giữa các tầng, dễ mở rộng khi thêm module mới.

## Controller

- Chỉ làm 3 việc: nhận request (đã tự validate qua `[ApiController]` + DataAnnotations), gọi **đúng 1** Service method, trả `ActionResult<ApiResponse<T>>`.
- **Không** chứa business logic (if/else nghiệp vụ, tính toán, query trực tiếp DbContext).
- **Không** tự `try/catch` để bắt lỗi và trả response lỗi — mọi exception được `AppExceptionHandler` (trong `TransConnect.Common`) xử lý tập trung. Nếu 1 lỗi nghiệp vụ cần trả về, Service phải `throw` exception cụ thể, không phải Controller tự xử lý.
- Route đặt qua `[Route("<resource-so-nhieu>")]` ở class, HTTP verb attribute (`[HttpGet]`, `[HttpPost]`...) ở method, không mix route string thủ công.
- Áp `[Authorize]`/`[Authorize(Roles = "...")]` ở mức class khi cả controller cần cùng 1 điều kiện quyền; chỉ áp ở method khi có ngoại lệ.

## Service

- Chứa toàn bộ business logic của module.
- Luôn có interface `IXxxService` đi kèm class `XxxService`, đăng ký qua `AddScoped` trong `<TenModule>ModuleExtensions.cs`.
- Method đặt tên verb rõ nghĩa + hậu tố `Async` (`RegisterAsync`, `AssignPermissionAsync`).
- Nhận/trả **DTO** (`Dto/Requests`, `Dto/Responses`), không để lộ Entity ra khỏi tầng Service.
- Lỗi nghiệp vụ → `throw` exception cụ thể từ `TransConnect.Base.Exceptions` (`NotFoundException`, `ConflictException`, `ValidationAppException`, `UnauthorizedAppException`). **Không** trả `null`/`bool`/`-1` mập mờ để báo lỗi.
- Khi cần join/aggregate phức tạp qua nhiều bảng (vd tính permission của user), có thể query trực tiếp qua DbContext của module thay vì ép dùng `IRepository<T>` — repository chỉ bắt buộc cho CRUD đơn giản trên 1 aggregate root.

## Repository (khi dùng)

- Chỉ CRUD/query đơn giản qua `IRepository<T>` (`TransConnect.Data`), không chứa business rule.
- Không tạo repository cho join table thuần (vd `UserRole`, `RolePermission`) — thao tác trực tiếp qua DbContext trong Service phụ trách quan hệ đó.

## Exception handling

- Toàn bộ exception nghiệp vụ kế thừa `AppException` (`TransConnect.Base.Exceptions`), có `ErrorCode` riêng.
- `AppExceptionHandler` (implement `IExceptionHandler` — cơ chế chuẩn từ .NET 8, đăng ký qua `AddExceptionHandler<AppExceptionHandler>()` + `app.UseExceptionHandler()`) là **nơi duy nhất** map exception → HTTP status + `ApiResponse<T>`:
  - `NotFoundException` → 404
  - `ValidationAppException` → 400
  - `ConflictException` → 409
  - `UnauthorizedAppException` → 401
  - `ForbiddenAppException` → 403 (tài khoản đã xác thực nhưng thiếu quyền nghiệp vụ)
  - `TooManyRequestsException` → 429
  - `ServiceUnavailableAppException` → 503 (ví dụ kho file tạm không truy cập được; không kèm lỗi SDK/credentials trong response)
  - Exception khác (không lường trước) → 500, log qua `ILogger`, **không** trả message chi tiết ra ngoài.
- Khi thêm loại lỗi mới: thêm exception mới kế thừa `AppException` trong `Base`, thêm 1 case trong `AppExceptionHandler` — không tạo cơ chế xử lý lỗi song song ở nơi khác.

## DTO & validation

- Trường bắt buộc phải ghi rõ `[Required(ErrorMessage = "...")]`; mọi DataAnnotation có thông báo tiếng Việt. MVC đã tắt implicit Required cho kiểu non-nullable để tránh thông báo mặc định tiếng Anh. `AddVietnameseApiValidation` trả lỗi theo field trong `ApiResponse.errors`.
- Hậu tố `XxxRequest`/`XxxResponse` (xem `naming-and-folder-conventions.md`).
- Validate field đơn giản (bắt buộc, độ dài, định dạng email...) bằng **DataAnnotations** trực tiếp trên record request — `[ApiController]` tự trả 400 khi model invalid, không cần code thêm.
- Chỉ cân nhắc thêm **FluentValidation** khi phát sinh rule validate liên-field hoặc rule phụ thuộc dữ liệu (vd "ngày kết thúc phải sau ngày bắt đầu", "chỉ Admin mới được set field X") — không thêm thư viện này ngay từ đầu để tránh phức tạp hoá sớm.

## Dependency Injection

- Mặc định `AddScoped` cho Service, Repository, DbContext (đúng vòng đời 1 request).
- **Không đăng ký interface không generic dùng chung giữa các module** (vd `IUnitOfWork`): module đăng ký sau sẽ ghi đè module trước, gây bug rất khó tìm. Nếu cần abstraction dùng chung, để nó generic theo `DbContext` (`IXxx<TContext>`) hoặc đặt interface trong `Common` với implementation riêng của từng module.
- Chỉ dùng `AddSingleton` cho thứ thực sự stateless và không giữ state theo request (vd 1 class chỉ đọc config, không có dependency scoped nào).
- Không dùng `AddTransient` trừ khi có lý do cụ thể (tránh tạo nhiều instance không cần thiết).

## Async

- Mọi thao tác I/O (DB, gọi HTTP ra ngoài, đọc file) phải `async`/`await`.
- Không dùng `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` — có thể gây deadlock trong ASP.NET Core.
- Luôn nhận `CancellationToken cancellationToken = default` ở method Service/Repository public và forward xuống các lời gọi async bên trong.
