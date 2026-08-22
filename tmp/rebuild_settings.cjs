const fs = require('fs');
const path = 'Views/Contracts/Settings.cshtml';
let t = fs.readFileSync(path, 'utf8');
const modalStart = '@* Add Template Modal';
const stylesStart = '@section Styles {';
const si = t.indexOf(modalStart);
const ei = t.indexOf(stylesStart);
if (si === -1 || ei === -1 || ei < si) { console.error('split fail', si, ei); process.exit(1); }
const before = t.slice(0, si);
const after = t.slice(ei);
modal.push(
`@* Add Template Modal - Redesigned 2-Column Grid Layout *@
<div class="rhyno-modal-overlay" id="addTemplateModal" style="position:fixed;inset:0;z-index:50;display:none;align-items:center;justify-content:center;background:rgba(0,0,0,0.45);backdrop-filter:blur(4px);">
    <div class="rhyno-modal-content" style="width:100%;max-width:960px;max-height:85vh;overflow-y:auto;background:#fff;border-radius:14px;border:1px solid #e5e7eb;box-shadow:0 20px 60px rgba(0,0,0,0.25);">
        <div class="flex items-center justify-between py-2 px-3 border-b border-slate-100 dark:border-slate-800">
            <div class="flex items-center gap-2">
                <i class="bi bi-file-earmark-plus text-teal-600 text-xs"></i>
                <span class="text-xs font-bold text-slate-900 dark:text-white">قالب جدید</span>
            </div>
            <button type="button" style="width:28px;height:28px;display:flex;align-items:center;justify-content:center;border:none;background:transparent;color:#9ca3af;cursor:pointer;border-radius:6px;font-size:16px;" onclick="closeModal('addTemplateModal')">&times;</button>
        </div>
        <form asp-action="SaveTemplate" method="post">
            @Html.AntiForgeryToken()
            <input type="hidden" asp-for="CurrentTemplateId" />
            <div class="p-3">
                <div class="grid grid-cols-12 gap-3">
                    <div class="col-span-12 lg:col-span-9 space-y-3">
                        <div>
                            <label asp-for="TemplateName" class="erp-label"></label>
                            <input asp-for="TemplateName" class="erp-input" required placeholder="نام قالب را وارد کنید" />
                        </div>
                        <div>
                            <label asp-for="Description" class="erp-label"></label>
                            <textarea asp-for="Description" class="erp-input resize-y" rows="2" placeholder="توضیحات قالب (اختیاری)"></textarea>
                        </div>
                        <div>
modal.push(
`<div class="flex flex-wrap items-center gap-1 mb-1.5 px-2 py-1.5 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-lg">
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" data-cmd="bold" title="ضخیم"><b>B</b></button>
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" data-cmd="italic" title="کج"><i>I</i></button>
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" data-cmd="underline" title="زیرخط"><u>U</u></button>
    <span class="w-px h-4 bg-slate-300 dark:bg-slate-600 mx-1"></span>
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" data-cmd="insertOrderedList" title="لیست شماره‌دار"><i class="bi bi-list-ol"></i></button>
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" data-cmd="insertUnorderedList" title="لیست نقطه‌ای"><i class="bi bi-list-ul"></i></button>
    <span class="w-px h-4 bg-slate-300 dark:bg-slate-600 mx-1"></span>
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" data-cmd="justifyRight" title="چپ‌چین"><i class="bi bi-text-right"></i></button>
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" data-cmd="justifyCenter" title="وسط‌چین"><i class="bi bi-text-center"></i></button>
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" data-cmd="justifyLeft" title="راست‌چین"><i class="bi bi-text-left"></i></button>
    <span class="w-px h-4 bg-slate-300 dark:bg-slate-600 mx-1"></span>
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" onclick="insertPageBreak()" title="درج صفحه جدید"><i class="bi bi-file-earmark-break"></i></button>
    <button type="button" class="wysiwyg-btn px-2 py-0.5 text-[10px] font-bold text-slate-600 hover:bg-slate-200 dark:hover:bg-slate-800 rounded transition border-0 bg-transparent cursor-pointer" data-cmd="createLink" title="درج لینک"><i class="bi bi-link-45deg"></i></button>
</div>
<div id="htmlEditor" contenteditable="true" class="w-full min-h-[320px] bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-lg p-3 text-xs text-slate-800 dark:text-slate-200 focus:ring-teal-500/20 focus:border-teal-500 outline-none overflow-y-auto" dir="rtl" style="white-space:pre-wrap;"></div>
<textarea asp-for="HtmlContent" id="HtmlContent" class="hidden"></textarea>
</div>
<div class="flex items-center gap-2">
    <input asp-for="IsDefault" class="h-4 w-4 rounded border-slate-300 text-teal-600 focus:ring-teal-500/20" type="checkbox" />
    <label asp-for="IsDefault" class="text-[11px] text-slate-700 dark:text-slate-300 font-bold">به عنوان قالب پیش‌فرض</label>
</div>
</div>`
);
                            <label class="erp-label">
                                <i class="bi bi-pencil-square text-xs ml-1 text-blue-500"></i>ویرایشگر بصری قالب
                            </label>`
);

