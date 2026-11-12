# BFun Agent Plugins

BFun's internal plugin for coding agents. It packages the team's know-how for Unity game
development, ads integration (Ads Manager, AdMob, mediation), Firebase, and monetization
as **agent skills**, so any supported agent can load the right guidance when a task needs it.

Works with **Claude Code**, **Codex**, **Antigravity**, and **GitHub Copilot CLI**.

| Skill | What it does |
|---|---|
| [`ads-integration`](skills/ads-integration/SKILL.md) | Integrates TripSoft Ads Manager into a Unity project end to end: packages, AdsSettings, Firebase/AppsFlyer, AdsSplash, and turning `TODO ADS` comments into ad calls |

## Install

**Claude Code**

```bash
claude plugin marketplace add Unity-UPM-Packages/BFun-Agent-Plugins
claude plugin install bfun@bfun-agent-plugins
```

Or inside a session: `/plugin marketplace add Unity-UPM-Packages/BFun-Agent-Plugins`, then `/plugin install bfun@bfun-agent-plugins`.

**Codex**

```bash
codex plugin marketplace add Unity-UPM-Packages/BFun-Agent-Plugins
codex plugin add bfun@bfun-agent-plugins
```

**Antigravity**

```bash
agy plugin install https://github.com/Unity-UPM-Packages/BFun-Agent-Plugins
```

The Antigravity app and IDE have no "install from GitHub" button. Install with the CLI above (it syncs to the app),
or clone this repo and copy it to `~/.gemini/config/plugins/bfun/` (all workspaces) or `<project>/.agents/plugins/bfun/`
(one project).

**GitHub Copilot CLI**

```bash
copilot plugin marketplace add Unity-UPM-Packages/BFun-Agent-Plugins
copilot plugin install bfun@bfun-agent-plugins
```

### Updating

| Agent | Command |
|---|---|
| Claude Code | `claude plugin marketplace update bfun-agent-plugins`, then update `bfun` (or the 🔄 button in Manage Plugins) |
| Codex / Copilot CLI | update the marketplace, then the plugin, with their `plugin` commands |
| Antigravity | `agy plugin uninstall bfun`, then install again |

Start a new agent session afterwards so the new version is loaded.

### Manual install (any agent that reads a skills folder)

Link or copy each folder in `skills/` into the agent's personal skills directory:
`~/.claude/skills` (Claude Code), `~/.agents/skills` (Codex), `~/.copilot/skills` (Copilot).
On Windows, copy the folders into `%USERPROFILE%\.claude\skills` and so on.

## How to use

Skills load automatically when your request matches what they do. You can also call one explicitly:

| Agent | Call a skill explicitly |
|---|---|
| Claude Code | `/bfun:ads-integration` |
| Antigravity | `/bfun:ads-integration` |
| Codex | `/bfun:ads-integration` |
| Copilot | describe the task, the agent picks the skill |

### ads-integration

Integrates `com.thelegends.ads.manager` into a Unity project, from an empty manifest to working ad calls.

**Before you start**

- Unity 2021.3 or newer. The project must compile without errors.
- Commit your work: the skill edits `Packages/manifest.json`, scenes, settings assets and scripts.
- Keep the Unity Editor available. The skill opens the project or brings Unity to the front when it needs to.
- Mark where ads should go with `TODO ADS` comments (see below). Optional, but that is what step 10 converts.

**Start it** by asking, for example:

> Integrate Ads Manager into this Unity project.

**What it does**

| Phase | Steps |
|---|---|
| Packages | adds the TripSoft and OpenUPM registries, Ads Manager (latest non-preview), the AdMob adapters for that version, UnityMainThreadDispatcher and EditorAttributes (pinned per Unity version), all in one edit, then triggers one Unity import |
| Editor setup | creates and saves AdsSettings (AdMob, Firebase, AppsFlyer, native preload), installs Firebase/AppsFlyer Manager through Ads Manager's own SAVE logic, creates AppsFlyer Setting |
| Project wiring | copies AdsSplash as build scene 0, asks you a few questions, then replaces the `TODO ADS` comments with `AdsManager` / `AdsCaller` calls and checks that the project compiles |

Editor actions go through a small temporary Editor script (`Assets/Editor/BFunAgent/`), removed at the end.

**When it asks you something** (at most three times):

1. versions that differ from what is already in your manifest
2. Unity's "Importing N scoped registries" popup: click **Close** so Unity can continue
3. one review: the scene after the splash and the two splash callbacks (clickable choices), then a table of TODO
   fixes. Answer with codes like `1A 6A 7B:ResumeGame`, or `all A` to accept the recommendations.

Nothing is applied until you answer. Every message that leaves work for you ends with a
**⚠️ NEEDS YOUR ATTENTION** block. Read that block at least.

**Marking ad placements**

```csharp
// TODO ADS NativeInter Show order=One,Two pos=level_complete onClose=GoNextLevel
// TODO ADS NativeReward Show order=One pos=revive onClose=GrantRevive
// TODO ADS NativeBanner Show order=One pos=home
```

`onClose` names a parameterless method in the same class. It is required for `NativeInter Show` and
`NativeReward Show` (for rewards, it is where the reward is granted). Full syntax and rules:
[todo-convention.md](skills/ads-integration/references/todo-convention.md).

**Still manual afterwards:** AdMob App IDs, ad unit IDs in AdsSettings, `google-services.json` /
`GoogleService-Info.plist`, AppsFlyer dev key and Apple app ID, and a test build on a device.

## Repository layout

```
skills/                       Skill content, shared by every agent (one folder per skill)
.claude-plugin/               Claude Code manifest + marketplace
.codex-plugin/                Codex manifest
.agents/plugins/              Generic marketplace (Codex, Copilot, other agents)
.antigravity-plugin/          Antigravity manifest
scripts/plugin-metadata.json  Single source of truth for version, author, keywords...
scripts/sync-manifests.mjs    Copies the shared metadata into every manifest
scripts/check-skills.mjs      Validates every skills/*/SKILL.md
tests/                        Test fixtures for the skills (not loaded by agents)
assets/                       Icons referenced by the manifests
```

## Adding a skill

1. Create `skills/<skill-name>/SKILL.md` (kebab-case, folder name = `name`):

   ```markdown
   ---
   name: my-skill
   description: What it does. Use when the user asks to ... Not for ...
   ---

   Step-by-step instructions for the agent...
   ```

2. Put long code samples or docs in `skills/<skill-name>/references/` and files the agent copies or runs in
   `skills/<skill-name>/scripts/`, so they are only read when needed.
3. Keep skill text agent-neutral: no tool names or syntax specific to one agent.
4. Run `node scripts/check-skills.mjs`.
5. Add a row to the skill table at the top of this README and a section under **How to use**.

## Releasing

Every agent has its own manifest, and each one repeats the version, author and keywords. To change those in one
place:

1. Bump `version` in `scripts/plugin-metadata.json`.
2. Run `node scripts/sync-manifests.mjs` locally. It copies the shared values into all four manifests.
3. Commit and push.

On every push and pull request, the GitHub Action in `.github/workflows/validate.yml` runs
`node scripts/sync-manifests.mjs --check` (fails if a manifest was edited by hand or the sync was forgotten) and
`node scripts/check-skills.mjs` (fails on a broken `SKILL.md`, which agents would otherwise drop silently).
