param(
    [Parameter(Mandatory = $true)][string]$Path
)

$bytes = [System.IO.File]::ReadAllBytes($Path)
$enc1256 = [System.Text.Encoding]::GetEncoding(1256)
$text = $enc1256.GetString($bytes)

# Write decoded text to a temp UTF-8 file so we can inspect it
$outPath = $Path + ".decoded"
$utf8WithBom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($outPath, $text, $utf8WithBom)
Write-Output ("Decoded to: " + $outPath)
Write-Output ("Length: " + $text.Length)
