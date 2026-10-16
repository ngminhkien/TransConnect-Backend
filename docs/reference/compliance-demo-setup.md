# Demo hồ sơ nhẹ RAM — AIStor thật, scan/OCR mô phỏng

Luồng test xuyên suốt và mẫu request/response nằm trong [file kiểm thử hiện hành](../current-test.md); chọn nhóm 06 để test Compliance sau khi xong setup bên dưới.

Ngày 15/10/2026, chủ dự án chọn demo trước và yêu cầu tích hợp vào backend .NET. Dùng file giả lập. Không cần Python, Redis, ClamAV hoặc Tesseract để chạy luồng này. Host và SQL Server hiện có vẫn cần hoạt động. Chưa có giao diện mới; thao tác qua Swagger/API.

## 1. Cấu trúc đã tích hợp

```text
TransConnect.Modules.Compliance/
  Controllers/          # /compliance/demo/documents; dùng JWT hiện có
  Dto/                  # request/response ghi rõ demo
  Entities/             # DemoDocument, tách hồ sơ pháp lý thực
  Persistence/          # ComplianceDbContext, schema compliance
  Migrations/           # InitComplianceDemo
  Services/             # upload, mô phỏng, duyệt demo, download/delete
  Storage/              # IPrivateDocumentStorage -> AWS SDK S3 (AIStor/R2)
  Options/              # ComplianceDemo; mặc định tắt
infra/document-demo/    # chỉ AIStor, .env.example, policy tài khoản ứng dụng
```

Không thêm tham chiếu tới module Identity/Operator. Quyền tài khoản qua Common/IAccountDirectory; chỉ chủ tài khoản đọc/sửa/xóa file của mình. Chủ file tự thao tác duyệt để trình diễn, **không phải quyền thẩm định giấy tờ thật**. Admin cũng không mặc nhiên đọc file của người khác. Không liên kết demo với trạng thái duyệt Organization/Driver/Vehicle.

File nằm trong bucket private riêng `transconnect-demo-unscanned`; không có bước chuyển sang clean bucket. Metadata SQL gồm owner, bucket/key, SHA-256, loại/dung lượng, trạng thái mô phỏng, thời điểm và concurrency token. API không trả bucket/key hoặc tên file gốc. Không lưu nội dung OCR thật hoặc thông tin định danh do người dùng nhập.

## 2. Dừng scanner đang chạy để giải phóng RAM

Output người dùng cung cấp: `transconnect-clamav` healthy, khoảng 952.6 MiB RAM; 4 GiB là giới hạn. Container PostgreSQL `nest_prisma_db` thuộc dự án khác, không phải SQL Server của TransConnect; không cần tác động tới container này.

Chạy trên PowerShell của người dùng:

```powershell
docker stop transconnect-clamav
docker stats --no-stream
```

Nếu ClamAV tự chạy lại mỗi lần mở Docker, có thể bỏ tự khởi động cho chính container đó:

```powershell
docker update --restart=no transconnect-clamav
```

Không xóa container/volume chữ ký. Khi nghiệm thu scan thật về sau, `docker start transconnect-clamav`. Không chạy `docker compose up` của bản Python trong `document-verification-demo`, vì nó sẽ khởi động cả scanner và OCR worker riêng.

## 3. Tiếp tục từ bước lấy license AIStor Free

Tải license qua hướng dẫn chính thức: https://docs.min.io/aistor/installation/container/install/ (Retrieve your license file -> Free Tier). Tự hoàn tất đăng ký/điều khoản và tải file, không gửi license vào chat.

```powershell
Set-Location "C:\Users\PC\Desktop\CNTTB\doantotnghiep\TransConnect-Backend\infra\document-demo"
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
notepad .env
```

Điền `MINIO_ROOT_PASSWORD` bằng mật khẩu riêng, giữ `MINIO_ROOT_USER` hoặc tự đổi. Đặt file license tại **chính thư mục trên** tên `minio.license`, tránh `.license.txt`:

```powershell
Get-Item .\minio.license | Select-Object Name, Length
docker compose config --quiet
docker compose pull
docker compose up -d
docker compose ps
docker compose logs --tail 50 aistor
curl.exe -i http://localhost:9000/minio/health/live
```

Kỳ vọng service chạy và health HTTP 200. Nếu thiếu license hoặc password, Compose sẽ dừng báo lỗi. Chưa có bằng chứng AIStor chạy được trên máy từ phiên agent; không ghi kiểm thử live đã pass. Không chạy đồng thời stack AIStor khác chiếm cổng 9000/9001.

Mở `http://localhost:9001`, đăng nhập bằng root trong `.env`. API S3 là `http://localhost:9000`, không dùng cổng console làm endpoint backend.

Image dùng `latest` cho lần cài đầu. Sau khi chạy thành công:

```powershell
docker image inspect quay.io/minio/aistor/minio:latest --format '{{index .RepoDigests 0}}'
```

Ghi giá trị digest vào `AISTOR_IMAGE` trong `.env` để lần sau dùng đúng bản đã thử. `restart: no` được chọn để dịch vụ không tự chiếm RAM mỗi khi mở Docker. Dừng bằng `docker compose stop`, chạy lại bằng `docker compose up -d`. Không dùng `down -v` nếu muốn giữ file.

