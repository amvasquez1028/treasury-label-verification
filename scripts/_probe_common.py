import json
import os
import urllib.request
from http.cookiejar import CookieJar

DEFAULT_BASE = "https://label-verify-trackc.azurewebsites.net"
DEMO_AGENT_EMAIL = "demo.agent@label-verify.demo"


def require_demo_password() -> str:
    password = os.environ.get("DEMO_AGENT_PASSWORD", "").strip()
    if not password:
        raise SystemExit(
            "Set DEMO_AGENT_PASSWORD before running production probes "
            "(never commit production passwords to git)."
        )
    return password


def build_opener(base: str = DEFAULT_BASE) -> urllib.request.OpenerDirector:
    password = require_demo_password()
    jar = CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    login = json.dumps({"email": DEMO_AGENT_EMAIL, "password": password}).encode()
    req = urllib.request.Request(
        f"{base}/api/v1/auth/login",
        data=login,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    opener.open(req, timeout=30).read()
    return opener
