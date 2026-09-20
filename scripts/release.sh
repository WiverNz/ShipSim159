#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: bash scripts/release.sh [patch|minor|major|X.Y.Z] [options]
Create an annotated release tag on the current commit. Default bump: patch.
  -n, --dry-run       Print the plan without changing anything (allows a dirty tree).
  -y, --yes           Skip confirmation.
      --push          Push the current branch to origin, then the new tag.
  -m, --message TEXT  Tag message (default: Release vX.Y.Z).
  -h, --help          Show this help.
Versions come from the highest local vX.Y.Z tag, or 0.0.0 if none exist.
Fetch origin's tags before releasing. This script never edits files or commits.
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

current=0.0.0
while IFS= read -r tag; do
  if valid_version "${tag#v}"; then
    current="${tag#v}"
    break
  fi
done < <(git for-each-ref --sort=-version:refname --format='%(refname:strip=2)' 'refs/tags/v*')

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
printf 'Current: %s\nRelease: v%s\nCommit: %s\nMessage: %s\nPush to origin: %s\n' \
  "$current" "$new" "$(git rev-parse HEAD)" "$message" "$do_push"
if (( dry_run )); then
  echo "Dry run: no files, refs or remotes changed."
  exit 0
fi
if (( ! assume_yes )); then
  read -r -p "Create this release tag? [y/N] " answer
  [[ "$answer" =~ ^[Yy]$ ]] || die "aborted"
fi

git tag -a "v$new" -m "$message"
if (( do_push )); then
  branch="$(git symbolic-ref --short HEAD)"
  git push origin "$branch"
  git push origin "refs/tags/v$new:refs/tags/v$new"
else
  printf 'Push when ready: git push origin refs/tags/v%s:refs/tags/v%s\n' "$new" "$new"
fi
