# BFun Agent Plugins

BFun's internal plugin for coding agents. It packages the team's know-how for Unity game
development, ads integration (Ads Manager, AdMob, mediation), Firebase, and monetization
as **agent skills**, so any supported agent can load the right guidance when a task needs it.

Works with **Claude Code**, **Codex**, **Antigravity**, and **GitHub Copilot CLI**.

## Layout

```
skills/                       Skill content, shared by every agent (one folder per skill)
.claude-plugin/               Claude Code manifest + marketplace
.codex-plugin/                Codex manifest
.agents/plugins/              Generic marketplace (Codex, Copilot, other agents)
.antigravity-plugin/          Antigravity manifest
scripts/plugin-metadata.json  Single source of truth for version, author, keywords...
scripts/sync-manifests.mjs    Copies the shared metadata into every manifest
scripts/check-skills.mjs      Validates every skills/*/SKILL.md
assets/                       Icons referenced by the manifests
```

## Adding a skill

1. Create `skills/<skill-name>/SKILL.md` (kebab-case, folder name = `name`):

   ```markdown
   ---
   name: integrate-rewarded-ads
   description: Integrates rewarded ads with BFun Ads Manager. Use when the user asks to show rewarded video, grant rewards, or debug reward callbacks. Not for interstitial or banner ads.
   ---

   Step-by-step instructions for the agent...
   ```

2. Put long code samples or docs in `skills/<skill-name>/references/` and point to them
   from `SKILL.md`, so the agent only reads them when needed.
3. Keep skill text agent-neutral: no tool names or syntax specific to one agent.
4. Run `node scripts/check-skills.mjs`.

## Releasing

1. Bump `version` in `scripts/plugin-metadata.json`.
2. Run `node scripts/sync-manifests.mjs`.
3. Commit and push. CI runs both checks.

## Install

Replace `QuangHaiVu/BFun-Agent-Plugins` with the actual repo, or use a local path.

**Claude Code**

```bash
claude plugin marketplace add QuangHaiVu/BFun-Agent-Plugins
claude plugin install bfun@bfun-agent-plugins
```

**Codex**

```bash
codex plugin marketplace add QuangHaiVu/BFun-Agent-Plugins
codex plugin add bfun@bfun-agent-plugins
```

**Antigravity**

```bash
agy plugin install https://github.com/QuangHaiVu/BFun-Agent-Plugins
```

**GitHub Copilot CLI**

```bash
copilot plugin marketplace add QuangHaiVu/BFun-Agent-Plugins
copilot plugin install bfun@bfun-agent-plugins
```

### Manual install (any agent that reads a skills folder)

Link or copy each folder in `skills/` into the agent's personal skills directory:
`~/.claude/skills` (Claude Code), `~/.agents/skills` (Codex), `~/.copilot/skills` (Copilot).
On Windows, copy the folders into `%USERPROFILE%\.claude\skills` and so on.
