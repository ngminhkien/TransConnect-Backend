# Quy tắc dùng các thư viện base (Base / Common / Data)

## Hướng phụ thuộc bắt buộc

```
TransConnect.Base        <- không phụ thuộc project nào khác
TransConnect.Common       -> Base
TransConnect.Data         -> Base
TransConnect.Modules.Xxx  -> Base, Common, Data
TransConnect.Host         -> Base, Common, Data, tất cả Modules.Xxx
```

- `Base` không bao giờ được reference `Common` hay `Data`.
- `Common` và `Data` không được reference lẫn nhau.
- Không project nào trong `Modules/` được reference trực tiếp 1 module khác (vd `TransConnect.Modules.Booking` không được reference `TransConnect.Modules.Identity`). Nếu 1 module cần dữ liệu/hành vi của module khác, expose qua interface đặt trong `Common` (hoặc gọi HTTP nội bộ nếu về sau tách microservice), không gọi thẳng Entity/DbContext của module kia.

## Cái gì thuộc về đâu

**`TransConnect.Base`** — chỉ chứa những khái niệm domain thuần túy, không phụ thuộc framework nào ngoài BCL:
- `BaseEntity`, `IAuditable`
- Các exception dùng chung (`NotFoundException`, `ValidationAppException`, `ConflictException`, `UnauthorizedAppException`)

**`TransConnect.Common`** — cross-cutting kỹ thuật, không chứa logic nghiệp vụ của bất kỳ module nào:
- `ApiResponse<T>`, `PagedResult<T>`
- `AppExceptionHandler` (map exception -> HTTP response)
- `ICurrentUser`/`CurrentUser`, `AppClaimTypes`
- `JwtOptions` (cấu hình JWT dùng chung giữa nơi phát token và nơi validate token)

**`TransConnect.Data`** — hạ tầng truy cập dữ liệu dùng chung, không chứa entity của module nào:
- `IRepository<T>`/`Repository<T,TContext>` (`Remove` = xoá mềm, `HardRemove` = xoá cứng)
- `AppDbContextBase` (soft-delete query filter tự động, auto-update `UpdatedAt`)

> **Không tạo `IUnitOfWork` dùng chung.** Interface không generic sẽ bị đăng ký trùng giữa các
> module (module đăng ký sau ghi đè module trước), khiến service commit nhầm vào `DbContext` của
> module khác và **không save gì cả mà cũng không báo lỗi**. `DbContext` của module bản thân nó
> đã là Unit of Work — gọi thẳng `dbContext.SaveChangesAsync()` trong service của module đó.

## Checklist trước khi thêm code mới vào 3 thư viện này

1. Code có phải logic nghiệp vụ riêng của 1 module cụ thể không? → Nếu có, để trong module đó, **không** đưa vào Base/Common/Data.
2. Code có được dùng bởi **từ 2 module trở lên** không? → Nếu chỉ 1 module cần, để trong module đó trước, chỉ "nâng cấp" lên thư viện chung khi có module thứ 2 thực sự cần dùng lại.
3. Thêm dependency (NuGet) mới vào Base/Common/Data nghĩa là **mọi module đều gánh dependency đó** — cân nhắc kỹ trước khi thêm.
4. Thay đổi ở 3 thư viện này ảnh hưởng toàn hệ thống — build lại toàn bộ solution sau khi sửa, đừng chỉ build riêng project vừa đổi.
