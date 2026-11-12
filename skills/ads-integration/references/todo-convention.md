# TODO ADS convention (steps 8–9)

Game code marks where ads go with one-line comments. The combined review (steps 8–9) finds, parses and validates
them all, and asks the user once ([how to ask](review.md)). Step 10 only generates code from rows that passed.

## Syntax

```
// TODO ADS <Format> <Action> [key=value ...]
```

- Exactly `TODO ADS` (upper case), then the format and the action, separated by spaces.
- `key=value` pairs, separated by spaces. Values contain no spaces. Lists use commas: `order=One,Two`.
- The comment must sit **inside a method body**, on its own line. The generated call goes on the next line.

Examples:

```csharp
// TODO ADS NativeInter Load order=One,Two
// TODO ADS NativeInter Show order=One,Two pos=level_complete onClose=GoNextLevel
// TODO ADS NativeReward Load order=One
// TODO ADS NativeReward Show order=One pos=revive onClose=GrantRevive
// TODO ADS NativeBanner Show order=One pos=home
// TODO ADS NativeBanner Hide order=One
// TODO ADS Interstitial Show order=One pos=pause onClose=ResumeGame
// TODO ADS Rewarded Show order=One pos=double_coins onReward=GiveDoubleCoins
```

## Keys

| Key | Meaning |
|---|---|
| `order` | `PlacementOrder` names: `One` … `Ten`. Default `One` where a single order is allowed |
| `pos` | the position string passed to the SDK for analytics. Letters, digits and `_` |
| `onClose` | method in the **same class** to run when the ad closes (or is skipped) |
| `onShow` | optional method in the same class to run when the ad shows |
| `onReward` | method in the same class to run when the reward is earned (`Rewarded` only) |
| `adPos` | `AdsPos` enum name for MREC formats (`Top`, `Center`, `Bottom`, `BottomLeft` …). Default `Bottom` |

## Formats and rules

✱ = required. A missing required key makes the row **invalid**. Anything else missing is a **warning** with a
proposed value.

| Format | Action | order | pos | Callbacks |
|---|---|---|---|---|
| `NativeInter` | `Load` | ✱ exactly 2 | – | – |
| `NativeInter` | `Show` | ✱ exactly 2 | ✱ | ✱ `onClose`, optional `onShow` |
| `NativeReward` | `Load` | exactly 1 | – | – |
| `NativeReward` | `Show` | exactly 1 | ✱ | ✱ `onClose` (this is where the reward is granted), optional `onShow` |
| `NativeBanner` | `Show` | exactly 1 | ✱ | optional `onShow`, `onClose` |
| `NativeBanner` | `Hide` | exactly 1 | – | – |
| `NativeMrec` | `Load` / `Hide` | exactly 1 | – | – |
| `NativeMrec` | `Show` | exactly 1 | ✱ | optional `onShow`, `onClose`; `adPos` |
| `Interstitial` | `Load` | exactly 1 | – | – |
| `Interstitial` | `Show` | exactly 1 | ✱ | `onClose` recommended (warning if missing) |
| `Rewarded` | `Load` | exactly 1 | – | – |
| `Rewarded` | `Show` | exactly 1 | ✱ | ✱ `onReward` |
| `AppOpen` | `Load` | exactly 1 | – | – |
| `AppOpen` | `Show` | exactly 1 | ✱ | `onClose` recommended |
| `Banner` | `Load` / `Hide` | exactly 1 | – | – |
| `Banner` | `Show` | exactly 1 | ✱ | – |
| `Mrec` | `Load` / `Hide` | exactly 1 | – | – |
| `Mrec` | `Show` | exactly 1 | ✱ | `adPos` |

Use the native formats by default. Standard formats (`Interstitial`, `Rewarded`, `AppOpen`, `Banner`, `Mrec`) are
only for projects that already use them.

## Validation checklist (per row)

1. Format and action are in the table above (case-sensitive). Unknown → invalid. Suggest the closest name.
2. `order` values are valid `PlacementOrder` names, and the count matches the table.
3. Required `pos` present. If missing, propose `<method_name_in_snake_case>` as a warning.
4. Every callback (`onClose`, `onShow`, `onReward`) names a method that **exists in the same class** and takes **no
   parameters**. A missing or parameterised method → invalid; ask the user which method to use.
5. Required callbacks present (✱). Missing → invalid; ask the user for the method.
6. The comment is inside a method body. If not → invalid.
7. Duplicates: the same format, action and order in the same method twice → warning.
8. Pairing hints (warnings only): a `Show` of a native fullscreen format with no `Load` of the same format anywhere in
   the project, unless the format is preloaded by AdsSettings (Native Banner, Native Reward, Native AppOpen).

## Scanning

Search `Assets/**/*.cs`, excluding `Assets/Editor/BFunAgent/`, `Assets/Plugins/` and any third-party SDK folders:

```
regex: //\s*TODO ADS\s+(\w+)\s+(\w+)(.*)$
```

Also report near-misses so the user can fix them: `TODO ads`, `TODO: ADS`, `TODO Ads`, or `TODO` lines that mention
`Load`/`Show` together with `inter`, `reward`, `banner` or `native`. List them as "not in the convention" with a
suggested rewrite. Do not convert them silently.

Lines already marked `// ADS:` were converted in an earlier run. Skip them.

## Statuses

Each parsed row gets one status, which decides how it appears in the review ([review.md](review.md)):

| Status | Meaning | In the review |
|---|---|---|
| ✅ | passes every check | summarised on one line, no question |
| ⚠️ | only warnings, with a proposed fix | a choice row, option A = accept the proposal |
| ❌ | a required value is missing or wrong | a choice row, option A = best guess, or "type a value" |
| near-miss | not in the convention | a choice row (`N…`), option A = suggested rewrite |
