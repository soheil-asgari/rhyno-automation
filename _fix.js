const fs = require('fs');
let t = fs.readFileSync('Views/Contracts/Settings.cshtml', 'utf8');
const old = 'w-full bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-lg px-2.5 py-1.5 text-xs focus:ring-teal-500/20 focus:border-teal-500 outline-none';
while (t.includes(old)) t = t.replace(old, 'erp-input');
fs.writeFileSync('Views/Contracts/Settings.cshtml', t, 'utf8');
console.log('erp-input count:', (t.match(/erp-input/g) || []).length);