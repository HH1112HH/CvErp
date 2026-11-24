# -*- coding: utf-8 -*-
"""Sinh 3 trang PBIR cho CvErp_Dashboard.

Chạy: python build-report.py
Ghi đè toàn bộ CvErp_Dashboard.Report/definition/pages.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent / "CvErp_Dashboard"
REPORT = ROOT / "CvErp_Dashboard.Report"
DEF = REPORT / "definition"
PAGES = DEF / "pages"
THEME_SRC = Path(__file__).resolve().parent / "theme.json"
THEME_DST = REPORT / "StaticResources" / "RegisteredResources" / "CvErpTheme.json"

VISUAL_SCHEMA = "https://developer.microsoft.com/json-schemas/fabric/item/report/definition/visualContainer/2.7.0/schema.json"
PAGE_SCHEMA = "https://developer.microsoft.com/json-schemas/fabric/item/report/definition/page/2.1.0/schema.json"
PAGES_SCHEMA = "https://developer.microsoft.com/json-schemas/fabric/item/report/definition/pagesMetadata/1.1.0/schema.json"

NAVY = "#1E3A5F"
GREEN = "#15803D"
RED = "#DC2626"
AMBER = "#D97706"
SLATE = "#64748B"
BORDER = "#E2E8F0"
PAGE_BG = "#F1F5F9"

PAGE_W = 1280
PAGE_H = 720
M = 16
G = 8
CARD_Y = 62
CARD_H = 96


def oid(name: str) -> str:
    return hashlib.md5(name.encode("utf-8")).hexdigest()[:20]


def lit(value) -> dict:
    return {"expr": {"Literal": {"Value": value}}}


def lit_str(s: str) -> dict:
    return lit("'" + s.replace("'", "''") + "'")


def lit_num(n: int | float) -> dict:
    if isinstance(n, int):
        return lit(f"{n}L")
    return lit(f"{n}D")


def lit_bool(v: bool) -> dict:
    return lit("true" if v else "false")


def solid(color: str) -> dict:
    return {"solid": {"color": lit_str(color)}}


def col(entity: str, prop: str, display: str | None = None) -> dict:
    p = {
        "field": {"Column": {"Expression": {"SourceRef": {"Entity": entity}}, "Property": prop}},
        "queryRef": f"{entity}.{prop}",
        "nativeQueryRef": display or prop,
    }
    if display:
        p["displayName"] = display
    return p


def meas(entity: str, prop: str, display: str | None = None) -> dict:
    p = {
        "field": {"Measure": {"Expression": {"SourceRef": {"Entity": entity}}, "Property": prop}},
        "queryRef": f"{entity}.{prop}",
        "nativeQueryRef": display or prop,
    }
    if display:
        p["displayName"] = display
    return p


def title_props(text: str, size: int = 11, color: str = NAVY) -> list:
    return [{
        "properties": {
            "show": lit_bool(True),
            "text": lit_str(text),
            "fontColor": solid(color),
            "fontSize": lit_num(size),
            "bold": lit_bool(True),
            "alignment": lit_str("left"),
        }
    }]


def frame(title: str | None = None, fill: bool = True, border: bool = True) -> dict:
    vco: dict = {
        "title": title_props(title) if title else [{"properties": {"show": lit_bool(False)}}],
        "subTitle": [{"properties": {"show": lit_bool(False)}}],
    }
    if fill:
        vco["background"] = [{
            "properties": {
                "show": lit_bool(True),
                "color": solid("#FFFFFF"),
                "transparency": lit_num(0),
            }
        }]
    if border:
        vco["border"] = [{
            "properties": {
                "show": lit_bool(True),
                "color": solid(BORDER),
                "radius": lit_num(8),
            }
        }]
    vco["dropShadow"] = [{
        "properties": {
            "show": lit_bool(True),
            "color": solid("#0F172A"),
            "transparency": lit_num(92),
            "shadowBlur": lit_num(8),
            "shadowDistance": lit_num(1),
            "angle": lit_num(90),
        }
    }]
    return vco


def visual(
    name: str,
    vtype: str,
    x: float,
    y: float,
    w: float,
    h: float,
    projections: dict | None = None,
    title: str | None = None,
    objects: dict | None = None,
    sort: dict | None = None,
    filters: list | None = None,
    vco_extra: dict | None = None,
    z: int = 0,
) -> dict:
    query: dict = {}
    if projections:
        query["queryState"] = {
            role: {"projections": projs} for role, projs in projections.items()
        }
        if sort:
            query["sortDefinition"] = sort
    visual_obj: dict = {"visualType": vtype}
    if query:
        visual_obj["query"] = query
    if objects:
        visual_obj["objects"] = objects
    vco = frame(title)
    if vco_extra:
        vco.update(vco_extra)
    visual_obj["visualContainerObjects"] = vco
    visual_obj["drillFilterOtherVisuals"] = True

    out = {
        "$schema": VISUAL_SCHEMA,
        "name": oid(name),
        "position": {
            "x": x,
            "y": y,
            "z": z,
            "height": h,
            "width": w,
            "tabOrder": z,
        },
        "visual": visual_obj,
    }
    if filters:
        out["filterConfig"] = {"filters": filters}
    return out


def textbox(name: str, text: str, x: float, y: float, w: float, h: float, size: int = 16,
            color: str = NAVY, bold: bool = True, bg: bool = False) -> dict:
    run = {"value": text, "textStyle": {"fontSize": f"{size}pt", "color": color}}
    if bold:
        run["textStyle"]["fontWeight"] = "bold"
    obj = {
        "visualType": "textbox",
        "objects": {
            "general": [{
                "properties": {
                    "paragraphs": [{
                        "textRuns": [run],
                        "horizontalTextAlignment": "left",
                    }]
                }
            }]
        },
        "visualContainerObjects": {
            "title": [{"properties": {"show": lit_bool(False)}}],
            "background": [{"properties": {"show": lit_bool(bg)}}],
            "border": [{"properties": {"show": lit_bool(False)}}],
        },
        "drillFilterOtherVisuals": True,
    }
    return {
        "$schema": VISUAL_SCHEMA,
        "name": oid(name),
        "position": {"x": x, "y": y, "z": 100, "height": h, "width": w, "tabOrder": 100},
        "visual": obj,
    }


def card(name: str, measure_name: str, label: str, x: float, w: float) -> dict:
    return visual(
        name, "card", x, CARD_Y, w, CARD_H,
        projections={"Values": [meas("_Measures", measure_name, label)]},
        objects={
            "labels": [{
                "properties": {
                    "fontSize": lit_num(24),
                    "bold": lit_bool(True),
                    "color": solid(NAVY),
                    "labelDisplayUnits": lit_num(1),
                }
            }],
            "categoryLabels": [{
                "properties": {
                    "show": lit_bool(True),
                    "fontSize": lit_num(8),
                    "color": solid(SLATE),
                }
            }],
        },
        z=10,
    )


def title_text(value: str, y: float = 6, h: float = 42, size: int = 16) -> dict:
    return textbox("Header " + value, value, M, y, 780, h, size=size)


def subtitle_text(value: str) -> dict:
    return textbox("Sub " + value, value, M, 40, 780, 16, size=9, color=SLATE, bold=False)


def legend(top: bool = True) -> dict:
    return {
        "legend": [{
            "properties": {
                "show": lit_bool(True),
                "position": lit_str("Top" if top else "Bottom"),
                "fontSize": lit_num(9),
            }
        }]
    }


def axis(font: int = 9) -> dict:
    return {
        "categoryAxis": [{
            "properties": {
                "fontSize": lit_num(font),
                "showAxisTitle": lit_bool(False),
            }
        }],
        "valueAxis": [{
            "properties": {
                "fontSize": lit_num(font),
                "showAxisTitle": lit_bool(False),
                "gridlineShow": lit_bool(True),
                "gridlineColor": solid("#EEF2F7"),
            }
        }],
    }


def no_labels() -> dict:
    return {"labels": [{"properties": {"show": lit_bool(False)}}]}


def with_labels(size: int = 9) -> dict:
    return {"labels": [{"properties": {"show": lit_bool(True), "fontSize": lit_num(size)}}]}


def sort_col(entity: str, prop: str, direction: str = "Ascending") -> dict:
    return {"sort": [{"field": {
        "Column": {"Expression": {"SourceRef": {"Entity": entity}}, "Property": prop}
    }, "direction": direction}]}


def sort_meas(entity: str, prop: str, direction: str = "Descending") -> dict:
    return {"sort": [{"field": {
        "Measure": {"Expression": {"SourceRef": {"Entity": entity}}, "Property": prop}
    }, "direction": direction}]}


def cat_filter(name: str, entity: str, prop: str, value: str | int, alias: str = "f") -> dict:
    return in_filter(name, entity, prop, [value], alias)


def in_filter(name: str, entity: str, prop: str, values: list, alias: str = "f") -> dict:
    lits = []
    for v in values:
        literal = f"'{v}'" if isinstance(v, str) else f"{v}L"
        lits.append([{"Literal": {"Value": literal}}])
    return {
        "name": oid("filter " + name),
        "field": {"Column": {"Expression": {"SourceRef": {"Entity": entity}}, "Property": prop}},
        "type": "Categorical",
        "filter": {
            "Version": 2,
            "From": [{"Name": alias, "Entity": entity, "Type": 0}],
            "Where": [{
                "Condition": {
                    "In": {
                        "Expressions": [{
                            "Column": {
                                "Expression": {"SourceRef": {"Source": alias}},
                                "Property": prop,
                            }
                        }],
                        "Values": lits,
                    }
                }
            }],
        },
    }


COMPLETE_MONTHS = ["2025-12"] + [f"2026-{m:02d}" for m in range(1, 11)]


# ---------------------------------------------------------------------------
# Page 1 — Sản xuất
# ---------------------------------------------------------------------------
P1 = "page-san-xuat"
p1_visuals = []
p1_visuals.append(title_text("SẢN XUẤT SỢI — VINATEX PHÚ HƯNG"))
p1_visuals.append(visual(
    "Slicer sản phẩm", "slicer", 1030, 6, 234, 48,
    projections={"Values": [col("DimProduct", "ItemCode")]},
    objects={
        "data": [{"properties": {"mode": lit_str("Dropdown")}}],
        "header": [{"properties": {"show": lit_bool(False)}}],
    },
    filters=[cat_filter("slicer-finished", "DimProduct", "ItemTypeName", "Thành phẩm")],
))
P1_CARD_W = 240
for i, (mname, label) in enumerate([
    ("Tổng sản lượng (tấn)", "Sản lượng tốt (tấn)"),
    ("Tỷ lệ hoàn thành lệnh %", "Tỷ lệ hoàn thành"),
    ("Tỷ lệ lỗi %", "Tỷ lệ lỗi"),
    ("Số lệnh đang sản xuất", "Lệnh đang SX"),
    ("Số lệnh trễ hạn", "Lệnh trễ hạn"),
]):
    p1_visuals.append(card(f"KPI {mname}", mname, label, M + i * (P1_CARD_W + G), P1_CARD_W))

p1_visuals.append(visual(
    "Sản lượng theo tháng", "lineChart", M, 162, 736, 250,
    projections={
        "Category": [col("vw_OutputMonthly", "PeriodLabel")],
        "Y": [meas("_Measures", "Tổng sản lượng (tấn)")],
        "Series": [col("DimProduct", "Category")],
    },
    title="Sản lượng theo tháng",
    objects={**legend(True), **axis(9)},
    sort=sort_col("vw_OutputMonthly", "PeriodLabel"),
    filters=[in_filter("p1-output-complete", "vw_OutputMonthly", "PeriodLabel", COMPLETE_MONTHS)],
    z=20,
))
p1_visuals.append(visual(
    "Tỷ lệ lỗi theo sản phẩm", "clusteredBarChart", 760, 162, 504, 250,
    projections={
        "Category": [col("vw_ProductionPerformance", "ProductCode")],
        "Y": [meas("_Measures", "Tỷ lệ lỗi %")],
    },
    title="Tỷ lệ lỗi theo sản phẩm",
    objects={
        **legend(False), **axis(9),
        "dataPoint": [{"properties": {"fill": solid(RED)}}],
        **with_labels(9),
    },
    sort=sort_meas("_Measures", "Tỷ lệ lỗi %", "Descending"),
    z=21,
))
p1_visuals.append(visual(
    "Kế hoạch vs thực tế", "clusteredColumnChart", M, 420, 736, 284,
    projections={
        "Category": [col("vw_ProductionPerformance", "ProductCode")],
        "Y": [
            meas("_Measures", "Sản lượng kế hoạch (tấn)"),
            meas("_Measures", "Sản lượng thực tế (tấn)"),
        ],
    },
    title="Kế hoạch vs thực tế theo sản phẩm",
    objects={**legend(True), **axis(9), **no_labels()},
    z=22,
))
p1_visuals.append(visual(
    "Tiến độ lệnh sản xuất", "tableEx", 760, 420, 504, 284,
    projections={"Values": [
        col("vw_ProductionOrderProgress", "OrderNo"),
        col("vw_ProductionOrderProgress", "ProductName", "Sản phẩm"),
        col("vw_ProductionOrderProgress", "StatusName", "Trạng thái"),
        col("vw_ProductionOrderProgress", "ProgressPct", "Tiến độ %"),
        col("vw_ProductionOrderProgress", "DueDate", "Hạn"),
        col("vw_ProductionOrderProgress", "IsOverdue", "Trễ"),
    ]},
    title="Tiến độ lệnh sản xuất",
    objects={
        "columnHeaders": [{"properties": {
            "bold": lit_bool(True),
            "fontColor": solid("#FFFFFF"),
            "backColor": solid(NAVY),
            "fontSize": lit_num(8),
            "wordWrap": lit_bool(True),
        }}],
        "values": [{"properties": {
            "fontSize": lit_num(8),
            "wordWrap": lit_bool(True),
        }}],
    },
    sort=sort_col("vw_ProductionOrderProgress", "DueDate"),
    z=23,
))

# ---------------------------------------------------------------------------
# Page 2 — Kho & NVL
# ---------------------------------------------------------------------------
P2 = "page-kho-nvl"
p2_visuals = []
p2_visuals.append(title_text("KHO & NGUYÊN VẬT LIỆU"))
p2_visuals.append(visual(
    "Slicer kho", "slicer", 1030, 6, 234, 48,
    projections={"Values": [col("DimWarehouse", "WarehouseName")]},
    objects={
        "data": [{"properties": {"mode": lit_str("Dropdown")}}],
        "header": [{"properties": {"show": lit_bool(False)}}],
    },
))
P2_CARD_W = 300
for i, (mname, label) in enumerate([
    ("Giá trị tồn kho (tỷ đồng)", "Giá trị tồn kho (tỷ đ)"),
    ("Số mặt hàng dưới định mức", "Mặt hàng dưới định mức"),
    ("Nhập kho (kg)", "Nhập kho (kg)"),
    ("Tồn kho (kg)", "Tồn kho hiện tại (kg)"),
]):
    p2_visuals.append(card(f"KPI {mname}", mname, label, M + i * (P2_CARD_W + G), P2_CARD_W))

p2_visuals.append(visual(
    "Nhập xuất tồn", "lineClusteredColumnComboChart", M, 162, 736, 250,
    projections={
        "Category": [col("vw_StockMovement", "PeriodLabel")],
        "Y": [
            meas("_Measures", "Nhập kho (kg)"),
            meas("_Measures", "Xuất kho (kg)"),
        ],
        "Y2": [meas("_Measures", "Tồn cuối kỳ theo tháng")],
    },
    title="Nhập – xuất – tồn theo tháng",
    objects={**legend(True), **axis(9), **no_labels()},
    sort=sort_col("vw_StockMovement", "PeriodLabel"),
    filters=[in_filter("p2-stock-complete", "vw_StockMovement", "PeriodLabel", COMPLETE_MONTHS)],
    z=20,
))
p2_visuals.append(visual(
    "Giá trị tồn theo kho", "clusteredColumnChart", 760, 162, 504, 250,
    projections={
        "Category": [col("DimWarehouse", "WarehouseName")],
        "Y": [meas("_Measures", "Giá trị tồn kho (tỷ đồng)")],
    },
    title="Giá trị tồn theo kho",
    objects={**legend(False), **axis(9), **with_labels(9)},
    z=21,
))
p2_visuals.append(visual(
    "Giá trị tồn theo nhóm", "clusteredBarChart", M, 420, 504, 284,
    projections={
        "Category": [col("vw_InventoryOnHand", "Category", "Nhóm vật tư")],
        "Y": [meas("_Measures", "Giá trị tồn kho (tỷ đồng)")],
    },
    title="Giá trị tồn theo nhóm hàng hóa",
    objects={**legend(False), **axis(9), **with_labels(9)},
    sort=sort_meas("_Measures", "Giá trị tồn kho (tỷ đồng)", "Descending"),
    z=22,
))
p2_visuals.append(visual(
    "Mặt hàng dưới định mức", "tableEx", 528, 420, 736, 284,
    projections={"Values": [
        col("vw_InventoryOnHand", "ItemCode", "Mã hàng"),
        col("vw_InventoryOnHand", "ItemName", "Tên hàng"),
        col("vw_InventoryOnHand", "WarehouseName", "Kho"),
        col("vw_InventoryOnHand", "OnHandQuantity", "Tồn"),
        col("vw_InventoryOnHand", "MinStock", "Định mức"),
    ]},
    title="Mặt hàng dưới định mức",
    objects={
        "columnHeaders": [{"properties": {
            "bold": lit_bool(True),
            "fontColor": solid("#FFFFFF"),
            "backColor": solid(NAVY),
            "fontSize": lit_num(8),
        }}],
        "values": [{"properties": {"fontSize": lit_num(8)}}],
    },
    filters=[cat_filter("below-min", "vw_InventoryOnHand", "IsBelowMinStock", 1)],
    sort=sort_col("vw_InventoryOnHand", "OnHandQuantity", "Ascending"),
    z=23,
))

# ---------------------------------------------------------------------------
# Page 3 — Chi phí & Dự báo
# ---------------------------------------------------------------------------
P3 = "page-chi-phi-du-bao"
p3_visuals = []
p3_visuals.append(title_text("CHI PHÍ & XU HƯỚNG"))
P3_CARD_W = 240
for i, (mname, label) in enumerate([
    ("Chi phí NVL định mức (triệu đồng)", "Định mức (triệu đ)"),
    ("Chi phí NVL thực tế (triệu đồng)", "Thực tế (triệu đ)"),
    ("Chênh lệch chi phí (triệu đồng)", "Chênh lệch (triệu đ)"),
    ("Tỷ lệ chênh lệch chi phí %", "Tỷ lệ chênh lệch"),
    ("Số lệnh vượt định mức", "Lệnh vượt định mức"),
]):
    p3_visuals.append(card(f"KPI {mname}", mname, label, M + i * (P3_CARD_W + G), P3_CARD_W))

p3_visuals.append(visual(
    "Sản lượng và xu hướng", "lineChart", M, 162, 736, 250,
    projections={
        "Category": [col("vw_OutputMonthly", "PeriodStart")],
        "Y": [meas("_Measures", "Tổng sản lượng (tấn)")],
    },
    title="Sản lượng theo tháng & xu hướng",
    objects={
        **legend(False), **axis(9),
        **with_labels(9),
        "trend": [{
            "properties": {
                "show": lit_bool(True),
                "style": lit_str("dashed"),
                "lineColor": solid(AMBER),
                "transparency": lit_num(0),
                "combineSeries": lit_bool(True),
            }
        }],
    },
    sort=sort_col("vw_OutputMonthly", "PeriodStart"),
    filters=[in_filter("p3-output-complete", "vw_OutputMonthly", "PeriodLabel", COMPLETE_MONTHS)],
    z=20,
))
p3_visuals.append(visual(
    "Chênh lệch chi phí theo sản phẩm", "clusteredBarChart", 760, 162, 504, 250,
    projections={
        "Category": [col("vw_MaterialCostVariance", "ProductCode")],
        "Y": [meas("_Measures", "Chênh lệch chi phí (triệu đồng)")],
    },
    title="Chênh lệch chi phí NVL theo sản phẩm",
    objects={
        **legend(False), **axis(9),
        "dataPoint": [{"properties": {"fill": solid(AMBER)}}],
        **with_labels(9),
    },
    sort=sort_meas("_Measures", "Chênh lệch chi phí (triệu đồng)", "Descending"),
    z=21,
))
p3_visuals.append(visual(
    "Chi phí theo kỳ", "lineChart", M, 420, 504, 284,
    projections={
        "Category": [col("vw_MaterialCostVariance", "DueMonth", "Tháng")],
        "Y": [
            meas("_Measures", "Chi phí NVL định mức (triệu đồng)"),
            meas("_Measures", "Chi phí NVL thực tế (triệu đồng)"),
        ],
    },
    title="Chi phí NVL theo kỳ",
    objects={**legend(True), **axis(9), **no_labels()},
    sort=sort_col("vw_MaterialCostVariance", "DueMonth"),
    filters=[in_filter("p3-cost-complete", "vw_MaterialCostVariance", "DueMonth", COMPLETE_MONTHS)],
    z=22,
))
p3_visuals.append(visual(
    "Ma trận chênh lệch", "matrix", 528, 420, 736, 284,
    projections={
        "Rows": [col("vw_MaterialCostVariance", "ProductCode")],
        "Columns": [col("vw_MaterialCostVariance", "VarianceStatus", "Trạng thái")],
        "Values": [meas("_Measures", "Chênh lệch chi phí (triệu đồng)", "Chênh lệch")],
    },
    title="Ma trận chênh lệch theo trạng thái",
    objects={
        "columnHeaders": [{"properties": {
            "fontColor": solid("#FFFFFF"),
            "backColor": solid(NAVY),
        }}],
    },
    z=23,
))


PAGES_SPEC = [
    (P1, "Sản xuất", p1_visuals),
    (P2, "Kho & NVL", p2_visuals),
    (P3, "Chi phí & Xu hướng", p3_visuals),
]


def write_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def ensure_schema(path: Path, schema: str) -> None:
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if data.get("$schema") == schema:
        return
    write_json(path, {"$schema": schema, **data})


def main() -> None:
    if PAGES.exists():
        shutil.rmtree(PAGES)
    PAGES.mkdir(parents=True)

    page_order = []
    for page_id, display, visuals in PAGES_SPEC:
        page_order.append(page_id)
        page_dir = PAGES / page_id
        write_json(page_dir / "page.json", {
            "$schema": PAGE_SCHEMA,
            "name": page_id,
            "displayName": display,
            "displayOption": "FitToPage",
            "height": PAGE_H,
            "width": PAGE_W,
            "objects": {
                "background": [{
                    "properties": {
                        "color": solid(PAGE_BG),
                        "transparency": lit_num(0),
                    }
                }]
            },
        })
        seen = set()
        for v in visuals:
            if v["name"] in seen:
                raise SystemExit(f"Trùng visual name: {v['name']}")
            seen.add(v["name"])
            write_json(page_dir / "visuals" / v["name"] / "visual.json", v)

    write_json(PAGES / "pages.json", {
        "$schema": PAGES_SCHEMA,
        "pageOrder": page_order,
        "activePageName": page_order[0],
    })

    # Đăng ký theme tùy chỉnh
    THEME_DST.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(THEME_SRC, THEME_DST)
    report_json = DEF / "report.json"
    report = json.loads(report_json.read_text(encoding="utf-8-sig"))
    report["themeCollection"]["customTheme"] = {
        "name": THEME_DST.name,
        "reportVersionAtImport": {"visual": "2.11.0", "report": "3.4.0", "page": "2.3.1"},
        "type": "RegisteredResources",
    }
    packages = report.setdefault("resourcePackages", [])
    rr = next((p for p in packages if p.get("type") == "RegisteredResources"), None)
    if rr is None:
        rr = {"name": "RegisteredResources", "type": "RegisteredResources", "items": []}
        packages.append(rr)
    rr["items"] = [i for i in rr.get("items", []) if i.get("name") != THEME_DST.name]
    rr["items"].append({
        "name": THEME_DST.name,
        "path": THEME_DST.name,
        "type": "CustomTheme",
    })
    write_json(report_json, report)

    ensure_schema(
        ROOT / "CvErp_Dashboard.pbip",
        "https://developer.microsoft.com/json-schemas/fabric/pbip/pbipProperties/1.0.0/schema.json",
    )
    ensure_schema(
        REPORT / "definition.pbir",
        "https://developer.microsoft.com/json-schemas/fabric/item/report/definitionProperties/2.0.0/schema.json",
    )

    total = sum(len(v) for _, _, v in PAGES_SPEC)
    print(f"OK: {len(PAGES_SPEC)} trang, {total} visual")
    for page_id, display, visuals in PAGES_SPEC:
        print(f"  {display}: {len(visuals)} visual")


if __name__ == "__main__":
    main()
