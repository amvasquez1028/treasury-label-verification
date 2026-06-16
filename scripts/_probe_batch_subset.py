"""Quick batch probe with subset of samples."""
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

subset = manifest[:3]  # mismatch + ambhar
boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW"
body = bytearray()
for item in subset:
    path = repo / "public/samples" / item["file"]
    body.extend(f"--{boundary}\r\n".encode())
    body.extend(
        f'Content-Disposition: form-data; name="images"; filename="{path.name}"\r\n'.encode()
    )
    body.extend(b"Content-Type: image/png\r\n\r\n")
    body.extend(path.read_bytes())
    body.extend(b"\r\n")
body.extend(f"--{boundary}\r\n".encode())
body.extend(b'Content-Disposition: form-data; name="expectedList"\r\n\r\n')
body.extend(json.dumps([i["expectedLabelFields"] for i in subset]).encode())
body.extend(f"\r\n--{boundary}\r\n".encode())
body.extend(b'Content-Disposition: form-data; name="useClientOcr"\r\n\r\nfalse\r\n')
body.extend(f"--{boundary}--\r\n".encode())

t0 = time.time()
req = urllib.request.Request(
    f"{base}/api/v1/verify/batch",
    data=bytes(body),
    headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
    method="POST",
)
with opener.open(req, timeout=120) as resp:
    batch = json.loads(resp.read().decode())
print(f"2-label batch OK in {int((time.time()-t0)*1000)}ms")
