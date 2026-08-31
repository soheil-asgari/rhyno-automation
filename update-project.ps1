dotnet ef dbcontext list | Where-Object { $_ -and $_ -like "*DbContext*" } | ForEach-Object {
    $context = $_.Trim()
    Write-Host ">>> Updating Database for: $context" -ForegroundColor Cyan
    dotnet ef database update --context $context
}