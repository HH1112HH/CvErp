# Database — CvErp

Thư mục chứa script SQL độc lập với mã ứng dụng, dùng cho báo cáo và Power BI.

Database thuộc dự án **CvErp** — portfolio hướng tới vị trí **Chuyên viên Công nghệ thông tin** tại **Công ty Cổ phần Vinatex Phú Hưng**. Dữ liệu chỉ phục vụ demo, không phải dữ liệu vận hành thật.

## Yêu cầu

- SQL Server (instance `.\SQLEXPRESS` trên máy dev)
- Database `CvErp` đã được tạo bằng EF migration

## Tạo database

```powershell
dotnet ef database update --project src/CvErp.Infrastructure --startup-project src/CvErp.Web
```

## Tạo bộ view báo cáo

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -f 65001 -i database\views\00_create_views.sql
```

## Danh sách view

| View | Mục đích | Nguồn dữ liệu |
|---|---|---|
| `vw_InventoryOnHand` | Tồn kho hiện tại theo vật tư/kho, giá trị tồn, cảnh báo dưới mức tối thiểu | Items × Warehouses, InventoryTransactions |
| `vw_ProductionPerformance` | Kế hoạch vs thực tế, tỷ lệ hoàn thành, tỷ lệ lỗi, chênh lệch chi phí NVL | ProductionOrders, Items, Boms |
| `vw_MaterialCostVariance` | Chênh lệch chi phí nguyên vật liệu định mức vs thực tế theo lệnh | ProductionOrders, Items |
| `vw_ProductionOrderProgress` | Tiến độ lệnh, số ngày trễ, cờ trễ hạn | ProductionOrders, Items |
| `vw_StockMovement` | Nhập–xuất–tồn theo tháng, có tồn cuối kỳ lũy kế | InventoryTransactions |
| `vw_OutputMonthly` | Sản lượng tốt/lỗi theo tháng và sản phẩm, đơn giá thành | ProductionOutputs, ProductionOrders, Items |

## Kiểm tra nhanh

```sql
USE CvErp;
SELECT COUNT(*) FROM dbo.vw_InventoryOnHand;
SELECT COUNT(*) FROM dbo.vw_ProductionPerformance;
SELECT COUNT(*) FROM dbo.vw_MaterialCostVariance;
SELECT COUNT(*) FROM dbo.vw_ProductionOrderProgress;
SELECT COUNT(*) FROM dbo.vw_StockMovement;
SELECT COUNT(*) FROM dbo.vw_OutputMonthly;
```

## Kết nối Power BI

- Server: `localhost\SQLEXPRESS` (hoặc `localhost,1433` nếu đã bật TCP cổ định)
- Database: `CvErp`
- Authentication: Windows (hoặc SQL Server với tài khoản `sa` trên môi trường dev)
- Import 6 view ở trên; không cần import bảng gốc.

## Xuất CSV cho Power BI (phương án dự phòng)

```powershell
powershell -ExecutionPolicy Bypass -File database\export-views.ps1
```

File CSV nằm trong `database/exports/`, mã hoá UTF-8, dấu chấm thập phân (en-US) để Power BI parse đúng.
Dùng khi không kết nối trực tiếp được SQL Server: Power BI → Get Data → Text/CSV.

## Mở kết nối nhanh bằng Power BI

Double-click `database/CvErp.pbids` để Power BI Desktop mở sẵn kết nối tới `localhost\SQLEXPRESS`, database `CvErp`.

## Lưu ý

- Script dùng `CREATE OR ALTER VIEW`, chạy lại nhiều lần an toàn.
- Nếu database bị tạo lại hoặc seed lại từ đầu, **phải chạy lại script view** (view không được EF migration quản lý nên sẽ mất theo database cũ).
- View không phụ thuộc dữ liệu cụ thể, nên dùng được cho cả dữ liệu mô phỏng hiện tại và dữ liệu ngành sợi sau khi chuyển seed.
- Khi đổi schema (thêm/xóa cột), phải chạy lại script view.
