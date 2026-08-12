param(
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$FromLine = 1,
    [int]$ToLine = 100
)

$bytes = [System.IO.File]::ReadAllBytes($Path)
$hasBom = $false
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    $hasBom = $true
}

Write-Output ("File: " + $Path)
Write-Output ("UTF-8 BOM: " + $hasBom)

# Try to decode as UTF-8 first (should produce valid Persian if file is UTF-8)
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
$utf8Ok = $true
$utf8Text = ""
try {
    $utf8Text = $utf8.GetString($bytes)
} catch {
    $utf8Ok = $false
}
Write-Output ("Valid UTF-8: " + $utf8Ok)

# Decode as Windows-1256
$enc1256 = [System.Text.Encoding]::GetEncoding(1256)
$text1256 = $enc1256.GetString($bytes)

Write-Output "===== Decoded as Windows-1256 ====="
$lines = $text1256 -split "`r?`n"
for ($i = $FromLine; $i -le [Math]::Min($ToLine, $lines.Length); $i++) {
    Write-Output ("{0,5}: {1}" -f $i, $lines[$i - 1])
}
