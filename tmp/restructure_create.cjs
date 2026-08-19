const fs = require('fs');
const path = 'Views/Contracts/Create.cshtml';
const lines = fs.readFileSync(path, 'utf8').split(/\r?\n/);

const openL  = lines.findIndex(l => l.includes('class="contract-create-layout"'));
const c1 = lines.findIndex(l => l.includes('@* Card 1'));
const c2 = lines.findIndex(l => l.includes('@* Card 2'));
const c3 = lines.findIndex(l => l.includes('@* Card 3'));
const c4 = lines.findIndex(l => l.includes('@* Card 4'));
const closeL = lines.findIndex(l => l.includes('// /contract-create-layout'));

// Extract each card's own lines by dropping wrapper/comment/blank lines between markers.
function cardBetween(startIdx, endIdx) {
  return lines.slice(startIdx, endIdx).filter(l =>
    !l.includes('form-col') &&
    !l.includes('─') &&            // box-drawing comment lines
    l.trim() !== ''
  );
}

const card1 = cardBetween(c1, c2);
const card2 = cardBetween(c2, c3);
const card3 = cardBetween(c3, c4);
const card4 = cardBetween(c4, closeL);

const rightCol = ['    <div class="form-col">']
  .concat(card1)
  .concat(card3)
  .concat(['    </div><!-- /form-col: right -->']);

const leftCol = ['    <div class="form-col">']
  .concat(card2)
  .concat(card4)
  .concat(['    </div><!-- /form-col: left -->']);

const out = lines.slice(0, openL + 1)
  .concat(rightCol)
  .concat(leftCol)
  .concat(lines.slice(closeL));

fs.writeFileSync(path, out.join('\n') + '\n', 'utf8');
console.log('card1:%d card2:%d card3:%d card4:%d closeL:%d', c1, c2, c3, c4, closeL);
console.log('card sizes:', card1.length, card2.length, card3.length, card4.length);
console.log('old:', lines.length, 'new:', out.length);