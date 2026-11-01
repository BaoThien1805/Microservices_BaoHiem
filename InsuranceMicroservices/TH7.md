# Thực hành 7: PaymentService, ProductService và Marten

## Phạm vi

Đã tích hợp đủ sáu project từ các file RAR: PaymentService, PaymentService.Api, PaymentService.Test, ProductService, ProductService.Api, ProductService.Test. Marten 7.19.1 được thêm vào PaymentService theo mã mẫu .NET 8. ProductService giữ EF Core và cơ sở dữ liệu InMemory/SQL Server như mẫu, không đổi sang Marten.

- ProductService: danh mục sản phẩm, tạo bản nháp, kích hoạt, ngừng bán, các quyền lợi và câu hỏi lựa chọn/ngày/số. Cổng 5030, Swagger `/swagger`.
- PaymentService: tài khoản hợp đồng, bút toán dự kiến/thu/chi, truy vấn số dư, lưu document Marten/PostgreSQL, listener PolicyCreated/PolicyTerminated, tác vụ Hangfire nhập file ngân hàng. Cổng 5070, Swagger `/swagger`, Hangfire `/hangfire` (chỉ truy cập cục bộ theo mặc định Hangfire).
- Bổ sung contract PolicyTerminated còn thiếu vào PolicyService.Api để khớp listener mẫu. Không thay đổi nghiệp vụ PricingService hay dịch vụ khác.
- File nộp ở thư mục gốc repository: `NopBai_TH7.txt`. Chưa commit/push các thay đổi của phiên này.

## Điều chỉnh so với mã mẫu

- Loại bỏ file template WeatherForecast/Class1/test rỗng và DTO tạm bị trùng. Giữ tên và namespace các contract chính.
- Thêm bước SaveChanges sau handler Product để thay đổi được giữ qua các request, sửa route activate/discontinue và trả 404 cho sản phẩm không tồn tại.
- Câu trả lời Choice được định danh theo từng câu hỏi, tránh trùng mã YES/NO giữa các câu hỏi. Câu hỏi trong lệnh tạo bản nháp được deserialize theo danh sách kiểu cố định, không nhận tên lớp tùy ý.
- Payment chờ khởi tạo dữ liệu trước khi nhận request; không bật TypeNameHandling cho JSON HTTP; phân biệt tìm bằng mã hợp đồng và số tài khoản ngân hàng. Tạo bút toán dự kiến theo phí trong PolicyCreated.
- File ngân hàng chỉ được đánh dấu xử lý sau commit, không nhập lại file đã có dấu xử lý, báo lỗi rõ ràng khi không tìm thấy tài khoản. Parse số tiền dùng InvariantCulture.
- Sửa gói Hangfire PostgreSQL trùng lặp/API cũ, lỗi đường dẫn COPY Dockerfile, cấu hình RabbitMQ theo môi trường và vòng đời consumer.
- ProductService dùng Steeltoe 3.4.0 cùng nhánh với TH6. Khóa/kết nối được truyền qua biến môi trường. Không bật sensitive data logging của EF.

## Build và test

Chạy từ `InsuranceMicroservices`:

```powershell
dotnet build .\InsuranceMicroservices.sln
dotnet build .\TH7.slnf
dotnet test .\ProductService.Test
dotnet test .\PaymentService.Test
```

Kết quả đã kiểm tra trong phiên thực hiện: toàn solution build thành công, 0 lỗi; ProductService 17 test đạt; PaymentService 10 test đạt và 1 test bỏ qua. Test bổ sung kiểm tra thay đổi trạng thái sản phẩm qua nhiều request, câu hỏi có mã lựa chọn trùng nhau, tra sản phẩm không tồn tại, nhập thanh toán và không đánh dấu file khi commit lỗi.

Test Marten round-trip dùng container PostgreSQL tạm, cổng ngẫu nhiên; cần Docker Desktop chạy Linux containers:

```powershell
$env:RUN_DOCKER_TESTS = '1'
dotnet test .\PaymentService.Test
Remove-Item Env:RUN_DOCKER_TESTS
```

Test này chưa chạy trong phiên thực hiện vì Docker daemon chưa hoạt động. Không coi kết quả build/test nghiệp vụ là đã xác minh kết nối PostgreSQL/RabbitMQ thực tế.

## Chạy ProductService

```powershell
dotnet run --project .\ProductService
```

Mở `http://localhost:5030/swagger` hoặc gọi `http://localhost:5030/api/Products`. Mẫu dùng InMemory và tạo bốn sản phẩm TRI/HSI/FAI/CAR. Dữ liệu InMemory mất khi dừng process. Muốn lưu SQL Server, đặt `Settings__UseInMemoryDatabase=false` và `ConnectionStrings__Products` trước khi chạy.

Chạy độc lập, không dùng Eureka:

