"""
MT5 Bridge — local HTTP server that connects to MetaTrader 5 and returns deal history.

Requirements:
    pip install MetaTrader5 flask

Usage:
    1. Open MetaTrader 5 terminal (any account — the bridge will log in with the credentials you pass).
    2. Run this script once:   python scripts/mt5_bridge.py
    3. Go to TradeJ → Import → MT5 Sync, select your account, enter investor password, click Sync.

Listens on http://127.0.0.1:8765 — only accessible from localhost (TradeJ backend).
"""

from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse, parse_qs
from datetime import datetime, timezone
import json
import sys
import threading

try:
    import MetaTrader5 as mt5
except ImportError:
    print("ERROR: MetaTrader5 package not found.")
    print("Install it with:  pip install MetaTrader5")
    sys.exit(1)

HOST = "127.0.0.1"
PORT = 8765

# Mutex so concurrent requests don't interfere with MT5 login state.
_mt5_lock = threading.Lock()

_initialized = False


def _ensure_session(login: int, password: str, server: str) -> None:
    """Initialize the MT5 connection once and reuse it for the life of the process.

    Calling mt5.initialize()/mt5.shutdown() on every request pops the MT5 terminal
    window to the foreground each time — so we connect once and only re-login when
    the caller asks for a different account than the one we're currently on.
    """
    global _initialized
    if not _initialized:
        if not mt5.initialize():
            raise RuntimeError(f"mt5.initialize() failed: {mt5.last_error()}")
        _initialized = True

    info = mt5.account_info()
    if info is None or info.login != login:
        if not mt5.login(login, password=password, server=server):
            raise PermissionError(f"mt5.login() failed: {mt5.last_error()}")


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
        _ensure_session(login, password, server)

        # Ensure UTC-aware datetimes for the API call
        from_utc = date_from.astimezone(timezone.utc)
        to_utc   = date_to.astimezone(timezone.utc)

        deals = mt5.history_deals_get(from_utc, to_utc)
        if deals is None:
            return []

        # Deals themselves carry no sl/tp — only the order that generated them does.
        # Build a ticket -> (sl, tp) lookup from orders in the same window.
        orders = mt5.history_orders_get(from_utc, to_utc) or []
        sl_tp_by_order = {o.ticket: (o.sl, o.tp) for o in orders}

        result = []
        for d in deals:
            sl, tp = sl_tp_by_order.get(d.order, (0.0, 0.0))
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
                "stopLoss":   float(sl) if sl else None,
                "takeProfit": float(tp) if tp else None,
            })
        return result


_TIMEFRAMES = {
    "M1":  "TIMEFRAME_M1",
    "M5":  "TIMEFRAME_M5",
    "M15": "TIMEFRAME_M15",
    "M30": "TIMEFRAME_M30",
    "H1":  "TIMEFRAME_H1",
    "H4":  "TIMEFRAME_H4",
    "D1":  "TIMEFRAME_D1",
}


def fetch_bars(login: int, password: str, server: str, symbol: str, timeframe: str,
               date_from: datetime, date_to: datetime) -> list[dict]:
    """Connect to MT5 and return OHLC bars for a symbol/timeframe/range."""
    tf_name = _TIMEFRAMES.get(timeframe.upper())
    if tf_name is None:
        raise ValueError(f"Unknown timeframe '{timeframe}'. Use one of: {', '.join(_TIMEFRAMES)}")
    tf_const = getattr(mt5, tf_name)

    with _mt5_lock:
        _ensure_session(login, password, server)

        from_utc = date_from.astimezone(timezone.utc)
        to_utc   = date_to.astimezone(timezone.utc)

        rates = mt5.copy_rates_range(symbol, tf_const, from_utc, to_utc)
        if rates is None:
            return []

        return [
            {
                "time":   int(r["time"]),
                "open":   float(r["open"]),
                "high":   float(r["high"]),
                "low":    float(r["low"]),
                "close":  float(r["close"]),
                "volume": float(r["tick_volume"]),
            }
            for r in rates
        ]


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
        if parsed.path not in ("/deals", "/bars"):
            self.send_json(404, {"error": "Not found. Use GET /deals or GET /bars"})
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
            if parsed.path == "/bars":
                symbol    = require("symbol")
                timeframe = require("timeframe")
        except (ValueError, KeyError) as e:
            self.send_json(400, {"error": str(e)})
            return

        try:
            if parsed.path == "/deals":
                data = fetch_deals(login, password, server, date_from, date_to)
            else:
                data = fetch_bars(login, password, server, symbol, timeframe, date_from, date_to)
            self.send_json(200, data)
        except PermissionError as e:
            self.send_json(401, {"error": str(e)})
        except ValueError as e:
            self.send_json(400, {"error": str(e)})
        except Exception as e:
            print(f"ERROR: {e}")
            self.send_json(500, {"error": str(e)})


if __name__ == "__main__":
    print(f"MT5 Bridge starting on http://{HOST}:{PORT}")
    print("Make sure MetaTrader 5 is open on this computer.")
    print("Press Ctrl+C to stop.\n")
    server = HTTPServer((HOST, PORT), Handler)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")
    finally:
        if _initialized:
            mt5.shutdown()
