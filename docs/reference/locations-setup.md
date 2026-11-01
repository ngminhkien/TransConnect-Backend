# Setup Locations: địa giới, import và bản đồ

Cập nhật 27/10/2026. Hướng dẫn này phân biệt chuẩn bị file dữ liệu, tạo schema và import vào DB. Script lấy dữ liệu không tự kết nối DB. Tiến độ triển khai/kiểm thử chung ở [tracker](../../../TransConnect-Docs/00-project/progress-tracker.md); nghiệp vụ ở [planlocation](../../../TransConnect-Docs/00-project/planlocation.md).

## 1. Cần cài gì?

| Thành phần | Cách chuẩn bị |
|---|---|
| Backend | Giữ SDK theo `global.json` và SDK repo đang dùng; không cần cài GIS/NetTopologySuite cho lat/lon |
| SQL Server | Dùng DB Development/test đã chọn; kiểm tra tên DB trong cấu hình local trước migration/import |
| Python | Python 3.9+ cho script chuẩn bị dataset, chỉ dùng thư viện chuẩn, không cần pip. Máy hiện tại đã có Python 3.14 |
| Git | Chỉ cần nếu muốn clone toàn bộ repository nguồn; cách tải ba file cố định không cần clone |
| Management web | Node/npm hiện có; dependency Leaflet và lockfile được quản trong repo frontend |
| Customer | Flutter/Dart hiện có; dependency flutter_map/latlong2 và lockfile được quản trong repo Customer |
| Tài khoản bản đồ | OSM standard raster cho demo nhỏ không cần API key; provider khác có thể yêu cầu key/quota riêng |

Không cần PostGIS, QGIS, máy chủ tile, Google Maps billing, GPS thiết bị hay dịch vụ geocoding/routing để chạy phạm vi này. Quyền Internet cho app vẫn cần.

## 2. Dataset đã chọn và bằng chứng kiểm tra

Phạm vi đã được người dùng chốt khi tiếp tục 27/10: địa giới toàn quốc và bản đồ không giới hạn một vùng demo. Bến xe/kho/điểm đón thực tế vẫn cần được nhập/xét duyệt; không coi danh mục hành chính là dữ liệu đầy đủ của các điểm vận tải.

