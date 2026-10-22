# Sourced by the other scripts: finds a .NET 10 SDK, restores the pinned plugin CLI and defines mdp.
# Not meant to be run on its own.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/src/MuteDeckPlugin"
SOLUTION="$REPO_ROOT/MuteDeckPlugin.slnx"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"

log() {
	printf '==> %s\n' "$*" >&2
}

die() {
	printf 'error: %s\n' "$*" >&2
	exit 1
}

has_dotnet10_sdk() {
	local sdks
	sdks="$("$1" --list-sdks 2>/dev/null)" || return 1
	[[ "$sdks" == *$'\n'10.* || "$sdks" == 10.* ]]
}

# Follows symlinks such as /opt/homebrew/bin/dotnet to the real install, whose directory is the
# DOTNET_ROOT. macOS ships bash 3.2 and a readlink without -f, hence the loop.
resolve_path() {
	local path="$1" target
	while [[ -L "$path" ]]; do
		target="$(readlink "$path")"
		if [[ "$target" == /* ]]; then
			path="$target"
		else
			path="$(dirname "$path")/$target"
		fi
	done
	printf '%s\n' "$path"
}

# ~/.dotnet first: a user-local install is where the .NET 10 SDK usually lives on a Mac that also has
# an older SDK installed system-wide.
find_dotnet() {
	local candidate
	for candidate in \
		"${DOTNET_ROOT:-}/dotnet" \
		"$HOME/.dotnet/dotnet" \
		"$(command -v dotnet 2>/dev/null || true)" \
		/usr/local/share/dotnet/dotnet \
		/opt/homebrew/share/dotnet/dotnet; do
		if [[ -n "$candidate" && -x "$candidate" ]] && has_dotnet10_sdk "$candidate"; then
			printf '%s\n' "$candidate"
			return 0
		fi
	done
	return 1
}

DOTNET="$(find_dotnet)" || die "no .NET 10 SDK found. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 (the install script puts it in ~/.dotnet)."
DOTNET="$(resolve_path "$DOTNET")"
DOTNET_ROOT="$(cd "$(dirname "$DOTNET")" && pwd -P)"
export DOTNET_ROOT
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

sdk_version() {
	sed -n 's:.*<MacroDeckSdkVersion>\(.*\)</MacroDeckSdkVersion>.*:\1:p' "$REPO_ROOT/Directory.Packages.props"
}

# Reads tools."macrodeck.plugin.cli".version without needing python3, which a fresh macOS only stubs.
cli_version() {
	sed -n '/"macrodeck.plugin.cli"/,/}/s/.*"version": *"\([^"]*\)".*/\1/p' "$REPO_ROOT/.config/dotnet-tools.json" | head -n 1
}

# The CLI and the SDK packages must come from the same release: a mismatched CLI tests and packs against
# a protocol the plugin does not speak.
check_versions() {
	local sdk cli
	sdk="$(sdk_version)"
	cli="$(cli_version)"
	[[ -n "$sdk" ]] || die "MacroDeckSdkVersion is not set in Directory.Packages.props"
	[[ "$sdk" == "$cli" ]] || die "CLI $cli in .config/dotnet-tools.json differs from MacroDeckSdkVersion $sdk; keep them equal"
}

restore_tools() {
	(cd "$REPO_ROOT" && dotnet tool restore >/dev/null) || die "dotnet tool restore failed"
}

mdp() {
	(cd "$REPO_ROOT" && dotnet tool run macrodeck-plugin -- "$@")
}

host_rid() {
	case "$(uname -s)-$(uname -m)" in
		Darwin-arm64) echo osx-arm64 ;;
		MINGW*-x86_64 | MSYS*-x86_64 | CYGWIN*-x86_64) echo win-x64 ;;
		*) echo "" ;;
	esac
}

check_versions
restore_tools
log "Using $("$DOTNET" --version) from $DOTNET_ROOT, macrodeck-plugin $(cli_version)"
