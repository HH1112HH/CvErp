# -*- coding: utf-8 -*-
"""Sinh semantic model TMDL cho CvErp_Dashboard từ schema 6 SQL view.

Chạy: python generate-model.py
Yêu cầu: sqlcmd trong PATH, SQL Server .\\SQLEXPRESS, DB CvErp.
"""
from __future__ import annotations

import json
import subprocess
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parent / "CvErp_Dashboard"
SM = ROOT / "CvErp_Dashboard.SemanticModel"
DEF = SM / "definition"
TABLES = DEF / "tables"

SERVER = r"localhost\SQLEXPRESS"
DATABASE = "CvErp"

VIEWS = [
    "vw_InventoryOnHand",
    "vw_ProductionPerformance",
    "vw_MaterialCostVariance",
    "vw_ProductionOrderProgress",
    "vw_StockMovement",
    "vw_OutputMonthly",
]

NS = uuid.UUID("7f1cd1b0-1111-4c4e-9d2a-0d9a5c0e7a01")


def tag(name: str) -> str:
    return str(uuid.uuid5(NS, name))


def run_sql(query: str) -> str:
    out = subprocess.run(
        ["sqlcmd", "-S", r".\SQLEXPRESS", "-E", "-C", "-h", "-1", "-W", "-s", "|", "-Q", query],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    return out.stdout


def load_columns() -> dict[str, list[dict]]:
    names = ",".join(f"'{v}'" for v in VIEWS)
    query = f"""
SET NOCOUNT ON; USE {DATABASE};
SELECT OBJECT_NAME(c.object_id), c.name, t.name
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE OBJECT_NAME(c.object_id) IN ({names})
ORDER BY OBJECT_NAME(c.object_id), c.column_id;
"""
    raw = run_sql(query)
    result: dict[str, list[dict]] = {v: [] for v in VIEWS}
    for line in raw.splitlines():
        parts = [p.strip() for p in line.split("|")]
        if len(parts) != 3 or parts[0] not in result:
            continue
        result[parts[0]].append({"ColumnName": parts[1], "TypeName": parts[2]})
    return result


def data_type(row: dict) -> str:
    t = row["TypeName"].lower()
    if t in ("int", "bigint", "smallint", "tinyint"):
        return "int64"
    if t in ("decimal", "numeric", "float", "real", "money", "smallmoney"):
        return "double"
    if t == "bit":
        return "boolean"
    if t in ("date", "datetime", "datetime2", "smalldatetime", "datetimeoffset", "time"):
        return "dateTime"
    return "string"


def is_date(row: dict) -> bool:
    return row["TypeName"].lower() in ("date", "datetime", "datetime2", "smalldatetime")


def is_number(row: dict) -> bool:
    return data_type(row) in ("int64", "double")


def summarize(row: dict) -> str:
    name = row["ColumnName"]
    if is_date(row) or not is_number(row):
        return "none"
    lower = name.lower()
    if lower.startswith("is") or lower.endswith("id"):
        return "none"
    if any(k in lower for k in ("pct", "rate", "cost", "price", "dayspastdue")):
        return "none"
    if lower in ("minstock", "standardcost", "avgunitcost", "unitcost"):
        return "none"
    return "sum"


def hidden(row: dict) -> bool:
    return row["ColumnName"].lower().endswith("id")


def format_string(row: dict) -> str | None:
    if is_date(row):
        return "yyyy-mm-dd"
    if data_type(row) != "double":
        return None
    if row["ColumnName"].lower().endswith("pct"):
        return "0.00"
    return "#,##0"


def col_block(row: dict, source: bool, table: str) -> str:
    name = row["ColumnName"]
    lines = [f"\tcolumn {name}"]
    lines.append(f"\t\tdataType: {data_type(row)}")
    fmt = format_string(row)
    if fmt:
        lines.append(f"\t\tformatString: {fmt}")
    if hidden(row):
        lines.append("\t\tisHidden")
    lines.append(f"\t\tlineageTag: {tag('column/' + table + '/' + name)}")
    lines.append(f"\t\tsummarizeBy: {summarize(row)}")
    if source:
        lines.append(f"\t\tsourceColumn: {name}")
    return "\n".join(lines)


def partition_sql(view: str) -> str:
    return (
        f"\tpartition {view} = m\n"
        "\t\tmode: import\n"
        "\t\tsource =\n"
        "\t\t\t\tlet\n"
        f'\t\t\t\t\tSource = Sql.Database("{SERVER}", "{DATABASE}"),\n'
        f'\t\t\t\t\tData = Source{{[Schema="dbo",Item="{view}"]}}[Data]\n'
        "\t\t\t\tin\n"
        "\t\t\t\t\tData"
    )


def table_sql_view(view: str, cols: list[dict]) -> str:
    parts = [f"table {view}", f"\tlineageTag: {tag('table/' + view)}", ""]
    parts += [col_block(c, True, view) for c in cols]
    parts += ["", partition_sql(view)]
    return "\n".join(parts) + "\n"


DIM_TABLES = {
    "DimProduct": {
        "view": "vw_InventoryOnHand",
        "columns": [
            ("ItemId", "int64", True),
            ("ItemCode", "string", False),
            ("ItemName", "string", False),
            ("Category", "string", False),
            ("ItemTypeName", "string", False),
            ("Unit", "string", False),
        ],
    },
    "DimWarehouse": {
        "view": "vw_InventoryOnHand",
        "columns": [
            ("WarehouseId", "int64", True),
            ("WarehouseCode", "string", False),
            ("WarehouseName", "string", False),
        ],
    },
}


def table_dim(name: str, spec: dict) -> str:
    parts = [f"table {name}", f"\tlineageTag: {tag('table/' + name)}", ""]
    for col, dtype, hide in spec["columns"]:
        block = [f"\tcolumn {col}", f"\t\tdataType: {dtype}"]
        if hide:
            block.append("\t\tisHidden")
        block.append(f"\t\tlineageTag: {tag('column/' + name + '/' + col)}")
        block.append("\t\tsummarizeBy: none")
        block.append(f"\t\tsourceColumn: {col}")
        parts.append("\n".join(block))
    cols = ", ".join(f'"{c}"' for c, _, _ in spec["columns"])
    parts += [
        "",
        f"\tpartition {name} = m",
        "\t\tmode: import",
        "\t\tsource =",
        "\t\t\t\tlet",
        f'\t\t\t\t\tSource = Sql.Database("{SERVER}", "{DATABASE}"),',
        f'\t\t\t\t\tData = Source{{[Schema="dbo",Item="{spec["view"]}"]}}[Data],',
        f"\t\t\t\t\tDim = Table.Distinct(Table.SelectColumns(Data, {{ {cols} }}))",
        "\t\t\t\tin",
        "\t\t\t\t\tDim",
    ]
    return "\n".join(parts) + "\n"


MEASURES = [
    # (tên, DAX, format, folder, mô tả)
    ("Tổng sản lượng tốt", "SUM ( vw_OutputMonthly[GoodQuantity] )", "#,##0", "1. Sản xuất", "Tổng sản lượng sợi đạt chất lượng (kg)"),
    ("Tổng sản lượng lỗi", "SUM ( vw_OutputMonthly[DefectQuantity] )", "#,##0", "1. Sản xuất", "Tổng sản lượng sợi lỗi (kg)"),
    ("Tổng sản lượng (tấn)", "DIVIDE ( [Tổng sản lượng tốt], 1000 )", "#,##0.0", "1. Sản xuất", "Tổng sản lượng tốt quy đổi tấn"),
    ("Tỷ lệ lỗi %", "DIVIDE ( [Tổng sản lượng lỗi], [Tổng sản lượng tốt] + [Tổng sản lượng lỗi] )", "0.00%", "1. Sản xuất", "Tỷ lệ sợi lỗi trên tổng sản lượng"),
    ("Sản lượng kế hoạch", "SUM ( vw_ProductionPerformance[PlannedQuantity] )", "#,##0", "1. Sản xuất", "Tổng sản lượng theo kế hoạch (kg)"),
    ("Sản lượng thực tế", "SUM ( vw_ProductionPerformance[ActualQuantity] )", "#,##0", "1. Sản xuất", "Tổng sản lượng thực tế (kg)"),
    ("Sản lượng kế hoạch (tấn)", "DIVIDE ( [Sản lượng kế hoạch], 1000 )", "#,##0.0", "1. Sản xuất", "Sản lượng kế hoạch quy đổi tấn"),
    ("Sản lượng thực tế (tấn)", "DIVIDE ( [Sản lượng thực tế], 1000 )", "#,##0.0", "1. Sản xuất", "Sản lượng thực tế quy đổi tấn"),
    ("Tỷ lệ hoàn thành lệnh %", "DIVIDE ( [Sản lượng thực tế], [Sản lượng kế hoạch] )", "0.00%", "1. Sản xuất", "Mức độ hoàn thành kế hoạch sản xuất"),
    ("Số lệnh đang sản xuất", "CALCULATE ( COUNTROWS ( vw_ProductionOrderProgress ), vw_ProductionOrderProgress[StatusCode] = 3 )", "#,##0", "1. Sản xuất", "Số lệnh đang trong quá trình sản xuất"),
    ("Số lệnh hoàn thành", "CALCULATE ( COUNTROWS ( vw_ProductionOrderProgress ), vw_ProductionOrderProgress[StatusCode] = 4 )", "#,##0", "1. Sản xuất", "Số lệnh đã hoàn thành"),
    ("Số lệnh trễ hạn", "CALCULATE ( COUNTROWS ( vw_ProductionOrderProgress ), vw_ProductionOrderProgress[IsOverdue] = 1 )", "#,##0", "1. Sản xuất", "Số lệnh quá hạn hoàn thành"),
    ("Số ngày trễ TB", "AVERAGEX ( FILTER ( vw_ProductionOrderProgress, vw_ProductionOrderProgress[IsOverdue] = 1 ), vw_ProductionOrderProgress[DaysPastDue] )", "#,##0.0", "1. Sản xuất", "Số ngày trễ hạn trung bình của các lệnh quá hạn"),
    ("Sản lượng tháng gần nhất (tấn)", "VAR LastMonth = MAX ( vw_OutputMonthly[PeriodStart] ) RETURN CALCULATE ( [Tổng sản lượng (tấn)], vw_OutputMonthly[PeriodStart] = LastMonth )", "#,##0.0", "1. Sản xuất", "Sản lượng tốt của tháng có dữ liệu mới nhất"),
    ("Giá trị tồn kho", "SUM ( vw_InventoryOnHand[StockValue] )", "#,##0", "2. Kho & NVL", "Giá trị tồn kho theo giá vốn bình quân (đồng)"),
    ("Giá trị tồn kho (tỷ đồng)", "DIVIDE ( [Giá trị tồn kho], 1000000000 )", "#,##0.00", "2. Kho & NVL", "Giá trị tồn kho quy đổi tỷ đồng"),
    ("Tồn kho (kg)", "SUM ( vw_InventoryOnHand[OnHandQuantity] )", "#,##0", "2. Kho & NVL", "Số lượng tồn kho hiện tại (kg hoặc đơn vị)"),
    ("Số mặt hàng dưới định mức", "CALCULATE ( DISTINCTCOUNT ( vw_InventoryOnHand[ItemCode] ), vw_InventoryOnHand[IsBelowMinStock] = 1 )", "#,##0", "2. Kho & NVL", "Số mã hàng có tồn kho thấp hơn định mức tối thiểu"),
    ("Tồn cuối kỳ", "VAR LastPeriod = MAX ( vw_StockMovement[PeriodStart] ) RETURN CALCULATE ( SUM ( vw_StockMovement[ClosingQuantity] ), vw_StockMovement[PeriodStart] = LastPeriod )", "#,##0", "2. Kho & NVL", "Tồn kho cuối kỳ gần nhất (kg)"),
    ("Tồn cuối kỳ theo tháng", "SUM ( vw_StockMovement[ClosingQuantity] )", "#,##0", "2. Kho & NVL", "Tồn kho cuối kỳ theo từng tháng"),
    ("Nhập kho (kg)", "SUM ( vw_StockMovement[ReceiptQuantity] )", "#,##0", "2. Kho & NVL", "Tổng nhập kho trong kỳ"),
    ("Xuất kho (kg)", "SUM ( vw_StockMovement[IssueQuantity] )", "#,##0", "2. Kho & NVL", "Tổng xuất kho trong kỳ"),
    ("Chi phí NVL định mức", "SUM ( vw_MaterialCostVariance[StandardMaterialCost] )", "#,##0", "3. Chi phí", "Chi phí nguyên vật liệu theo định mức (đồng)"),
    ("Chi phí NVL thực tế", "SUM ( vw_MaterialCostVariance[ActualMaterialCost] )", "#,##0", "3. Chi phí", "Chi phí nguyên vật liệu thực tế (đồng)"),
    ("Chênh lệch chi phí NVL", "[Chi phí NVL thực tế] - [Chi phí NVL định mức]", "#,##0", "3. Chi phí", "Chênh lệch chi phí NVL thực tế so với định mức (đồng)"),
    ("Tỷ lệ chênh lệch chi phí %", "DIVIDE ( [Chênh lệch chi phí NVL], [Chi phí NVL định mức] )", "0.00%", "3. Chi phí", "Tỷ lệ chênh lệch chi phí NVL"),
    ("Chi phí NVL định mức (triệu đồng)", "DIVIDE ( [Chi phí NVL định mức], 1000000 )", "#,##0.0", "3. Chi phí", "Chi phí NVL định mức quy đổi triệu đồng"),
    ("Chi phí NVL thực tế (triệu đồng)", "DIVIDE ( [Chi phí NVL thực tế], 1000000 )", "#,##0.0", "3. Chi phí", "Chi phí NVL thực tế quy đổi triệu đồng"),
    ("Chênh lệch chi phí (triệu đồng)", "DIVIDE ( [Chênh lệch chi phí NVL], 1000000 )", "#,##0.0", "3. Chi phí", "Chênh lệch chi phí NVL quy đổi triệu đồng"),
    ("Số lệnh vượt định mức", "CALCULATE ( COUNTROWS ( vw_MaterialCostVariance ), vw_MaterialCostVariance[VarianceStatus] = \"Vượt định mức\" )", "#,##0", "3. Chi phí", "Số lệnh sản xuất có chi phí NVL vượt định mức"),
]


def measures_tmdl() -> str:
    lines = [f"table _Measures", f"\tlineageTag: {tag('table/_Measures')}", ""]
    for name, dax, fmt, folder, desc in MEASURES:
        lines.append(f"\t/// {desc}")
        lines.append(f"\tmeasure '{name}' =")
        for dax_line in dax.splitlines():
            lines.append(f"\t\t\t{dax_line}")
        lines.append(f"\t\tformatString: {fmt}")
        lines.append(f"\t\tdisplayFolder: {folder}")
        lines.append(f"\t\tlineageTag: {tag('measure/' + name)}")
        lines.append("")
    lines.append("\tcolumn Placeholder")
    lines.append("\t\tisHidden")
    lines.append(f"\t\tlineageTag: {tag('column/_Measures/Placeholder')}")
    lines.append("\t\tsummarizeBy: none")
    lines.append("\t\tisNameInferred")
    lines.append("\t\tsourceColumn: [Placeholder]")
    lines.append("")
    lines.append("\tpartition _Measures = calculated")
    lines.append("\t\tmode: import")
    lines.append("\t\tsource =")
    lines.append('\t\t\t\tROW ("Placeholder", 1)')
    return "\n".join(lines) + "\n"


RELATIONSHIPS = [
    # (from = phía nhiều, to = phía một)
    ("vw_InventoryOnHand", "ItemId", "DimProduct", "ItemId"),
    ("vw_ProductionPerformance", "ProductItemId", "DimProduct", "ItemId"),
    ("vw_OutputMonthly", "ItemId", "DimProduct", "ItemId"),
    ("vw_StockMovement", "ItemId", "DimProduct", "ItemId"),
    ("vw_InventoryOnHand", "WarehouseId", "DimWarehouse", "WarehouseId"),
    ("vw_StockMovement", "WarehouseId", "DimWarehouse", "WarehouseId"),
    ("vw_MaterialCostVariance", "ProductCode", "DimProduct", "ItemCode"),
    ("vw_ProductionOrderProgress", "ProductCode", "DimProduct", "ItemCode"),
]


def relationships_tmdl() -> str:
    blocks = []
    for i, (from_t, from_c, to_t, to_c) in enumerate(RELATIONSHIPS):
        blocks.append(
            f"relationship {tag('rel/' + str(i))}\n"
            f"\tfromColumn: {from_t}.{from_c}\n"
            f"\ttoColumn: {to_t}.{to_c}"
        )
    return "\n\n".join(blocks) + "\n"


def validate_lineage_tags() -> None:
    """lineageTag là identity của object trong TOM; trùng tag làm column ID bị ghi đè."""
    import re

    pat = re.compile(r"^\s*lineageTag:\s*([0-9a-f-]+)\s*$")
    seen: dict[str, str] = {}
    dups: list[str] = []
    for path in sorted(DEF.rglob("*.tmdl")):
        for line in path.read_text(encoding="utf-8").splitlines():
            m = pat.match(line)
            if not m:
                continue
            lt = m.group(1)
            if lt in seen:
                dups.append(f"{lt} ({seen[lt]} <-> {path.name})")
            else:
                seen[lt] = path.name
    if dups:
        raise SystemExit("Duplicate lineageTag(s):\n  " + "\n  ".join(dups))


def main() -> None:
    cols = load_columns()
    for d in (SM, DEF, TABLES, DEF / "cultures"):
        d.mkdir(parents=True, exist_ok=True)

    (DEF / "database.tmdl").write_text("database CvErp\n\tcompatibilityLevel: 1600\n", encoding="utf-8")

    for view in VIEWS:
        (TABLES / f"{view}.tmdl").write_text(table_sql_view(view, cols[view]), encoding="utf-8")

    for name, spec in DIM_TABLES.items():
        (TABLES / f"{name}.tmdl").write_text(table_dim(name, spec), encoding="utf-8")

    (TABLES / "_Measures.tmdl").write_text(measures_tmdl(), encoding="utf-8")
    (DEF / "relationships.tmdl").write_text(relationships_tmdl(), encoding="utf-8")
    (DEF / "cultures" / "vi-VN.tmdl").write_text("cultureInfo vi-VN\n", encoding="utf-8")

    refs = "\n".join(
        ["ref table " + v for v in VIEWS] + ["ref table DimProduct", "ref table DimWarehouse", "ref table _Measures"]
    )
    model = (
        "model Model\n"
        "\tculture: vi-VN\n"
        "\tdefaultPowerBIDataSourceVersion: powerBI_V3\n"
        "\tsourceQueryCulture: vi-VN\n"
        "\tdataAccessOptions\n"
        "\t\tlegacyRedirects\n"
        "\t\treturnErrorValuesAsNull\n\n"
        "annotation __PBI_TimeIntelligenceEnabled = 0\n\n"
        f"{refs}\n\n"
        "ref cultureInfo vi-VN\n"
    )
    (DEF / "model.tmdl").write_text(model, encoding="utf-8")

    (SM / ".platform").write_text(
        json.dumps(
            {
                "$schema": "https://developer.microsoft.com/json-schemas/fabric/gitIntegration/platformProperties/2.0.0/schema.json",
                "metadata": {"type": "SemanticModel", "displayName": "CvErp_Dashboard"},
                "config": {"version": "2.0", "logicalId": str(uuid.uuid5(NS, "sm/logicalId"))},
            },
            indent=2,
            ensure_ascii=False,
        )
        + "\n",
        encoding="utf-8",
    )

    (SM / "definition.pbism").write_text(
        json.dumps(
            {
                "$schema": "https://developer.microsoft.com/json-schemas/fabric/item/semanticModel/definitionProperties/1.0.0/schema.json",
                "version": "4.2",
                "settings": {},
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    validate_lineage_tags()

    print(f"OK: {len(VIEWS)} view tables, {len(DIM_TABLES)} dimension tables (M), 1 measure table, {len(MEASURES)} measures, {len(RELATIONSHIPS)} relationships")
    for view in VIEWS:
        print(f"  {view}: {len(cols[view])} columns")


if __name__ == "__main__":
    main()
