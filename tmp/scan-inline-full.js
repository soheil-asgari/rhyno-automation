const fs = require('fs');
const path = require('path');
const root = 'f:\\rhyno-automation-1\\rhyno-automation';
const skip = /(node_modules|\\bin\\|\\obj\\|\\rhyno-build\\|\\rhyno-build-obj\\|\\\.git\\|\\tmp\\)/;
function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!skip.test(p + path.sep)) walk(p, out); }
    else if (/\.cshtml$/i.test(e.name)) out.push(p);
  }
  return out;
}
let reportAll = [];
for (const f of walk(root)) {
  const src = fs.readFileSync(f, 'utf8');
  const rel = f.replace(root, '.').replace(/\\/g, '/');
  const reScriptGlobal = /<script\b([^>]*)>([\s\S]*?)<\/script\s*>/gi;
  const reStyleGlobal = /<style\b[^>]*>([\s\S]*?)<\/style\s*>/gi;
  let m; const reScripts = [];
  while ((m = reScriptGlobal.exec(src))) {
    const attrs = m[1] || ''; const body = m[2] || '';
    if (/\bsrc\s*=\s*["']/i.test(attrs)) continue;
    if (/type\s*=\s*["'](application\/json|text\/template)["']/i.test(attrs)) continue;
    const hasRazor = /@[a-zA-Z@]/.test(body);
    const startLine = src.slice(0, m.index).split('\n').length;
    const clean = body.trim();
    reScripts.push({ startLine, bodyLen: clean.length, hasRazor, head: clean.slice(0, 50).replace(/\s+/g, ' ') });
  }
  const reStyles = []; 
  while ((m = reStyleGlobal.exec(src))) {
    const body = m[1] || '';
    const startLine = src.slice(0, m.index).split('\n').length;
    const clean = body.trim();
    reStyles.push({ startLine, bodyLen: clean.length, head: clean.slice(0, 50).replace(/\s+/g, ' ') });
  }
  if (reScripts.length || reStyles.length) reportAll.push({ rel, scripts: reScripts, styles: reStyles });
}
let out = '';
for (const r of reportAll) {
  out += 'FILE: ' + r.rel + '\n';
  for (const s of r.scripts) out += `   SCRIPT line ${s.startLine} len=${s.bodyLen} razor=${s.hasRazor} :: ${s.head}\n`;
  for (const s of r.styles) out += `   STYLE  line ${s.startLine} len=${s.bodyLen} :: ${s.head}\n`;
}
out += '\nTOTAL: ' + reportAll.length + '\n';
fs.writeFileSync(path.join(root, 'tmp', 'inline-report.txt'), out, 'utf8');
console.log('written, total files:', reportAll.length);