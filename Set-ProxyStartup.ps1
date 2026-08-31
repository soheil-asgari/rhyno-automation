Add-Type -AssemblyName System.Windows.Forms

$proxyHttp  = "http://127.0.0.1:10808"
$proxySocks = "socks5://127.0.0.1:10808"

$result = [System.Windows.Forms.MessageBox]::Show(
    "Enable Terminal Proxy (Port 10808)?", 
    "Terminal Proxy", 
    [System.Windows.Forms.MessageBoxButtons]::YesNo, 
    [System.Windows.Forms.MessageBoxIcon]::Question
)

if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
    [System.Environment]::SetEnvironmentVariable("http_proxy", $proxyHttp, "User")
    [System.Environment]::SetEnvironmentVariable("https_proxy", $proxyHttp, "User")
    [System.Environment]::SetEnvironmentVariable("all_proxy", $proxySocks, "User")
    
    [System.Windows.Forms.MessageBox]::Show("Terminal Proxy Enabled!", "Proxy Status", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
} else {
    [System.Environment]::SetEnvironmentVariable("http_proxy", $null, "User")
    [System.Environment]::SetEnvironmentVariable("https_proxy", $null, "User")
    [System.Environment]::SetEnvironmentVariable("all_proxy", $null, "User")
    
    [System.Windows.Forms.MessageBox]::Show("Terminal Proxy Disabled.", "Proxy Status", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
}
