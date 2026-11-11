import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire, stripTypeScriptTypes } from 'node:module';
import vm from 'node:vm';

const require = createRequire(new URL('../client/package.json', import.meta.url));
const source = readFileSync(new URL('../client/src/navigation.ts', import.meta.url), 'utf8');
const executable = stripTypeScriptTypes(source)
  .replace(/^import .* from "lucide-react";/m, match => match.replace('import {', 'const {').replace('} from "lucide-react";', '} = require("lucide-react");'))
  .replace(/export /g, '') + '\nmodule.exports = { navigationSections, canOpenTab, matchesTab, taskGroups };';
const module = { exports: {} };
vm.runInNewContext(executable, { require, module });
const { navigationSections, canOpenTab, matchesTab, taskGroups } = module.exports;
const tabs = navigationSections.flatMap(s => s.tabs);
assert.equal(navigationSections.length, 6);
assert.equal(tabs.length, 36);
for (const section of navigationSections) {
  assert(taskGroups(section.tabs).length <= 3, section.label);
  for (const group of taskGroups(section.tabs)) assert(group.tabs.length <= 4, group.label);
}
const settingsArea = navigationSections.find(s => s.id === 'administration');
for (const path of ['/app/membership-settings', '/app/governance-settings', '/app/event-settings']) assert(settingsArea.tabs.some(t => t.to === path));
const visible = (permissions, disabled = []) => tabs.filter(t => canOpenTab(t, permissions, disabled));
const paths = visible(['members.view']).map(t => t.to);
assert(paths.includes('/app/members') && paths.includes('/app/membership-insights'));
assert(!paths.includes('/app/imports') && !paths.includes('/app/membership-settings'));
assert(visible([]).some(t => t.to === '/app/my-membership'));
assert(!visible(['events.view'], ['events']).some(t => t.to === '/app/events' || t.to === '/app/my-events'));
assert(visible(['clubs.manage']).some(t => t.to === '/app/governance'));
assert(!visible(['clubs.view']).some(t => t.to === '/app/club-administration'));
assert(visible(['governance.reports']).some(t => t.to === '/app/club-administration'));
assert(!visible(['intelligence.assistant'], ['assistant']).some(t => t.to === '/app/intelligence'));
assert.equal(new Set(tabs.map(t => t.to)).size, tabs.length);
for (const path of ['/app/members/123', '/app/clubs/123', '/app/committees/123', '/app/events/new', '/app/events/123/checkin', '/app/finance/member', '/app/operations/meetings/123']) {
  assert.equal(tabs.filter(t => matchesTab(path, t.to)).length, 1, path);
}
assert(!matchesTab('/app/leadership-calendar', '/app/leadership'));
assert(!matchesTab('/app/members', '/app'));
const app = readFileSync(new URL('../client/src/App.tsx', import.meta.url), 'utf8');
for (const tab of tabs) {
  if (tab.to === '/app') continue;
  const path = tab.to.slice('/app/'.length);
  assert(app.includes(`path="${path}"`) || app.includes(`path="${path}/*"`), `Missing route: ${tab.to}`);
}
console.log(`Navigation checks passed: ${navigationSections.length} sections, ${tabs.length} preserved feature destinations, permissions, disabled features and detail routes.`);

for (const [file, expected] of [['pages/Finance.tsx', 14], ['pages/Operations.tsx', 8], ['pages/Intelligence.tsx', 7], ['cards/IdCards.tsx', 6]]) {
  const content = readFileSync(new URL('../client/src/' + file, import.meta.url), 'utf8');
  const nav = content.match(/<ModuleNavigation[^>]+\/>/)[0];
  const items = JSON.parse(nav.match(/items=\{(\[.*?\])\} groups=/)[1]);
  const groups = JSON.parse(nav.match(/groups=\{(\{.*?\})\}/)[1]);
  assert.equal(items.length, expected, file);
  assert(Object.keys(groups).length <= 3, file);
  for (const [path] of items) assert.equal(Object.values(groups).flat().filter(p => p === path).length, 1, `${file}: ${path}`);
  for (const path of Object.values(groups).flat()) assert(items.some(i => i[0] === path), `${file}: orphan ${path}`);
}
console.log('All 35 nested module tools remain reachable through three task groups per module.');
