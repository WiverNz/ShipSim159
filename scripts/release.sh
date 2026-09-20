#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: bash scripts/release.sh [patch|minor|major|X.Y.Z] [options]
Update Unity's bundleVersion, commit Release vX.Y.Z, then tag it. Default bump: patch.
  -n, --dry-run       Print the plan without changing anything (allows a dirty tree).
  -y, --yes           Skip confirmation.
      --push          Push the current branch to origin, then the new tag.
  -m, --message TEXT  Tag message (default: Release vX.Y.Z).
  -h, --help          Show this help.
Versions come from PlayerSettings.bundleVersion in ProjectSettings/ProjectSettings.asset.
Fetch origin's tags before releasing. Only the version settings file is committed.
EOF
}
die() { echo "error: $*" >&2; exit 1; }
valid_version() { [[ "$1" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; }

spec="" message="" dry_run=0 assume_yes=0 do_push=0
while (( $# )); do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    -n|--dry-run) dry_run=1; shift ;;
    -y|--yes) assume_yes=1; shift ;;
    --push) do_push=1; shift ;;
    -m|--message)
      (( $# >= 2 )) || die "$1 requires text"
      message="$2"; shift 2 ;;
    -*) die "unknown option: $1" ;;
    *) [[ -z "$spec" ]] || die "multiple versions supplied"; spec="$1"; shift ;;
  esac
done

cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
git rev-parse --verify HEAD >/dev/null 2>&1 || die "no committed HEAD"
git symbolic-ref --quiet HEAD >/dev/null || die "detached HEAD"
[[ "$(git rev-parse --is-shallow-repository)" == false ]] || die "fetch full history and tags first"
if [[ -n "$(git status --porcelain)" ]]; then
  (( dry_run )) || die "working tree is dirty; review and commit changes before releasing"
  echo "warning: working tree is dirty; a real release would be refused" >&2
fi

settings=ProjectSettings/ProjectSettings.asset
read_version() {
  awk '/^[ \t]*bundleVersion:/ { count++; value=$0; sub(/^[ \t]*bundleVersion:[ \t]*/, "", value); sub(/[ \t\r]+$/, "", value) }
       END { if (count != 1) exit 1; print value }' "$1"
}
current="$(read_version "$settings")" || die "expected exactly one bundleVersion in $settings"
valid_version "$current" || die "bundleVersion is not stable SemVer: $current"

IFS=. read -r major minor patch <<< "$current"
case "${spec:-patch}" in
  major) new="$((major + 1)).0.0" ;;
  minor) new="$major.$((minor + 1)).0" ;;
  patch) new="$major.$minor.$((patch + 1))" ;;
  *) new="$spec" ;;
esac
valid_version "$new" || die "expected a stable SemVer X.Y.Z without leading zeros: $new"
[[ "$new" != "$current" && "$(printf '%s\n' "$current" "$new" | sort -V | tail -1)" == "$new" ]] \
  || die "version must be greater than $current"
git show-ref --verify --quiet "refs/tags/v$new" && die "tag v$new already exists"
if (( do_push )); then
  git remote get-url origin >/dev/null || die "origin remote is required for --push"
fi
message="${message:-Release v$new}"
printf 'Current: %s\nRelease: v%s\nFile: %s\nCommit: Release v%s\nMessage: %s\nPush to origin: %s\n' \
  "$current" "$new" "$settings" "$new" "$message" "$do_push"
if (( dry_run )); then
  echo "Dry run: no files, refs or remotes changed."
  exit 0
fi
if (( ! assume_yes )); then
  read -r -p "Update bundleVersion, commit and tag this release? [y/N] " answer
  [[ "$answer" =~ ^[Yy]$ ]] || die "aborted"
fi

temporary="$(mktemp "${settings}.XXXXXX")"
trap 'rm -f -- "$temporary"' EXIT
awk -v version="$new" '/^[ \t]*bundleVersion:/ { sub(/bundleVersion:[^\r]*/, "bundleVersion: " version) } { print }' \
  "$settings" > "$temporary"
mv -- "$temporary" "$settings"
git add -- "$settings"
git commit --only -m "Release v$new" -- "$settings"
git show "HEAD:$settings" > "$temporary"
[[ "$(read_version "$temporary")" == "$new" ]] || die "committed bundleVersion does not match v$new"
git tag -a "v$new" -m "$message"
if (( do_push )); then
  branch="$(git symbolic-ref --short HEAD)"
  git push origin "$branch"
  git push origin "refs/tags/v$new:refs/tags/v$new"
else
  printf 'Push when ready:\n  git push origin %s\n  git push origin refs/tags/v%s:refs/tags/v%s\n' \
    "$(git symbolic-ref --short HEAD)" "$new" "$new"
fi
