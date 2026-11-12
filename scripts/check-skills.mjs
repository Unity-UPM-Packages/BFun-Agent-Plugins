#!/usr/bin/env node
// Validates every skills/*/SKILL.md. Agents silently drop a skill whose frontmatter is
// broken, so this is the only place that defect becomes visible.
//
//   node scripts/check-skills.mjs
//
// Checks: frontmatter block exists; `name` is non-empty, kebab-case and matches its folder;
// `description` is non-empty, has no angle brackets, and fits in 1024 chars;
// root README.md has at least 40 words.
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const root = 'skills';
const failures = [];
let checked = 0;

// Minimal parser for flat `key: value` frontmatter (quoted values and >/| blocks supported).
function parseFrontmatter(text) {
  const m = text.match(/^---\r?\n([\s\S]*?)\r?\n---(?:\r?\n|$)/);
  if (!m) return null;
  const data = {};
  const lines = m[1].split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    const kv = lines[i].match(/^([A-Za-z0-9_-]+):\s*(.*)$/);
    if (!kv) continue;
    let [, key, value] = kv;
    if (['>', '|', '>-', '|-'].includes(value)) {
      const block = [];
      while (i + 1 < lines.length && /^\s+/.test(lines[i + 1])) block.push(lines[++i].trim());
      value = block.join(value.startsWith('>') ? ' ' : '\n');
    } else if (/^(['"]).*\1$/.test(value)) {
      value = value.slice(1, -1);
    }
    data[key] = value.trim();
  }
  return data;
}

for (const dir of readdirSync(root, { withFileTypes: true })) {
  if (!dir.isDirectory()) continue;
  const path = join(root, dir.name, 'SKILL.md');
  if (!existsSync(path)) {
    failures.push(`${path}: missing`);
    continue;
  }
  const data = parseFrontmatter(readFileSync(path, 'utf8'));
  checked++;
  if (!data) {
    failures.push(`${path}: no YAML frontmatter block`);
    continue;
  }
  if (!data.name) failures.push(`${path}: missing name`);
  else if (data.name !== dir.name) failures.push(`${path}: name "${data.name}" must match folder "${dir.name}"`);
  else if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(data.name)) failures.push(`${path}: name must be kebab-case`);
  if (!data.description) failures.push(`${path}: missing description`);
  else {
    if (/[<>]/.test(data.description)) failures.push(`${path}: description must not contain < or >`);
    if (data.description.length > 1024) failures.push(`${path}: description longer than 1024 chars`);
  }
}

const words = readFileSync('README.md', 'utf8').split(/\s+/).filter(Boolean).length;
if (words < 40) failures.push(`README.md: ${words} words, needs at least 40`);

if (failures.length) {
  for (const f of failures) console.error(f);
  process.exit(1);
}
console.log(`${checked} skill(s) OK`);
