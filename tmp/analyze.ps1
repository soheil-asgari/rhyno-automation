$ErrorActionPreference = 'SilentlyContinue'
Set-Location 'f:\rhyno-automation-1\rhyno-automation'

Write-Output '===== FILES SCANNED (count) ====='
$files = Get-ChildItem -Recurse -Include *.cshtml,*.html,*.js,*.css `
  | Where-Object { $_.FullName -notmatch '\\(node_modules|bin|obj|rhyno-build|rhyno-build-obj|\.git)\\' }
Write-Output $files.Count

Write-Output '===== CDN / EXTERNAL URL REFERENCES ====='
$cdn = $files | Select-String -Pattern '(https?:)?//[^\s"''`]*|cdn\.|jsdelivr|cloudflare|unpkg|googleapis|gstatic|bootstrapcdn|cdnjs|unpkg\.com|code\.jquery'
$cdn | ForEach-Object { '{0}:{1}: {2}' -f $_.Path.Replace((Get-Location).Path,'.'), $_.LineNumber.Trim(), $_.Line.Trim() }

Write-Output '===== INLINE <style> blocks in cshtml ====='
$files | Where-Object { $_.Extension -eq '.cshtml' } | Select-String -Pattern '<style' `
  | ForEach-Object { '{0}:{1}' -f $_.Path.Replace((Get-Location).Path,'.'), $_.LineNumber.Trim() }

Write-Output '===== INLINE <script> blocks in cshtml ====='
$files | Where-Object { $_.Extension -eq '.cshtml' } | Select-String -Pattern '<script' `
  | ForEach-Object { '{0}:{1}: {2}' -f $_.Path.Replace((Get-Location).Path,'.'), $_.LineNumber.Trim(), $_.Line.Trim() }

Write-Output '===== style= attributes in cshtml (count per file) ====='
$files | Where-Object { $_.Extension -eq '.cshtml' } | ForEach-Object {
  $c = (Select-String -Path $_.FullName -Pattern 'style="').Count
  if ($c -gt 0) { '{0}: {1}' -f $_.Path.Replace((Get-Location).Path,'.'), $c }
}