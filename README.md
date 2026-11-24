# CvErp — Mini ERP quản trị kho & sản xuất

> **Bối cảnh ứng tuyển:** Chuyên viên Công nghệ thông tin — Công ty Cổ phần Vinatex Phú Hưng.
>
> Dự án portfolio mô phỏng một hệ thống ERP sản xuất thu nhỏ, lấy cảm hứng từ quy trình quản trị kho – sản xuất của BRAVO nhưng được thiết kế và lập trình hoàn toàn mới.

**ASP.NET Core 10 MVC · EF Core 10 · SQL Server · Bootstrap 5 · Chart.js · PWA**

![Dashboard điều hành](docs/screenshots/02-dashboard.png)

*Ảnh chụp từ bản demo độc lập, không phải hệ thống chính thức của Công ty Cổ phần Vinatex Phú Hưng.*

## Dashboard Power BI — 3 trang

Dashboard dựng bằng PBIP (TMDL + PBIR), kết nối trực tiếp SQL Server `CvErp`: 6 view báo cáo, 2 bảng chiều, 8 quan hệ và 30 measure DAX.

| Trang | Nội dung chính |
|---|---|
| **Sản xuất** | 5 KPI, sản lượng theo tháng, tỷ lệ lỗi theo sản phẩm, kế hoạch vs thực tế, tiến độ lệnh sản xuất |
| **Kho & NVL** | Giá trị tồn, nhập kho, tồn hiện tại, nhập–xuất–tồn, tồn theo kho/nhóm hàng, mặt hàng dưới định mức |
| **Chi phí & Xu hướng** | Định mức/thực tế/chênh lệch, chênh lệch theo sản phẩm, chi phí theo kỳ, ma trận chênh lệch theo trạng thái |

![Dashboard sản xuất](database/powerbi/screenshots/01-san-xuat.png)

![Dashboard kho & NVL](database/powerbi/screenshots/02-kho-nvl.png)

![Dashboard chi phí & xu hướng](database/powerbi/screenshots/03-chi-phi-xu-huong.png)

Mở project: `database/powerbi/CvErp_Dashboard/CvErp_Dashboard.pbip` (Power BI Desktop 2.159+).

## Tổng quan

CvErp tập trung vào hai nghiệp vụ được làm sâu thay vì trải rộng nhiều phân hệ. Giao diện demo mang tên **Vinatex Phú Hưng** để mô phỏng bối cảnh ứng tuyển; đây là sản phẩm độc lập, không phải hệ thống chính thức của công ty:

- **Kho vận:** danh mục vật tư, kho, nhà cung cấp; phiếu nhập – xuất – điều chuyển; sổ cái kho; thẻ kho; cảnh báo tồn thấp.
- **Sản xuất:** định mức BOM có phiên bản; lệnh sản xuất; nhu cầu vật tư được chốt theo BOM tại thời điểm tạo lệnh; cấp phát vật tư; ghi nhận thành phẩm; phân tích chênh lệch chi phí.

Điểm cốt lõi của dự án là **tính đúng đắn của luồng chứng từ**: trạng thái rõ ràng, phân quyền theo vai trò, ghi sổ một lần, chống ghi trùng, chặn xuất vượt tồn và lưu vết kiểm toán.

## Ảnh chụp giao diện

