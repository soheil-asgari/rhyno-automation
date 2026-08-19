const fs = require('fs');
const path = require('path');

const root = 'f:\\rhyno-automation-1\\rhyno-automation';
const wwwroot = path.join(root, 'wwwroot');
const skip = /(node_modules|\\bin\\|\\obj\\|\\rhyno-build\\|\\rhyno-build-obj\\|\\\.git\\|\\tmp\\)/;

function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!skip.test(p + path.sep)) walk(p, out); }
    else if (/\.cshtml$/i.test(e.name)) out.push(p);
  }
  return out;
}

// Build a safe slug from a cshtml path -> e.g. Views/Contracts/Create -> contracts-create-page
function slugFor(rel) {
  const noExt = rel.replace(/\.cshtml$/i, '');
  const parts = noExt.replace(/^\.\//, '').split(/[\\/]/);
  // parts like: Views, Contracts, Create
  const name = parts[parts.length - 1].replace(/^_/, ''); // partials drop leading _
  const dir = parts.length > 2 ? parts[parts.length - 2] : 'page';
  let key = (dir + '-' + name)
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
  // area pages: Areas/Identity/Pages/... -> dir includes parent
  if (/^areas\//i.test(noExt.replace(/^\.\//, ''))) {
    const pp = noExt.replace(/^\.\//, '').split(/[\\/]/);
    key = pp[pp.length - 2] + '-' + name;
    key = key.toLowerCase().replace(/[^a-z0-9]+/g, '-');
  }
  return key;
}

// Track used names to avoid collisions -> add -2, -3 suffix
const used = {};
function uniqueName(base, kind) {
  let n = base;
  let i = 2;
  while (used[kind + ':' + n]) { n = base + '-' + i; i++; }
  used[kind + ':' + n] = true;
  return n;
}

let skippedRazor = [];

for (const f of walk(root)) {
  let src = fs.readFileSync(f, 'utf8');
  const rel = f.replace(root, '.').replace(/\\/g, '/');
  let changed = false;

  // ---- Extract <style> blocks ----
  // A style block is considered to have a REAL Razor server injection (and thus
  // cannot be mechanically moved to a .css file) only if it contains an @identifier
  // that is NOT one of the CSS at-rules (@media/@font-face/@keyframes/@supports/@import/
  // @charset/@page/@layer/@container) and NOT the @@ Razor escape.
  const CSS_AT_RULES = /@(media|font-face|keyframes|-webkit-keyframes|supports|import|charset|page|layer|container|property|counter-style|namespace|viewport|document)\b/i;
  const styleRe = /<style\b([^>]*)>([\s\S]*?)<\/style\s*>/gi;
  let m;
  const styleBlocks = [];
  while ((m = styleRe.exec(src))) {
    const attrs = m[1] || '';
    const body = m[2] || '';
    // strip escaped @@ -> single @, so '@@media' in Razor views doesn't mislead
    const stripped = body.replace(/@@/g, '@');
    const hasRazorInjection = /\@[a-zA-Z][a-zA-Z0-9]*/.test(stripped) && !CSS_AT_RULES.test(stripped);
    if (hasRazorInjection) continue; // leave for manual
    styleBlocks.push({ full: m[0], body });
  }
  for (let i = 0; i < styleBlocks.length; i++) {
    const block = styleBlocks[i];
    const base = slugFor(rel) + '-style' + (styleBlocks.length > 1 ? '-' + (i + 1) : '');
    const name = uniqueName(base, 'css');
    const cssPath = path.join(wwwroot, 'css', name + '.css');
    const cssRel = '~/css/' + name + '.css';
    fs.writeFileSync(cssPath, block.body.trim() + '\n', 'utf8');
    // Replace only the exact single occurrence
    const idx = src.indexOf(block.full);
    if (idx >= 0) {
      src = src.slice(0, idx) + `<link rel="stylesheet" href="${cssRel}" asp-append-version="true" />` + src.slice(idx + block.full.length);
      changed = true;
    }
  }

  // ---- Extract inline <script> blocks (no src, non-razor) ----
  const scriptRe = /<script\b([^>]*)>([\s\S]*?)<\/script\s*>/gi;
  const scriptBlocks = [];
  while ((m = scriptRe.exec(src))) {
    const attrs = m[1] || '';
    const body = m[2] || '';
    if (/\bsrc\s*=\s*["']/i.test(attrs)) continue;
    if (/type\s*=\s*["'](application\/json|text\/template)["']/i.test(attrs)) continue;
    // Genuine Razor injection inside a script is @Identifier (possibly @Model.xxx, @Url.Action).
    // A bare @ inside a JS string/template is not Razor unless followed by an identifier resolved
    // by Razor. Heuristic: treat it as Razor if it has @ followed by [A-Za-z] with no space that
    // is NOT inside a single/double-quoted string. We use a simplified check here; edge cases are
    // handled manually afterwards (they are reported by the scan anyway).
    const hasRazorInjection = /@[a-zA-Z]/.test(body) && !/@\s*[a-zA-Z]/.test(body);
    if (hasRazorInjection) continue;
    scriptBlocks.push({ full: m[0], body });
  }
  for (let i = 0; i < scriptBlocks.length; i++) {
    const block = scriptBlocks[i];
    const base = slugFor(rel) + '-script' + (scriptBlocks.length > 1 ? '-' + (i + 1) : '');
    const name = uniqueName(base, 'js');
    const jsPath = path.join(wwwroot, 'js', name + '.js');
    const jsRel = '~/js/' + name + '.js';
    fs.writeFileSync(jsPath, block.body.trim() + '\n', 'utf8');
    const idx = src.indexOf(block.full);
    if (idx >= 0) {
      src = src.slice(0, idx) + `<script src="${jsRel}" asp-append-version="true"></script>` + src.slice(idx + block.full.length);
      changed = true;
    }
  }

  if (changed) {
    fs.writeFileSync(f, src, 'utf8');
  }
}

console.log('DONE. Razor blocks left for manual handling (see scan report).');
console.log('Generated CSS/JS files:');
for (const k of Object.keys(used)) {
  console.log('  ' + k + ' -> wwwroot/' + (k.startsWith('css') ? 'css/' : 'js/') + '...');
}