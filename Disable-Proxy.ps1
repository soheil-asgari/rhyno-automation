# ظ¾ط§ع© ع©ط±ط¯ظ† ظ…طھط؛غŒط±ظ‡ط§غŒ ظ…ط­غŒط·غŒ ط³غŒط³طھظ…â€Œط¹ط§ظ…ظ„ (User Environment Variables)
[System.Environment]::SetEnvironmentVariable("http_proxy", $null, "User")
[System.Environment]::SetEnvironmentVariable("https_proxy", $null, "User")
[System.Environment]::SetEnvironmentVariable("all_proxy", $null, "User")

# ظ¾ط§ع© ع©ط±ط¯ظ† ظ…طھط؛غŒط±ظ‡ط§ ط§ط² ظ†ط´ط³طھ ظپط¹ظ„غŒ طھط±ظ…غŒظ†ط§ظ„
$env:http_proxy = $null
$env:https_proxy = $null
$env:all_proxy = $null

# ظ¾ط§ع© ع©ط±ط¯ظ† ظ¾ط±ظˆع©ط³غŒ ع¯غŒطھ
# ظ¾ط§ع©ط³ط§ط²غŒ ظ¾ط±ظˆع©ط³غŒ ط¯ط± طھظ…ط§ظ… ط³ط·ظˆط­ ع¯غŒطھ (Global ظˆ System)
git config --global --unset-all http.proxy
git config --global --unset-all https.proxy
git config --system --unset-all http.proxy
git config --system --unset-all https.proxy

# ظ¾ط§ع©ط³ط§ط²غŒ ظ…طھط؛غŒط±ظ‡ط§غŒ ظ…ط­غŒط·غŒ ط¯ط± ظ‡ظ…غŒظ† ظ†ط´ط³طھ طھط±ظ…غŒظ†ط§ظ„
$env:http_proxy = $null
$env:https_proxy = $null
$env:all_proxy = $null
$env:HTTP_PROXY = $null
$env:HTTPS_PROXY = $null
$env:ALL_PROXY = $null
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.MessageBox]::Show("Terminal & Git Proxy Disabled Successfully!", "Proxy Status", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
