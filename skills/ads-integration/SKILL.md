---
name: ads-integration
description: Integrates the TripSoft Ads Manager (com.thelegends.ads.manager) into a Unity game end to end - scoped registries, manifest packages, AdMob mediation adapters, AdsSettings, Firebase and AppsFlyer managers, the AdsSplash start scene, and turning TODO ADS comments into AdsManager and AdsCaller calls. Use when the user asks to add or integrate ads, set up Ads Manager, AdMob or native ads, or wire ad TODOs in a Unity project. Not for changing the Ads Manager package itself or any com.thelegends sdk package.
---

# Ads integration (TripSoft Ads Manager)

Runs the full integration of `com.thelegends.ads.manager` into the user's Unity project, in three phases:

| Phase | Steps | Result |
|---|---|---|
| A. Packages | 0–3 | All file changes made in one batch (manifest + bridge), Unity imports them once |
| B. Editor setup | 4–6 | AdsSettings configured and saved, Firebase + AppsFlyer managers installed, AppsFlyer Setting created |
| C. Project wiring | 7–10 | AdsSplash is the start scene, one combined review with the user, `TODO ADS` comments replaced with ad calls |

Work through the steps in order. Each step lists what must be true before moving on. Talk to the user in
their language.

## Ground rules

- **Never edit** `Packages/com.thelegends.*` package sources or anything under `Library/PackageCache`.
  All changes go into the user's `Assets/`, `Packages/manifest.json` and `ProjectSettings` (through the bridge).
- **Editor actions go through the bridge** (`scripts/BFunAdsAgentBridge.cs`). Read
  [references/editor-bridge.md](references/editor-bridge.md) before step 0. Never hand-edit `.asset` or `.unity`
  files.
- **Wait for Unity after every package change.** A step that changes the manifest is done only when the bridge
  reports `state: "ready"` and the expected package or menu is present. See [references/pitfalls.md](references/pitfalls.md).
- **Ask only where a step says so**, and batch questions: the whole integration has at most three stops for the
  user (version conflicts in step 1, the combined review in steps 8–9, and an import nudge in step 2 if nothing else
  works). Do not guess scene names, callbacks or placement orders.

## Phase A: Packages

### Step 0: Preflight (read only)

1. Find the Unity project root: it has `Assets/`, `Packages/manifest.json` and `ProjectSettings/ProjectVersion.txt`.
   Read `m_EditorVersion`. Unity must be 2021.3 or newer.
