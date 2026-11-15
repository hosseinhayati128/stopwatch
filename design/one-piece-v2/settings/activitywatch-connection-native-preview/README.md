# ActivityWatch: Connection & Status native proposal

Design preview for review, not a production implementation. Rendered with native Avalonia controls inside the existing Settings shell and shared frame. All connection results, times and version numbers are samples. No ActivityWatch process is launched, no HTTP requests are made, and no live settings are loaded or saved.

## Proposed layout

Use Connection and Connection status cards side by side, matching the recently upgraded Habitica page. Stack them in smaller windows. The Connection card contains the integration preference, server address, Test connection and Start ActivityWatch. The status card contains the last check result, server version, check time and an explanation of what the check proves. A short How it works card explains the division between the two applications.

The two-card approach keeps the integration preference separate from evidence that the server answered. A single long form would bury that distinction. A setup wizard would add unnecessary steps to an optional local integration. The proposed tabs reserve Sync & export and Review prompts for the subsequent layers; they are disabled in this preview.

## Interaction and state decisions

- Integration on/off is independent of server reachability. Turning integration off does not stop ActivityWatch, and connection testing remains available.
- The initial state is Not checked yet. An enabled checkbox alone must never produce a connected status.
- A successful check reports Server reachable, a sample version and the check time. It does not claim that window/browser/AFK watchers are collecting data or that an export succeeded.
- Checking and starting use an indeterminate activity indicator. Both actions and the address field are disabled during a pending operation.
- Editing the address clears the previous result and version. No recurring server polling is proposed for this page.
- Empty addresses retain the existing local default, `http://localhost:5600`; addresses without a scheme can use HTTP. Invalid addresses disable both actions and show inline feedback.
- Start ActivityWatch is intended for the local server. It is disabled for a remote address in this proposal; testing that address remains available.
- An unsuccessful executable lookup says Open ActivityWatch manually. It does not claim that the app is definitely uninstalled. A successful launch still needs a successful server response.
- Existing settings persistence and the current connection-test/launcher operations should be reused when implementation is approved. Late responses must not update a different address or a closed page.

## Captures

`connected.png` is the primary layout. `setup.png`, `checking.png`, `starting.png`, `unreachable.png`, `app-not-found.png`, `integration-off.png`, `invalid-address.png` and `remote-address.png` cover sample states. `compact-connection.png` and `compact-status.png` show the stacked layout at different scroll positions. `connected-70.png`, `connected-125.png` and `large-text.png` document scale checks.

## Verification

The preview checks regular 1120×720 and compact 840×600 windows at 70%, 90% and 125% page scale, plus 175% text at compact 125%. Checks cover horizontal fit, action captions, address field height, keyboard focus, duplicate-action disabling, address-result invalidation and the fixed Done button. The enlarged-text stress state has a small scrolling viewport because the existing shared header also grows.

The fixture uses independent sample controls and holds the unused legacy Settings controls in their loading state to prevent deferred setup events from changing the sample Settings model. It asserts zero SettingsChanged callbacks and unchanged ActivityWatch model values throughout the interactions. It uses the isolated preview application with no controller, tray, sync service or external-process action.

After building DesktopRegressionChecks with the shared desktop runtime, reproduce on a private Windows desktop:

```powershell
& tools/DesktopRegressionChecks/Run-HiddenDesktop.ps1 -Mode --settings-activitywatch-preview -LogPath .superpowers/sdd/2026-11-15-settings-activitywatch-preview/native.log
```

The installed app, production Settings page and clock-overlay assets remain unchanged. No live ActivityWatch connection or Linux runtime was tested.
