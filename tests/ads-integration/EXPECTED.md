# Expected steps 8–9 result for AdsTodoSample.cs

The agent shows one combined review ([review.md](../../skills/ads-integration/references/review.md)):

- one summary line, then the ✅ rows collapsed to one line (cases 1, 2, 3, 7, 10 below)
- a **multiple-choice picker** with three separate questions: `S1` (next scene, recommended `SampleScene`),
  `S2` (OnInitFirebaseDone, starting with "used to fetch and apply this project's own Firebase Remote Config"),
  `S3` (OnCompleteSplash, starting with "used to load and show the Native Banner"). Skip is recommended for S2/S3
  because no component exists in AdsSplash.
- after the picker: a **choice table for the TODOs only**, one row per ⚠️/❌ case with column "A (recommended)",
  and `N1`/`N2` for the near-misses
- the line "Nothing has been applied yet. Please choose: …" with the reply syntax (`all A` accepts every recommendation)
- the "⚠️ NEEDS YOUR ATTENTION" block as the very last thing, then the agent **stops and waits**

After you answer, it shows only the changed rows, then the notice block again (now listing "leave it" rows,
skipped callbacks and the "Check" items accepted through `all A`).

## Validation per case

| Case | Line content | Expected status | What the agent should say or do |
|---|---|---|---|
| 1 | `NativeInter Load order=One,Two` | ✅ | – |
| 2 | `NativeInter Show order=One,Two pos=level_complete onClose=GoNextLevel` | ✅ | – |
| 3 | `NativeReward Load order=One` | ✅ | – |
| 4 | `NativeReward Show order=One pos=revive` | ❌ | ask for `onClose` (`GrantRevive` is a likely candidate, but it must ask) |
| 5 | `NativeInter Show order=One pos=pause onClose=ResumeGame` | ❌ | needs exactly 2 orders; ask for the second one |
| 6 | `NativeBanner Show order=One` | ⚠️ | propose a `pos` (for example `start`) |
| 7 | `NativeBanner Hide order=One` | ✅ | – |
| 8 | `NativeInter Show ... onClose=OpenShop` | ❌ | `OpenShop` not found in the class; ask which method |
| 9 | `NativeReward Show ... onClose=AddCoins` | ❌ | `AddCoins(int)` has a parameter; ask for a parameterless method |
| 10 | `Rewarded Show ... onReward=GiveDoubleCoins` | ✅ | – |
| 11 | `Interstitial Show order=One pos=back_to_menu` | ⚠️ | `onClose` recommended |
| 12 | `NativeInters Show ...` | ❌ | unknown format, suggest `NativeInter` |
| 13 | class-level `NativeBanner Show` | ❌ | not inside a method body |
| 14a | `// TODO: show reward ads here` | near-miss | listed separately with a suggested rewrite, not converted |
| 14b | `// TODO ads NativeInter show ...` | near-miss | listed separately, not converted |
| 15 | `// ADS: NativeBanner Hide order=One` | skipped | not listed at all |

Totals: 13 `TODO ADS` rows (5 ✅, 2 ⚠️, 6 ❌) plus 2 near-misses.

The agent must stop after the review and must not edit code until every row it converts is ✅ and you confirm.

If you answer `all A`, the notice block must list a **Check** line for each value the agent picked for you
(for example `onClose=GrantRevive` for revive).

## Expected step 10 result (after you answer)

For example, if you answer: case 4 → `GrantRevive`, case 5 → `order=One,Two`, case 6 → `pos=start`,
case 8 → `ResumeGame`, case 9 → `GrantRevive`, case 11 → `ResumeGame`, case 12 → `NativeInter`, and move case 13 into `Start()`:

- The TODO comments are updated first, then re-validated (only the changed rows are shown again).
- Each `// TODO ADS` becomes `// ADS:` with the generated call on the next line.
- `using TheLegends.Base.Ads;` is added at the top.
- Case 2: `AdsCaller.ShowNativeInterLoopMax(PlacementOrder.One, PlacementOrder.Two, "level_complete", null, () => GoNextLevel(), <default cfg>, <meta cfg>);`
- Case 3: `AdsManager.Instance.LoadNativeReward(PlacementOrder.One);`
- Case 4: `AdsCaller.ShowNativeRewardNoLoop(PlacementOrder.One, "revive", null, () => GrantRevive(), null, <default cfg>, <meta cfg>);`
- Case 10: `AdsManager.Instance.ShowRewarded(PlacementOrder.One, "double_coins", () => GiveDoubleCoins());`
- Near-misses stay untouched. The project compiles (bridge `state: ready`).
