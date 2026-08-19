const fs = require('fs');
const m = fs.readFileSync('Models/ContractViewModels.cs', 'utf8');
const props = new Set();
const block = m.split('class ContractCreateVM')[1].split('class ContractEditVM')[0];
for (const mm of block.matchAll(/public [^{}]+? (\w+) \{ get; set; \}/g)) {
  props.add(mm[1]);
}
console.log('VM properties:', [...props].sort().join(', '));

const v = fs.readFileSync('Views/Contracts/Create.cshtml', 'utf8');
const miss = [];
for (const m2 of v.matchAll(/asp-for="([A-Za-z]+)/g)) {
  if (!props.has(m2[1])) miss.push('asp-for:' + m2[1]);
}
for (const m3 of v.matchAll(/@Model\.(\w+)/g)) {
  if (!props.has(m3[1])) miss.push('@Model.' + m3[1]);
}
console.log('MISSING refs:', miss.length ? miss.join(', ') : 'NONE - all valid');

const re = /asp-for="([A-Za-z]+)/g;
const set = new Set();
let x;
while ((x = re.exec(v))) set.add(x[1]);
console.log('used fields:', [...set].sort().join(', '));