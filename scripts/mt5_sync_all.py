"""
MT5 Sync All Accounts — automated scheduled sync.

Fetches all MT5 accounts from the TradeJ backend, connects to MT5 for each,
and pushes new deals. Runs via the same mt5_bridge.py listener OR directly
if you prefer (configure MODE below).

Usage:
    1. Make sure mt5_bridge.py is running:  python scripts/mt5_bridge.py
    2. Run this script manually or via Windows Task Scheduler:
           python scripts/mt5_sync_all.py
    3. Optional: pass --days N to sync the last N days (default: 30)
           python scripts/mt5_sync_all.py --days 7

Requirements:
    pip install requests
"""

import argparse
import sys
from datetime import datetime, timedelta, timezone

try:
    import requests
    from requests.packages.urllib3.exceptions import InsecureRequestWarning
except ImportError:
    print("ERROR: 'requests' package not found. Install with: pip install requests")
    sys.exit(1)

# ── Configuration ──────────────────────────────────────────────────────────────
BACKEND    = "https://localhost:7157"  # change to your Docker server address for production
VERIFY_SSL = False  # set True in production with a real certificate
# ───────────────────────────────────────────────────────────────────────────────


def main():
    parser = argparse.ArgumentParser(description="Sync all MT5 accounts to TradeJ")
    parser.add_argument("--days", type=int, default=30,
                        help="How many days back to sync (default: 30)")
    args = parser.parse_args()

    date_from = (datetime.now(timezone.utc) - timedelta(days=args.days)).isoformat()
    date_to   = datetime.now(timezone.utc).isoformat()

    print(f"Syncing last {args.days} days ({date_from[:10]} → {date_to[:10]})")
    print(f"Backend: {BACKEND}\n")

    if not VERIFY_SSL:
        requests.packages.urllib3.disable_warnings(InsecureRequestWarning)

    # Fetch all accounts
    try:
        resp = requests.get(f"{BACKEND}/api/accounts", timeout=10, verify=VERIFY_SSL)
        resp.raise_for_status()
    except requests.RequestException as e:
        print(f"ERROR: Cannot reach backend at {BACKEND}: {e}")
        sys.exit(1)

    accounts = resp.json()
    mt5_accounts = [
        a for a in accounts
        if a.get("broker") == "MT5"
        and a.get("mt5Server")
        and a.get("hasMT5InvestorPassword")
    ]

    if not mt5_accounts:
        print("No MT5 accounts with server + investor password configured.")
        print("Go to Accounts in TradeJ and fill in the MT5 Sync section.")
        return

    print(f"Found {len(mt5_accounts)} MT5 account(s) ready to sync:\n")

    total_imported = 0
    total_skipped  = 0
    total_errors   = 0

    for acc in mt5_accounts:
        name = acc["name"]
        acct_id = acc["id"]
        print(f"  → {name} (#{acct_id}, server: {acc['mt5Server']}) ... ", end="", flush=True)

        try:
            r = requests.post(
                f"{BACKEND}/api/import/mt5-sync?accountId={acct_id}",
                json={"dateFrom": date_from, "dateTo": date_to},
                timeout=130,
                verify=VERIFY_SSL,
            )
            if not r.ok:
                msg = r.json().get("message", r.text[:200])
                print(f"FAILED — {msg}")
                total_errors += 1
                continue

            result = r.json()
            imported = result.get("imported", 0)
            skipped  = result.get("skipped",  0)
            errors   = result.get("errors",   0)
            print(f"{imported} imported, {skipped} skipped, {errors} errors")

            for msg in result.get("errorMessages", []):
                print(f"      ! {msg}")

            total_imported += imported
            total_skipped  += skipped
            total_errors   += errors

        except requests.RequestException as e:
            print(f"FAILED — {e}")
            total_errors += 1

    print(f"\nDone. Total: {total_imported} imported, {total_skipped} skipped, {total_errors} errors")


if __name__ == "__main__":
    main()
