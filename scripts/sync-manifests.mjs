#!/usr/bin/env node
// Keeps the values every platform manifest shares (version, author, homepage, repository,
// license, keywords, category) in one place: scripts/plugin-metadata.json.
//
//   node scripts/sync-manifests.mjs          write the shared values into every manifest
//   node scripts/sync-manifests.mjs --check  fail if any manifest has drifted (CI runs this)
//
// To release: change "version" in plugin-metadata.json, then run without --check.
// Platform-only fields (descriptions, Codex "interface", Antigravity prompts) are not touched.
import { readFileSync, writeFileSync } from 'node:fs';

const PLUGIN = 'bfun';
const check = process.argv.includes('--check');
const meta = JSON.parse(readFileSync('scripts/plugin-metadata.json', 'utf8'));
const shared = ['version', 'author', 'homepage', 'repository', 'license', 'keywords'];

const assign = (m) => {
  for (const k of shared) m[k] = meta[k];
};
const entry = (m) => {
  const e = m.plugins?.find((p) => p.name === PLUGIN);
  if (!e) throw new Error(`marketplace has no "${PLUGIN}" plugin entry`);
  return e;
};

const targets = [
  { file: '.claude-plugin/plugin.json', set: (m) => assign(m) },
  {
    file: '.codex-plugin/plugin.json',
    set: (m) => {
      assign(m);
      m.interface.category = meta.category;
    },
  },
  {
    // Antigravity's strict validation rejects an unknown "category" field.
    file: '.antigravity-plugin/plugin.json',
    set: (m) => {
      assign(m);
      delete m.category;
    },
  },
  {
    // Claude marketplace entry must not carry a version: plugin.json wins at install.
    file: '.claude-plugin/marketplace.json',
    set: (m) => {
      const e = entry(m);
      delete e.version;
      e.category = meta.category;
    },
  },
  {
    file: '.agents/plugins/marketplace.json',
    set: (m) => {
      entry(m).category = meta.category;
    },
  },
];

let drift = 0;
for (const { file, set } of targets) {
  const before = readFileSync(file, 'utf8');
  const manifest = JSON.parse(before);
  set(manifest);
  const after = JSON.stringify(manifest, null, 2) + '\n';
  if (before.replace(/\r\n/g, '\n') === after) continue;
  if (check) {
    console.error(`${file} has drifted from scripts/plugin-metadata.json`);
    drift++;
  } else {
    writeFileSync(file, after);
    console.log(`updated ${file}`);
  }
}
if (drift) {
  console.error('Run: node scripts/sync-manifests.mjs');
  process.exit(1);
}
console.log(check ? 'manifests in sync' : 'done');
