"""
Cuts the actual execution plans out of a sqlcmd run of measure.sql with SET STATISTICS XML ON, and writes each one
to a .sqlplan file that SSMS opens as a diagram. The file names follow the numbers in measure.sql.

    python docs/performance/export_plans.py <sqlcmd output> <folder>

Used by .github/workflows/performance.yml. Standard library only.
"""
import re
import sys
from pathlib import Path

NAMES = {
    "1.1": "list-count",
    "1.2": "list-first-page",
    "1.3": "list-first-page-without-index",
    "2.1": "status-waiting-count",
    "2.2": "status-waiting-first-page",
    "2.3": "status-waiting-count-without-index",
    "3.1": "status-new-or-waiting-count",
    "3.2": "status-new-or-waiting-first-page",
    "4.1": "organization-prefix-count",
    "4.2": "organization-prefix-first-page",
    "4.3": "organization-prefix-count-without-index",
    "5.1": "organization-prefix-90-days-count",
    "5.2": "organization-prefix-90-days-first-page",
    "6.1": "handler-prefix-count",
    "6.2": "handler-prefix-first-page-by-priority",
    "6.3": "handler-prefix-count-without-index",
    "7.1": "text-search-count",
    "7.2": "text-search-first-page",
    "7.3": "text-search-no-match-count",
    "7.4": "text-search-no-match-first-page",
    "8.1": "list-page-2000",
    "9.1": "summary-by-status-and-priority",
    "9.2": "summary-top-handlers",
    "9.3": "summary-by-status-and-priority-without-index",
    "9.4": "summary-top-handlers-without-index",
    "10.1": "summary-priority-high-by-status-and-priority",
    "10.2": "summary-priority-high-top-handlers",
    "11.1": "summary-text-search-by-status-and-priority",
    "11.2": "summary-text-search-top-handlers",
    "12.1": "request-details",
    "12.2": "request-history",
    "13.1": "status-change",
    "14.1": "alternative-text-search-binary-collation",
    "14.2": "alternative-text-search-binary-collation-upper",
    "14.3": "alternative-page-2000-keyset",
    "14.4": "alternative-page-2000-offset-on-index",
}


def main() -> None:
    source, target = Path(sys.argv[1]), Path(sys.argv[2])
    target.mkdir(parents=True, exist_ok=True)

    # Every measured query is preceded by PRINT N'=== <number> <description>'.
    parts = re.split(r"^=== (\S+) .*$", source.read_text(encoding="utf-8"), flags=re.M)
    for i in range(1, len(parts) - 1, 2):
        number, output = parts[i], parts[i + 1]
        plans = re.findall(r"<ShowPlanXML.*?</ShowPlanXML>", output, flags=re.S)
        # Statements that only prepare a measurement are not measurements.
        plans = [plan for plan in plans if "SELECT @lastCreatedAt" not in plan and "SELECT @currentVersion" not in plan]
        name = NAMES.get(number)
        if name is None or not plans:
            continue

        major, minor = number.split(".")
        for index, plan in enumerate(plans):
            suffix = f"-{chr(ord('a') + index)}" if len(plans) > 1 else ""
            (target / f"{int(major):02d}.{minor}-{name}{suffix}.sqlplan").write_text(plan, encoding="utf-8")


if __name__ == "__main__":
    main()
