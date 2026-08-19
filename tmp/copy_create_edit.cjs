const fs = require('fs');

const editPath = 'Views/Contracts/Edit.cshtml';
const createPath = 'Views/Contracts/Create.cshtml';

let text = fs.readFileSync(editPath, 'utf8');
const lines = text.split(/\r?\n/);

// ── 1. Header / form opening ──────────────────────────────────────────
// Replace the whole header + form open of Edit with Create's header + form open.
// Edit header: title=ویرایش قرارداد, action=Edit, has hidden Id, no-style
// We replace lines from `<div class="container-fluid...` down to `<form ...>` open.

// Find form open
const formOpen = lines.findIndex(l => l.includes('<form asp-action="Edit"'));
// Find header title block markers by unique content
const heroEditIcon = lines.findIndex(l => l.includes('bi-file-earmark-text'));
const heroIconClose = lines.findIndex((l, i) => i > heroEditIcon && l.trim().startsWith('</div>'));
// replace title h1 => Create
lines[lines.findIndex((l, i) => i < formOpen && l.includes('<h1>ویرایش قرارداد</h1>'))] = '                <h1>ایجاد قرارداد جدید</h1>';
lines[lines.findIndex((l, i) => i < formOpen && l.includes('>ویرایش اطلاعات قرارداد</p>'))] = '                <p>اطلاعات قرارداد جدید را وارد کنید</p>';
lines[lines.findIndex((l, i) => i < formOpen && l.includes('bi-file-earmark-text'))] = '            <div class="contracts-header-icon"><i class="bi bi-file-earmark-plus"></i></div>';
lines[lines.findIndex((l, i) => i < formOpen && l.includes('>بازگشت'))] = '                    <i class="bi bi-arrow-right"></i>بازگشت به فهرست';

// form open -> action Create
lines[formOpen] = lines[formOpen].replace('asp-action="Edit"', 'asp-action="Create"');
// remove the hidden Id line (Create has no Id)
const idIdx = lines.findIndex((l, i) => i > formOpen && l.includes('asp-for="Id"'));
if (idIdx > formOpen) lines[idIdx] = '';

// ── 2. Editor id: editContractEditor -> createContractEditor ──────────
let joined = lines.join('\n');
joined = joined.replace(/editContractEditor/g, 'createContractEditor');

fs.writeFileSync(createPath, joined + '\n', 'utf8');
console.log('wrote Create.cshtml header override. formOpen@', formOpen, 'idIdx@', idIdx);