#!/usr/bin/env bash
# Runs the plugin from source for development.
#
#   scripts/dev.sh                    against the running Macro Deck, rebuilt on every save. Needs
#                                     Developer Mode on in Macro Deck; approve the pairing prompt once.
#   scripts/dev.sh --host-url URL     against the Macro Deck host at URL instead of the one found locally
#   scripts/dev.sh --no-watch         against the running Macro Deck, without rebuilding on save
#   scripts/dev.sh --stub             against a disposable stub host, no Macro Deck needed
#   scripts/dev.sh --test             run the conformance suite against the project and exit
#
# Anything after -- is passed to macrodeck-plugin as is. Stop with Ctrl-C.
set -euo pipefail

usage() {
	sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
}

mode=real
watch=true
host_url=""
extra=()
while [[ $# -gt 0 ]]; do
	case "$1" in
		--stub) mode=stub ;;
		--test) mode=test ;;
		--no-watch) watch=false ;;
		--host-url)
			[[ $# -ge 2 ]] || { usage >&2; exit 2; }
			host_url="$2"
			shift
			;;
		--)
			shift
			extra=("$@")
			break
			;;
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

case "$mode" in
	stub)
		log "Running against a disposable stub host"
		mdp run --project "$PROJECT_DIR" --stub-host ${extra[@]+"${extra[@]}"}
		;;
	test)
		log "Running the conformance suite against the project"
		mdp test --project "$PROJECT_DIR" --timeout 60 ${extra[@]+"${extra[@]}"}
		;;
	real)
		# A state directory of its own keeps the pairing across runs, so Macro Deck asks only once. It is
		# git-ignored; delete it to pair again.
		args=(run --project "$PROJECT_DIR" --state-directory "$REPO_ROOT/.macrodeck-dev-state")
		[[ "$watch" == true ]] && args+=(--watch)
		[[ -n "$host_url" ]] && args+=(--host-url "$host_url")
		log "Running against Macro Deck${host_url:+ at $host_url}. Approve the pairing prompt in Macro Deck if one appears."
		mdp "${args[@]}" ${extra[@]+"${extra[@]}"}
		;;
esac
