# Combined review (steps 8–9)

One review covers the splash scene and every ad TODO, in two parts:

1. **Splash (S1–S3): the agent's multiple-choice question tool.** Three clickable questions: next scene,
   `OnInitFirebaseDone`, `OnCompleteSplash`.
2. **TODOs: one choice table** in the message, answered with short codes.

Afterwards a notice block lists what is still open, because users skim long messages and miss the parts that need them.

## Stop and wait (hard rule)

The review is a **question**. Do not run `configure-splash`, edit any TODO, or generate code until the user has
answered, and never treat the recommendations as answers. A recommendation is a suggestion, never a default the
agent may apply on its own. After printing the TODO table, **end your turn** and wait for the reply.

## Order of the review

1. **Short summary message:** one line ("Found 13 TODO ADS: 5 ready, 8 need a decision."), plus the ready rows
   collapsed to one line (or just a count when there are more than ~8):
   `✅ 5 ready: AdsTodoSample.cs:18 NativeInter Load · :21 NativeReward Load · :31 NativeInter Show · …`
2. **Splash picker:** call the multiple-choice tool with S1, S2, S3 ([below](#part-1-splash-picker-s1s3)).
3. **TODO table:** after the splash answers come back, print the choice table, the reply line and the notice block,
   then end the turn ([below](#part-2-todo-choice-table)).

If there are no TODO decisions (every row ✅, no near-misses), skip part 2. If the environment has **no**
multiple-choice tool, put S1–S3 as the first rows of the TODO table instead.

## Part 1: splash picker (S1–S3)

Use the agent's structured question tool: `AskUserQuestion` in Claude Code, the question picker in Antigravity,
Codex's user-input request, and so on. Three separate questions, never merged. In each question the **first option is
the recommendation**, marked "(Recommended)" or the tool's equivalent. A "type another value" choice uses the tool's
free-text / "Other" answer.

**S1: scene after the splash (required).**
> [S1] Which scene opens after the splash?

Options: `buildScenes` without AdsSplash first, then other `.unity` files under `Assets/` (at most what the tool
allows). Recommend the first build scene. Free text for any other name.

### Required wording for S2 and S3

The purpose comes **first**, then when it runs. "When it runs" alone is not enough: users skip what they cannot see a
use for. Translate into the user's language, but keep every part.

**S2 question:**
> [S2] OnInitFirebaseDone: **used to fetch and apply this project's own Firebase Remote Config** (difficulty, feature
> flags, prices, ad frequency…). Runs once Firebase and Remote Config are ready, before ads start and before the game
> opens. Hook a method here?

- skip option: "Skip. The game must read its Remote Config somewhere else, and the first screens may use default values."
- method option: "Give a method: public void, no parameters, on an object in the AdsSplash scene (for example `RemoteConfigLoader.Fetch`)."

**S3 question:**
> [S3] OnCompleteSplash: **used to load and show the Native Banner** (with `AdsCaller.ShowNativeBanner`). Runs when
> the splash has finished and the next scene is open. Hook a method here?

- skip option: "Skip. The Native Banner must then be shown from the next scene, with a `TODO ADS NativeBanner Show` there."
- method option: "Give a method: public void, no parameters, on an object in the AdsSplash scene that survives the scene change."

When skip is the recommendation because no suitable component exists, put it first, marked recommended, and add that
reason to its description ("the AdsSplash scene has no component of this project yet"). The purpose sentence stays.
Callback rules: [splash.md](splash.md#explaining-the-two-callbacks).

## Part 2: TODO choice table

| # | Where | What to decide | A (recommended) | B | C |
|---|---|---|---|---|---|
| 1 | [AdsTodoSample.cs:10](…) NativeBanner Show | outside any method | move into `Start()` | delete the TODO | leave it, convert nothing |
| 4 | [AdsTodoSample.cs:24](…) NativeBanner Show | `pos` missing | `pos=start` | type another pos | leave it |
| 6 | [AdsTodoSample.cs:37](…) NativeReward Show | `onClose` missing (grants the reward) | `GrantRevive` | type another method | leave it |
| 7 | [AdsTodoSample.cs:43](…) NativeInter Show | needs 2 orders | `order=One,Two` | type orders | leave it |
| N1 | [AdsTodoSample.cs:85](…) `// TODO: show reward ads here` | not in the convention | rewrite as `NativeReward Show order=One pos=daily_reward`, ask for `onClose` | leave it | – |

Rows: ❌ rows first, then ⚠️ rows, then near-misses (`N…`). Rules:

- Label the column **"A (recommended)"** in the user's language, so nobody reads it as already chosen.
- **Option A is the recommendation** when there is a sensible one: an existing method with a matching name, the
  obvious second order, a pos derived from the method name. If there is no sensible guess, A is "type a value".
- Every row has a **"leave it"** option: nothing is converted, and the row goes into the notice block.
- Offer **"delete the TODO"** only where the TODO looks wrong rather than incomplete (outside a method, unknown format
  with no close match).
- Keep cells short: one decision per row, no paragraphs. Make file:line a link when the environment renders links.

Under the table, ask directly:

> **Nothing has been applied yet. Please choose:** reply with codes, e.g. `1A 4A 6A 7A 8B:ResumeGame`, or **`all A`**
> to accept every recommendation. Anything you leave out stays as "leave it".

Then the notice block, using what is already known, and **end the turn**.

Parsing the reply:

- `<row><letter>` picks an option. `<row><letter>:<value>` fills a "type …" option.
- `all A`, or a phrase meaning "go with your suggestions" in the user's language, selects A on every row. Rows whose
  A is "type a value" still need an answer. Ask about those rows alone.
- Free text is fine too. Map it to rows. If something is ambiguous, ask about that row alone.
- Rows the user does not mention count as **leave it**. Never invent an answer for them.

## After the answers

1. Send `configure-splash` with the scene and any callbacks that can be wired.
2. Rewrite the TODO comments with the chosen values, delete the ones marked "delete", and leave "leave it" rows untouched.
3. Re-validate. Show only the rows that changed, with their new status. If a row is still invalid, ask about that row alone.
4. Print the notice block (below), then continue with step 10 for every ✅ row.

## The notice block

A short, separate block that is always the **last** thing in the message. It must stand out from the report above it.

Use exactly this shape, translated into the user's language:

```markdown
---

## ⚠️ NEEDS YOUR ATTENTION (4)

1. **AdsTodoSample.cs:55**: `NativeReward Show` left unconverted (you chose "leave it"). No reward ad here until it is fixed.
2. **AdsTodoSample.cs:85**: `// TODO: show reward ads here` is not in the convention, so it was skipped.
3. **Splash OnCompleteSplash**: not wired. The Native Banner now comes from the TODO in `AdsTodoSample.Start()`.
4. **Check**: you accepted `onClose=GrantRevive` for **revive** (:37) via "all A". Make sure that method really grants the revive.
```

What goes in, in this order:

1. TODO rows left unconverted or still invalid, with the consequence ("no ad will show here")
2. near-misses that were not rewritten
3. splash settings not applied (callbacks skipped or not wireable) and what replaces them, if anything
4. **"Check" items**: every value the agent chose for the user through "all A", or by mapping free text. These are the
   places where a wrong answer slips in silently.
5. at Finish only: the manual follow-ups (App IDs, ad unit IDs, Firebase config files, AppsFlyer dev key, device test)

Rules:

- one line per item: **where** in bold, then what happened, then the consequence or action
- no more than ~10 items. Group the rest ("…and 4 more unconverted TODOs in Shop.cs").
- if nothing is open, still print the block with "Nothing left for you to handle."
- print it after steps 8–9, and again, complete, at the end of the integration
