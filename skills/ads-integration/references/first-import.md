# First import (steps 0 and 2)

Unity only notices changed files when it opens a project or when its window gains focus. Until the bridge is
running, the agent cannot ask Unity to refresh. So step 1 writes **every** file change first (manifest + bridge),
and step 2 makes Unity import them **once**. After that, the bridge `refresh` command handles everything.

Try the options in order and stop at the first one that works. Tell the user what you are doing in one line
("Opening Unity…" or "Bringing Unity to the front so it imports the changes…").

## Is the Editor open?

Check both. Either one is enough to say "open":

- `<project>/Temp/UnityLockfile` exists. Unity deletes it on a clean exit. After a crash it can be stale, so confirm
  with the process check when possible.
- A Unity process whose window title contains the project folder name. The title looks like
  `<ProjectName> - <Scene> - <Platform> - Unity <version>`.

```powershell
# Windows
Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*<ProjectName>*" } | Select-Object Id, MainWindowTitle
```

```bash
# macOS
pgrep -fl "Unity.*-projectPath.*<ProjectName>" || osascript -e 'tell application "System Events" to get name of every window of process "Unity"'
```

## 1. Unity CLI or MCP

If the environment has a Unity CLI (`unity --help` works) or a Unity MCP server, use it to refresh the asset
database or execute `Assets/Refresh`. Then go to "Wait".

## 2. Editor not running: launch it

Find the Editor that matches `m_EditorVersion` from `ProjectSettings/ProjectVersion.txt`:

| OS | Where Unity Hub installs editors |
|---|---|
| Windows | `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`, or a custom root stored in `%APPDATA%\UnityHub\secondaryInstallPath.json` (then `<root>\<version>\Editor\Unity.exe`) |
| macOS | `/Applications/Unity/Hub/Editor/<version>/Unity.app` |

```powershell
# Windows
Start-Process "<path>\Unity.exe" -ArgumentList '-projectPath', '"<project root>"'
```

```bash
# macOS
open -a "/Applications/Unity/Hub/Editor/<version>/Unity.app" --args -projectPath "<project root>"
```

If that exact version is not installed, do not open the project with a different version: an upgrade prompt or a
silent upgrade is the user's call. Ask the user to open the project themselves.

The first open of a project with new packages can take several minutes. Allow up to 10 minutes in "Wait".

## 3. Editor running: bring it to the front

Focus is enough to make Unity refresh.

```powershell
# Windows
$p = Get-Process Unity | Where-Object { $_.MainWindowTitle -like "*<ProjectName>*" } | Select-Object -First 1
if ($p) { (New-Object -ComObject WScript.Shell).AppActivate($p.Id) | Out-Null }
```

```bash
# macOS
osascript -e 'tell application "Unity" to activate'
```

Windows may refuse to hand focus to another app and only flash the taskbar button. That is why "Wait" has a fallback.

## The scoped registry popup

When the manifest gains a scoped registry Unity has not seen in this project, Unity shows a modal dialog
**"Importing N scoped registries"** ("N new scoped registries are now available in the Package Manager", with buttons
Read more / Close). Unity has no setting to turn it off. While it is open, Unity is blocked: package resolution, the
compile, and the bridge all wait. Only a person can close it.

- Warn the user before step 2 (SKILL.md step 2), so they expect it.
- It appears **only** when a registry is new to the project. If step 1 only added scopes to an existing registry, or
  nothing changed in `scopedRegistries`, skip the warning.
- Clicking **Close** opens Project Settings > Package Manager. That is harmless: the user can close that window too.

Bringing the popup to the front (Windows) makes it easier for the user to find. `AppActivate` matches a window title
prefix and returns `True` if such a window exists:

```powershell
(New-Object -ComObject WScript.Shell).AppActivate("Importing")
```

Do **not** send keystrokes or click the dialog on the user's behalf. Bring it forward and ask.

## Wait

Poll `Library/BFunAgent/status.json` every 2–3 seconds:

- it appears (or its `updatedAt` becomes fresh) → the bridge is running. Continue with step 3.
- Unity is running but nothing appears after ~20 s, and a new registry was added → the popup is almost certainly
  open. Bring it to the front (above) and remind the user in one line: "Unity is waiting on the 'Importing scoped
  registries' popup. Click Close."
- `updatedAt` was fresh and then stops advancing for more than ~15 s while Unity is running → a modal dialog is open
  (this popup, or another Unity prompt). Same reminder, naming whatever dialog the user can see.
- nothing after ~60 s (option 3) or ~10 min (option 2) → ask the user, in one short line, to click into the Unity
  window. This is the only other time the user has to touch Unity.
- `state: "compile-failed"` → show `compileErrors` and stop.

If the project already had compile errors before the integration, the bridge cannot load at all. Ask the user to
fix those first.