<table>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/01-login.png" alt="Đăng nhập" />
      <br /><sub>Đăng nhập và tài khoản demo theo vai trò</sub>
    </td>
    <td width="50%">
      <img src="docs/screenshots/03-inventory-dashboard.png" alt="Dashboard kho" />
      <br /><sub>Dashboard kho: dòng nhập xuất, cơ cấu tồn, cảnh báo định mức</sub>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/04-production-dashboard.png" alt="Dashboard sản xuất" />
      <br /><sub>Dashboard sản xuất: kế hoạch, sản lượng tốt/lỗi, trạng thái lệnh</sub>
    </td>
    <td width="50%">
      <img src="docs/screenshots/05-variance-report.png" alt="Báo cáo chênh lệch chi phí" />
      <br /><sub>Chênh lệch chi phí nguyên vật liệu thực tế so với định mức BOM</sub>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/06-stock-documents.png" alt="Danh sách chứng từ kho" />
      <br /><sub>Chứng từ kho với trạng thái duyệt và ghi sổ</sub>
    </td>
    <td width="50%">
      <img src="docs/screenshots/07-stock-card.png" alt="Thẻ kho" />
      <br /><sub>Thẻ kho: số dư đầu kỳ, nhập xuất và tồn lũy kế</sub>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/08-production-order-detail.png" alt="Chi tiết lệnh sản xuất" />
      <br /><sub>Lệnh sản xuất: nhu cầu, đã cấp, chênh lệch và tiến độ</sub>
    </td>
    <td width="50%">
      <img src="docs/screenshots/09-material-issue-detail.png" alt="Phiếu cấp phát vật tư" />
      <br /><sub>Phiếu cấp phát đối chiếu với định mức của lệnh</sub>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/11-stock-document-create.png" alt="Lập phiếu nhập kho" />
      <br /><sub>Line editor tự động điền đơn giá chuẩn và tính thành tiền</sub>
    </td>
    <td width="50%">
      <img src="docs/screenshots/12-production-order-create.png" alt="Tạo lệnh sản xuất" />
      <br /><sub>Xem trước nhu cầu vật tư từ BOM khi lập lệnh</sub>
    </td>
  </tr>
  <tr>
    <td width="50%">
      <img src="docs/screenshots/10-audit-log.png" alt="Nhật ký thao tác" />
      <br /><sub>Audit log: truy vết người dùng, hành động và đối tượng</sub>
    </td>
    <td width="50%">
      <img src="docs/screenshots/13-mobile-dashboard.png" alt="Giao diện mobile" />
      <br /><sub>Responsive trên mobile; sidebar dạng drawer</sub>
    </td>
  </tr>
</table>

## Tính năng chính

### Kho vận

- Danh mục vật tư / thành phẩm, kho, nhà cung cấp; đơn giá chuẩn, tồn tối thiểu, trạng thái hoạt động.
- Phiếu **nhập kho**, **xuất kho**, **điều chuyển** với dòng vật tư động.
- Kiểm tra **tồn khả dụng** theo kho trước khi ghi sổ; chặn xuất vượt tồn.
- Sổ cái kho là nguồn dữ liệu tồn kho duy nhất; mọi báo cáo đều đọc từ đây.
- Bảng tồn kho theo vật tư/kho/nhóm, thẻ kho có số dư đầu kỳ và tồn lũy kế.
- Cảnh báo vật tư dưới định mức tối thiểu, xuất CSV cho các danh sách và báo cáo.

### Sản xuất

- Định mức **BOM** nhiều dòng, có phiên bản, tỷ lệ hao hụt và đơn giá chuẩn.
- Lệnh sản xuất chụp lại nhu cầu vật tư tại thời điểm tạo lệnh; sửa BOM sau đó không làm thay đổi lệnh đã tạo.
- Theo dõi nhu cầu – đã cấp – chênh lệch – giá trị định mức/thực tế theo từng vật tư.
- Phiếu cấp phát vật tư gắn với lệnh; ghi sổ sẽ trừ tồn và chuyển lệnh sang **Đang sản xuất**.
- Ghi nhận hoàn thành: sản lượng tốt nhập kho thành phẩm, sản lượng lỗi chỉ ghi nhận; đơn giá thành phẩm tính từ chi phí vật tư thực tế.
- Báo cáo chênh lệch chi phí theo lệnh và theo tháng, top vật tư vượt định mức.

### Phi chức năng

- **Workflow:** Nháp → Chờ duyệt → Đã duyệt → Ghi sổ; hủy trước khi ghi sổ.
- **Chống ghi trùng:** kiểm tra trạng thái trong transaction SQL Server `Serializable`; rowversion trên chứng từ.
- **Phân quyền:** ASP.NET Core Identity, vai trò Administrator / Manager / Warehouse / Production / Purchasing.
- **Audit log:** ghi nhận CREATE, UPDATE, SUBMIT, APPROVE, POST, CANCEL, COMPLETE, LOGIN; lọc theo hành động, đối tượng, người dùng, khoảng ngày và xuất CSV.
- **PWA:** manifest (`VPH ERP`), service worker, icon 192/512, trang offline. Chỉ cache tài nguyên tĩnh; **không cache trang nghiệp vụ đã đăng nhập**.
- **Responsive:** sidebar drawer trên mobile, bảng có vùng cuộn ngang, form và KPI co giãn theo màn hình.

## Kiến trúc

