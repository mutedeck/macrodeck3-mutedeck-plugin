# MuteDeck plugin for Macro Deck 3

Control your meetings from [Macro Deck](https://macro-deck.app/) through [MuteDeck](https://mutedeck.com):
mute and unmute, start and stop your camera, screen share and recording, leave the meeting, bring the
call to the front and run MuteDeck custom actions such as raise hand or reactions. Buttons show your live
meeting status, and the plugin keeps the variables of the Macro Deck 2 plugin so existing setups carry
over.

MuteDeck works with Zoom, Microsoft Teams, Google Meet, Webex and more.

## Requirements

- Macro Deck 3.0.0-beta.18 or newer, on macOS (Apple silicon) or Windows (x64)
- The MuteDeck desktop app, running on the same computer or reachable over the network

## Setup

1. Install the plugin from the Macro Deck Store, or from a file (see [Installing a build by hand](#installing-a-build-by-hand)).
2. Open **MuteDeck** on the Integrations page and run its setup. Host and port are prefilled with
   `localhost` and `3492`, MuteDeck's defaults, so on one computer you only click **Save**. Change them
   only when MuteDeck runs on another computer or you changed its plugin port.
3. MuteDeck asks whether Macro Deck may control your meetings. Click **Allow** in MuteDeck. Until you do,
   Macro Deck shows "Approve Macro Deck in MuteDeck" as a notification and as an issue on the integration.

The approval is remembered, so MuteDeck asks only once.

## Actions

| Action | Settings | What it does |
| --- | --- | --- |
| Microphone: mute/unmute | Toggle, Mute or Unmute | Toggles the microphone, or always mutes or always unmutes |
| Camera: start/stop | Toggle, Start camera or Stop camera | Toggles or sets the camera |
| Screen share: start/stop | Toggle, Start sharing or Stop sharing | Toggles or sets screen sharing |
| Recording: start/stop | Toggle, Start recording or Stop recording | Toggles or sets recording |
| Leave meeting | | Leaves the current meeting |
| Bring call to front | | Brings the meeting window to the foreground |
| Trigger custom action | Custom action | Runs a MuteDeck custom action. Pick one from the list MuteDeck reports, or type its name |

An action that could not do its job says so: when MuteDeck is not connected, there is no meeting, or
MuteDeck refuses, the press is reported as failed instead of silently doing nothing.

## Live buttons

The microphone, camera, screen share, recording and leave meeting actions can drive their button's
state. When you add one, Macro Deck offers to let it control the button's states; accept, and the button
switches by itself:

| Action | States |
| --- | --- |
| Microphone | Muted, Unmuted, No microphone, Unavailable |
| Camera, Screen share, Recording | On, Off, Disabled, Unavailable |
| Leave meeting | In call, No call, Unavailable |

Each state starts with a sensible color, and you style it like any other button state. **Unavailable**
means MuteDeck is not connected.

The plugin also bundles the **MuteDeck Icons** pack: muted and unmuted, camera, sharing, recording, leave
meeting, bring to front, raise hand, reactions and more, with disabled variants. Every action can show
the matching icon live: pick the action as the button's icon provider in the button editor, and the icon
follows the meeting, for example a crossed-out microphone while muted. **Trigger custom action** shows the
icon named like the custom action when the pack has one, such as `raise-hand` or `react-heart`. While
MuteDeck is not connected, buttons show their own icon again. The icons are also in the icon picker for
any button.

## Variables

The variables have the same names as in the Macro Deck 2 plugin, so labels such as
`{{ vars.mutedeck_mute_label }}` and state bindings keep working.

| Variable | Values |
| --- | --- |
| `mutedeck_connected` | `true` while connected to MuteDeck and approved |
| `mutedeck_in_call` | `true` while in a meeting |
| `mutedeck_muted` | `true` while the microphone is muted |
| `mutedeck_video_active` | `true` while the camera is on |
| `mutedeck_share_active` | `true` while you share your screen |
| `mutedeck_record_active` | `true` while the meeting is recorded |
| `mutedeck_call_status`, `mutedeck_mute_status`, `mutedeck_video_status`, `mutedeck_share_status`, `mutedeck_record_status` | MuteDeck's raw value: `active`, `inactive` or `disabled` |
| `mutedeck_control_status` | MuteDeck's control status, as MuteDeck reports it |
| `mutedeck_call_label` | `In call`, `No call` or `Disabled` |
| `mutedeck_mute_label` | `Muted`, `Unmuted` or `No mic` |
| `mutedeck_video_label`, `mutedeck_share_label`, `mutedeck_record_label` | `On`, `Off` or `Disabled` |

While MuteDeck is not connected, every variable except `mutedeck_connected` has no value; templates can
tell that apart from an empty value with `vars.<name>.state`.

## Coming from Macro Deck 2

Macro Deck 3's import from Macro Deck 2 recognizes this plugin. With the plugin installed, the import:

- turns every MuteDeck button into the matching action, keeping its Toggle, On or Off choice and the
  custom action's name;
- creates the MuteDeck setup with the host and port you used, so there is nothing to set up again;
- carries over MuteDeck's approval when you let the import read Macro Deck 2's stored credentials, so
  MuteDeck does not ask again. Otherwise approve once in MuteDeck.

Variable names are unchanged, so labels and bindings that use them keep working.

## Network and privacy

The plugin talks to two programs, both normally on your own computer:

- **MuteDeck**, over a WebSocket connection to the host and port from the setup (`localhost:3492` by
  default). It sends the actions you press and introduces itself the way MuteDeck's approval prompt
  shows it: the name "Macro Deck", the plugin version, the Macro Deck version and your operating system.
  It receives your meeting status and the list of custom actions. MuteDeck issues an approval token and
  the plugin keeps an install id; both are stored in Macro Deck's encrypted secret store and never logged.
- **Macro Deck**, over the local connection every Macro Deck 3 plugin uses.

Nothing else: no internet services and no analytics. If you point the setup at another computer, the
connection to MuteDeck goes over your network, unencrypted, as MuteDeck's plugin protocol defines it.

## Development

The plugin is a .NET 10 out-of-process Macro Deck 3 plugin built on the Macro Deck SDK `3.0.0-beta.18`
and [MuteDeck.PluginClient](https://www.nuget.org/packages/MuteDeck.PluginClient).
[AGENTS.md](AGENTS.md) has the code rules and the layout.

### Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The scripts look for it in
  `$DOTNET_ROOT`, then `~/.dotnet` (where Microsoft's install script puts it), then on `PATH`, so an
  older system-wide SDK does not get in the way.
- Only for rebuilding the icon pack (`scripts/make-icon-pack.sh`): Python 3, `cwebp`
  (`brew install webp`) and Google Chrome (or set `CHROME` to a Chromium binary). Nothing else in the
  scripts needs them.

The Macro Deck plugin CLI (`macrodeck-plugin`) is a local tool pinned in `.config/dotnet-tools.json` to
the SDK version in `Directory.Packages.props`. The scripts restore it and refuse to run when the two
differ; outside the scripts, run it as `dotnet tool run macrodeck-plugin -- <command>`.

### Scripts

| Command | What it does |
| --- | --- |
| `scripts/dev.sh` | Runs the plugin from source against the running Macro Deck, rebuilt on every save. Turn on **Developer Mode** in Macro Deck's settings and approve the pairing prompt once; the pairing is kept in `.macrodeck-dev-state/`. |
| `scripts/dev.sh --stub` | Runs the plugin against a disposable stub host. No Macro Deck needed. |
| `scripts/dev.sh --host-url <url>`, `--no-watch` | Another Macro Deck host, or no rebuild on save. |
| `scripts/dev.sh --test` | Runs the conformance suite against the project. |
| `scripts/build.sh` | Builds with warnings as errors, runs the unit tests, packs this machine's platform (osx-arm64 on Apple silicon), validates the package for publication, inspects it and runs the conformance suite against it on a stub host. |
| `scripts/build.sh --all` | The same, with one package for every declared platform (osx-arm64 and win-x64). |
| `scripts/build.sh --rid win-x64` | One platform. Conformance runs only for this machine's platform. |
| `scripts/build.sh --skip-tests --skip-conformance` | Just build and pack. |
| `scripts/make-icon-pack.sh` | Renders the vector icons (`icon-src/vector/`, listed in `scripts/render_icons.py`) to 512 px PNGs in `icon-src/`, then rebuilds `src/MuteDeckPlugin/icon-packs/mutedeck.macroDeckIconPack` from `icon-src/*.png` and registers it in the manifest. Packing the same PNGs always gives the same bytes; a different Chrome version may render slightly different PNGs. Icons without a vector source (clap, applause, tada, bring to front, background blur, custom action, logo) are hand-made PNGs in `icon-src/`. |

Packages and conformance reports land in `artifacts/`. Plain `dotnet build` and `dotnet test` on
`MuteDeckPlugin.slnx` work too.

### Installing a build by hand

Open the Integrations page in Macro Deck, choose **Install from file…** and pick the `.macroDeckPlugin`
from `artifacts/`. A package you build yourself is unsigned, so Macro Deck asks you to confirm. Once the
signed Store version is installed, an unsigned build is refused as an update; uninstall it first.

### Tests

`tests/MuteDeckPlugin.Tests` drives the plugin through the SDK's `PluginTestHarness` with MuteDeck
replaced by a fake connection: actions and their results, states and icons, variables, the approval
issue and notification, the setup flow, the token store, the Macro Deck 2 migration, localization, and
that every icon the code names is in the bundled pack.

### Releasing

CI (`.github/workflows/ci.yml`) builds, tests and runs the conformance suite on macOS and Windows, and
packs and validates on Linux. Publishing a GitHub release runs `.github/workflows/release.yml`, which
hands the build to Macro Deck's official publishing workflow; the build then waits in the Creator
Portal. The release tag is the version, so `version` in `manifest.json` needs no bump.

## License

MIT, see [LICENSE](LICENSE). Third-party components are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
