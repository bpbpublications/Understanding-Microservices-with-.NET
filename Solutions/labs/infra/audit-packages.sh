#!/usr/bin/env bash
# From labs: bash infra/audit-packages.sh [optional saved NuGet audit report]
set -euo pipefail
report=$(mktemp)
trap 'rm -f "$report"' EXIT
if [[ $# -gt 0 ]]; then
  cp "$1" "$report"
else
  # Treat an unavailable advisory feed as an inconclusive build.
  dotnet restore Exercises.sln -p:WarningsAsErrors=NU1900 >&2
  dotnet list Exercises.sln package --no-restore --vulnerable --include-transitive --format json > "$report"
fi
jq -e '(.projects | type == "array" and length > 0) and
  ([.problems[]?] | length == 0) and
  ([.projects[] | .frameworks[]? | (.topLevelPackages[]?, .transitivePackages[]?) |
    .vulnerabilities[]?] | length == 0)' "$report" >/dev/null || {
  cat "$report"; exit 1;
}
