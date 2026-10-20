"""
Response times of the main API calls, against a running API with the test data (100,000 requests).

    python docs/performance/time_api.py [base-url]        (default: http://localhost:5080)

Every call is made once, and then 20 more times. The table shows the first call and the average and 95th
percentile of the other 20, in milliseconds, as measured by the client (HTTP included). Status changes are
measured on 50 different requests, after 5 warm-up changes.
The scenarios follow the numbering of measure.sql. Standard library only.

It makes real changes: the status of requests 1–55 changes, with history rows. Run it on test data, and run
`dotnet run --project src/EmployerRequests.Api -- seed` afterwards for a fresh start.
"""
import json
import statistics
import sys
import time
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone

BASE_URL = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080"
REPEATS = 20


def quote(text: str) -> str:
    return urllib.parse.quote(text)


def call(method: str, path: str, body: dict | None = None) -> tuple[float, object]:
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(BASE_URL + path, data=data, method=method,
                                     headers={"Content-Type": "application/json", "X-User-Name": "time_api.py"})
    started = time.perf_counter()
    with urllib.request.urlopen(request) as response:
        payload = json.loads(response.read() or b"null")
    return (time.perf_counter() - started) * 1000, payload


def row(name: str, call_text: str, results: object, first_ms: float, timings: list[float]) -> str:
    p95 = statistics.quantiles(timings, n=20)[18]
    return f"| {name} | `{call_text}` | {results} | {first_ms:.0f} | {statistics.mean(timings):.1f} | {p95:.1f} |"


def measure_get(name: str, path: str) -> str:
    first_ms, payload = call("GET", path)
    timings = [call("GET", path)[0] for _ in range(REPEATS)]
    results = payload.get("totalCount", "–") if isinstance(payload, dict) else len(payload)
    return row(name, path, results, first_ms, timings)


def measure_status_changes() -> str:
    """
    Changes the status of requests 1–55: each one is loaded first, for its current version and allowed statuses.
    The first 5 changes warm up the write path; the table shows the first one and statistics of the other 50.
    """
    timings = []
    for request_id in range(1, 56):
        _, request = call("GET", f"/api/requests/{request_id}")
        elapsed, _ = call("PATCH", f"/api/requests/{request_id}/status",
                          {"status": request["allowedNextStatuses"][0], "rowVersion": request["rowVersion"]})
        timings.append(elapsed)
    return row("13. עדכון סטטוס", "PATCH /api/requests/{id}/status", "–", timings[0], timings[5:])


def main() -> None:
    now = datetime.now(timezone.utc)
    created_from = quote((now - timedelta(days=90)).strftime("%Y-%m-%dT%H:%M:%SZ"))
    created_to = quote(now.strftime("%Y-%m-%dT%H:%M:%SZ"))

    scenarios = [
        ("1. ללא סינון, עמוד ראשון", "/api/requests?page=1&pageSize=20"),
        ("2. סטטוס: ממתינה", "/api/requests?status=Waiting"),
        ("3. סטטוס: חדשה או ממתינה", "/api/requests?status=New&status=Waiting"),
        ("4. ארגון מתחיל ב\"אלון\"", f"/api/requests?organizationName={quote('אלון')}"),
        ("5. ארגון \"אלון\" + 90 הימים האחרונים", f"/api/requests?organizationName={quote('אלון')}&createdFrom={created_from}&createdTo={created_to}"),
        ("6. מטפל/ת מתחיל ב\"שרה\", מיון לפי עדיפות", f"/api/requests?assignedTo={quote('שרה')}&sortBy=priority&sortDirection=desc"),
        ("7. חיפוש טקסט \"אישור\"", f"/api/requests?search={quote('אישור')}"),
        ("8. עמוד 2,000", "/api/requests?page=2000&pageSize=20"),
        ("9. סיכום ללא סינון (מה-Cache אחרי הקריאה הראשונה)", "/api/requests/summary"),
        ("10. סיכום, עדיפות גבוהה", "/api/requests/summary?priority=High"),
        ("11. סיכום, חיפוש טקסט \"אישור\"", f"/api/requests/summary?search={quote('אישור')}"),
        ("12. פרטי פנייה", "/api/requests/1"),
        ("12. היסטוריית פנייה", "/api/requests/1/history"),
    ]

    status_changes = measure_status_changes()  # first, so that request 1 has a history to show
    rows = [measure_get(name, path) for name, path in scenarios] + [status_changes]

    print(f"Measured at {now:%Y-%m-%d %H:%M} UTC against {BASE_URL}\n")
    print("| תרחיש | קריאה | תוצאות | קריאה ראשונה (ms) | ממוצע (ms) | p95 (ms) |")
    print("|---|---|---|---|---|---|")
    print("\n".join(rows))


if __name__ == "__main__":
    main()
