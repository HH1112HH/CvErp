# Power BI — Dựng CvErp Dashboard (3 trang)

Mục tiêu: tạo `database/powerbi/CvErp_Dashboard.pbix` gồm 3 trang, dùng 6 SQL view.

Thời gian ước tính: 60–90 phút.

---

## Bước 0 — Kết nối dữ liệu

1. Mở `database/CvErp.pbids` (double-click) → Power BI Desktop tự mở hộp thoại kết nối.
   - Nếu hỏi quyền: chọn **Windows** → **Connect** (hoặc **Use my current credentials**).
2. Trong **Navigator**, tick cả 6 view:
   - `vw_InventoryOnHand`
   - `vw_ProductionPerformance`
   - `vw_MaterialCostVariance`
   - `vw_ProductionOrderProgress`
   - `vw_StockMovement`
   - `vw_OutputMonthly`
3. Bấm **Load** (chế độ Import).

> Nếu không kết nối được SQL Server: dùng **Get Data → Text/CSV**, import 6 file trong `database/exports/`. Tên bảng sẽ tự trùng tên view nên các measure bên dưới vẫn dùng được.

---

## Bước 1 — Bảng phụ và quan hệ

### 1.1. Bảng measure

**Modeling → New table**, dán:

```dax
_Measures = ROW ( "Placeholder", 1 )
```

Sau đó ẩn cột `Placeholder` (chuột phải → Hide in report view).

### 1.2. Bảng sản phẩm và kho

**Modeling → New table**, dán lần lượt:

```dax
DimProduct =
SUMMARIZE (
    vw_InventoryOnHand,
    vw_InventoryOnHand[ItemId],
    vw_InventoryOnHand[ItemCode],
    vw_InventoryOnHand[ItemName],
    vw_InventoryOnHand[Category],
    vw_InventoryOnHand[Unit]
)
```

```dax
DimWarehouse =
SUMMARIZE (
    vw_InventoryOnHand,
    vw_InventoryOnHand[WarehouseId],
    vw_InventoryOnHand[WarehouseCode],
    vw_InventoryOnHand[WarehouseName]
)
```

### 1.3. Quan hệ (Model view)

Kéo-thả để tạo 4 quan hệ, tất cả **1 → nhiều**, hướng lọc **Single**:

| Từ (1) | Đến (nhiều) |
|---|---|
| `DimProduct[ItemId]` | `vw_InventoryOnHand[ItemId]` |
| `DimProduct[ItemId]` | `vw_ProductionPerformance[ProductItemId]` |
| `DimProduct[ItemId]` | `vw_OutputMonthly[ItemId]` |
| `DimWarehouse[WarehouseId]` | `vw_InventoryOnHand[WarehouseId]` |

---

## Bước 2 — Measures

Với mỗi measure trong `database/powerbi/measures.dax`:

1. Chọn bảng `_Measures` (hoặc click vùng trống).
2. **Modeling → New measure**.
3. Dán tên + công thức → Enter.

Định dạng sau khi tạo:

| Nhóm | Format |
|---|---|
| Các measure `%` | Percentage, 2 số lẻ |
| `Giá trị tồn kho`, `Chi phí NVL...`, `Chênh lệch...` | Currency, 0 số lẻ, đơn vị đồng |
| `Tổng sản lượng...`, `Sản lượng...`, `Số lệnh...` | Whole number, 0 số lẻ |
| `Số ngày trễ TB` | Decimal, 1 số lẻ |

---

## Bước 3 — Theme

**View → Themes → Browse for themes** → chọn `database/powerbi/theme.json`.

---

## Bước 4 — Trang 1: Sản xuất

Đổi tên trang thành **Sản xuất**.

### Bố cục
- Hàng trên: 5 card KPI
- Giữa trái: line chart sản lượng theo tháng
- Giữa phải: 2 biểu đồ cột (kế hoạch vs thực tế, tỷ lệ lỗi)
- Dưới: bảng tiến độ lệnh
- Góc phải trên: slicer sản phẩm

### Visual chi tiết

| Visual | Trường |
|---|---|
| Card | `[Tổng sản lượng tốt]` — tiêu đề "Sản lượng tốt" |
| Card | `[Tỷ lệ hoàn thành lệnh %]` — tiêu đề "Tỷ lệ hoàn thành" |
| Card | `[Tỷ lệ lỗi %]` — tiêu đề "Tỷ lệ lỗi" |
| Card | `[Số lệnh đang sản xuất]` — tiêu đề "Lệnh đang SX" |
| Card | `[Số lệnh trễ hạn]` — tiêu đề "Lệnh trễ hạn" |
| Line chart | Axis: `vw_OutputMonthly[PeriodStart]` · Values: `[Tổng sản lượng tốt]` · Legend: `DimProduct[Category]` · Tiêu đề "Sản lượng theo tháng" |
| Clustered column | Axis: `vw_ProductionPerformance[ProductCode]` · Values: `[Sản lượng kế hoạch]`, `[Sản lượng thực tế]` · Tiêu đề "Kế hoạch vs thực tế" |
| Clustered bar | Axis: `vw_ProductionPerformance[ProductCode]` · Values: `[Tỷ lệ lỗi %]` · Tiêu đề "Tỷ lệ lỗi theo sản phẩm" |
| Table | `vw_ProductionOrderProgress`: OrderNo, ProductName, StatusName, ProgressPct, DueDate, IsOverdue |
| Slicer | `DimProduct[ItemCode]` — kiểu Dropdown |