```
CV_ERP/
├─ src/
│  ├─ CvErp.Domain/          # Entity, enum, quy tắc miền
│  ├─ CvErp.Infrastructure/  # EF Core, Identity stores, seeding, services, reporting
│  └─ CvErp.Web/             # MVC controllers, Razor views, view models, wwwroot, PWA
├─ docs/screenshots/         # Ảnh giao diện dùng trong README
├─ tools/Generate-PwaIcons.ps1
└─ CV_ERP.slnx
```

### Quyết định thiết kế quan trọng

- **`InventoryTransactions` là single source of truth** cho tồn kho. Không có cột “tồn kho” cập nhật trực tiếp trên vật tư; mọi số dư đều được tổng hợp từ sổ cái.
- **BOM snapshot:** mỗi lệnh sản xuất có bảng `ProductionOrderMaterials` riêng, chụp số lượng và đơn giá tại thời điểm tạo lệnh.
- **Số thập phân:** tất cả số lượng/đơn giá/giá trị dùng `decimal(18,4)` để tránh sai số dấu phẩy động.
- **Văn hóa dữ liệu:** input số/ngày dùng `InvariantCulture` (dấu chấm thập phân), hiển thị tiếng Việt định dạng thủ công qua `UiText`; tránh lỗi `37,08` bị hiểu thành `3708` trên máy có locale vi-VN.
- **Transaction:** ghi sổ kho, cấp phát vật tư và hoàn thành sản xuất chạy trong transaction SQL Server với isolation `Serializable`.
- **PWA an toàn:** service worker chỉ precache `offline.html`, icon và manifest; tài nguyên tĩnh dùng cache-first; navigation luôn ưu tiên mạng và trả trang offline khi mất kết nối.

## Luồng nghiệp vụ

### Chứng từ kho

```
Nháp ──Trình duyệt──▶ Chờ duyệt ──Phê duyệt──▶ Đã duyệt ──Ghi sổ──▶ Đã ghi sổ
  │                        │                       │
  └────────────── Hủy ─────┴───────────────────────┘
```

- Chỉ chứng từ **Đã duyệt** mới được ghi sổ.
- Ghi sổ tạo giao dịch sổ cái; phiếu nhập tạo bút toán dương, phiếu xuất tạo bút toán âm, điều chuyển tạo cặp bút toán âm/ dương ở hai kho.
- Chứng từ đã ghi sổ không thể sửa hoặc hủy.

### Lệnh sản xuất

```
Nháp ──▶ Chờ duyệt ──▶ Đã duyệt ──(cấp phát vật tư)──▶ Đang sản xuất ──▶ Hoàn thành
```

- Chỉ lệnh **Đã duyệt** hoặc **Đang sản xuất** mới được cấp phát vật tư / hoàn thành.
- Ghi sổ phiếu cấp phát đầu tiên tự động chuyển lệnh sang **Đang sản xuất**.
- Hoàn thành lệnh tạo `ProductionOutput` và giao dịch nhập kho thành phẩm; không cho hoàn thành lần hai.

### Công thức chênh lệch

```
Chênh lệch chi phí = Chi phí vật tư thực tế (phiếu cấp phát đã ghi sổ)
                    − Chi phí định mức (BOM × số lượng kế hoạch)
Tỷ lệ chênh lệch   = Chênh lệch / Chi phí định mức × 100%
```

Chênh lệch dương: thực tế cao hơn định mức (hao hụt, cấp vượt, giá tăng).
Chênh lệch âm: thực tế thấp hơn định mức.

## Phân quyền

| Nghiệp vụ | Administrator | Manager | Warehouse | Production | Purchasing |
|---|:---:|:---:|:---:|:---:|:---:|
| Xem dashboard kho / sản xuất | ✓ | ✓ | ✓ | ✓ | ✓ |
| Xem chênh lệch chi phí, audit log | ✓ | ✓ |  |  |  |
| Quản lý người dùng | ✓ |  |  |  |  |
| Tạo/sửa danh mục, BOM | ✓ | ✓ |  |  |  |
| Lập/trình duyệt chứng từ kho | ✓ |  | ✓ |  | ✓ |
| Phê duyệt chứng từ kho | ✓ | ✓ |  |  |  |
| Ghi sổ chứng từ kho | ✓ |  | ✓ |  |  |
| Lập/trình duyệt lệnh sản xuất | ✓ |  |  | ✓ |  |
| Phê duyệt lệnh sản xuất | ✓ | ✓ |  |  |  |
| Lập/trình duyệt phiếu cấp phát | ✓ |  |  | ✓ |  |
| Phê duyệt phiếu cấp phát | ✓ | ✓ |  |  |  |
| Ghi sổ phiếu cấp phát | ✓ |  | ✓ |  |  |
| Ghi nhận hoàn thành lệnh | ✓ |  |  | ✓ |  |

