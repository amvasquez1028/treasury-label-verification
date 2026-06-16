import json
import pathlib
import sys
import time
import urllib.error
import urllib.request

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _probe_common import DEFAULT_BASE as base, build_opener

repo = pathlib.Path(__file__).resolve().parents[1]
strict = "--strict" in sys.argv

EXPECTED_STD = {
    "01-mismatch-act-of-treason.png": "fail",
    "02-mismatch-juniper-tree-gin.png": "fail",
    "03-odp-ambhar-plata.png": "pass",
    "04-odp-la-venenosa-raicilla.png": "pass",
    "05-odp-jack-daniels-old-no7.png": "pass",
}


def multipart_post(opener, path: str, fields: dict, file_field: str, file_path: pathlib.Path):
    boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW"
    body = bytearray()
    for key, value in fields.items():
        body.extend(f"--{boundary}\r\n".encode())
        body.extend(f'Content-Disposition: form-data; name="{key}"\r\n\r\n'.encode())
        body.extend(value.encode())
        body.extend(b"\r\n")
    data = file_path.read_bytes()
    body.extend(f"--{boundary}\r\n".encode())
    body.extend(
        f'Content-Disposition: form-data; name="{file_field}"; filename="{file_path.name}"\r\n'.encode()
    )
    body.extend(b"Content-Type: image/png\r\n\r\n")
    body.extend(data)
    body.extend(f"\r\n--{boundary}--\r\n".encode())
    req = urllib.request.Request(
        f"{base}{path}",
        data=bytes(body),
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
        method="POST",
    )
    with opener.open(req, timeout=120) as resp:
        return json.loads(resp.read().decode())


def summarize_fields(payload: dict, prefix: str = "") -> str:
    verification = payload.get("verification", payload)
    fields = verification.get("fields") or []
    failed = [f"{f.get('fieldName')}" for f in fields if not f.get("isMatch")]
    return f"{prefix}failed={failed}" if failed else f"{prefix}all fields matched"


manifest = json.loads((repo / "public/samples/manifest.json").read_text())
std_results: dict[str, str] = {}

for item in manifest:
    img = repo / "public/samples" / item["file"]
    if not img.exists():
        print(item["file"], "MISSING")
        continue

    opener = build_opener()

    t0 = time.time()
    try:
        j = multipart_post(
            opener,
            "/api/v1/verify",
            {"expected": json.dumps(item["expectedLabelFields"])},
            "image",
            img,
        )
        ms = int((time.time() - t0) * 1000)
        status = (j.get("overallStatus") or "error").lower()
        std_results[item["file"]] = status
        msg = (j.get("statusMessage") or "")[:80]
        print(
            f"STD {item['file']}: {status} {ms}ms "
            f"proc={j.get('processingTimeMs')} {summarize_fields(j)} msg={msg}"
        )
    except urllib.error.HTTPError as e:
        ms = int((time.time() - t0) * 1000)
        body = e.read().decode(errors="replace")[:200]
        std_results[item["file"]] = "error"
        print(f"STD {item['file']}: ERROR {ms}ms HTTP {e.code} {body}")
    except Exception as e:
        ms = int((time.time() - t0) * 1000)
        std_results[item["file"]] = "error"
        print(f"STD {item['file']}: ERROR {ms}ms {e}")

    t0 = time.time()
    try:
        j = multipart_post(opener, "/api/v1/verify/autonomous", {}, "image", img)
        ms = int((time.time() - t0) * 1000)
        v = j.get("verification", {})
        ex = j.get("extraction", {})
        msg = (v.get("statusMessage") or "")[:80]
        print(
            f"AUTO {item['file']}: {v.get('overallStatus')} {ms}ms "
            f"proc={v.get('processingTimeMs')} extract={ex.get('processingTimeMs')} "
            f"ttb={j.get('resolvedTtbId')} cola={j.get('colaRegistryHit')} "
            f"{summarize_fields(j)} msg={msg}"
        )
    except Exception as e:
        ms = int((time.time() - t0) * 1000)
        print(f"AUTO {item['file']}: ERROR {ms}ms {e}")

matched = sum(
    1
    for file_name, expected in EXPECTED_STD.items()
    if std_results.get(file_name) == expected
)
total = len(EXPECTED_STD)
print(f"\nReviewer STD outcomes (single): {matched}/{total} match walkthrough")

ui_results: dict[str, str] = {}
ui_matched = 0
try:
    opener = build_opener()
    t0 = time.time()
    print("\nUI path (sequential /verify, one session):")
    for item in manifest:
        img = repo / "public/samples" / item["file"]
        label_t0 = time.time()
        j = multipart_post(
            opener,
            "/api/v1/verify",
            {"expected": json.dumps(item["expectedLabelFields"])},
            "image",
            img,
        )
        label_ms = int((time.time() - label_t0) * 1000)
        status = (j.get("overallStatus") or "error").lower()
        ui_results[item["file"]] = status
        print(
            f"UI  {item['file']}: {status} {label_ms}ms "
            f"proc={j.get('processingTimeMs')} {summarize_fields(j)}"
        )
    ui_ms = int((time.time() - t0) * 1000)
    ui_matched = sum(
        1
        for file_name, expected in EXPECTED_STD.items()
        if ui_results.get(file_name) == expected
    )
    print(f"Reviewer STD outcomes (UI sequential): {ui_matched}/{total} in {ui_ms}ms")
except Exception as e:
    print(f"\nUI sequential verify ERROR: {e}")

if strict:
    if matched < total:
        print("STRICT GATE FAILED (single verify)", file=sys.stderr)
        sys.exit(1)
    if ui_matched < total:
        print("STRICT GATE FAILED (UI sequential path)", file=sys.stderr)
        sys.exit(1)
    print("STRICT GATE PASSED (single + UI sequential)")