Nguồn là repository cộng đồng [thanglequoc/vietnamese-provinces-database](https://github.com/thanglequoc/vietnamese-provinces-database), không phải dịch vụ bản đồ và không phải cơ quan nhà nước. Dự án dùng một bản cố định của nguồn đã được chủ dự án chọn:

| Thuộc tính | Giá trị |
|---|---|
| Release | [v5.2.0](https://github.com/thanglequoc/vietnamese-provinces-database/releases/tag/v5.2.0) |
| Commit được pin | `62b1abdfe39625289c37a58ef6678e9db3d37f84` |
| Artifact | `json/simplified_json_generated_data_vn_units.json` |
| SHA-256 artifact | `f624138116bde151c860cbd25a84cd6b9b38250e8a049ed371c55dee729ac286` |
| Kích thước artifact | 805331 byte |
| Kết quả đọc artifact | 34 tỉnh/thành, 3321 xã/phường/đặc khu; mã duy nhất, không có quan hệ cha–con sai |
| License | [MIT tại commit đã pin](https://github.com/thanglequoc/vietnamese-provinces-database/blob/62b1abdfe39625289c37a58ef6678e9db3d37f84/LICENSE), lưu kèm nguồn |
| Metadata nguồn | DatasetVersion v5.2.0, GeneratedAt `2026-09-20T13:13:17Z`; giữ file gốc, không tự coi ngày tạo file là ngày hiệu lực pháp lý |

Các số lượng trên là kiểm tra của artifact này, không hardcode thành quy tắc nghiệp vụ hoặc tuyên bố danh mục luôn mới nhất. Không tải GIS, polygon, dữ liệu tuyến hoặc địa điểm vận tải từ nguồn này. Không tự thêm cấp huyện vào model hai cấp.

## 3. Lấy dữ liệu — cách khuyến nghị

Mở PowerShell tại `TransConnect-Backend`:

```powershell
python --version
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Get-LocationDataset.ps1
```

`-ExecutionPolicy Bypass` chỉ áp dụng cho tiến trình lệnh này, không thay policy toàn máy. Nếu máy dùng Python ở đường dẫn riêng:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Get-LocationDataset.ps1 -PythonPath 'C:\Users\PC\AppData\Local\Programs\Python\Python314\python.exe'
```

Hoặc chạy trực tiếp, không cần PowerShell script:

```powershell
python .\scripts\get_location_dataset.py
```

Script tải đúng ba file tại commit cố định, xác minh SHA-256 của từng file, kiểm tra mã chuỗi/quan hệ/tên rồi tạo:

```text
.codex-build/locations/v5.2.0/
  locations-dataset.json       # JSON dành cho API import
  manifest.json               # nguồn, commit, checksum, số lượng
  source/LICENSE
  source/json/simplified_json_generated_data_vn_units.json
  source/json/vn_provinces_metadata.json
```

Kết quả thành công phải ghi `Prepared v5.2.0 with 34 provinces and 3321 wards` và `No database connection or import was performed`. Mã tỉnh/phường giữ số 0 đầu. Trường `name` chuẩn hóa lấy từ `FullName` để UI phân biệt Phường/Xã/Đặc khu. Không thêm tọa độ giả cho địa giới.

Manifest ghi SHA-256 của file chuẩn hóa; bản đã kiểm tra trong lượt này là `b989887f065358b1c23fa140efbb421656f3e6421908524a96ca29b23cd2a797`. File tạo ra nằm trong thư mục bị Git ignore, có thể tái tạo từ script. Trường `checksum` trong request import là checksum artifact nguồn, không phải bằng chứng server tự kiểm chứng mọi byte request; importer vẫn phải kiểm tra nội dung và quyền.

## 4. Nếu muốn clone repository nguồn

Clone phục vụ xem nguồn hoặc chuẩn bị offline, không đưa repo nguồn thành module code và không chạy SQL của nguồn lên DB ứng dụng. Chạy tại Backend, dùng một thư mục mới:

```powershell
git clone --filter=blob:none --no-checkout https://github.com/thanglequoc/vietnamese-provinces-database.git .codex-build/vendor/vietnamese-provinces-database
git -C .codex-build/vendor/vietnamese-provinces-database checkout --detach 62b1abdfe39625289c37a58ef6678e9db3d37f84
git -C .codex-build/vendor/vietnamese-provinces-database rev-parse HEAD
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Get-LocationDataset.ps1 -SourceDirectory .\.codex-build\vendor\vietnamese-provinces-database
```

Nếu đã clone rồi, dùng đúng đường dẫn hiện có và chỉ checkout khi không có thay đổi cần giữ. Script vẫn so sánh checksum, nên file bị sửa hoặc checkout sai sẽ bị từ chối. Không xóa thư mục hiện có để chạy lại hướng dẫn.

Sau khi đã tải một lần, có thể tái tạo JSON không truy cập mạng:

```powershell
python .\scripts\get_location_dataset.py --source-directory .\.codex-build\locations\v5.2.0\source
```

## 5. Tạo schema rồi import vào DB

Trước khi chạy phần này, xác định DB Development/test và dừng Host đang dùng DB đó. Đây là các thao tác ghi DB, khác với bước lấy file ở trên. Không dùng DB nền tảng đang nghiệm thu nếu chưa thống nhất bộ migration.

Migration Locations là opt-in. Helper của dự án có lựa chọn riêng:

**Quyết định DB và nguồn cấu hình hiện hành 29/10:** tiếp tục phát triển/test trên `TransConnectPhase1Dev`. Chủ dự án tự sửa ConnectionStrings:Default trong appsettings.Development.json; agent không sửa file local thay người dùng. `TransConnectDb` giữ làm đích cho phần đã ổn định, đã được backup rồi clear dữ liệu test; không tự chuyển feature/data vào đó trong quá trình phát triển.

**Development chỉ dùng file JSON:** Host đọc appsettings.json rồi appsettings.Development.json; User Secrets, biến môi trường và CLI không ghi đè cấu hình ứng dụng ở Development. Helper migration cũng không còn gán ConnectionStrings__Default. Môi trường/cổng hosting theo launch profile được giữ để chạy cổng 5267. Launcher không còn -EmailProvider; muốn đổi Email/DB/JWT/storage thì sửa file Development local (không commit). Chính sách này không đổi cấu hình cô lập Testing hoặc chính sách triển khai ngoài Development.

Trước khi bật Backend/migrate/import, kiểm tra tên DB trong file Development phải là TransConnectPhase1Dev và SSMS cùng instance. Có thể kiểm tra cửa sổ query bằng:

```sql
USE [TransConnectPhase1Dev];
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;
```

Helper in `Development SQL target: server / database`; chỉ tiếp tục khi đích khớp file bạn đã chọn. Các bảng Locations nằm trong schema `locations`, ví dụ `locations.Provinces`; DB VieGoLocationSource_Dev vẫn là nguồn riêng. Hai DB ứng dụng hiện có đủ bảy bảng Locations nhưng chưa có dataset; phần import tiếp theo dùng Phase1Dev. Bằng chứng clear/backup và giữ nguyên dữ liệu Phase1Dev ở tracker DB-DEV.

Hai migration trong source: `20261027115710_InitLocations` tạo bảy bảng và `20261027121011_LocationsIntegrity` bổ sung CHECK. Helper dưới đây áp dụng cả các migration nền tảng còn thiếu trong DB được cấu hình, rồi mới đến Locations; dùng đúng DB Development/test đã chọn. Script chuẩn bị file ở bước 3 không thực hiện các thao tác này.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Update-DevelopmentDatabase.ps1 -DotnetPath 'C:\Users\PC\.dotnet\dotnet.exe' -IncludeLocations
```

Helper này áp dụng migration nền tảng theo quy trình hiện có rồi thêm Locations; đọc kết quả từng context. Không tự bật `Locations:ApplyMigrationsOnStartup` chỉ để thử giao diện. Tùy chọn startup chỉ dành Development khi người vận hành chủ động bật.

**Lỗi cache NuGet khác tài khoản Windows (sửa 29/10):** nếu MSB3106/CS0006 báo thiếu DLL/analyzer tại `C:\Users\CodexSandboxOffline\.nuget\packages` hoặc thư mục của tài khoản khác, `obj/project.assets.json` và NuGet props có thể được sinh từ phiên build trước. Helper hiện chạy `dotnet restore TransConnect.sln --force` trước build `--no-restore`, để tạo lại đường dẫn cho môi trường đang gọi. Nếu restore lỗi, helper dừng trước build/migration. Dừng Backend và chạy lại lệnh helper ở trên; không đổi connection string, không copy DLL thủ công hoặc xóa DB. Khi cần thử riêng bước khôi phục package mà chưa chạy migration, dùng:

```powershell
& 'C:\Users\PC\.dotnet\dotnet.exe' restore .\TransConnect.sln --force
```

Lệnh restore chỉ cập nhật dependency/cache và file build sinh tự động, không kết nối DB hoặc chạy Host. Nếu lỗi vẫn còn, kiểm tra `NUGET_PACKAGES`/NuGet config có ép đường dẫn của tài khoản khác hay không; gửi log restore đã che thông tin riêng để chẩn đoán tiếp. Không dùng lệnh xóa toàn bộ cache như bước mặc định.

Sau migration, bật Backend bằng launcher hiện có và đăng nhập Admin có quyền `locations.manage`. Import qua `POST /locations/admin/datasets/import`, gửi nội dung `locations-dataset.json`. Có thể dùng Swagger Authorize rồi gửi body; không ghi JWT vào file, Git hoặc ảnh chụp.

Ví dụ PowerShell dưới đây nhận JWT bằng ô nhập ẩn, không đưa token vào lịch sử lệnh:

```powershell
$locationToken = Read-Host 'Access token Admin có locations.manage' -AsSecureString
$locationCredential = [System.Net.NetworkCredential]::new('', $locationToken)
$locationHeaders = @{ Authorization = 'Bearer ' + $locationCredential.Password }
try {
    Invoke-RestMethod -Method Post -Uri 'http://localhost:5267/locations/admin/datasets/import' -Headers $locationHeaders -ContentType 'application/json; charset=utf-8' -InFile '.\.codex-build\locations\v5.2.0\locations-dataset.json'
}
finally {
    $locationHeaders.Clear()
    $locationCredential = $null
    $locationToken = $null
}
```

Không log toàn bộ biến request/header. Nếu được trả 403, kiểm tra quyền Admin; không bỏ authorization hoặc dùng token Customer. Nếu import trùng phiên bản nhưng khác nội dung/checksum thì phải xử lý xung đột, không ghi đè dữ liệu lịch sử. Khi nâng cấp nguồn sau này: khảo sát diff → pin commit/hash mới → chạy validation → import phiên bản mới; không sửa checksum để bỏ qua lỗi.

Sau import, dùng API tỉnh/phường và form địa chỉ để kiểm tra số lượng, tên và quan hệ. Import chỉ là danh mục hành chính; không tự sinh địa điểm dùng chung đã phê duyệt. Bến xe/kho/cổng đón phải qua luồng Admin/Owner riêng.

`GET /locations/dataset` trả provenance/version đang hoạt động. Trên web: `/admin/locations` để Admin tạo/duyệt/xem lịch sử; Partner → tổ chức → Địa điểm cho Owner. Customer → Tài khoản → Địa chỉ đã lưu. Danh mục chưa import thì các dropdown trống; cần hoàn tất bước import, không sửa code để hardcode mã tỉnh/phường.

## 6. Bản đồ web và Customer

Thư viện vẽ bản đồ và nhà cung cấp ảnh nền là hai phần khác nhau. [Leaflet 1.9.4](https://leafletjs.com/download.html) được pin ở web; Customer pin [flutter_map 8.2.2](https://pub.dev/packages/flutter_map/versions/8.2.2), latlong2 0.9.1 và url_launcher 6.3.2 cho link attribution. Đã kiểm tra với Flutter 3.35.2/Dart 3.9.0, không cần nâng SDK. Khi dependency/lockfile đã có trong repo, dùng `npm ci` / `flutter pub get`, không chạy lệnh thêm package hoặc nâng version mỗi lần setup. PowerShell chặn npm.ps1 thì dùng `npm.cmd ci` / `npm.cmd run dev`.

Tại `TransConnect-Frontend/transconnect-management-web`:

```powershell
npm ci
npm run dev
```

Tại `TransConnect-Frontend/transconnect-customer-app`:

```powershell
flutter pub get
flutter run -d chrome --web-port 5174
```

Giữ cấu hình API base URL theo README từng client. Android emulator dùng địa chỉ host theo README (không coi localhost bên trong emulator là máy chạy Backend). [flutter_map yêu cầu quyền INTERNET ở Android release](https://docs.fleaflet.dev/getting-started/installation); ghim thủ công không cần quyền GPS. Với workaround máy ảo đã xác định trước đây, giữ các cờ rendering trong README Customer nếu cần.

Mặc định dùng OSM standard raster, không cần tạo tài khoản hoặc key. Nếu đổi provider, web cấu hình trong `.env.local` theo mẫu an toàn `.env.example`: `VITE_MAP_TILE_URL` và `VITE_MAP_ATTRIBUTION` (HTML attribution từ provider). Đây là biến public của Vite, chỉ đặt token được provider cho phép công khai và giới hạn origin; không đặt secret. Customer truyền cấu hình public bằng `--dart-define=MAP_TILE_URL=...`, `--dart-define=MAP_ATTRIBUTION=...`, `--dart-define=MAP_ATTRIBUTION_URL=...`; khởi động/build lại khi đổi. Giữ đúng attribution và chính sách cache của provider mới, không chỉ đổi URL ảnh nền.

OSM standard raster dùng URL `https://tile.openstreetmap.org/{z}/{x}/{y}.png`, attribution hiển thị `© OpenStreetMap contributors` kèm link copyright. [Chính sách OSM](https://operations.osmfoundation.org/policies/tiles/) yêu cầu nhận diện ứng dụng, Referer hợp lệ trên web, tôn trọng HTTP cache hoặc cache tối thiểu bảy ngày khi không đọc được headers. Không bật tải offline/prefetch hay bỏ attribution. Dịch vụ best-effort, không SLA; nếu cần quota/hỗ trợ có cam kết thì chuyển provider phù hợp.

[flutter_map từ 8.2 có cache mặc định trên native](https://docs.fleaflet.dev/layers/raster-tile-layer/caching); trình duyệt dùng HTTP cache. Giữ cache khi cấu hình provider, đặt app identity riêng cho native. Không gọi geocoding khi chỉ chọn ghim; địa chỉ hành chính lấy từ Backend.

Khi tile lỗi: giữ nguyên nội dung form, cho nhập lat/lon và bấm xác nhận thủ công. Có ghim mặc định chưa đồng nghĩa người dùng xác nhận; địa chỉ riêng chưa có tọa độ vẫn được lưu nhưng chưa dùng cho điểm cần chính xác. Không tự đổi tỉnh/phường theo vị trí tâm bản đồ.

## 7. Bằng chứng và giới hạn trong lượt setup

- Đã tải ba artifact qua HTTPS bằng Python; hash khớp pin, 34/3321 mã duy nhất và đúng quan hệ.
- Đã chạy wrapper PowerShell với source local; kiểm tra nguồn bị đổi checksum bị từ chối mà không xuất payload; kiểm tra mã trùng, sai cha, mã số thay vì chuỗi; chạy lặp tạo byte JSON giống nhau.
- PowerShell Invoke-WebRequest/curl hệ thống gặp lỗi Schannel ở môi trường agent; Python HTTPS hoạt động, không tắt xác minh TLS. Nếu máy khác gặp lỗi mạng, dùng clone/cache đã kiểm tra; không bỏ checksum.
- Chưa apply migration, chưa import vào SQL Server, chưa xác nhận tiles trên thiết bị thật trong bước chuẩn bị dữ liệu. Kết quả backend/frontend được ghi riêng tại tracker sau khi các phần triển khai hoàn tất.

## Public place search — 01/11/2026

Không cần migration hoặc API key cho cấu hình Nominatim demo. Sau khi cập nhật code cần restart Host để nạp `/locations/place-search`. Options `Locations:PlaceSearch`: `Enabled` (mặc định true), `Endpoint` (mặc định `https://nominatim.openstreetmap.org/search`), `UserAgent` (mặc định `VieGo/0.1 (transport-public-place-search)`). Endpoint phải HTTPS, không credentials/query/fragment; giữ nhận diện ứng dụng thực và bổ sung contact khi vận hành. Provider có thể đổi qua cấu hình, không thay URL từ request người dùng.

Public provider chỉ dành cho tìm POI có nút bấm ở single-instance; không autocomplete, bulk hoặc dữ liệu cá nhân. Singleton throttle/cache nằm trong process, không đáp ứng nhiều replica. Mạng backend phải phân giải được Nominatim; lỗi DNS tải **tiles phía trình duyệt** là vấn đề độc lập. Không đổi DNS hệ thống theo gói này. Xem contract và current-test để phân biệt 200 rỗng / 429 / 503.
