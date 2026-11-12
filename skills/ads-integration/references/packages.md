# Packages (step 1)

Everything here is written into `<project>/Packages/manifest.json` in **one** edit, after all versions are resolved
and any conflicts have been confirmed with the user. Keep the file valid JSON with 2-space indentation, keep every
existing entry, and do not reorder unrelated keys.

## Scoped registries

Required registries:

```json
"scopedRegistries": [
  {
    "name": "TripSoft",
    "url": "https://verdaccio.thelegends.io.vn",
    "scopes": [
      "com.google",
      "com.thelegends",
      "appsflyer-unity-plugin"
    ]
  },
  {
    "name": "OpenUPM",
    "url": "https://package.openupm.com",
    "scopes": [
      "com.annulusgames.lit-motion"
    ]
  }
]
```

Merge rules:

- Match existing registries by **url**, not by name. If one exists, add only the missing scopes.
- If another registry already claims one of these scopes (for example an OpenUPM entry with `com.google`), tell
  the user. Two registries with the same scope make resolution unpredictable. Ask which to keep.
- `com.annulusgames.lit-motion` is needed because `com.thelegends.unity.ui` (an Ads Manager dependency) depends on
  LitMotion.

## Core packages

| Package | Version |
|---|---|
| `com.thelegends.ads.manager` | Verdaccio `dist-tags.latest` (see below) |
| `com.pimdewitte.unitymainthreaddispatcher` | `https://github.com/PimDeWitte/UnityMainThreadDispatcher.git` |
| `com.v0lt.editor-attributes` | depends on the Unity version, see below |

Ads Manager's own dependencies (`com.thelegends.unity.ui`, `com.thelegends.unity.patterns`,
`com.unity.ads.ios-support`) are pulled in automatically. Do not add them.

### EditorAttributes by Unity version

EditorAttributes 3.x does not declare a minimum Unity version, so Unity installs it on 2022.3 and then fails to
compile it. Pin by the project's `m_EditorVersion`:

| Unity | Value |
|---|---|
| 6000.x (Unity 6) and newer | `https://github.com/v0lt13/EditorAttributes.git` (latest) |
| 2022.3 | `https://github.com/v0lt13/EditorAttributes.git#v2.9.2` (last 2.x) |
| 2021.3 | `https://github.com/v0lt13/EditorAttributes.git#v2.9.2`. Not verified on 2021.3: if it fails to compile, stop and tell the user |

If the manifest already has EditorAttributes with a different ref, treat it like any other conflict: show it and
ask. Mention the chosen ref in the final report.

## Resolving versions

Use the registry's `latest` dist-tag. **Do not** filter version lists with a "preview" regex: the registry contains
misspelled pre-release tags such as `1.1.100-pewview.2`, and those sort above the real latest.

Any of these work:

```bash
# curl (any OS with curl)
curl -s https://verdaccio.thelegends.io.vn/com.thelegends.ads.manager | grep -o '"latest":"[^"]*"'

# npm
npm view com.thelegends.ads.manager dist-tags.latest --registry https://verdaccio.thelegends.io.vn
```

```powershell
# PowerShell
(Invoke-RestMethod https://verdaccio.thelegends.io.vn/com.thelegends.ads.manager).'dist-tags'.latest
```

The same request with another package name gives that package's latest version (used for the `1.2.x` adapters).
If the registry is unreachable, stop and tell the user. Do not fall back to a remembered version.

## AdMob and mediation adapters

Choose the table from the **major.minor** of the Ads Manager version resolved above.

### Ads Manager 1.1.x: pinned versions

```json
"com.google.ads.mobile": "9.6.0",
"com.google.ads.mobile.mediation.applovin": "8.0.4",
"com.google.ads.mobile.mediation.ironsource": "3.6.1",
"com.google.ads.mobile.mediation.liftoffmonetize": "5.2.4",
"com.google.ads.mobile.mediation.metaaudiencenetwork": "3.14.0",
"com.google.ads.mobile.mediation.mintegral": "1.8.0",
"com.google.ads.mobile.mediation.pangle": "4.5.4",
"com.google.ads.mobile.mediation.unity": "3.12.5"
```

### Ads Manager 1.2.x: latest of each

The same eight package names, each at its own Verdaccio `dist-tags.latest`. Resolve all eight before writing the
manifest, and show the user the resulting versions.

### Any other version

Stop and ask the user which adapter versions to use. Do not extrapolate.

## Conflicts

Before writing, compare every package above with what the manifest already has:

- same version (or same git ref): leave it
- different: collect it, then show **one** list (package, current, target) and ask once. Write only after the answer.

## After writing

Validate that the manifest is still valid JSON, copy the bridge (SKILL.md step 1), then trigger the import (step 2).