const modal = [];
modal.push(
`<div class="col-span-12 lg:col-span-3">
    <div class="erp-card p-2 max-h-[400px] overflow-y-auto">
        <div class="text-[9px] font-black text-slate-500 dark:text-slate-400 mb-2 px-1 flex items-center gap-1"><i class="bi bi-tag"></i><span>تگ‌های قابل جایگزاری</span></div>
        <div class="mb-2">
            <div class="text-[8px] font-black text-slate-600 dark:text-slate-400 mb-1 px-1"><i class="bi bi-info-circle"></i>عمومی</div>
            @foreach (var tag in Model.AvailableTags.Where(t => new[] { "{{ContractNumber}}","{{Title}}","{{ContractType}}","{{Description}}","{{Status}}","{{CompanyName}}" }.Contains(t.Tag)))
            {
                <button type="button" class="w-full text-right rounded px-2 py-1 text-[10px] font-mono text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 hover:text-teal-600 dark:hover:text-teal-400 transition cursor-pointer border-0" onclick="insertTagWysiwyg('@tag.Tag')" title="@tag.Description">
                    <span class="font-bold">@tag.Tag</span>
                    <span class="block text-[8px] text-slate-400 dark:text-slate-500">@tag.DisplayName</span>
                </button>
            }
        </div>
        <div class="mb-2">
            <div class="text-[8px] font-black text-blue-600 dark:text-blue-400 mb-1 px-1"><i class="bi bi-people"></i>طرفین قرارداد</div>
            @foreach (var tag in Model.AvailableTags.Where(t => new[] { "{{CustomerName}}","{{CustomerCompany}}","{{CustomerPhone}}","{{CustomerAddress}}","{{SecondPartyPhone}}","{{AssignedUser}}","{{ResponsibleUser}}" }.Contains(t.Tag)))
            {
                <button type="button" class="w-full text-right rounded px-2 py-1 text-[10px] font-mono text-slate-700 dark:text-slate-300 hover:bg-blue-50 dark:hover:bg-blue-950/20 hover:text-blue-600 dark:hover:text-blue-400 transition cursor-pointer border-0" onclick="insertTagWysiwyg('@tag.Tag')" title="@tag.Description">
                    <span class="font-bold">@tag.Tag</span>
                    <span class="block text-[8px] text-slate-400 dark:text-slate-500">@tag.DisplayName</span>
                </button>
            }
        </div>
        <div class="mb-2">
            <div class="text-[8px] font-black text-emerald-600 dark:text-emerald-400 mb-1 px-1"><i class="bi bi-cash-stack"></i>اطلاعات مالی</div>
            @foreach (var tag in Model.AvailableTags.Where(t => new[] { "{{TotalAmount}}","{{PaidAmount}}","{{RemainingBalance}}","{{PaymentStatus}}" }.Contains(t.Tag)))
            {
                <button type="button" class="w-full text-right rounded px-2 py-1 text-[10px] font-mono text-slate-700 dark:text-slate-300 hover:bg-emerald-50 dark:hover:bg-emerald-950/20 hover:text-emerald-600 dark:hover:text-emerald-400 transition cursor-pointer border-0" onclick="insertTagWysiwyg('@tag.Tag')" title="@tag.Description">
                    <span class="font-bold">@tag.Tag</span>
                    <span class="block text-[8px] text-slate-400 dark:text-slate-500">@tag.DisplayName</span>
                </button>
            }
        </div>
        <div>
            <div class="text-[8px] font-black text-purple-600 dark:text-purple-400 mb-1 px-1"><i class="bi bi-calendar3"></i>تاریخ‌ها</div>
            @foreach (var tag in Model.AvailableTags.Where(t => new[] { "{{StartDate}}","{{EndDate}}","{{SignDate}}" }.Contains(t.Tag)))
            {
                <button type="button" class="w-full text-right rounded px-2 py-1 text-[10px] font-mono text-slate-700 dark:text-slate-300 hover:bg-purple-50 dark:hover:bg-purple-950/20 hover:text-purple-600 dark:hover:text-purple-400 transition cursor-pointer border-0" onclick="insertTagWysiwyg('@tag.Tag')" title="@tag.Description">
                    <span class="font-bold">@tag.Tag</span>
                    <span class="block text-[8px] text-slate-400 dark:text-slate-500">@tag.DisplayName</span>
                </button>
            }
        </div>
    </div>
</div>`);
// Write out the reconstructed file
const result = before + modal.join('\n') + '\n' + after;
fs.writeFileSync(path, result, 'utf8');
console.log('Settings.cshtml rebuilt. New length:', result.length);
