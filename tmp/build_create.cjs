const fs = require('fs');

const editPath = 'Views/Contracts/Edit.cshtml';
const createPath = 'Views/Contracts/Create.cshtml';

const L = fs.readFileSync(editPath, 'utf8').split(/\r?\n/);
const gi = (s, from = 0) => L.findIndex((l, i) => i >= from && l.includes(s));

// ── Boundaries (0-based) ────────────────────────────────────────────
const formOpen   = gi('<form asp-action="Edit"');                      // 32
const card1      = gi('@* Card 1');                                    // 37
const card2      = gi('@* Card 2');                                    // 84
const card3      = gi('@* Card 3');                                    // 124
const card4      = gi('@* Card 4');                                    // 180
const card5      = gi('@* Card 5');                                    // 209
const actions    = gi('form-actions-row');                             // 258
const formClose  = L.findIndex(l => l.trim() === '</form>');            // 271

// ── Header rewrite (lines 0 .. formOpen, exclusive) ─────────────────
const header = [
  '@model ContractCreateVM',
  '@{',
  '    ViewData["Title"] = "ایجاد قرارداد جدید";',
  '}',
  '',
  '<div class="container-fluid py-2 space-y-2 contracts-create-page" dir="rtl">',
  '    @* Page Header Card *@',
  '    <div class="contracts-header-card">',
  '        <partial name="_Breadcrumb" model=\'new[] {',
  '            new RhynoBreadcrumbItem("خانه", "/"),',
  '            new RhynoBreadcrumbItem("قراردادها", "/Contracts"),',
  '            new RhynoBreadcrumbItem("ایجاد قرارداد", null)',
  '        }\' />',
  '',
  '        <div class="contracts-header-main">',
  '            <div class="contracts-header-icon"><i class="bi bi-file-earmark-plus"></i></div>',
  '            <div>',
  '                <h1>ایجاد قرارداد جدید</h1>',
  '                <p>اطلاعات قرارداد جدید را وارد کنید</p>',
  '            </div>',
  '            <div class="contracts-header-actions">',
  '                <a asp-action="Index" class="contracts-header-btn">',
  '                    <i class="bi bi-arrow-right"></i>بازگشت به فهرست',
  '                </a>',
  '            </div>',
  '        </div>',
  '    </div>',
  '',
  '    @section Styles {',
  '        <link rel="stylesheet" href="~/css/contracts-form.css" asp-append-version="true" />',
  '    }',
  '',
  '    <form asp-action="Create" method="post">',
  '        @Html.AntiForgeryToken()',
  '        <div asp-validation-summary="All" class="text-danger alert alert-danger mb-3" style="font-size:0.85rem;"></div>',
  '',
  '        <div class="contract-create-layout">',
  '            @* ── Right column: general info + dates & financials ──────────── *@',
  '            <div class="form-col">',
];

// ── Card 1 block: from start of Card1 line .. (card2 line), inclusive of Card1's own card markup
// We take lines[card1 .. card2-1]; strip the blank line separating? Keep as-is.
const card1Block = L.slice(card1, card2).map(ln => '    ' + ln);
const card3Block = L.slice(card3, card4).map(ln => '    ' + ln);

const rightColClose = [
  '            </div><!-- /form-col: right -->',
  '            @* ── Left column: client/party + assignment ─────────────────── *@',
  '            <div class="form-col">',
];
const card2Block = L.slice(card2, card3).map(ln => '    ' + ln);
const card4Block = L.slice(card4, card5).map(ln => '    ' + ln);
const leftColClose = [
  '            </div><!-- /form-col: left -->',
  '        </div><!-- /contract-create-layout -->',
];

// Card 5 remains full-width below the two columns; then actions.
const card5Block = L.slice(card5, actions).map(ln =>
  ln.includes('<div class="form-section">')
    ? ln.replace('<div class="form-section">', '<div class="form-section form-section--wide">')
    : ln
).map(ln => '    ' + ln);
const actionsBlock = L.slice(actions, formClose).map(ln => '    ' + ln);

// form + closing, then remainder (from formClose to end) — contains the scripts.
const rest = L.slice(formClose).join('\n') // starts with </form>
  .replace(/editContractEditor/g, 'createContractEditor')
  .replace(/\bsyncEditEditor\b/g, 'syncContractEditor')
  .replace(/\beditEditor\b/g, 'createEditor')
  .replace(/\beditTextarea\b/g, 'createTextarea');

const outArr = header
  .concat(card1Block)
  .concat(card3Block)
  .concat(rightColClose)
  .concat(card2Block)
  .concat(card4Block)
  .concat(leftColClose)
  .concat(card5Block)
  .concat(actionsBlock)
  .concat(L.slice(formClose));

const full = outArr.join('\n')
  .replace(/editContractEditor/g, 'createContractEditor')
  .replace(/\bsyncEditEditor\b/g, 'syncContractEditor')
  .replace(/\beditEditor\b/g, 'createEditor')
  .replace(/\beditTextarea\b/g, 'createTextarea');

fs.writeFileSync(createPath, full + '\n', 'utf8');
console.log('formOpen', formOpen, 'card1..card5', card1, card2, card3, card4, card5, 'actions', actions, 'formClose', formClose);
console.log('lines written:', full.split('\n').length);