```powershell
$env:Eureka__Client__ShouldRegisterWithEureka = 'false'
$env:Eureka__Client__ShouldFetchRegistry = 'false'
dotnet run --project .\ProductService
```

Nếu cần Gateway TH6 khám phá ProductService, xóa hai biến tắt discovery và chạy Eureka ở cổng 8761 trước. Route sản phẩm trên Gateway vẫn yêu cầu JWT.

## Chạy PaymentService

Marten 7 yêu cầu PostgreSQL từ 12 trở lên. Hạ tầng cũ `postgres/Dockerfile` dùng PostgreSQL 11.4 nên không dùng DB đó cho TH7. File Compose riêng bên dưới dùng PostgreSQL 16, cổng 5433, volume riêng; không thay đổi volume DB bài trước.

Trong PowerShell, đặt mật khẩu cục bộ của bạn và giữ nguyên khi sử dụng lại volume:

```powershell
$env:TH7_POSTGRES_PASSWORD = Read-Host 'Mat khau PostgreSQL TH7'
docker compose -f .\scripts\th7-postgres.yml up -d --wait

$env:ConnectionStrings__PgConnection = "Host=localhost;Port=5433;Database=lab_netmicro_payments;Username=lab_user;Password=$env:TH7_POSTGRES_PASSWORD"
$env:BackgroundJobs__HangfireConnectionStringName = "Host=localhost;Port=5433;Database=lab_netmicro_jobs;Username=lab_user;Password=$env:TH7_POSTGRES_PASSWORD"
$env:BackgroundJobs__InPaymentFileFolder = Join-Path $PWD 'bank-statements'
New-Item -ItemType Directory -Path $env:BackgroundJobs__InPaymentFileFolder -Force
```

Mật khẩu đọc từ terminal chỉ dùng cho môi trường lab. Script PostgreSQL tự tạo hai DB payments/jobs trong volume mới. Đổi biến mật khẩu không đổi mật khẩu của volume đã tồn tại. Connection string mẫu ở trên cần mật khẩu không chứa dấu `;`; nếu có, dùng NpgsqlConnectionStringBuilder để dựng chuỗi có escape đúng.

Nếu chạy đầy đủ, bật Eureka/RabbitMQ bài trước:

```powershell
docker compose -f .\scripts\infra.yml up -d eureka rabbitmq
Remove-Item Env:Eureka__Client__ShouldRegisterWithEureka -ErrorAction SilentlyContinue
Remove-Item Env:Eureka__Client__ShouldFetchRegistry -ErrorAction SilentlyContinue
dotnet run --project .\PaymentService
```

Hoặc chỉ thử PostgreSQL/API, tắt broker/discovery trong terminal này:

```powershell
$env:RabbitMqOptions__Enabled = 'false'
$env:Eureka__Client__ShouldRegisterWithEureka = 'false'
$env:Eureka__Client__ShouldFetchRegistry = 'false'
dotnet run --project .\PaymentService
```

API thử: `http://localhost:5070/api/Payment/accounts/POLICY_1` (dữ liệu demo). Seed mặc định tạo POLICY_1/2/3 nếu chưa có; đặt `SeedData__Enabled=false` để tắt. Có thể tắt tác vụ Hangfire bằng `BackgroundJobs__Enabled=false`.

File nhập có tên `bankStatements_2026_11_1.csv`, tương ứng ngày chạy tác vụ, trong thư mục đã cấu hình. Ví dụ:

```csv
id,type,date,account,amount
1,credit,2026-11-01,231232132131,100.50
```

Job chạy mỗi phút. CSV mẫu dùng dấu phẩy, không hỗ trợ trường trích dẫn chứa dấu phẩy. Mã hợp đồng và số tài khoản ngân hàng là hai trường khác nhau. Cơ chế file đánh dấu chỉ phù hợp bài thực hành một instance; không bảo đảm exactly-once nếu process chết giữa commit DB và ghi file đánh dấu.

## Cảnh báo package

Giữ Marten 7.19.1 theo mã giảng viên nên NuGet báo NU1904 cho GHSA-rfx3-98h7-v3xp và GHSA-vmw2-qwm8-x84c. Không tắt hoặc che cảnh báo. Các API full-text/regConfig, dynamic dictionary key và tenant của các advisory không được dùng trong phần cài đặt này. Không dùng bản lab để triển khai production.

Nguồn: https://github.com/advisories/GHSA-rfx3-98h7-v3xp và https://github.com/advisories/GHSA-vmw2-qwm8-x84c. Bản sửa 9.13.0 yêu cầu .NET 9/10; việc đổi target framework và chuyển phiên bản Marten lớn không nằm trong phạm vi tích hợp mã mẫu .NET 8 lần này. Yêu cầu PostgreSQL: https://martendb.io/migration-guide.
