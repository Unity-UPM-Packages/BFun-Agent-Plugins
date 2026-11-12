# Pitfalls

Things that broke, or would break, an Ads Manager integration. Read before steps 4–6.

## AdsSettings must be created and configured in one go

`PackagesManagerIntergration` is `[InitializeOnLoad]`. After **every** compile it calls `UpdateManifest()`, which
**removes** from `manifest.json` every optional package whose flag in AdsSettings is off. That includes
`com.google.ads.mobile` when `showADMOB` is false.

A freshly created AdsSettings has every flag off. So if the asset is created through the menu and a compile happens
before Admob, Firebase and AppsFlyer are switched on, Ads Manager deletes AdMob (and any Firebase/AppsFlyer managers)
from the manifest.

→ Always use the single `setup-ads-settings` bridge command. It creates and configures the asset in one Editor tick.
Never run the `TripSoft/Ads Settings` menu on its own.

The same mechanism also means: if the user later switches a service off in AdsSettings, its package disappears
from the manifest on the next compile. That is by design. Mention it in the final report.

## UpdateManifest only upgrades

The SAVE logic adds missing optional packages and raises versions lower than `optionalDependencies`. It never
lowers a version. If the project already has a newer Firebase or AppsFlyer manager, it stays. Report it, do not
"fix" it.

## Wait for Unity, every time

After a manifest change Unity must resolve packages, import and compile. Commands sent too early fail with
"type not found" or "menu not found". The bridge refuses to run commands while compiling or updating, but a manifest
change only starts that work once Unity notices the file. Before the bridge runs, that takes a launch or a window
focus ([first-import.md](first-import.md)). After that, the agent sends `refresh` after each edit.

Check `updatedAt`: if it stops advancing, Unity is closed, frozen, or throttled in the background. Try bringing the
window to the front (first-import.md, option 3), and ask the user only if that does not help.

## New scoped registries open a blocking popup

Adding a scoped registry that is new to the project makes Unity show the modal "Importing N scoped registries"
dialog. Unity, including the bridge heartbeat, stops until someone clicks Close, and it cannot be disabled.
Warn the user before triggering the import, and when the heartbeat stalls, remind them about the popup instead of
waiting silently ([first-import.md](first-import.md#the-scoped-registry-popup)).

## EditorAttributes 3.x breaks Unity 2022.3

`com.v0lt.editor-attributes` declares no minimum Unity version. Installing the git URL without a ref gets 3.x, which
does not compile on 2022.3. Pin `#v2.9.2` for Unity 2022.3 and older ([packages.md](packages.md#editorattributes-by-unity-version)).

## Compile errors block the bridge

If the project does not compile, Unity does not reload scripts, so a newly copied bridge never starts. Fix or report
existing errors before step 1. If a compile fails later, `state` becomes `compile-failed` with `compileErrors`. Show
them and stop. Do not run more steps on a broken project.

## Version selection

- Use Verdaccio `dist-tags.latest`. Pre-release tags are inconsistent (`-preview.1`, `-pewview.2`), so "highest
  version without preview" picks the wrong one.
- Adapter tables depend on the Ads Manager **minor** version (1.1 vs 1.2). Decide after resolving Ads Manager, not before.

## Registry scopes

The TripSoft registry claims the whole `com.google` scope, so every `com.google.*` package (AdMob, adapters, EDM4U,
Firebase) resolves from Verdaccio. If a package version is missing there, resolution fails with "package not found".
Report the exact package and version. Do not add another registry for `com.google`.

## USE_ADMOB is required

All native ad APIs (`AdsManager.Show/LoadNative*`, `NativePlatformShowBuilder`, `AdsCaller` native methods) only
exist when `USE_ADMOB` is defined. `setup-ads-settings` turns it on. If `androidDefines` or `iosDefines` lack it after
step 5, the setup did not take effect. Re-run the command, do not add the symbol by hand.

## Splash callbacks

`OnInitFirebaseDone`, `OnInitAdsDone` and `OnCompleteSplash` are private serialized `UnityEvent`s. They can only call
public parameterless methods on objects inside the AdsSplash scene. `OnCompleteSplash` fires after the next scene has
loaded. See [splash.md](splash.md).

## Never edit packages

Do not edit anything under `Packages/com.thelegends.*` or `Library/PackageCache/`. If something in Ads Manager looks
wrong, report it to the user instead of patching it.
