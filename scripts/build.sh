#!/usr/bin/env bash
# Builds, tests and packs the plugin the way the release workflow does, and leaves an installable
# .macroDeckPlugin in ./artifacts.
#
#   scripts/build.sh                  this machine's platform (osx-arm64 on Apple silicon)
#   scripts/build.sh --rid win-x64    one declared platform
#   scripts/build.sh --all            every platform the manifest declares, in one package
#   --skip-tests, --skip-conformance  leave those steps out for a quicker pack
set -euo pipefail

usage() {
	sed -n '2,9p' "$0" | sed 's/^# \{0,1\}//'
}

rid=""
all=false
run_tests=true
run_conformance=true
while [[ $# -gt 0 ]]; do
	case "$1" in
		--all) all=true ;;
		--rid)
			[[ $# -ge 2 ]] || { usage >&2; exit 2; }
			rid="$2"
			shift
			;;
		--skip-tests) run_tests=false ;;
		--skip-conformance) run_conformance=false ;;
		-h | --help)
			usage
			exit 0
			;;
		*)
			printf 'unknown option: %s\n\n' "$1" >&2
			usage >&2
			exit 2
			;;
	esac
	shift
done

source "$(dirname "${BASH_SOURCE[0]}")/_env.sh"

if [[ "$all" == false && -z "$rid" ]]; then
	rid="$(host_rid)"
	rid="${rid:-osx-arm64}"
fi

mkdir -p "$ARTIFACTS_DIR"
rm -f "$ARTIFACTS_DIR"/*.macroDeckPlugin

log "Building the solution (Release, warnings as errors)"
dotnet build "$SOLUTION" -c Release -warnaserror -nologo

if [[ "$run_tests" == true ]]; then
	log "Running the unit tests"
	dotnet test "$SOLUTION" -c Release --no-build -nologo
fi

if [[ "$all" == true ]]; then
	log "Packing every declared platform"
	mdp build --source "$PROJECT_DIR" --output "$ARTIFACTS_DIR" --force --no-color
else
	log "Packing $rid"
	mdp build --source "$PROJECT_DIR" --rid "$rid" --output "$ARTIFACTS_DIR" --force --no-color
fi

artifacts=("$ARTIFACTS_DIR"/*.macroDeckPlugin)
[[ -f "${artifacts[0]}" && ${#artifacts[@]} -eq 1 ]] || die "expected exactly one package in $ARTIFACTS_DIR"
artifact="${artifacts[0]}"

log "Validating $(basename "$artifact") for publication"
mdp validate --artifact "$artifact" --level publication --no-color

log "Inspecting the package"
# inspect prints its warnings on stderr, so both streams are captured.
inspection="$(mdp inspect --artifact "$artifact" --no-color 2>&1)"
printf '%s\n' "$inspection"
# The host skips a bundled icon pack that the signed file list does not cover, so the live icons
# would silently resolve to nothing.
if grep -q 'bundled-icon-pack-not-in-files' <<<"$inspection"; then
	die "the bundled icon pack is not listed in the package's files"
fi

if [[ "$run_conformance" == true ]]; then
	this_rid="$(host_rid)"
	if [[ -n "$this_rid" ]] && { [[ "$all" == true ]] || [[ "$rid" == "$this_rid" ]]; }; then
		# Against the package, the way the release workflow's stub host runs it. The per-check timeout is
		# above the default because one Recommended logging check needs longer than 30 s to conclude.
		report="$ARTIFACTS_DIR/conformance-$this_rid.md"
		log "Running the conformance suite against the package on a stub host ($this_rid)"
		mdp test --artifact "$artifact" --report markdown --output "$report" --timeout 60 --no-color \
			|| die "the conformance suite failed; see $report"
		grep -E '^(Passed|Conformant)' "$report" >&2 || true
	else
		log "Skipping conformance: $rid cannot run on this machine; the release workflow runs it per platform"
	fi
fi

log "Done: $artifact"
