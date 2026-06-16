#!/usr/bin/env python3
"""Export US-trained layout anchor model to ONNX for the Treasury Label Verification Tool (detection-only v1).

Reads testdata/layout-annotations/annotations.json and writes a lightweight ONNX
graph: features[1,9] -> regions[1, N*5] where each region is (x, y, w, h, confidence).
"""
from __future__ import annotations

import json
from collections import defaultdict
from pathlib import Path

try:
    import numpy as np
    import onnx
    from onnx import TensorProto, helper, numpy_helper
except ImportError as exc:  # pragma: no cover
    raise SystemExit(
        "Install export deps: pip install numpy onnx"
    ) from exc

ROOT = Path(__file__).resolve().parents[1]
ANNOTATIONS = ROOT / "testdata" / "layout-annotations" / "annotations.json"
OUT_DIR = ROOT / "testdata" / "layout-models"
OUT_MODEL = OUT_DIR / "label-layout-v1.onnx"

CLASS_ORDER = [
    "OdpStack3Page",
    "OdpStack2Page",
    "OdpFlatLabel",
    "BottlePhoto",
    "Screenshot",
]

MAX_REGIONS = 10


def load_anchors() -> dict[str, list[tuple[float, float, float, float, float]]]:
    data = json.loads(ANNOTATIONS.read_text(encoding="utf-8"))
    grouped: dict[str, list[list[float]]] = defaultdict(list)
    for sample in data["samples"]:
        doc_class = sample["documentLayoutClass"]
        for region in sample["regions"]:
            grouped[doc_class].append(
                [
                    float(region["x"]),
                    float(region["y"]),
                    float(region["width"]),
                    float(region["height"]),
                    0.9,
                ]
            )

    anchors: dict[str, list[tuple[float, float, float, float, float]]] = {}
    for doc_class, rows in grouped.items():
        if not rows:
            continue
        avg = np.mean(np.array(rows, dtype=np.float32), axis=0)
        anchors[doc_class] = [tuple(float(v) for v in avg)] * min(MAX_REGIONS, len(rows))
        # Expand to per-field averages when multiple regions exist
        if len(rows) > 1:
            anchors[doc_class] = [
                tuple(float(v) for v in row)
                for row in rows[:MAX_REGIONS]
            ]
    return anchors


def build_model() -> onnx.ModelProto:
    anchors = load_anchors()
    # Weight tensor: [5 classes, MAX_REGIONS, 5]
    weights = np.zeros((len(CLASS_ORDER), MAX_REGIONS, 5), dtype=np.float32)
    for class_idx, class_name in enumerate(CLASS_ORDER):
        rows = anchors.get(class_name, [])
        for region_idx, row in enumerate(rows[:MAX_REGIONS]):
            weights[class_idx, region_idx, :] = np.array(row, dtype=np.float32)

    # Simple selector: argmax over one-hot class slice (implemented as matmul with identity features)
    # Graph: output = sum_c(features[c] * weights[c]) — linear combo of class templates
    input_features = helper.make_tensor_value_info(
        "features", TensorProto.FLOAT, [1, len(CLASS_ORDER) + 4]
    )
    output_regions = helper.make_tensor_value_info(
        "regions", TensorProto.FLOAT, [1, MAX_REGIONS * 5]
    )

    w_init = numpy_helper.from_array(weights.reshape(len(CLASS_ORDER), MAX_REGIONS * 5), name="W")
    # Use first 5 one-hot dims only
    w_class = numpy_helper.from_array(
        weights.reshape(len(CLASS_ORDER), MAX_REGIONS * 5), name="Wclass"
    )

    # Simplified export: MatMul(features[:, :5], Wclass) -> [1, MAX_REGIONS*5]
    node = helper.make_node(
        "MatMul",
        inputs=["features_slice", "Wclass"],
        outputs=["regions"],
        name="class_weighted_regions",
    )

    # Slice first 5 feature dims via initializer pad (export-time constant folding substitute)
    # For maximum compatibility, bake default OdpStack3Page output as constant fallback
    default_rows = anchors.get("OdpStack3Page") or anchors.get("OdpStack2Page") or []
    flat = np.zeros((MAX_REGIONS * 5,), dtype=np.float32)
    for i, row in enumerate(default_rows[:MAX_REGIONS]):
        flat[i * 5 : (i + 1) * 5] = np.array(row, dtype=np.float32)

    # Build graph with Gemm: regions = features @ W (features 5 x 5 class one-hot padded)
    w_gemm = np.zeros((len(CLASS_ORDER), MAX_REGIONS * 5), dtype=np.float32)
    for i, class_name in enumerate(CLASS_ORDER):
        rows = anchors.get(class_name, [])
        for r, row in enumerate(rows[:MAX_REGIONS]):
            offset = r * 5
            w_gemm[i, offset : offset + 5] = np.array(row, dtype=np.float32)

    w_tensor = numpy_helper.from_array(w_gemm.T, name="Wgemm")
    gemm = helper.make_node("MatMul", ["features5", "Wgemm"], ["regions"], name="region_gemm")

    # Pad features to 5 dims via Slice/Gather is heavy; use 9-dim input and truncate with initializer mask
    mask = numpy_helper.from_array(
        np.eye(len(CLASS_ORDER), dtype=np.float32), name="class_mask"
    )
    mul = helper.make_node("MatMul", ["features", "class_mask"], ["features5"], name="class_select")

    graph = helper.make_graph(
        [mul, gemm],
        "label_layout_v1",
        [input_features],
        [output_regions],
        [w_tensor, mask],
    )
    model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 17)])
    model.doc_string = "Treasury Label Verification Tool TTB label layout detector v1 (US-labeled anchors)"
    onnx.checker.check_model(model)
    return model


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    model = build_model()
    onnx.save(model, OUT_MODEL)
    print(f"Wrote {OUT_MODEL}")


if __name__ == "__main__":
    main()
