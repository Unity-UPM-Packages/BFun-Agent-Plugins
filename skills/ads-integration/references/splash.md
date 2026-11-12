# AdsSplash (steps 7–8)

## What the splash does

`AdsSplash.unity` ships in `Packages/com.thelegends.ads.manager/Runtime/AdsManager/Scenes/`. Its
`AdsSplashController` runs, in order:

1. consent (UMP) and ATT
2. Firebase init and remote config fetch, then **`OnInitFirebaseDone`** (only when `USE_FIREBASE` is on)
3. AppsFlyer init (when `USE_APPSFLYER` is on), then other trackers
4. `AdsManager.DoInit()`, then **`OnInitAdsDone`**, then the initial (open) ads
5. `SceneManager.LoadSceneAsync(sceneName)`, which also starts the preloads ticked in AdsSettings
6. after the open ad or brand screen closes, **`OnCompleteSplash`**, which fires **after** the next scene has loaded

## Step 7: copy and set as start scene

Use the bridge `install-splash-scene` command. Default target: `Assets/Scenes/AdsSplash.unity`. Copy the scene, do
not reference the package copy: package scenes are read-only, and Build Settings should point at a project asset.
If the user already has a copy, the bridge keeps it and only fixes the build order.

## Splash decisions (asked in the combined review)

These three become rows `S1`–`S3` of the choice table in [review.md](review.md):

1. **Next scene (`sceneName`), required.** Candidates: `buildScenes` without AdsSplash, then any other `.unity` files
   under `Assets/`. The value is the file name without `.unity`.
2. **`OnInitFirebaseDone`, optional.**
3. **`OnCompleteSplash`, optional.**

### Explaining the two callbacks

Users often skip what they do not understand. Every time these callbacks are offered (table row, multiple-choice
question, or notice block), explain them in plain words with the four points below, translated into the user's
language. Do not just name them, and always lead with **what it is used for**. Exact question wording:
[review.md](review.md#required-wording-for-s2-and-s3).

| | `OnInitFirebaseDone` | `OnCompleteSplash` |
|---|---|---|
| **Used for (say this first)** | fetching and applying **this project's own Firebase Remote Config** (difficulty, feature flags, prices, ad frequency) before gameplay starts | **loading and showing the Native Banner** (with `AdsCaller`), plus anything else that must wait until the player sees the game (music, tutorial) |
| **When it runs** | right after Firebase has started and Remote Config has been fetched, **before** ads start and before the next scene opens | at the very end of the splash: the next scene has loaded and the opening ad or brand screen has closed |
| **If skipped** | nothing breaks. The game must read Remote Config somewhere else, and early screens may run with default values | nothing breaks. The first banner must be called from the next scene instead (a `TODO ADS NativeBanner Show` there) |
| **What it needs** | a `public void` method with no parameters on a component **inside the AdsSplash scene** | the same, and the object must survive the scene change (for example `DontDestroyOnLoad`), because it fires after the next scene has loaded |

When the project has no suitable component in the AdsSplash scene yet, still explain both. Make "skip" option A, and
say in the notice block how to enable them later: add a component with such a method to the AdsSplash scene, then re-run.

## Wiring rule (decided with the team)

Callbacks are wired **directly** only. They are serialized `UnityEvent`s, so the target must be:

- a component on a GameObject **inside the AdsSplash scene** (give its hierarchy path)
- a `public void` method with **no parameters**

If the user names a method that does not meet this (a static method, a method on an object in another scene, a
method with parameters), **do not** create bridge components or wrappers. Skip it and put it in the notice block.
Two common alternatives to mention there:

- the user can move or add the method's owner into the AdsSplash scene, then re-run the skill
- for the Native Banner, put `// TODO ADS NativeBanner Show order=One pos=<name>` in the first scene's start-up
  code and let step 10 generate the call

Keep in mind when suggesting a hook for `OnCompleteSplash`: it fires after the next scene is loaded, so the target
object must survive the scene change (for example a `DontDestroyOnLoad` object), or the call is lost.