## Công nghệ

- **Backend:** ASP.NET Core 10 MVC, C#, EF Core 10 (SQL Server), ASP.NET Core Identity.
- **Frontend:** Razor, Bootstrap 5.3.3, Bootstrap Icons 1.11.3, jQuery, Chart.js 4.4.7.
- **PWA:** Web App Manifest, service worker, offline fallback, icon 192/512; tên cài đặt **VPH ERP**.
- **Thư viện vendor** được lưu local trong `src/CvErp.Web/wwwroot/lib` để demo không phụ thuộc CDN.

## Chạy dự án

### Yêu cầu

- .NET SDK **10.0**.
- SQL Server Express (mặc định `.\SQLEXPRESS`) hoặc SQL Server khác.
- Windows được khuyến nghị cho script icon, nhưng ứng dụng chạy đa nền tảng.

### Cấu hình

Chuỗi kết nối mặc định trong `src/CvErp.Web/appsettings.json`:

```json
"Default": "Server=.\\SQLEXPRESS;Database=CvErp;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Đổi `Server` / `Database` nếu môi trường khác. Ứng dụng dùng integrated security, không cần mật khẩu SQL.

### Chạy

```powershell
dotnet restore
dotnet build CV_ERP.slnx
dotnet run --project src/CvErp.Web
```

Mở `http://localhost:5088`.

`launchSettings.json` đặt sẵn `ASPNETCORE_ENVIRONMENT=Development` và URL trên. Khi chạy Development, ứng dụng tự động:

1. Chạy migration EF Core.
2. Seed dữ liệu demo nếu database chưa có vật tư.

Nếu chạy không dùng launch profile:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/CvErp.Web --urls http://localhost:5088
```

### Tạo lại database sạch

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -Q "ALTER DATABASE CvErp SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE CvErp;"
dotnet run --project src/CvErp.Web
```

Hoặc dùng EF CLI (nếu đã cài `dotnet-ef`):

```powershell
dotnet ef database drop --project src/CvErp.Infrastructure --startup-project src/CvErp.Web
```

Migration có sẵn:

```powershell
dotnet ef database update --project src/CvErp.Infrastructure --startup-project src/CvErp.Web
```

### Tạo lại icon PWA

```powershell
powershell -ExecutionPolicy Bypass -File tools/Generate-PwaIcons.ps1
```

## Tài khoản demo

| Vai trò | Tên đăng nhập | Mật khẩu | Phạm vi chính |
|---|---|---|---|
| Administrator | `admin` | `Admin@123` | Toàn quyền, quản lý người dùng |
| Manager | `manager` | `Manager@123` | Phê duyệt, danh mục, báo cáo chênh lệch, audit |
| Warehouse | `kho` | `Kho@123` | Chứng từ kho, ghi sổ, tồn kho |
| Production | `sanxuat` | `Sanxuat@123` | Lệnh sản xuất, cấp phát, hoàn thành |
| Purchasing | `muahang` | `Muahang@123` | Lập/trình duyệt phiếu nhập kho |

> Mật khẩu chỉ dùng cho môi trường demo. Không triển khai nguyên trạng tài khoản này ra Internet.

## Dữ liệu mẫu

Seeder tạo dữ liệu tương đối so với ngày chạy:

- 20 vật tư / thành phẩm, 3 kho, 5 nhà cung cấp.
- 3 định mức BOM cho bàn làm việc, kệ sách và tủ tài liệu.
- 10 lệnh sản xuất ở các trạng thái khác nhau.
- 8 phiếu cấp phát ở các trạng thái chờ duyệt, đã duyệt và đã ghi sổ.
- 8 chứng từ kho ở các trạng thái chờ duyệt, đã duyệt và đã ghi sổ; 122 giao dịch sổ cái ban đầu.
- Cảnh báo tồn thấp cho `RAY-TR`, `XOP`, `KEO-NH`.
- Audit log mẫu cho các thao tác chính.

> Bộ seed hiện tại dùng kịch bản nội thất (MDF, bàn, tủ) để minh họa đầy đủ luồng nghiệp vụ; có thể thay bằng dữ liệu ngành sợi khi mở rộng demo.

## API nội bộ phục vụ giao diện

Các endpoint JSON yêu cầu phiên đăng nhập:

