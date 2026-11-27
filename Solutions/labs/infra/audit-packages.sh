#!/usr/bin/env bash
# From labs: bash infra/audit-packages.sh [optional saved NuGet audit report]
set -euo pipefail
report=$(mktemp)
trap 'rm -f "$report"' EXIT
if [[ $# -gt 0 ]]; then
  cp "$1" "$report"
else
  dotnet list Exercises.sln package --vulnerable --include-transitive --format json > "$report"
fi
jq -e '(.projects | type == "array") and
  ([.projects[] | .frameworks[]? | (.topLevelPackages[]?, .transitivePackages[]?) |
    .vulnerabilities[]?] | length == 0)' "$report" >/dev/null || {
  cat "$report"; exit 1;
}
