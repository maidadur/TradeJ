"""
MT5 Bridge — local HTTP server that connects to MetaTrader 5 and returns deal history.

Requirements:
    pip install MetaTrader5 flask

Usage:
    1. Open MetaTrader 5 terminal (any account — the bridge will log in with the credentials you pass).
    2. Run this script once:   python scripts/mt5_bridge.py
    3. Go to TradeJ → Import → MT5 Sync, select your account, enter investor password, click Sync.

Listens on http://0.0.0.0:8765 — accessible from any network interface (TradeJ backend).
"""

from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse, parse_qs
from datetime import datetime, timezone
import json
import sys
import threading
import time
import ctypes

try:
    import pystray
    from PIL import Image, ImageDraw
except ImportError:
    pystray = None

try:
    import MetaTrader5 as mt5
except ImportError:
    print("ERROR: MetaTrader5 package not found.")
    print("Install it with:  pip install MetaTrader5")
    sys.exit(1)

HOST = "0.0.0.0"
PORT = 8765

# Mutex so concurrent requests don't interfere with MT5 login state.
_mt5_lock = threading.Lock()


def _parse_dt(s: str) -> datetime:
    """Parse ISO 8601 datetime string (may or may not include Z/offset)."""
    s = s.replace("Z", "+00:00")
    try:
        return datetime.fromisoformat(s)
    except ValueError:
        # Fall back: treat as UTC naive
        return datetime.strptime(s[:19], "%Y-%m-%dT%H:%M:%S").replace(tzinfo=timezone.utc)


def _deal_entry_str(entry_code: int) -> str:
    if entry_code == mt5.DEAL_ENTRY_IN:
        return "DEAL_ENTRY_IN"
    if entry_code == mt5.DEAL_ENTRY_OUT:
        return "DEAL_ENTRY_OUT"
    if entry_code == mt5.DEAL_ENTRY_INOUT:
        return "DEAL_ENTRY_INOUT"
    return "DEAL_ENTRY_OTHER"


def _deal_type_str(type_code: int) -> str:
    if type_code == mt5.DEAL_TYPE_BUY:
        return "DEAL_TYPE_BUY"
    if type_code == mt5.DEAL_TYPE_SELL:
        return "DEAL_TYPE_SELL"
    return "DEAL_TYPE_OTHER"


def fetch_deals(login: int, password: str, server: str,
                date_from: datetime, date_to: datetime) -> list[dict]:
    """Connect to MT5 with the given credentials and return deals in the requested range."""
    with _mt5_lock:
        if not mt5.initialize():
            raise RuntimeError(f"mt5.initialize() failed: {mt5.last_error()}")

        try:
            # If the terminal is already logged into the correct account (e.g. with
            # the real/write password), reuse that session so we don't downgrade it
            # to investor-only access and break active trading.
            info = mt5.account_info()
            if info is None or info.login != login:
                if not mt5.login(login, password=password, server=server):
                    raise PermissionError(f"mt5.login() failed: {mt5.last_error()}")

            # Ensure UTC-aware datetimes for the API call
            from_utc = date_from.astimezone(timezone.utc)
            to_utc   = date_to.astimezone(timezone.utc)

            # MT5 may need a moment to load history after initialize/login.
            # Retry once with a short delay if the first call returns nothing.
            deals = mt5.history_deals_get(from_utc, to_utc)
            if not deals:
                time.sleep(1.5)
                deals = mt5.history_deals_get(from_utc, to_utc)
            if deals is None:
                return []

            result = []
            for d in deals:
                result.append({
                    "id":         str(d.ticket),
                    "positionId": str(d.position_id),
                    "type":       _deal_type_str(d.type),
                    "entry":      _deal_entry_str(d.entry),
                    "symbol":     d.symbol,
                    "time":       datetime.utcfromtimestamp(d.time).strftime("%Y-%m-%dT%H:%M:%SZ"),
                    "volume":     float(d.volume),
                    "price":      float(d.price),
                    "profit":     float(d.profit),
                    "commission": float(d.commission),
                    "swap":       float(d.swap),
                    "comment":    d.comment,
                })
            return result
        finally:
            mt5.shutdown()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):  # quieter logging
        print(f"  {self.address_string()} {fmt % args}")

    def send_json(self, code: int, data):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path != "/deals":
            self.send_json(404, {"error": "Not found. Use GET /deals"})
            return

        qs = parse_qs(parsed.query)

        def require(name: str) -> str:
            vals = qs.get(name)
            if not vals or not vals[0]:
                raise ValueError(f"Missing required query param: {name}")
            return vals[0]

        try:
            login    = int(require("login"))
            password = require("password")
            server   = require("server")
            date_from = _parse_dt(require("from"))
            date_to   = _parse_dt(require("to"))
        except (ValueError, KeyError) as e:
            self.send_json(400, {"error": str(e)})
            return

        try:
            deals = fetch_deals(login, password, server, date_from, date_to)
            self.send_json(200, deals)
        except PermissionError as e:
            self.send_json(401, {"error": str(e)})
        except Exception as e:
            print(f"ERROR: {e}")
            self.send_json(500, {"error": str(e)})


def _make_tray_icon():
    """Create a simple green circle icon for the tray."""
    img = Image.new("RGB", (64, 64), color=(30, 30, 30))
    draw = ImageDraw.Draw(img)
    draw.ellipse((8, 8, 56, 56), fill=(0, 180, 80))
    return img


if __name__ == "__main__":
    # Hide the console window on Windows
    ctypes.windll.user32.ShowWindow(ctypes.windll.kernel32.GetConsoleWindow(), 0)

    server = HTTPServer((HOST, PORT), Handler)
    server_thread = threading.Thread(target=server.serve_forever, daemon=True)
    server_thread.start()

    if pystray is None:
        # No tray support — just block until Ctrl+C
        try:
            server_thread.join()
        except KeyboardInterrupt:
            server.shutdown()
    else:
        def on_stop(icon, item):
            icon.stop()
            server.shutdown()

        icon = pystray.Icon(
            "MT5 Bridge",
            _make_tray_icon(),
            "MT5 Bridge :8765",
            menu=pystray.Menu(
                pystray.MenuItem("MT5 Bridge — port 8765", None, enabled=False),
                pystray.Menu.SEPARATOR,
                pystray.MenuItem("Stop", on_stop),
            ),
        )
        icon.run()  # blocks until Stop is chosen
