# server.ps1 - Lightweight Local Bridge for Git AutoSync Hub
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Web

$script:BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$script:RootDir = Split-Path -Parent $script:BaseDir
$script:ConfigPath = Join-Path $script:RootDir "sync-config.json"
$script:ManagerScript = Join-Path $script:RootDir "scripts\git_manager.ps1"
$script:HtmlPath = Join-Path $script:BaseDir "index.html"

$port = 56789
$prefix = "http://127.0.0.1:$port/"

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add($prefix)

try {
    $listener.Start()
} catch {
    # Neu port dang bi chiem, thu dong mo lai
    $port = 56790
    $prefix = "http://127.0.0.1:$port/"
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add($prefix)
    $listener.Start()
}

# Mo Edge o che do App Mode
$edgePaths = @(
    "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    "$env:LOCALAPPDATA\Microsoft\Edge\Application\msedge.exe"
)
$edgeExe = $edgePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($edgeExe) {
    $edgeProc = Start-Process -FilePath $edgeExe -ArgumentList "--app=$prefix", "--window-size=1120,780" -PassThru
} else {
    Start-Process $prefix
}

Write-Host "Git AutoSync Hub Bridge dang hoat dong tai: $prefix"

function Send-Response ($context, [string]$content, [string]$contentType = "text/html; charset=utf-8", [int]$statusCode = 200) {
    $buffer = [System.Text.Encoding]::UTF8.GetBytes($content)
    $context.Response.ContentType = $contentType
    $context.Response.StatusCode = $statusCode
    $context.Response.ContentLength64 = $buffer.Length
    $context.Response.Headers.Add("Access-Control-Allow-Origin", "*")
    $context.Response.OutputStream.Write($buffer, 0, $buffer.Length)
    $context.Response.OutputStream.Close()
}

function Read-Body ($request) {
    $reader = New-Object System.IO.StreamReader($request.InputStream, [System.Text.Encoding]::UTF8)
    $body = $reader.ReadToEnd()
    $reader.Close()
    if ($body) { return ($body | ConvertFrom-Json) }
    return $null
}

# Vong lap lang nghe Request
while ($listener.IsListening) {
    $context = $listener.GetContext()
    $req = $context.Request
    $urlPath = $req.Url.AbsolutePath

    try {
        if ($urlPath -eq "/" -or $urlPath -eq "/index.html") {
            $html = Get-Content $script:HtmlPath -Raw -Encoding UTF8
            Send-Response $context $html "text/html; charset=utf-8"
        }
        elseif ($urlPath -eq "/api/config" -and $req.HttpMethod -eq "GET") {
            $cfg = Get-Content $script:ConfigPath -Raw -Encoding UTF8
            Send-Response $context $cfg "application/json; charset=utf-8"
        }
        elseif ($urlPath -eq "/api/sync-all" -and $req.HttpMethod -eq "POST") {
            $out = & powershell.exe -ExecutionPolicy Bypass -File "$script:ManagerScript" -Action SyncAll 2>&1
            $res = [PSCustomObject]@{ output = ($out -join "`n") } | ConvertTo-Json
            Send-Response $context $res "application/json; charset=utf-8"
        }
        elseif ($urlPath -eq "/api/sync-repo" -and $req.HttpMethod -eq "POST") {
            $body = Read-Body $req
            $out = & powershell.exe -ExecutionPolicy Bypass -File "$script:ManagerScript" -Action SyncRepo -RepoName $body.name 2>&1
            $res = [PSCustomObject]@{ output = ($out -join "`n") } | ConvertTo-Json
            Send-Response $context $res "application/json; charset=utf-8"
        }
        elseif ($urlPath -eq "/api/test-token" -and $req.HttpMethod -eq "POST") {
            $out = & powershell.exe -ExecutionPolicy Bypass -File "$script:ManagerScript" -Action TestAuth 2>&1
            $res = [PSCustomObject]@{ output = ($out -join "`n") } | ConvertTo-Json
            Send-Response $context $res "application/json; charset=utf-8"
        }
        elseif ($urlPath -eq "/api/pick-folder" -and $req.HttpMethod -eq "POST") {
            $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
            $dlg.Description = "Chon thu muc repository tren may tinh cua ban"
            $path = ""
            if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
                $path = $dlg.SelectedPath
            }
            $res = [PSCustomObject]@{ path = $path } | ConvertTo-Json
            Send-Response $context $res "application/json; charset=utf-8"
        }
        elseif ($urlPath -eq "/api/add-repo" -and $req.HttpMethod -eq "POST") {
            $body = Read-Body $req
            $cfg = (Get-Content $script:ConfigPath -Raw -Encoding UTF8) | ConvertFrom-Json
            $newRepo = [PSCustomObject]@{
                name = $body.name
                local_path = $body.local_path
                remote_url = $body.remote_url
                branch = if ($body.branch) { $body.branch } else { "main" }
                enabled = $true
            }
            $cfg.repositories += $newRepo
            $cfg | ConvertTo-Json -Depth 5 | Set-Content -Path $script:ConfigPath -Encoding UTF8
            Send-Response $context '{"success":true}' "application/json; charset=utf-8"
        }
        elseif ($urlPath -eq "/api/delete-repo" -and $req.HttpMethod -eq "POST") {
            $body = Read-Body $req
            $cfg = (Get-Content $script:ConfigPath -Raw -Encoding UTF8) | ConvertFrom-Json
            $cfg.repositories = @($cfg.repositories | Where-Object { $_.name -ne $body.name })
            $cfg | ConvertTo-Json -Depth 5 | Set-Content -Path $script:ConfigPath -Encoding UTF8
            Send-Response $context '{"success":true}' "application/json; charset=utf-8"
        }
        elseif ($urlPath -eq "/api/save-config" -and $req.HttpMethod -eq "POST") {
            $body = Read-Body $req
            $cfg = (Get-Content $script:ConfigPath -Raw -Encoding UTF8) | ConvertFrom-Json
            if ($body.github_token) { $cfg.settings.github_token = $body.github_token }
            if ($body.git_user_name) { $cfg.settings.git_user_name = $body.git_user_name }
            if ($body.git_user_email) { $cfg.settings.git_user_email = $body.git_user_email }
            $cfg | ConvertTo-Json -Depth 5 | Set-Content -Path $script:ConfigPath -Encoding UTF8
            Send-Response $context '{"success":true}' "application/json; charset=utf-8"
        }
        else {
            Send-Response $context "Not Found" "text/plain" 404
        }
    } catch {
        Send-Response $context ($_.Exception.Message) "text/plain" 500
    }
}
