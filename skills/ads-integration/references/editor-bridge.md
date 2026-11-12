# Editor bridge

Unity Editor actions (TripSoft menus, AdsSettings inspector, SAVE, Build Settings, scene edits) go through a small
Editor script that ships with this skill. It works with any agent that can read and write files. No Unity CLI or
MCP server is needed.

## Install

In step 1, together with the manifest edit, copy both files from this skill's `scripts/` folder into the project:

```
<project>/Assets/Editor/BFunAgent/BFunAdsAgentBridge.cs
<project>/Assets/Editor/BFunAgent/BFun.AgentTools.Editor.asmdef
```

Unity loads the bridge during the single import triggered in step 2 ([first-import.md](first-import.md)). The asmdef
keeps the bridge in its own Editor-only assembly. The script uses reflection for every Ads Manager type, so it
compiles in the same pass as Ads Manager or before it. Remove the folder at the end of the integration.

## Protocol

| File (relative to the project root) | Written by | Purpose |
|---|---|---|
| `Library/BFunAgent/command.json` | agent | one command to run; the bridge deletes it when it picks it up |
| `Library/BFunAgent/status.json` | bridge | result of the last command plus Editor facts; rewritten every ~2 s |

Send a command:

1. Pick a new `id` (an increasing number or a timestamp).
2. Write `command.json`. Prefer writing `command.json.tmp` and renaming it, so the bridge never reads half a file.
3. Poll `status.json` every 2–3 seconds until `lastCommandId` equals your `id`. Then read `lastOk`, `lastMessage`
   and `lastDetails`.

The bridge only runs a command when Unity is idle (not compiling, not resolving packages). Use whatever waiting
mechanism your environment allows: a shell loop with `sleep`, or repeated reads.

```bash
# bash: wait for command id 7
until grep -q '"lastCommandId": "7"' Library/BFunAgent/status.json 2>/dev/null; do sleep 2; done
```

```powershell
# PowerShell: wait for command id 7
while (-not ((Get-Content Library/BFunAgent/status.json -Raw -ErrorAction SilentlyContinue) -match '"lastCommandId": "7"')) { Start-Sleep 2 }
```

Always put a timeout around the wait (about 3 minutes for commands, 10 minutes for package resolution and compilation).

## status.json fields

| Field | Meaning |
|---|---|
| `state` | `ready`, `compiling`, `updating` (Package Manager / asset import), `compile-failed` |
| `updatedAt` | UTC heartbeat. Older than ~15 s means the Editor is not ticking (closed, hung, or throttled in the background) |
| `compileErrors` | compiler errors from the last failed compile |
| `packages` | installed `name@version` for com.thelegends.*, com.google.ads.mobile*, com.google.firebase.*, appsflyer*, and the two git packages |
| `tripSoftMenus` | every `TripSoft/...` menu item currently available |
| `adsSettingsExists`, `appsFlyerSettingsExists` | whether the settings assets exist |
| `androidDefines`, `iosDefines` | scripting define symbols |
| `buildScenes` | Build Settings scene list in order |
| `lastCommandId`, `lastCommandName`, `lastOk`, `lastMessage`, `lastDetails` | result of the last command |

## Commands

### status

`{"id":"1","name":"status"}`: rewrites status.json immediately. Use it to check that the bridge is alive.

### refresh

`{"id":"2","name":"refresh"}`: `AssetDatabase.Refresh()` plus `Client.Resolve()`. Send it after editing
`manifest.json` or C# files so Unity picks up the change without the user focusing the window.

### setup-ads-settings

`{"id":"3","name":"setup-ads-settings"}` covers steps 4 and 5 in one go:

1. Runs menu `TripSoft/Ads Settings` if `Assets/TripSoft/AdsManager/Resources/AdsSettingsAsset.asset` does not exist.
2. Turns on Admob mediation through the `FlagMediations` setter, the same path as the inspector toggle, and makes it
   primary if none is set.
3. Sets `showADMOB`, `useFirebase` and `useAppsFlyer`, plus `preloadSettings.nativeAds.preloadNativeBanner`,
   `preloadNativeReward` and `preloadNativeAppOpen`, all to true. Then saves the asset.
4. Calls `PackagesManagerIntergration.UpdateManifest()`, which is what the SAVE button calls. It syncs the `USE_*`
   define symbols and writes the optional packages into the manifest at their `optionalDependencies` versions.
5. `lastDetails` lists each changed field and, for `com.google.ads.mobile`, `com.thelegends.firebase.manager` and
   `com.thelegends.appsflyer.manager`, the `optionalDependencies` version next to the manifest version.

Unity then resolves the new packages and recompiles. Wait for `ready` before the next command.

### create-appsflyer-settings

`{"id":"4","name":"create-appsflyer-settings"}`: runs menu `TripSoft/AppsFlyer Setting`, which creates
`Assets/TripSoft/AppsFlyer/Resources/AppsFlyerSettingsAsset.asset`. Succeeds without doing anything if the asset
already exists.

### install-splash-scene

`{"id":"5","name":"install-splash-scene","scenePath":"Assets/Scenes/AdsSplash.unity"}` (`scenePath` is optional,
default shown): copies the package scene to `scenePath` if it is not there yet, then makes it build index 0.

### configure-splash

```json
{
  "id": "6",
  "name": "configure-splash",
  "scenePath": "Assets/Scenes/AdsSplash.unity",
  "nextSceneName": "Home",
  "hooks": [
    { "eventName": "OnInitFirebaseDone", "gameObjectPath": "Systems/RemoteConfig", "component": "RemoteConfigLoader", "method": "Fetch" },
    { "eventName": "OnCompleteSplash", "gameObjectPath": "Systems/AdsBoot", "component": "AdsBoot", "method": "ShowBanner" }
  ]
}
```

- `nextSceneName` is the scene **file name without extension**. The bridge adds that scene to Build Settings if it is
  missing, and fails if no scene or several scenes have that name.
- `hooks` is optional. `eventName` is one of `OnInitFirebaseDone`, `OnInitAdsDone`, `OnCompleteSplash`.
  `gameObjectPath` is the hierarchy path **inside the AdsSplash scene**. The method must be `public void Name()`.
  Hooks that already exist are not added twice.
- Opens the scene additively if needed, saves it, and closes it again, so the user's open scene is not affected.
  If any part fails, nothing is saved.

## Fallbacks

- **Bridge never writes status.json:** the project probably has compile errors, or Unity has not imported the files.
  Ask the user to focus Unity and check the Console.
- **A Unity CLI or Unity MCP server is available:** you may use it to trigger a refresh or to run the same steps,
  but keep the same order and the same checks. The bridge commands remain the reference behaviour.
- **No way to drive the Editor:** give the user the exact clicks for that step (menu path, toggles, SAVE), wait for
  them to confirm, then verify by reading the files (`manifest.json`, the settings asset existing, `ProjectSettings`).
