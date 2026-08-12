param(
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$StartOffset = 0,
    [int]$Length = 200
)

$bytes = [System.IO.File]::ReadAllBytes($Path)
Write-Output ("Total bytes: " + $bytes.Length)

$end = [Math]::Min($StartOffset + $Length, $bytes.Length)
for ($i = $StartOffset; $i -lt $end; $i += 16) {
    $hex = ""
    for ($j = $i; $j -lt [Math]::Min($i + 16, $end); $j++) {
        $hex += ("{0:X2} " -f $bytes[$j])
    }
    Write-Output ("{0,8}: {1}" -f $i, $hex)
}
