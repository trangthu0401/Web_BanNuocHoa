## Cài đặt Entity Framework Core và Scaffold-DbContext
1. Chạy file Core_PerfumeStore để tạo database PerfumeStore trên SQL Server
- Lưu ý: 
+ Tạo người dùng SA với mật khẩu @Password123 nếu chưa có
+ Vào mục Databases -> Properties -> Security -> Chọn SQL Server and Windows Authentication mode

2. Mở terminal trong thư mục PerfumeStore và chạy các lệnh sau:
```
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 6.0.21
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.EntityFrameworkCore.Design --version 6.0.21

dotnet ef dbcontext scaffold "Server=MSI\SERVER1;Database=PerfumeStore;User Id=SA;Password=123;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer -o Models --force
```

*** Tương tự áp dụng cho Area (phần Admin)
```
dotnet ef dbcontext scaffold "Server=MSI,1433;Database=PerfumeStore;User Id=SA;Password=123;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer -o Areas/Admin/Models --force
```

## Tạo migration
- Mở terminal trong thư mục PerfumeStore và chạy các lệnh sau:
```
dotnet ef migrations add InitialCreate --context PerfumeStore.Models.PerfumeStoreContext --output-dir Migrations
```
*** Tương tự áp dụng cho Area (phần Admin)
```
dotnet ef migrations add AdminInitialCreate -o Areas/Admin/Migrations
```


## Cài đặt các package hỗ trợ khác

Package								  |				 Chức năng				 |						Lệnh cài đặt
--------------------------------------|--------------------------------------|--------------------------------------------------------
BCrypt.Net-Next						  |	Mã hóa mật khẩu						 | dotnet add package BCrypt.Net-Next --version 4.0.2
PayOS								  | Cổng thanh toán trực tuyến			 | dotnet add package payOS



## Cài các package có sẵn
Nhấn Alt + Enter -> Install ... 