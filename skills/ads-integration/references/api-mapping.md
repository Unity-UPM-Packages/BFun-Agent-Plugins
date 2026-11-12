# TODO ADS → code (step 10)

Insert the call on the line **below** the TODO, with the same indentation, and rewrite the marker from `TODO ADS` to
`ADS:` (keep the rest of the comment). Example:

```csharp
// ADS: NativeInter Show order=One,Two pos=level_complete onClose=GoNextLevel
AdsCaller.ShowNativeInterLoopMax(PlacementOrder.One, PlacementOrder.Two, "level_complete", null, () => GoNextLevel(),
    new NativePlatformShowBuilder.CountdownConfig
    {
        CountdownDurationSeconds = AdsManager.Instance.adsConfigs.nativeVideoCountdownTimerDuration,
        CloseButtonDelaySeconds = AdsManager.Instance.adsConfigs.nativeVideoCloseClickableDelay,
        InitialDelaySeconds = AdsManager.Instance.adsConfigs.nativeVideoDelayBeforeCountdown
    },
    new NativePlatformShowBuilder.CountdownConfig
    {
        CountdownDurationSeconds = AdsManager.Instance.adsConfigs.nativeMetaCountdownTimerDuration,
        CloseButtonDelaySeconds = AdsManager.Instance.adsConfigs.nativeMetaCloseClickableDelay,
        InitialDelaySeconds = AdsManager.Instance.adsConfigs.nativeMetaDelayBeforeCountdown
    });
```

General rules:

- Add `using TheLegends.Base.Ads;` to the file if it is missing. `using UnityEngine;` is needed for `Vector2Int` (MREC).
- Callbacks are always lambdas, `() => Method()`. Absent optional callbacks are `null`.
- `A`, `B` are the first and second `order` values, written as `PlacementOrder.One` and so on. `P` is the `pos` string.
- No `#if USE_ADMOB` guards are needed: step 5 turned `USE_ADMOB` on, and Ads Manager's native API requires it anyway.
- Do not change the surrounding logic. If the TODO sits where the callback flow obviously needs restructuring (for
  example the code after the TODO must only run after the ad closes), point it out to the user. Do not rewrite it yourself.

`{DEFAULT_CFG}` and `{META_CFG}` stand for the two `CountdownConfig` objects exactly as in the example above (copied
from Ads Manager's DemoManager).

## Native formats (through AdsCaller)

| TODO | Code |
|---|---|
| `NativeInter Load order=A,B` | `AdsCaller.LoadNativeInter(A, B);` |
| `NativeInter Show order=A,B pos=P onClose=C [onShow=S]` | `AdsCaller.ShowNativeInterLoopMax(A, B, "P", S_or_null, () => C(), {DEFAULT_CFG}, {META_CFG});` |
| `NativeReward Load order=A` | `AdsManager.Instance.LoadNativeReward(A);` |
| `NativeReward Show order=A pos=P onClose=C [onShow=S]` | `AdsCaller.ShowNativeRewardNoLoop(A, "P", S_or_null, () => C(), null, {DEFAULT_CFG}, {META_CFG});` |
| `NativeBanner Show order=A pos=P [onShow=S] [onClose=C]` | `AdsCaller.ShowNativeBanner(A, "P", S_or_null, C_or_null, null, null);` |
| `NativeBanner Hide order=A` | `AdsManager.Instance.HideNativeBanner(A);` |
| `NativeMrec Load order=A` | `AdsManager.Instance.LoadNativeMrec(A);` |
| `NativeMrec Show order=A pos=P [adPos=X] [onShow=S] [onClose=C]` | `AdsCaller.ShowNativeMrec(A, "P", S_or_null, C_or_null, null, AdsPos.X, Vector2Int.zero);` |
| `NativeMrec Hide order=A` | `AdsManager.Instance.HideNativeMrec(A);` |

Notes:

- Native Reward uses **one** placement: load through `AdsManager.Instance.LoadNativeReward`, show through
  `AdsCaller.ShowNativeRewardNoLoop`. Its `onClose` runs after a 1 s loading screen and is the place to grant the
  reward. There is no separate reward callback.
- Native Inter uses **two** placements for both load and show (`LoopMax` alternates between them).
  `ShowNativeInterLoopMax` calls `onClose` straight away when the inter cooldown has not elapsed. That is expected:
  the game must continue in `onClose` either way.
- `ShowNativeBanner` auto-reloads and shows when loaded, so no `NativeBanner Load` is needed. Native Banner is also
  preloaded by AdsSettings.

## Standard formats (through AdsManager.Instance)

| TODO | Code |
|---|---|
| `Interstitial Load order=A` | `AdsManager.Instance.LoadInterstitial(AdsType.Interstitial, A);` |
| `Interstitial Show order=A pos=P [onClose=C]` | `AdsManager.Instance.ShowInterstitial(AdsType.Interstitial, A, "P", C_lambda_or_null);` |
| `Rewarded Load order=A` | `AdsManager.Instance.LoadRewarded(A);` |
| `Rewarded Show order=A pos=P onReward=R` | `AdsManager.Instance.ShowRewarded(A, "P", () => R());` |
| `AppOpen Load order=A` | `AdsManager.Instance.LoadAppOpen(A);` |
| `AppOpen Show order=A pos=P [onClose=C]` | `AdsManager.Instance.ShowAppOpen(A, "P", C_lambda_or_null);` |
| `Banner Load order=A` | `AdsManager.Instance.LoadBanner(A);` |
| `Banner Show order=A pos=P` | `AdsManager.Instance.ShowBanner(A, "P");` |
| `Banner Hide order=A` | `AdsManager.Instance.HideBanner(A);` |
| `Mrec Load order=A` | `AdsManager.Instance.LoadMrec(AdsType.Mrec, A);` |
| `Mrec Show order=A pos=P [adPos=X]` | `AdsManager.Instance.ShowMrec(AdsType.Mrec, A, AdsPos.X, Vector2Int.zero, "P");` |
| `Mrec Hide order=A` | `AdsManager.Instance.HideMrec(AdsType.Mrec, A);` |

## Before trusting this table

These signatures were taken from Ads Manager 1.1.86. If the installed version differs, open the package's
`Runtime/AdsManager/Scripts/Caller/*.cs` and `AdsManager.cs` (read-only, under `Library/PackageCache/` or
`Packages/`) and check that each method you are about to call exists with the same parameters. If not, stop and
show the user the difference.

## After inserting

Send the bridge `refresh`, wait for `ready`. On `compile-failed`, fix only the errors in lines you added, then
refresh again.
