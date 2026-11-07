"""Creates/downloads the App Store profile of Patente Italiana, tied to the distribution certificate Codemagic put in the keychain (copied from the dawlat-tilawa setup)."""
import glob, hashlib, json, os, sys, time
import jwt, requests

APP_ID = os.environ.get("APP_ID", "6831822746")
LOCALE = os.environ.get("SHOT_LOCALE", "ar-SA")
DISPLAY = os.environ.get("SHOT_DISPLAY", "APP_IPHONE_67")
API = "https://api.appstoreconnect.apple.com/v1"

def load_key():
    """المفتاح ممكن ييجي بأشكال مختلفة (سطور حقيقية، \\n، أو base64)."""
    import base64
    raw = os.environ["APP_STORE_CONNECT_PRIVATE_KEY"].strip().strip('"').strip("'")
    if raw.startswith("@file:"):
        raw = open(os.path.expanduser(raw[6:])).read()
    raw = raw.replace("\\n", "\n").replace("\r", "")
    if "BEGIN" not in raw:
        try:
            dec = base64.b64decode(raw).decode()
            if "BEGIN" in dec:
                raw = dec
        except Exception:
            pass
    if "BEGIN" not in raw:
        body = "".join(raw.split())
        raw = "-----BEGIN PRIVATE KEY-----\n" + "\n".join(body[i:i+64] for i in range(0, len(body), 64)) + "\n-----END PRIVATE KEY-----\n"
    elif "\n" not in raw.strip():
        head, rest = raw.split("-----BEGIN PRIVATE KEY-----", 1)
        body, tail = rest.split("-----END PRIVATE KEY-----", 1)
        body = "".join(body.split())
        raw = "-----BEGIN PRIVATE KEY-----\n" + "\n".join(body[i:i+64] for i in range(0, len(body), 64)) + "\n-----END PRIVATE KEY-----\n"
    print("key: len", len(raw), "lines", raw.count("\n"), "begin", "BEGIN" in raw)
    return raw

KEY = None

def token():
    global KEY
    if KEY is None:
        KEY = load_key()
    key = KEY
    return jwt.encode({"iss": os.environ["APP_STORE_CONNECT_ISSUER_ID"], "iat": int(time.time()),
                       "exp": int(time.time()) + 1100, "aud": "appstoreconnect-v1"}, key, algorithm="ES256",
                      headers={"kid": os.environ["APP_STORE_CONNECT_KEY_IDENTIFIER"]})

def call(method, url, **kw):
    h = {"Authorization": "Bearer " + token(), "Content-Type": "application/json"}
    r = requests.request(method, url if url.startswith("http") else API + url, headers=h, **kw)
    if r.status_code >= 400:
        print(method, url, r.status_code, r.text[:800]); r.raise_for_status()
    return r.json() if r.text else {}


import subprocess, base64 as b64
BUNDLE = os.environ.get("BUNDLE_ID", "com.memo.patentemondo")

# أرقام الشهادات اللي في الـ keychain
pem = subprocess.run(["security", "find-certificate", "-a", "-p"], capture_output=True, text=True).stdout
serials = set()
for blk in pem.split("-----END CERTIFICATE-----"):
    if "BEGIN CERTIFICATE" not in blk: continue
    c = blk[blk.index("-----BEGIN CERTIFICATE-----"):] + "-----END CERTIFICATE-----\n"
    out = subprocess.run(["openssl", "x509", "-noout", "-serial"], input=c, capture_output=True, text=True).stdout
    if "=" in out: serials.add(out.strip().split("=", 1)[1].upper().lstrip("0"))
print("keychain serials:", len(serials))

certs = call("GET", "/certificates?limit=50")["data"]
cert = None
for c in certs:
    a = c["attributes"]
    sn = (a.get("serialNumber") or "").upper().lstrip("0")
    print("cert", c["id"], a.get("certificateType"), a.get("displayName"), sn[:8], "match" if sn in serials else "")
    if sn in serials and "DISTRIBUTION" in (a.get("certificateType") or ""):
        cert = c
if not cert:
    sys.exit("مفيش شهادة توزيع في الـ keychain مطابقة لشهادة في حساب أبل")

bid = call("GET", f"/bundleIds?filter[identifier]={BUNDLE}")["data"]
bid = next(b for b in bid if b["attributes"]["identifier"] == BUNDLE)["id"]
name = f"Patente Italiana AppStore {cert['id'][:6]}"
prof = None
for p in call("GET", f"/bundleIds/{bid}/profiles?limit=50")["data"]:
    a = p["attributes"]
    if a["name"] == name and a["profileState"] == "ACTIVE" and a["profileType"] == "IOS_APP_STORE":
        prof = p
if not prof:
    prof = call("POST", "/profiles", json={"data": {"type": "profiles", "attributes": {"name": name, "profileType": "IOS_APP_STORE"},
        "relationships": {"bundleId": {"data": {"type": "bundleIds", "id": bid}},
                          "certificates": {"data": [{"type": "certificates", "id": cert["id"]}]}}}})["data"]
    print("created profile", prof["id"])
d = os.path.expanduser("~/Library/MobileDevice/Provisioning Profiles")
os.makedirs(d, exist_ok=True)
path = os.path.join(d, prof["attributes"]["uuid"] + ".mobileprovision")
open(path, "wb").write(b64.b64decode(prof["attributes"]["profileContent"]))
print("profile saved", path)
