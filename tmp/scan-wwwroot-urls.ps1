$ErrorActionPreference = 'SilentlyContinue'
Get-ChildItem 'wwwroot' -Recurse -Include *.css,*.js,*.mjs -File | ForEach-Object {
  Select-String -Path $_.FullName -Pattern 'https?://' | ForEach-Object {
    $line = $_.Line.Trim()
    if ($line -notmatch 'example\.com|w3\.org' ) {
      '{0}:{1}: {2}' -f $_.Path.Replace((Get-Location).Path,'.'), $_.LineNumber, $line
    }
  }
}