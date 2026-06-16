"""Probe production batch verify (same path as UI Verify labels)."""
import json
import pathlib
import sys
import time
import urllib.request

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _probe_common import DEFAULT_BASE as base, build_opener

repo = pathlib.Path(__file__).resolve().parents[1]
manifest = json.loads((repo / "public/samples/manifest.json").read_text())

opener = build_opener(base)

boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW"
body = bytearray()
for item in manifest:
    path = repo / "public/samples" / item["file"]
    data = path.read_bytes()
    body.extend(f"--{boundary}\r\n".encode())
    body.extend(
        f'Content-Disposition: form-data; name="images"; filename="{path.name}"\r\n'.encode()
    )
    body.extend(b"Content-Type: image/png\r\n\r\n")
    body.extend(data)
    body.extend(b"\r\n")

body.extend(f"--{boundary}\r\n".encode())
body.extend(b'Content-Disposition: form-data; name="expectedList"\r\n\r\n')
body.extend(json.dumps([i["expectedLabelFields"] for i in manifest]).encode())
body.extend(b"\r\n")

body.extend(f"--{boundary}\r\n".encode())
body.extend(b'Content-Disposition: form-data; name="useClientOcr"\r\n\r\nfalse\r\n')

body.extend(f"--{boundary}\r\n".encode())
body.extend(b'Content-Disposition: form-data; name="ocrTextList"\r\n\r\n')
body.extend(json.dumps([""] * len(manifest)).encode())
body.extend(f"\r\n--{boundary}--\r\n".encode())

t0 = time.time()
req = urllib.request.Request(
    f"{base}/api/v1/verify/batch",
    data=bytes(body),
    headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
    method="POST",
)
with opener.open(req, timeout=300) as resp:
    batch = json.loads(resp.read().decode())
ms = int((time.time() - t0) * 1000)
print(f"BATCH total {ms}ms")

EXPECTED = {
    "01-mismatch-act-of-treason.png": "fail",
    "02-mismatch-juniper-tree-gin.png": "fail",
    "03-odp-ambhar-plata.png": "pass",
    "04-odp-la-venenosa-raicilla.png": "pass",
    "05-odp-jack-daniels-old-no7.png": "pass",
}

matched = 0
for item in batch["items"]:
    result = item.get("result") or {}
    status = (result.get("overallStatus") or item.get("error") or "error").lower()
    proc = result.get("processingTimeMs")
    fields = result.get("fields") or []
    failed = [f["fieldName"] for f in fields if not f.get("isMatch")]
    exp = EXPECTED.get(item["fileName"], "?")
    ok = status == exp
    matched += int(ok)
    suffix = " OK" if ok else f" MISMATCH (expected {exp})"
    print(
        f"  {item['fileName']}: {status} proc={proc} failed={failed}{suffix}"
    )

print(f"\nBatch walkthrough: {matched}/{len(EXPECTED)}")
