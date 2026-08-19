const fs = require('fs');
const L = fs.readFileSync('Views/Contracts/Edit.cshtml', 'utf8').split(/\r?\n/);
const gi = (s) => L.findIndex(l => l.includes(s));

const out = {
  formOpen: gi('<form asp-action="Edit"'),
  Card1: gi('@* Card 1'),
  Card2: gi('@* Card 2'),
  Card3: gi('@* Card 3'),
  Card4: gi('@* Card 4'),
  Card5: gi('@* Card 5'),
  actions: gi('form-actions-row'),
  formClose: L.findIndex(l => l.trim() === '</form>'),
  sectionsStart: gi('@section Scripts'),
  total: L.length,
};
console.log(JSON.stringify(out, null, 2));