Định dạng nhanh:
- Card: Font 28, nhãn 11.
- Line chart: bật Data labels, Marker.
- Bảng: bật Word wrap, sắp xếp theo `DueDate`.

---

## Bước 5 — Trang 2: Kho & Nguyên liệu

Thêm trang mới, đặt tên **Kho & NVL**.

| Visual | Trường |
|---|---|
| Card | `[Giá trị tồn kho]` — "Giá trị tồn kho" |
| Card | `[Số mặt hàng dưới định mức]` — "Mặt hàng dưới định mức" |
| Card | `[Tồn cuối kỳ]` — "Tồn cuối kỳ" |
| Clustered column | Axis: `vw_InventoryOnHand[WarehouseName]` · Values: `[Giá trị tồn kho]` · "Giá trị tồn theo kho" |
| Clustered bar | Axis: `vw_InventoryOnHand[ItemName]` · Values: `SUM(vw_InventoryOnHand[OnHandQuantity])` · Filter: `ItemTypeName = "Nguyên vật liệu"` · Top N 10 · "Top 10 nguyên liệu tồn" |
| Line and clustered column | X: `vw_StockMovement[PeriodStart]` · Column: `SUM(vw_StockMovement[ReceiptQuantity])`, `SUM(vw_StockMovement[IssueQuantity])` · Line: `[Tồn cuối kỳ theo tháng]` · "Nhập – xuất – tồn" |
| Table | `vw_InventoryOnHand` lọc `IsBelowMinStock = 1`: ItemCode, ItemName, WarehouseName, OnHandQuantity, MinStock |

---

## Bước 6 — Trang 3: Chi phí & Dự báo

Thêm trang mới, đặt tên **Chi phí & Dự báo**.

| Visual | Trường |
|---|---|
| Card | `[Chi phí NVL định mức]` |
| Card | `[Chi phí NVL thực tế]` |
| Card | `[Chênh lệch chi phí NVL]` |
| Card | `[Tỷ lệ chênh lệch chi phí %]` |
| Card | `[Số lệnh vượt định mức]` |
| Clustered bar | Axis: `vw_MaterialCostVariance[ProductCode]` · Values: `[Chênh lệch chi phí NVL]` · "Chênh lệch chi phí theo sản phẩm" |
| Line chart | Axis: `vw_ProductionPerformance[DueDate]` · Values: `[Chi phí NVL định mức]`, `[Chi phí NVL thực tế]` · "Chi phí theo kỳ" |
| Line chart | Axis: `vw_OutputMonthly[PeriodStart]` · Values: `[Tổng sản lượng tốt]` · "Sản lượng & dự báo" |
| Matrix | Rows: `vw_MaterialCostVariance[ProductCode]` · Columns: `vw_MaterialCostVariance[VarianceStatus]` · Values: `[Chênh lệch chi phí NVL]` |

### Bật dự báo (điểm nhấn DS/AI)
1. Chọn line chart "Sản lượng & dự báo".
2. **Visualizations → Analytics (kính lúp) → Forecast**.
3. Units: **3**, Forecast length: 3 tháng, Confidence: 95%, Seasonality: Auto.
4. Bấm **Add**.

### Tô màu chênh lệch
- Chọn bar "Chênh lệch chi phí".
- Format → Bars → Color → **fx** → Rules:
  - `[Chênh lệch chi phí NVL]` >= 0 → đỏ `#DC2626`
  - < 0 → xanh `#15803D`

---

## Bước 7 — Lưu và xuất ảnh

1. **File → Save as** → `D:\CV\CV_ERP\database\powerbi\CvErp_Dashboard.pbix`
2. Chụp 3 trang (Win + Shift + S), lưu vào `database/powerbi/screenshots/`:
   - `01-san-xuat.png`
   - `02-kho-nvl.png`
   - `03-chi-phi-du-bao.png`
3. Gửi lại cho mình kiểm tra: mình sẽ đọc cấu trúc `.pbix` để xác nhận đủ bảng, measure, trang.

---

## Checklist nghiệm thu

- [ ] Đủ 6 bảng view + `DimProduct` + `DimWarehouse` + `_Measures`
- [ ] 4 quan hệ đã tạo
- [ ] Đủ measures trong `measures.dax`
- [ ] 3 trang, đúng tên
- [ ] Có 1 line chart bật Forecast
- [ ] Số liệu khớp view (đối chiếu nhanh bằng SQL)
- [ ] File `.pbix` lưu đúng thư mục
- [ ] Có 3 ảnh chụp
