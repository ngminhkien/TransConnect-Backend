# Quy tắc đặt tên & chia thư mục

## Cấu trúc thư mục của 1 module

Mỗi module (`TransConnect.Modules.<TenModule>`) tổ chức nội bộ theo cùng 1 khuôn, bất kể module lớn hay nhỏ:

```
TransConnect.Modules.<TenModule>/
├── Entities/                 # entity, kế thừa BaseEntity nếu cần audit/soft-delete
├── Enums/                    # enum của module, mỗi enum một file riêng
├── Persistence/
│   ├── <TenModule>DbContext.cs
│   ├── Configurations/       # IEntityTypeConfiguration<T>, 1 file/entity
│   └── <TenModule>Seeder.cs  # nếu cần seed dữ liệu mặc định
├── Dto/
│   ├── Requests/              # XxxRequest
│   └── Responses/             # XxxResponse
├── Services/                  # IXxxService + XxxService cạnh nhau
├── Controllers/                # controller mỏng, xem docs/coding-conventions.md
└── <TenModule>ModuleExtensions.cs   # AddXxxModule(this IServiceCollection, IConfiguration)
```

Ví dụ tham chiếu: `TransConnect.Modules.Identity`.

### Khi `Services/` bắt đầu nhiều file

Giữ phẳng khi còn ít. **Từ khoảng 8–10 service trở lên** (tức 16–20 file vì mỗi service có kèm
interface), gom theo **nhóm nghiệp vụ** — không gom theo loại kỹ thuật:

```
Services/
├── Auth/              AuthService, TokenService, PasswordHasher, VerificationCodeService
└── Authorization/     RoleService, PermissionService
```

- Chỉ **1 cấp thư mục con**. Lồng sâu hơn (`Services/Auth/TokenServices/TokenService.cs`) làm
  đường dẫn dài mà không rõ thêm gì.
- Interface nằm **cạnh** class nó mô tả, không tách ra thư mục `Interfaces/` riêng.
- Namespace mirror theo thư mục: `TransConnect.Modules.Identity.Services.Auth`.
- Áp dụng tương tự cho `Dto/`, `Entities/` khi chúng phình to.

Gom thư mục là để **dễ tìm**, không phải để trông chuyên nghiệp. Module còn 5–6 service mà đã
chia 3 thư mục thì mỗi lần tìm file phải mở thêm một lớp — lỗ nhiều hơn lãi.

## Quy ước đặt tên

- **Namespace**: mirror theo đường dẫn thư mục, prefix `TransConnect.Modules.<TenModule>.<SubFolder>`.
- **DTO**: hậu tố thống nhất `XxxRequest` (input) / `XxxResponse` (output) — không dùng lẫn `XxxDto`, `XxxVm`, `XxxModel`.
- **Interface**: prefix `I` (`IAuthService`, `IRoleService`).
- **Role/Permission code**: PascalCase cho tên Role (`Admin`, `User`, không dùng `ADMIN_COMPANY` kiểu SCREAMING_SNAKE); Permission code dạng `resource.action` chữ thường (`roles.manage`, `booking.create`).
- **Async method**: luôn hậu tố `Async` (`GetByIdAsync`, `RegisterAsync`).
- **File = 1 class/interface/record public**, tên file trùng tên type.
- **Enum**: đặt trong `Enums/` của module sở hữu, namespace theo thư mục; không để trong `Entities/` hoặc chung file với entity. Gán giá trị số tường minh cho enum lưu trong DB và giữ nguyên giá trị khi đổi tên/di chuyển type.

## Nguyên tắc chia module — không chia quá nhỏ

- Một module nên gói gọn 1 bounded context nghiệp vụ rõ ràng có nhiều entity/luồng nghiệp vụ liên quan chặt với nhau — không tạo module chỉ vì 1-2 entity đơn lẻ.
- Trước khi tạo module mới, tự hỏi: "Domain này có thực sự độc lập về nghiệp vụ và có thể phát triển/scale riêng sau này không?" Nếu câu trả lời mơ hồ, **gộp vào module gần nhất có liên quan** thay vì tách.
- Ví dụ áp dụng cho dự án này: `Booking`/`Matching` (lớp kết nối cung-cầu giữa xe khách và xe tải) nên **gộp chung 1 module** thay vì tách `Booking` và `Matching` riêng, vì chúng luôn thay đổi cùng nhau và chia sẻ phần lớn entity.
- Chỉ tách module thành project riêng khi nó thực sự cần: (a) team/người phụ trách riêng, (b) khả năng tách deploy độc lập sau này (vd module Game — xem quyết định kiến trúc gốc), hoặc (c) domain boundary đã rõ ràng và ổn định.
- Việc gộp/tách module là quyết định có thể sửa sau — nhưng sửa sau tốn công hơn nhiều so với cân nhắc kỹ từ đầu, nên ưu tiên gộp khi còn nghi ngờ.
