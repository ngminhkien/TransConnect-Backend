# Test toàn giai đoạn nền tảng người dùng — 1 BE và 3 FE

Cập nhật nền tảng **22/10/2026**, Locations **27/10/2026**, tìm địa điểm và redesign hai app **01/11/2026** theo code hiện có. Đây là file test thủ công duy nhất. Gói FE-APPS-06 bổ sung kiểm tra UI/responsive đã được giao; các tích hợp ngoại vi vẫn theo phạm vi nghiệm thu riêng.

**Trạng thái:** đã có code và kiểm thử tự động; chưa xác nhận nghiệm thu chạy thật với SQL Server, SMTP, Cloudinary, S3 và trình duyệt/thiết bị. Kết quả thực hiện và lịch sử giữ tại [progress-tracker](../../TransConnect-Docs/00-project/progress-tracker.md#test-evidence).

## 1. Những phần đã làm

### LOC-CANCEL-03 — Hủy request Locations, 01/11

| Ca test | Thao tác | Kỳ vọng |
|---|---|---|
| TC-LOC-CANCEL-M01 — Hủy tải dữ liệu | Restart BE để nạp bản sửa; mở Management ở chế độ dev, chuyển nhanh Địa điểm → mục khác → Địa điểm; mở/đóng form, đổi tỉnh khi phường đang tải | Request cũ có thể hiện `(canceled)` ở Network. Request mới vẫn trả 200 và hiển thị đúng dữ liệu/quyền. Client abort không sinh chuỗi `HTTP ... 500` / JWT `Exception occurred while processing message` / `An exception was thrown attempting to execute the error handler`. BE có thể ghi 499 cho request đã hủy; đây là trạng thái log, không bảo đảm client đã đóng kết nối nhận được response. Nếu giữ nguyên trang vẫn nhận 500 thì ghi URL/thời điểm/log mới để phân biệt lỗi SQL thật |

Ca tự động đã tách hủy SqlClient với SQL timeout/lỗi thật và giữ kiểm tra quyền DB từng request. Không cần import/migrate lại hoặc cấu hình API key bản đồ cho ca này. Bằng chứng tại [LOC-CANCEL-03](../../TransConnect-Docs/00-project/progress-tracker.md#loc-cancel-03); chưa nghiệm thu live SQL/browser.

### FE-EXPERIENCE-03 — Horizontal workspace và giữ phiên, 31/10

Người dùng đã chọn bố cục ngang và yêu cầu không mất đăng nhập khi đóng/mở tab. Giữ BE token JSON/refresh/logout hiện hữu, không apply migration hoặc thay TTL. [Bằng chứng](../../TransConnect-Docs/00-project/progress-tracker.md#fe-experience-03) phân biệt fixture với live.

| Ca test | Thao tác | Kỳ vọng |
|---|---|---|
| TC-FE-EXPERIENCE-M01 — Đóng/mở tab | Đăng nhập Admin/Operator trong môi trường phát triển, đóng tab, mở lại đúng origin; thử access token đã hết hạn nhưng refresh còn hiệu lực | Khôi phục đúng tài khoản/ngữ cảnh, refresh khi 401, không bắt nhập lại chỉ vì đóng tab. Không hứa giữ phiên sau xóa storage/ẩn danh hoặc refresh bị BE thu hồi/hết hạn |
| TC-FE-EXPERIENCE-M02 — Logout/đa tab | Mở hai tab cùng tài khoản; logout ở một tab; đóng/mở lại và thử refresh đang chờ | Tab còn lại bỏ dữ liệu bảo vệ, không tự hồi phiên. Persistent storage được xóa; phản hồi refresh/retry cũ không khôi phục hoặc xóa phiên mới. Backend revoke offline vẫn có giới hạn cũ |
| TC-FE-EXPERIENCE-M03 — Đổi account/môi trường/lỗi | Đổi account từ tab khác; gửi thao tác của page cũ; đổi API base URL; block storage; giả lập refresh 503 hoặc 401 | Page cũ không thực hiện mutation bằng account mới. Key lưu phiên tách API. Storage bị chặn fallback không crash, không cam kết bền vững. 503 giữ phiên để retry; refresh bị từ chối xóa phiên. Web Locks phối hợp refresh đa tab khi browser hỗ trợ |
| TC-FE-EXPERIENCE-M04 — UI ngang và hồi quy | Desktop bốn nav/link detail/Back, mobile drawer, danh sách/filter/error/empty; login/Operator register | Header đỏ + nav ngang thay rail desktop, selection đúng URL/quyền. 320/390/768/1280/1440px không tràn. Giữ điều kiện duyệt tài xế/khóa account, form và các API Locations cũ. Bài `check:ui -- --admin --session` dùng fixture, live vẫn phải kiểm riêng |

<a id="fe-redesign"></a>
### FE-REDESIGN-02 — Management mới, 31/10

Người dùng đã yêu cầu redesign, giữ màu rule và contract BE, chọn danh sách làm trung tâm. Đây là delta UI, không thay policy tài khoản, xét duyệt, locations hoặc tiêu chí SQL/storage thật. Bằng chứng thực chạy ghi tại [tracker](../../TransConnect-Docs/00-project/progress-tracker.md#fe-redesign-02).

| Ca test | Thao tác | Kỳ vọng |
|---|---|---|
| TC-FE-REDESIGN-M01 — Sidebar/route | Chuyển Đơn vị vận tải → Tài xế → Địa điểm → Tài khoản; mở chi tiết tổ chức, Back, refresh deep link | Đúng URL/nội dung, chỉ một `aria-current=page`; mục chọn nền đỏ đặc/chữ trắng/marker, mục cũ hết chọn. Trang chi tiết vẫn chọn mục cha. Thử Admin chỉ `locations.manage`: không hiện menu ngoài quyền và logo dẫn qua landing hợp lệ |
| TC-FE-REDESIGN-M02 — Mobile navigation | Thu nhỏ 390/320/768px, mở menu bằng bàn phím, Tab/Shift+Tab, Escape; mở lại và chọn mục | Menu đóng inert, không focus vào link offscreen; mở đưa focus vào menu, Tab không lọt ra nền; Escape trả focus trigger, chọn mục đóng menu/đến nội dung. Không khóa scroll hoặc inert nền sau khi resize sang desktop |
| TC-FE-REDESIGN-M03 — Lists/states | Bảng org/accounts/drivers, bộ lọc, phân trang; thử rỗng/lỗi/loading, tên/email dài; xem 320/390/768/1280/1440px | Bảng desktop và records mobile có đủ thông tin/thao tác, không tràn viewport. Tổng số/phân trang lấy BE, không KPI giả. Error khác empty và có cách phục hồi theo hook hiện có; typography tiếng Việt tải local |
| TC-FE-REDESIGN-M04 — Mutation contract | Khóa/mở tài khoản và duyệt/từ chối tài xế trong môi trường test; thử khi lý do rỗng/tự khóa; thao tác busy | Giữ guard/lý do và quyền BE. Account PUT `/accounts/{id}/status` dùng `isActive/reason`; Driver POST `/drivers/{id}/review` dùng `approved/note`. Reload trạng thái từ BE sau thành công, lỗi không báo thành công. Browser fixture chỉ xác minh UI/request shape; live success phải thử riêng |
| TC-FE-REDESIGN-M05 — Regression foundation/locations | Login/Operator register, Partner profile/workspace, detail hồ sơ; form địa điểm tỉnh→phường/map/nhập tay/xác nhận/lưu, history và review | UI mới giữ flow/validation/permission/version của contract cũ; không thêm Trip/Booking/analytics hoặc thay token/CORS. Tiếp các TC-LOC-M01–M06 cho live dữ liệu/tiles; chụp fixture không thay nghiệm thu map/BE thật |

<a id="fe-quality"></a>
### FE-QUALITY-01 — Management base, 30/10

Đợt cải thiện FE được chủ dự án giao riêng, không biến toàn bộ UI polish thành điều kiện nghiệm thu nền tảng cũ. Kết quả/bằng chứng ở [tracker](../../TransConnect-Docs/00-project/progress-tracker.md#fe-quality-01). Dùng tài khoản/dữ liệu test trên môi trường phát triển; bộ `npm run check:ui` dùng API fixture không thay các ca live dưới đây.

| Ca test | Thao tác | Kỳ vọng |
|---|---|---|
| TC-FE-QUALITY-M01 — Nhận diện/ngữ cảnh | Mở `/login`, đổi Admin ↔ Đối tác, bật/tắt tạo tài khoản | UI/title là VieGo. Heading, mô tả, CTA và hướng dẫn đúng chế độ. Không có đăng ký Admin công khai; đổi mode xóa lỗi cũ. Repo/API/session key không đổi |
| TC-FE-QUALITY-M02 — Form/đang xử lý | Form trống/email sai, điền hợp lệ; hiện/ẩn mật khẩu; gửi khi mạng chậm, nhấn lại | Validation không gửi form thiếu; autocomplete current-password/new-password đúng mode; có accessible name/state cho hiện mật khẩu. Busy khóa cả Họ tên/cổng/checkbox/submit, có trạng thái chờ và không gửi lặp. Với API thật: đăng nhập đúng vai trò; đăng ký Operator đi tiếp xác thực email, không suy fixture chứng minh thành công live |
| TC-FE-QUALITY-M03 — Lỗi/bàn phím | Dùng API fixture hoặc validation từ BE, nhập sai rồi submit; dùng Tab/Enter trong summary | Lỗi giữ dữ liệu, summary nhận focus, lỗi field đọc được và liên kết đến ô; field có aria-invalid/description; thông báo không chỉ dùng màu. Sai mật khẩu/mất mạng không hiện thành công |
| TC-FE-QUALITY-M04 — Responsive/deploy | Xem 320/390/768/1440px; tăng zoom/cỡ chữ, màn hình ngang; khi có host, truy cập/refresh deep link trực tiếp | Không tràn ngang/che CTA; input mobile đọc được. Focus và điều khiển còn thao tác được. Host phục vụ asset/SPA đúng và dùng API HTTPS đúng origin; chưa có URL deploy thì ghi NOT RUN, không coi preview là pass |

<a id="locations"></a>
### Locations — phần bổ sung 27/10, cần chạy với DB đã chuẩn bị

**Delta UI/abort 30/10:** người dùng đã báo import v5.2.0 active thành công. Restart BE để nạp xử lý client disconnect (OperationCanceledException + RequestAborted → 499, không coi là lỗi 500; cancellation nội bộ vẫn báo lỗi), refresh Management. Trang Locations có hai tab, tìm kiếm bằng Enter/nút, form thêm/sửa chia thông tin–bản đồ và thông báo lưu. Kiểm tra Admin chỉ có locations.manage vào đúng trang/menu, chọn tỉnh→phường, nhập tọa độ hoặc ghim rồi xác nhận/lưu/mở lại; thu nhỏ 390px, thử lỗi API phải hiện lỗi thay vì danh sách rỗng. 42 test BE và npm check/16 test đã pass; Chrome API giả đã thử form submit/guard/error/mobile, không thay nghiệm thu SQL/tiles thật. Ảnh nền tiles không tải được trong môi trường agent, fallback nhập tay đã thử.

**Delta LOC-MAP-02, 31/10:** TC-LOC-M03 bổ sung sau khi mất mạng/tile lỗi: khôi phục kết nối, bấm **Thử tải lại bản đồ**, ảnh nền nạp lại và thông tin/ghim/tọa độ vẫn giữ nguyên, không cần đóng form. Người dùng báo DNS NXDOMAIN thật; đã đối chiếu DNS mặc định lỗi nhưng 1.1.1.1/8.8.8.8 phân giải đúng. Người dùng chọn chỉ đổi Secure DNS trong Chrome qua `chrome://settings/security`, không đổi DNS Wi-Fi. Kiểm tra trực tiếp URL tile sau khi đổi rồi quay lại form. Regression dùng tile/API fixture đã PASS cả lỗi→retry→giữ tọa độ; không thay xác nhận provider thật hoặc lưu vào DB.

**Quyết định hiện hành 29/10:** tiếp tục phát triển/test trên TransConnectPhase1Dev; TransConnectDb đã được backup rồi clear dữ liệu test, giữ schema và lịch sử migration. Trong Development, cấu hình ứng dụng chỉ lấy từ appsettings.json + appsettings.Development.json, không nhận User Secrets/env/CLI ghi đè. Helper migration không gán connection string qua env; launcher không còn -EmailProvider. Sửa trực tiếp file Development để chọn DB/Email, giữ file local bị ignore. Dòng helper completed vẫn cần đối chiếu đúng server/DB, không tự bàn giao phần mới sang DB gốc.

**Cập nhật setup 29/10:** helper migration đã thêm forced NuGet restore trước build, xử lý `obj` còn trỏ cache `CodexSandboxOffline` làm MSB3106/CS0006 trên terminal của người dùng. Lượt lỗi đó dừng ở build, chưa bắt đầu migration theo log. DB phát triển hiện chọn TransConnectPhase1Dev và DB nguồn VieGoLocationSource_Dev; xem kết quả audit/clear mới nhất trong tracker. Không coi bản sửa helper là đã nghiệm thu nghiệp vụ.

Code Locations có BE, Customer và Partner/Admin web; chưa có xác nhận nghiệm thu SQL Server hoặc tiles trên thiết bị thật. Setup chi tiết, clone/import nguồn và cấu hình provider tại [locations-setup](reference/locations-setup.md); contract API-LOC-001–020 tại [API Locations](../../TransConnect-Docs/05-api/endpoints/locations-endpoints.md). Giữ bốn terminal/URL nền tảng bên dưới. Không cần khởi động Driver để test Locations.

| Ca | Cách chạy | Kỳ vọng |
|---|---|---|
| TC-LOC-M01 — dữ liệu toàn quốc | Tại Backend, chạy Get-LocationDataset.ps1 theo setup, kiểm tra manifest. Xác định DB Development/test, dừng Host, chạy Update-DevelopmentDatabase.ps1 với `-IncludeLocations` rồi bật lại BE. Admin gọi `POST /locations/admin/datasets/import` bằng payload đã tạo, gọi lại cùng body | Chuẩn bị file cho 34/3321 của bản pin; import lần hai trả cùng datasetId, không nhân bản. `GET /locations/dataset` trả v5.2.0 và checksum; API provinces có 34 bản ghi, wards lọc đúng tỉnh, mã đầu 0 giữ nguyên. Không tự có điểm vận tải |
| TC-LOC-M02 — Customer chính chủ | Customer → Tài khoản → Địa chỉ đã lưu → Thêm địa chỉ; chọn tỉnh/phường, nhập chi tiết, để trống tọa độ và lưu. Xem/sửa. Dùng Customer B gọi list/detail/PUT/DELETE địa chỉ của A qua Swagger | Lưu/mở lại được địa chỉ chưa có ghim, ghi chưa xác nhận. B không list được, truy cập ID của A trả 404; danh mục shared không có địa chỉ riêng. Ngừng dùng bằng UI gỡ khỏi sổ địa chỉ |
| TC-LOC-M03 — map/xác nhận | Thử địa chỉ tại các vùng Bắc/Trung/Nam và một xã/đặc khu trong danh mục. Chạm/kéo ghim, xác nhận, lưu, mở lại; đổi tọa độ/địa chỉ, sau đó chỉ đổi nhãn. Chặn riêng request tile trong trình duyệt để thử nhập tay. Khi Console có `contentscript.js`/`ObjectMultiplex`/`runtime.lastError`, bấm source để phân biệt URL `chrome-extension://` với source web; thử profile Guest. Kiểm tra Network của tile riêng, gồm lỗi DNS `ERR_NAME_NOT_RESOLVED` | Ghim mở lại đúng lat/lon; mặc định chưa xác nhận, đổi vị trí yêu cầu xác nhận lại. Chỉ đổi nhãn giữ xác nhận đã lưu. Tile lỗi giữ form, nhập tay và xác nhận vẫn lưu được; attribution/link hiển thị. Không đổi tỉnh/phường tự động từ tâm bản đồ. Không dùng lỗi extension để kết luận Leaflet lỗi; regression `npm run check:ui -- --locations` dùng API/tile fixture, `--live-tiles` mới thử provider thật, cả hai không thay nghiệm thu lưu BE/DB |
| TC-LOC-M04 — Owner/Admin | Partner → chọn tổ chức đã duyệt với Owner → Địa điểm → Thêm địa điểm, điền tọa độ/xác nhận/lý do. Admin → Địa điểm → xem ghim đề xuất rồi duyệt/từ chối có lý do. Thử tạo trực tiếp bằng Admin | Pending/Rejected không xuất hiện shared. Approved xuất hiện với version 1; Owner không ghi trực tiếp bản công khai. Rejected gửi lại bằng Thêm địa điểm tạo request ID mới. Admin công bố trực tiếp có lịch sử, Customer/Manager không có quyền này |
| TC-LOC-M05 — sửa/ngừng dùng/lịch sử | Owner gửi yêu cầu sửa cùng điểm hoặc ngừng dùng; kiểm tra public trước/sau Admin duyệt. Admin có thể sửa/ngừng dùng trực tiếp có lý do, mở Lịch sử. Cổng/điểm phục vụ khác dùng Thêm địa điểm | Pending không đổi bản công khai. Duyệt tạo version mới, bản cũ bất biến. Điểm ngừng dùng không còn ở shared nhưng Admin vẫn thấy và đọc lịch sử; điểm mới có ID mới. Chưa có ca E2E Trip/Booking |
| TC-LOC-M06 — xung đột/validation | Hai tab Admin mở cùng điểm version N; một tab lưu trước, tab kia gửi version N. Thử duyệt một đề xuất Update dựa trên N sau khi điểm đã đổi. Customer thử chỉ một tọa độ, latitude 91, longitude 181 hoặc phường sai tỉnh | Trả 409 cho dữ liệu cũ, không ghi đè hoặc thêm lịch sử sai; đề xuất lỗi thời vẫn Pending. Validation trả 400 theo field. Customer nhập 0/0 được chấp nhận về miền giá trị, không có nghĩa đã xác minh địa điểm thực tế |

Chi tiết thao tác API cần nhớ: Customer archive dùng `DELETE /locations/my-addresses/{id}?version=N`; Admin archive dùng JSON `{ "version": N, "reason": "Lý do ngừng dùng" }`. Duyệt dùng `POST /locations/admin/requests/{id}/review` với `{ "version": N, "approve": true, "reason": "Đã đối chiếu vị trí" }`. Lấy UUID/mã/version từ response thực tế, không tự điền ID hoặc owner/actor. Tiến độ và kết quả chạy thực tế chỉ ghi trong tracker; bảng này là hướng dẫn, không là bằng chứng đã pass.

| Phần | Chức năng đã triển khai | Nơi có thể test |
|---|---|---|
| Locations | Địa giới có phiên bản/import, địa chỉ riêng, Owner đề xuất/Admin quản điểm chung, bản đồ ghim/xác nhận/nhập tay và lịch sử | Customer: Tài khoản → Địa chỉ đã lưu; Admin: /admin/locations; Owner: workspace tổ chức → Địa điểm; cần chuẩn bị DB/import theo setup |
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
| SQL Server + .NET 10 | Database Development, connection string/JWT local, apply migration | Người dùng xác nhận script migration thành công 22/10; BE/Swagger chạy được. Agent chưa đối chiếu DB trực tiếp |
| SMTP | Hộp thư test có thể đọc OTP và token lời mời | Chưa nghiệm thu gửi/nhận thật; không dựa vào file thư đã bị dọn sau gửi |
| Cloudinary | CloudName, ApiKey, ApiSecret phía BE | Người dùng đã điền cấu hình 22/10; chưa test avatar/upload |
| S3 demo | Storage/bucket private hoạt động, ComplianceDemo bật trong Development | Cờ demo đã bật ở lần kiểm tra gần nhất; chưa nghiệm thu storage thật |
| Google | ClientId và Google ID token hợp lệ | Tùy chọn; FE Google chưa phải luồng hoàn thiện |

Nhập secret trong `TransConnect.Host/appsettings.Development.json` bị Git ignore hoặc biến môi trường; không ghi secret vào file này/FE/chat. Mẫu tại [appsettings.Development.example.json](../TransConnect.Host/appsettings.Development.example.json). Hướng dẫn [email](reference/email-and-notification.md), [storage và Cloudinary](reference/compliance-demo-setup.md).

### Script cập nhật DB và kiểm tra kết nối

`scripts/Update-DevelopmentDatabase.ps1` đọc `TransConnect.Host/appsettings.Development.json`, dùng `sqlcmd` probe DB được cấu hình. `-ProbeOnly` chỉ chạy `SELECT 1 AS ConnectionProbe`, trả 1 và exit code 0 khi kết nối được; không chạy preflight/build/migration, không chứng minh schema đúng hoặc nghiệp vụ pass. Script yêu cầu file JSON local có connection string; probe không lấy thay thế từ biến môi trường. Khi migrate, EF/Host có thể nhận override cấu hình, nên phải bảo đảm target nhất quán.

Chỉ kiểm tra kết nối (có thể chạy riêng, không cần restore):

```powershell
Set-Location 'C:\Users\PC\Desktop\CNTTB\doantotnghiep\TransConnect-Backend'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Update-DevelopmentDatabase.ps1 -ProbeOnly
$LASTEXITCODE
```

Bỏ `-ProbeOnly` sẽ preflight dữ liệu, build `--no-restore`, rồi `database update` lần lượt Identity → Notification → Operator → Compliance nếu bật trong config hoặc có `-IncludeComplianceDemo`. Flag này chỉ thêm migration Compliance, không bật tính năng demo. Build cần SDK .NET 10 và `tools/dotnet-ef.exe` dưới SDK được chọn; lệnh đầy đủ bên dưới dùng SDK local đã biết. Script không chạy test nghiệp vụ. Lỗi giữa các module không rollback các module đã thành công: kiểm tra history trước retry.

Người dùng đã báo migration thành công 22/10; không cần chạy lại nếu code/migration và DB đích chưa đổi. Tiếp tục test các mục tài khoản/onboarding/SMTP/avatar/giấy demo bên dưới. Nếu cần kiểm tra schema, xem migration history trên đúng DB trong SSMS và đối chiếu [danh mục migration](../../TransConnect-Docs/04-database/phase-1-entities-and-relationships-review.md#current-schema-22-10); không gửi connection string/mật khẩu.

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
# Chỉ cập nhật khi cần áp migration còn thiếu; đã thành công và code không đổi thì không cần chạy lại:
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

**Delta email/Android — cách chạy cập nhật 29/10:** trước đây phát hiện provider File cả trong cấu hình chung và User Secrets; SMTP STARTTLS/auth đã kiểm tra thành công bằng cấu hình local, chưa chứng minh thư đến mailbox. Hiện sửa Email:Provider=Smtp trong appsettings.Development.json, dừng BE rồi chạy `powershell -NoProfile -ExecutionPolicy Bypass -File .\run.ps1` từ Backend. Không dùng -EmailProvider hoặc User Secrets để ghi đè. Khi BE sẵn sàng, xin mã mới một lần từ FE, đợi worker (mặc định 15 giây), kiểm tra log SMTP và Inbox/Spam; xác minh bằng mã mới. Không dùng lại mã đã qua sink hoặc gọi resend liên tục. Ghi kết quả giao thư và verify-email riêng; chưa Accepted.

Android: ADB online/boot hoàn tất tại lúc agent kiểm tra, tái hiện SurfaceSyncGroup timeout khi đưa Customer lên foreground; chưa tái hiện được đen toàn màn hình/mất ADB. Thực hiện Cold Boot trước, giữ dữ liệu AVD; nếu còn đơ thử Software graphics ở lần khởi động tiếp. Hướng dẫn tại Customer README. Xác nhận thao tác được qua nhiều lần chuyển nền/mở lại và không mất kết nối; chưa có bằng chứng sửa dứt điểm lỗi này.

Bổ sung cùng lượt: reboot Android guest thành công; reboot và tắt Impeller riêng lẻ vẫn timeout lúc mở. Sau khi thêm Flutter software rendering, UI hiện và thao tác Home → Orders được. Khi chạy lại bằng Flutter dùng `--no-enable-impeller --enable-software-rendering`; không thay cấu hình renderer production. Chưa xác định nguyên nhân gốc hoặc chứng minh ổn định dài hạn. Probe localhost:5267 không kết nối được nên chưa test lại API/email E2E; khởi động BE theo lệnh trên trước.

**Đã ghi nhận 22/10/2026 — FND-00, theo người dùng:** script báo Development migrations completed; BE/Swagger chạy được; Customer/Driver chạy trên máy ảo. Cloudinary đã điền cấu hình. SMTP, Cloudinary upload và storage giấy demo chưa test; Management chưa có báo cáo. Chưa suy ra login/restore/nghiệp vụ đã pass. Bằng chứng ở [tracker](../../TransConnect-Docs/00-project/progress-tracker.md#fnd-00-user-test-22-10). Tạm dừng chờ người dùng test tiếp ngày mai (23/10) hoặc khi quay lại; chưa nghiệm thu toàn gói.

- Ngày, môi trường và phiên bản code:
- Nhóm tài khoản/FE đã thử:
- Các mục/bước đã pass:
- Bước lỗi, request/response đã che token/secret, kết quả mong đợi và thực tế:
- Phần chưa thử vì thiếu cấu hình:
- Kết luận nghiệm thu: **CHƯA XÁC NHẬN**.

Sau mỗi lượt, ghi kết quả xác nhận vào [tracker](../../TransConnect-Docs/00-project/progress-tracker.md). Giữ file này làm hướng dẫn hiện hành và cập nhật tại chỗ khi chức năng thay đổi.

## Chọn ca test theo nhánh/gói

Theo [roadmap](../../TransConnect-Docs/00-project/roadmap.md), mỗi lượt ghi mã gói, nhánh/base SHA và bộ SHA BE/FE trong tracker; chọn ID E2E/API liên quan ở file này làm test delta. Không yêu cầu chạy toàn bộ sau thay đổi tài liệu/PR template. FND-00 phần workflow chỉ kiểm tra cấu hình/link; smoke DB/FE sẽ thực hiện riêng khi môi trường đủ. Câu hỏi gắn nhánh tại Q&A, không tạo file test thứ hai.

## LOC-SEARCH-03 / FE-APPS-06 — tìm địa điểm và app, 01/11

| Ca test | Thao tác | Kỳ vọng |
|---|---|---|
| TC-PLACE-M01 | Đăng nhập Management, mở thêm điểm, gõ tên POI rồi bấm tìm | Gõ chưa gửi request; bấm/Enter mới gửi `/locations/place-search`; nguồn OSM hiển thị |
| TC-PLACE-M02 | Chọn kết quả, kiểm tra tọa độ/ghim, tỉnh/phường, xác nhận | Điền đúng cặp lat/lon; không tự chọn mã địa giới hoặc duyệt điểm; tọa độ mới cần xác nhận lại |
| TC-PLACE-M03 | Thử truy vấn không có kết quả, provider ngắt và nhiều người tìm đồng thời | Rỗng phân biệt lỗi; 429 có thể thử lại; 503 cho phép chọn ghim/nhập tay; không mất form |
| TC-PLACE-M04 | Customer/Driver mở Địa điểm, tìm theo tên/địa chỉ, xem chi tiết | Chỉ điểm công bố từ backend; phân trang đúng; logout/đổi phiên không phục hồi kết quả cũ |
| TC-PLACE-M05 | Từ chi tiết mở Google Maps, Chỉ đường, Ứng dụng khác | URL dùng đúng cặp tọa độ; geo không mở được thì thử HTTPS; không có tọa độ thì không mở vị trí giả |
| TC-PLACE-M06 | Customer thêm địa chỉ, tìm POI, sửa ghim/địa chỉ, lưu | Dữ liệu riêng chính chủ; chọn kết quả không tự xác nhận; không gửi field hồ sơ riêng tới provider |
| TC-PLACE-M07 | Android điện thoại/màn hình rộng, chữ lớn, giao diện tối | Navigation bar/rail phù hợp; chữ/nút không tràn; Back về danh sách. iOS và thiết bị thật kiểm tra riêng |

`tool/preview.dart` của mỗi app là entrypoint fixture riêng, không được main.dart import. Dữ liệu minh họa chỉ phục vụ kiểm tra hình ảnh, không phải dữ liệu công bố/điểm hợp lệ cho chuyến. Bằng chứng tự động/native và giới hạn live tại tracker.