2. Find out whether the Editor currently has this project open
   ([how](references/first-import.md#is-the-editor-open)). This decides how step 2 triggers the import.
3. Check whether `Packages/manifest.json` already contains Ads Manager or any package from
   [references/packages.md](references/packages.md) at a different version than the one step 1 would write.
4. Recommend committing or stashing first if the project is under version control and has uncommitted changes.

### Step 1: Prepare every file change in one batch

Resolve all versions first, ask about conflicts once, then write everything without waiting for Unity in between.

1. **Resolve versions:** Ads Manager = Verdaccio `dist-tags.latest` (never a preview tag), then the adapter set for
   its minor version (pinned for `1.1.x`, latest of each for `1.2.x`, anything else: ask), then the EditorAttributes
   ref for the project's Unity version. All rules: [references/packages.md](references/packages.md).
2. **Conflicts:** if step 0 found packages at other versions, show one list (package, current, target) and ask once
   before writing.
3. **Write the manifest** in one edit: scoped registries, Ads Manager, the two git packages, AdMob and the adapters.
4. **Copy the bridge:** `scripts/BFunAdsAgentBridge.cs` and `scripts/BFun.AgentTools.Editor.asmdef` from this skill's
   folder into `<project>/Assets/Editor/BFunAgent/`.

**Done when:** the manifest is valid JSON with every entry from packages.md, and both bridge files are in place.

### Step 2: Trigger one import

Unity only picks up file changes when it opens the project or its window gets focus. If step 1 added a scoped
registry the project did not have, first tell the user in one line: **"Unity will show a popup 'Importing N scoped
registries'. Click Close so it can continue."** That popup is modal and blocks Unity, the bridge included, until
someone closes it ([details](references/first-import.md#the-scoped-registry-popup)).

Then make the import happen once, trying in order, and stop at the first that works ([commands](references/first-import.md)):

1. a Unity CLI or Unity MCP tool, if one is available: ask it to refresh
2. Editor **not** running for this project: launch it with `-projectPath` and tell the user it is opening
3. Editor running: bring its window to the front with an OS command
4. after about 60 seconds without a fresh `Library/BFunAgent/status.json` (10 minutes when Unity was just launched
   and is importing), ask the user to click into the Unity window

### Step 3: Verify the import

Wait until `status.json` shows `state: "ready"`, `packages` contains `com.thelegends.ads.manager@<version>`, and
`tripSoftMenus` contains `TripSoft/Ads Settings`. If `state` is `compile-failed`, show the user `compileErrors` and
stop. From here on, every refresh goes through the bridge `refresh` command. The bridge keeps working while Unity is
in the background, so the user does not need to do anything.

## Phase B: Editor setup

### Step 4: Create AdsSettings

Send `setup-ads-settings`. That single command does steps 4 and 5 together. It must stay one command
([why](references/pitfalls.md#adssettings-must-be-created-and-configured-in-one-go)).

### Step 5: Firebase, AppsFlyer and native preload (same command as step 4)

`setup-ads-settings` does what a person does in the AdsSettings inspector:

- turns on **Google Admob** mediation (primary), **Firebase** and **AppsFlyer**
- ticks preload for **Native Banner**, **Native Reward** and **Native AppOpen**
- runs the logic behind the **SAVE** button, which sets the `USE_*` define symbols and adds
  `com.thelegends.firebase.manager` and `com.thelegends.appsflyer.manager` to the manifest at the versions in Ads
  Manager's `optionalDependencies`

**Done when:** `lastOk` is true and `lastDetails` lists both managers with a manifest version equal to their
`optionalDependencies` version. Report those versions to the user.

### Step 6: AppsFlyer Setting

Wait for `ready` again, until `packages` contains both managers and `tripSoftMenus` contains `TripSoft/AppsFlyer Setting`.
Then send `create-appsflyer-settings`.

**Done when:** `appsFlyerSettingsExists` is true.

## Phase C: Project wiring

### Step 7: AdsSplash as start scene

Send `install-splash-scene`. It copies the package's `AdsSplash.unity` to `Assets/Scenes/AdsSplash.unity` (or the
`scenePath` you pass) and puts it at build index 0. Details: [references/splash.md](references/splash.md).

### Steps 8–9: One combined review (splash + TODOs)

Gather everything the user has to decide, then ask in one review: the **splash settings (S1–S3) through your
environment's multiple-choice question tool**, and the **TODOs as one choice table** answered with codes. Format and
examples: [references/review.md](references/review.md).

1. **Collect splash decisions:** the next scene (required) and the `OnInitFirebaseDone` / `OnCompleteSplash` callbacks
   (optional). Rules for what can be wired: [references/splash.md](references/splash.md).
2. **Scan and validate TODOs:** find every `TODO ADS` comment in the project's C# files and validate it against
   [references/todo-convention.md](references/todo-convention.md), including the required `onClose` callbacks and the
   placement-order counts. Also collect near-misses.
3. **Ask:** summarise the valid rows in one line. Ask S1 (next scene), S2 and S3 with the multiple-choice tool, as three
   separate questions, recommendation first. Then print the TODO choice table (option A = recommendation) and **end your
   turn**. Never apply a recommendation on your own ([hard rule](references/review.md#stop-and-wait-hard-rule)).
4. **Apply the answers:** send `configure-splash`, write the TODO answers back into the comments, re-validate, and
   show only the rows that changed. If a row is still invalid, ask about that row alone.
5. **End with the notice block** ([format](references/review.md#the-notice-block)): everything the user still has to
   handle, plus anything accepted in bulk that they should double-check. It is the last thing in the message.

Do not touch code until every TODO that will be converted is valid and the user has confirmed.

### Step 10: Replace TODOs with ad calls

For each confirmed TODO, insert the call from [references/api-mapping.md](references/api-mapping.md) directly below
the comment, and change the marker from `TODO ADS` to `ADS:` so a re-run skips it. Native formats go through
`AdsCaller`, everything else through `AdsManager.Instance`. Add `using TheLegends.Base.Ads;` where needed. Then send
`refresh` and wait for `ready`. If `compile-failed`, fix the errors you introduced and repeat.

## Finish

1. Remove `Assets/Editor/BFunAgent/` (and its `.meta`), then send nothing more: the bridge is gone.
2. Report to the user:
   - versions installed (Ads Manager, AdMob, adapters, Firebase Manager, AppsFlyer Manager, EditorAttributes ref)
   - what was configured (AdsSettings flags, AppsFlyer Setting, splash scene, next scene, callbacks)
   - every TODO replaced, as file:line
3. End with the notice block again, now complete: open items left over from steps 8–9, plus the **manual follow-ups
   the skill does not do**: AdMob App IDs (Google Mobile Ads settings), ad unit IDs in AdsSettings,
   `google-services.json` / `GoogleService-Info.plist`, the AppsFlyer dev key and Apple app ID, and a device test build.
