const fs = require('fs');
const s = fs.readFileSync('Views/Contracts/Create.cshtml', 'utf8');
const lines = s.split('\n');
const re = /editContractEditor|editTextarea|editEditor|syncEditEditor|asp-action="Edit"|\bEdit\b|ویرایش|data-edit/i;
lines.forEach((l, i) => {
  if (re.test(l)) console.log((i + 1) + ': ' + l.trim());
});
console.log('--- done ---');