## 4. Bucket và tài khoản ứng dụng

1. Tạo bucket `transconnect-demo-unscanned` bằng console quản trị. Giữ private; không bật anonymous access.
2. Tạo policy ứng dụng từ `app-policy.json` trong thư mục Compose. Policy cho GetBucketLocation và Get/Put/Delete object trong prefix `demo/` của đúng bucket.
3. Tạo user ứng dụng riêng, gắn policy này; tạo/lưu access key và secret của user đó. Không dùng tài khoản root trong backend. Console có thể thay đổi tên mục giữa các phiên bản; các thao tác thuộc Identity/Access Management.
4. Backend không tạo bucket và không cần quyền tạo bucket. File demo được upload qua backend, nên chưa cần CORS S3 hoặc presigned URL.

Nếu đã dùng AIStor theo hướng dẫn cũ ở `local-storage`, có thể giữ instance đó và tạo thêm bucket demo riêng; không chạy thêm instance chiếm cùng cổng. Thư mục `infra/document-demo` là cấu hình chuẩn để lưu trong repo.

## 5. Cấu hình backend

Mở **file local đã có**, không ghi đè toàn bộ từ mẫu:

```powershell
Set-Location "C:\Users\PC\Desktop\CNTTB\doantotnghiep\TransConnect-Backend"
notepad .\TransConnect.Host\appsettings.Development.json
```

Ghép section sau vào JSON, giữ nguyên JWT/connection string/email của bạn:

```json
"ComplianceDemo": {
  "Enabled": true,
  "Endpoint": "http://localhost:9000",
  "Region": "us-east-1",
  "Bucket": "transconnect-demo-unscanned",
  "AccessKey": "<access-key-tai-khoan-ung-dung>",
  "SecretKey": "<secret-key-tai-khoan-ung-dung>",
  "ForcePathStyle": true
}
```

File Development, `.env` và `*.license` đã được ignore. Không đưa secrets vào appsettings.json hoặc output chia sẻ. `Enabled=false` mặc định; khi tắt thì không có demo routes. Bật ngoài Development bị startup validation từ chối trước migrate. Bucket demo bắt buộc có tiền tố `transconnect-demo-`. Endpoint HTTP chỉ chấp nhận loopback local, cloud phải dùng HTTPS.

Khởi động sau khi kiểm tra `ConnectionStrings:Default` trỏ đúng database phát triển:

```powershell
& C:/Users/PC/.dotnet/dotnet.exe restore TransConnect.sln
& C:/Users/PC/.dotnet/dotnet.exe run --project TransConnect.Host --launch-profile http
```

Host Development sẽ migrate các module hiện có và thêm `compliance.DemoDocuments` khi demo được bật. Migration mới `20261015102849_InitComplianceDemo` là additive, không sửa bảng Identity/Operator. Phiên agent chỉ sinh migration và SQL test; **chưa apply vào DB thật**. Không dùng PostgreSQL trong output Docker thay cho SQL Server của solution.

## Kiểm tra và xử lý sự cố

Luồng test/API/DTO chỉ duy trì ở [test hiện hành](../current-test.md).

- 404 route: kiểm tra Development và `ComplianceDemo:Enabled`. 404 document: sai tài khoản/ID hoặc đã xóa.
- 503 storage: kiểm tra AIStor, bucket, endpoint 9000 và policy access key; không tự fallback sang “thành công” hay storage giả.
- Upload lỗi sau ghi DB có thể để `Uploading`/`UploadFailed`; GET list để tìm và DELETE dọn. Timeout/commit lệch có thể để object cần đối soát; chưa có distributed transaction/background reconciliation.
- Delete lỗi để `Deleting`, không cho download/simulate; gọi DELETE lại sau khi storage phục hồi. Không tự xóa volume để sửa lỗi.
- Cùng file bị thao tác đồng thời: revision conflict trả 409, tải lại trạng thái. File bị thay trong S3: download đối chiếu SHA-256 và size rồi từ chối nếu lệch.
- Rollback tính năng: đặt `Enabled=false`, restart Host, dừng Compose. Không tự rollback migration DropTable vì có thể mất metadata. Xóa dữ liệu thử qua API trước khi tắt nếu muốn dọn.

## Tham khảo adapter R2



Adapter dùng AWS SDK S3 4.0.104.4. Để thử R2 sau này: tạo bucket demo private, cấp Object Read & Write đúng bucket; đổi Endpoint HTTPS và Region `auto`, giữ demo mode Development. Đổi endpoint không chuyển file cũ: dùng DB/bucket thử riêng hoặc di chuyển có đối soát. Chưa kiểm chứng live R2 trong lượt này. R2 không tự quét ClamAV.

Công việc nâng cấp tập trung trong [tracker](../../../TransConnect-Docs/00-project/progress-tracker.md).

Nguồn: [AIStor container](https://docs.min.io/aistor/installation/container/install/), [AWS SDK S3 config](https://docs.aws.amazon.com/sdkfornet/v4/apidocs/items/S3/TS3Config.html), [R2 credentials](https://developers.cloudflare.com/r2/api/tokens/).