| Endpoint | Mục đích |
|---|---|
| `GET /Stock/ItemInfo?itemId=1` | Mã, tên, ĐVT, đơn giá chuẩn của vật tư |
| `GET /Stock/Availability?warehouseId=1&itemId=1` | Tồn khả dụng theo kho |
| `GET /ProductionOrders/BomPreview?productId=18&quantity=10` | Nhu cầu vật tư theo BOM cho số lượng kế hoạch |

## Kiểm thử thủ công đã thực hiện

- Đăng nhập cả 5 vai trò; kiểm tra menu và quyền truy cập từng route.
- Tạo phiếu nhập kho → trình duyệt → phê duyệt → ghi sổ; xác nhận tồn kho tăng.
- Ghi sổ lần hai: bị chặn, không phát sinh giao dịch trùng.
- Tạo phiếu xuất vượt tồn → phê duyệt → ghi sổ: bị chặn, không phát sinh giao dịch.
- Hủy chứng từ trước ghi sổ: trạng thái chuyển `Cancelled`, lưu lý do và audit log.
- Lệnh sản xuất: tạo → trình duyệt → phê duyệt → cấp phát vật tư → ghi sổ → hoàn thành; xác nhận thành phẩm nhập kho và chi phí thực tế.
- Hoàn thành lệnh lần hai: bị chặn.
- Kiểm tra line editor: thêm/xóa dòng, reindex, tự điền đơn giá, tính thành tiền, cảnh báo tồn khả dụng.
- Kiểm tra BOM preview: đổi thành phẩm/số lượng, bảng nhu cầu cập nhật.
- Kiểm tra Chart.js trên 3 dashboard và 2 báo cáo.
- Kiểm tra audit log: lọc theo hành động/đối tượng/người dùng/ngày, xuất CSV.
- Kiểm tra PWA: manifest, service worker active, icon; dừng server và điều hướng trang nghiệp vụ → hiển thị trang offline, không lộ nội dung cũ.
- Kiểm tra responsive ở 390×844: topbar, KPI, bảng cuộn ngang, sidebar drawer.

## Cấu trúc thư mục chính

```
src/CvErp.Domain/
  Entities/            # Item, Warehouse, Supplier, Bom, StockDocument, ...
  Enums/               # DocumentStatus, ProductionOrderStatus, ...
  Constants/           # AppRoles

src/CvErp.Infrastructure/
  Data/                # AppDbContext, AppDbContextFactory, DbSeeder
  Migrations/          # InitialCreate
  Services/            # InventoryService, ProductionService, WorkflowService,
                       # NumberSequenceService, AuditService, ReportingService
  Reporting/           # Report models
  Common/              # BusinessRuleException, UserContext

src/CvErp.Web/
  Controllers/         # Account, Dashboard, Items, StockDocuments, Stock,
                       # ProductionOrders, MaterialIssues, Reports, AuditLogs, Users
  ViewModels/
  Views/
  Helpers/             # UiText, ErpLookups, Csv
  Filters/             # BusinessRuleExceptionFilter
  wwwroot/
    css/site.css
    js/site.js         # line editor, BOM preview, charts, shell, PWA
    manifest.webmanifest
    sw.js
    offline.html
    icons/
    lib/
```

## Hạn chế và hướng phát triển

- Chưa quản lý lô/serial, hạn sử dụng, giá vốn theo phương pháp FIFO/bình quân.
- Chưa có đa tiền tệ, đa ngôn ngữ và phân quyền theo phòng ban/chi nhánh.
- Chưa tách người tạo và người duyệt bắt buộc (hiện dựa trên vai trò).
- Chưa có integration test tự động; kiểm thử hiện tại là smoke test thủ công qua HTTP/browser.
- Có thể bổ sung: barcode/QR, nhập liệu hàng loạt Excel, thông báo email, job cảnh báo tồn kho, báo cáo giá thành đầy đủ.

## Ghi chú

- Đây là dự án demo độc lập, không sao chép mã nguồn hay tài sản của BRAVO. Tên **Vinatex Phú Hưng** được dùng để mô phỏng bối cảnh ứng tuyển; dự án không phải hệ thống chính thức, không sử dụng logo/nhãn hiệu chính thức và không đại diện cho công ty.
- Logo/icon được sinh bằng script `tools/Generate-PwaIcons.ps1`.
- Các thư viện mã nguồn mở giữ bản quyền của tác giả tương ứng.
