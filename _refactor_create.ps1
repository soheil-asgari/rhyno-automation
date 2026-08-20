$ErrorActionPreference = 'Stop'
$path = 'f:\rhyno-automation-1\rhyno-automation\Views\Contracts\Create.cshtml'
$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$original = $text

function Swap([string]$s, [string]$from, [string]$to) {
  if ($s.Contains($from)) { return $s.Replace($from, $to) } else { return $s }
}

# Header + wizard header cards
$text = Swap $text 'class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl p-5 shadow-sm mb-6"' 'class="erp-card mb-3"'

# Connecting line
$text = Swap $text '<div class="absolute top-5 left-12 right-12 h-0.5 bg-slate-200 dark:bg-slate-800">' '<div class="absolute top-3 left-8 right-8 h-[3px] bg-slate-200 dark:bg-slate-800">'

# Step badges 24px
$text = Swap $text 'step-badge w-10 h-10 rounded-full flex items-center justify-center text-xs font-bold ring-4 ring-white dark:ring-slate-900' 'step-badge w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-bold ring-2 ring-white dark:ring-slate-900'

# Step labels
$text = Swap $text 'step-label text-xs font-bold text-teal-600 dark:text-teal-400 mt-2.5 transition-colors' 'step-label text-[10px] font-bold text-teal-600 dark:text-teal-400 mt-1 transition-colors'
$text = Swap $text 'step-label text-xs font-medium text-slate-400 dark:text-slate-500 mt-2.5 transition-colors' 'step-label text-[10px] font-medium text-slate-400 dark:text-slate-500 mt-1 transition-colors'

# Wizard inner
$text = Swap $text '<div class="relative max-w-2xl mx-auto px-6 py-1">' '<div class="relative max-w-2xl mx-auto px-4 py-2">'

# Step panel wrappers
$text = Swap $text 'class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl p-6 shadow-sm space-y-6"' 'class="erp-fieldset space-y-3"'

# Section h2 -> legend
$text = Swap $text 'class="text-sm font-bold text-slate-800 dark:text-slate-200 mb-4 pb-2 border-b border-slate-100 dark:border-slate-800 flex items-center gap-2"' 'class="erp-fieldset-legend flex items-center gap-1.5"'

# Grids
$text = Swap $text 'class="grid grid-cols-1 md:grid-cols-3 gap-4"' 'class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-2"'
$text = Swap $text 'class="grid grid-cols-1 md:grid-cols-2 gap-4"' 'class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-2"'
$text = Swap $text 'class="grid grid-cols-1 md:grid-cols-4 gap-4"' 'class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-2"'

# Jalali inputs first (they include extra classes)
$text = Swap $text 'class="w-full px-3 py-2 text-xs border border-slate-300 dark:border-slate-700 rounded-lg bg-white dark:bg-slate-950 focus:outline-none focus:border-teal-500 jalali-date-input cursor-pointer"' 'class="erp-input jalali-date-input cursor-pointer"'

# Selects to erp-select (class token only)
$inp = 'w-full px-3 py-2 text-xs border border-slate-300 dark:border-slate-700 rounded-lg bg-white dark:bg-slate-950 focus:outline-none focus:border-teal-500'
$escinp = [regex]::Escape($inp)
$text = [regex]::Replace($text, '(<select\b[^>]*?class=")[^"]*?' + $escinp + '[^"]*(")', { param($m) $m.Groups[1].Value + 'erp-select' + $m.Groups[2].Value })
$text = [regex]::Replace($text, '(<textarea\b[^>]*?class=")[^"]*?' + $escinp + '[^"]*(")', { param($m) $m.Groups[1].Value + 'erp-textarea' + $m.Groups[2].Value })

# Remaining input base class -> erp-input
$text = Swap $text 'class="' + $inp + '"' 'class="erp-input"'

# Labels
$text = Swap $text 'class="block text-xs font-medium text-slate-700 dark:text-slate-300 mb-1"' 'class="erp-label"'
$text = Swap $text 'class="block text-xs font-bold text-slate-800 dark:text-slate-200 mb-2"' 'class="erp-label"'

# Wizard footer
$text = Swap $text 'class="flex items-center justify-between mt-6 mb-28 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl p-4 shadow-sm relative z-10"' 'class="erp-card flex items-center justify-between mt-3 px-3 py-2"'

# Footer buttons
$text = Swap $text 'class="hidden px-4 py-2 text-xs font-semibold text-slate-700 dark:text-slate-300 border border-slate-300 dark:border-slate-700 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 transition cursor-pointer"' 'class="hidden erp-btn"'
$text = Swap $text 'class="px-5 py-2 text-xs font-semibold text-white bg-teal-600 rounded-lg hover:bg-teal-700 transition cursor-pointer"' 'class="erp-btn erp-btn-primary"'
$text = Swap $text 'class="hidden px-5 py-2 text-xs font-bold text-white bg-teal-600 rounded-lg hover:bg-teal-700 transition shadow-sm cursor-pointer"' 'class="hidden erp-btn erp-btn-primary"'

# Editor body
$text = Swap $text 'class="min-h-[220px] max-h-[450px] overflow-y-auto p-4 border border-t-0 border-slate-200 dark:border-slate-800 rounded-b-lg bg-white dark:bg-slate-950 text-xs leading-relaxed focus:outline-none"' 'class="min-h-[180px] max-h-[420px] overflow-y-auto px-3 py-2 border border-t-0 border-slate-200 dark:border-slate-800 rounded-b bg-white dark:bg-slate-950 text-[11px] leading-relaxed focus:outline-none"'

$ut8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($path, $text, $ut8)
'Changed=' + ($original -ne $text)