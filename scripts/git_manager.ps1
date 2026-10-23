# git_manager.ps1 - Bộ điều khiển tự động hóa đồng bộ Git đa Repository
[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [ValidateSet("SyncAll", "SyncRepo", "TestAuth", "ScanRepos", "AddRepo", "InstallGit")]
    [string]$Action = "SyncAll",

    [string]$RepoName = "",
    [string]$LocalPath = "",
    [string]$RemoteUrl = "",
    [string]$ConfigPath = ""
)

$script:BaseDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not $ConfigPath) {
    $ConfigPath = Join-Path $script:BaseDir "sync-config.json"
}

# 1. Tìm hoặc cài đặt Git
function Get-GitExecutable {
    param([string]$CustomPath = "auto")
    if ($CustomPath -and $CustomPath -ne "auto" -and (Test-Path $CustomPath)) {
        return $CustomPath
    }
    $whereGit = (where.exe git 2>$null | Select-Object -First 1)
    if ($whereGit -and (Test-Path $whereGit)) { return $whereGit }

    $commonPaths = @(
        "C:\Program Files\Git\cmd\git.exe",
        "C:\Program Files (x86)\Git\cmd\git.exe",
        "$env:LOCALAPPDATA\Programs\Git\cmd\git.exe",
        "$env:ProgramFiles\Git\bin\git.exe"
    )
    foreach ($path in $commonPaths) {
        if (Test-Path $path) { return $path }
    }
    return $null
}

function Install-GitAutomatically {
    Write-Host "[THONG BAO] Khong tim thay Git tren may. Dang tu dong cai dat qua winget..." -ForegroundColor Yellow
    $winget = where.exe winget 2>$null | Select-Object -First 1
    if (-not $winget) {
        throw "Khong tim thay cong cu winget de tu dong cai dat Git. Vui long cai Git tu https://git-scm.com/"
    }
    Start-Process -FilePath "winget" -ArgumentList "install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements" -Wait -NoNewWindow
    $git = Get-GitExecutable
    if (-not $git) {
        throw "Cai dat xong nhung chua nhan duoc Git. Vui long khoi dong lai chuong trinh."
    }
    Write-Host "[THANH CONG] Da cai dat Git thanh cong tai: $git" -ForegroundColor Green
    return $git
}

# 2. Đọc và lưu cấu hình
function Get-SyncConfig {
    if (-not (Test-Path $ConfigPath)) {
        throw "Khong tim thay file cau hinh: $ConfigPath"
    }
    $raw = Get-Content $ConfigPath -Raw -Encoding UTF8
    return ($raw | ConvertFrom-Json)
}

function Save-SyncConfig {
    param($Config)
    $json = $Config | ConvertTo-Json -Depth 5
    Set-Content -Path $ConfigPath -Value $json -Encoding UTF8
}

function Resolve-FullPath {
    param([string]$Path)
    if ($Path -match '^(\.[\/\\]|[^\/\\:]+$)') {
        return [System.IO.Path]::GetFullPath((Join-Path $script:BaseDir $Path))
    }
    return [System.IO.Path]::GetFullPath($Path)
}

function Build-AuthenticatedUrl {
    param([string]$Url, [string]$Token)
    if (-not $Token) { return $Url }
    if ($Url -match '^https:\/\/github\.com\/(.+)$') {
        $repoPath = $Matches[1]
        return "https://${Token}@github.com/${repoPath}"
    }
    return $Url
}

# 3. Chuẩn bị repository (Git init & remote)
function Ensure-RepositoryConfigured {
    param(
        [string]$GitExe,
        [string]$FullPath,
        [string]$RemoteUrl,
        [string]$Branch = "main",
        [string]$Token = "",
        [string]$UserName = "",
        [string]$UserEmail = ""
    )
    if (-not (Test-Path $FullPath)) {
        New-Item -ItemType Directory -Path $FullPath -Force | Out-Null
    }

    $gitDir = Join-Path $FullPath ".git"
    if (-not (Test-Path $gitDir)) {
        Write-Host "   -> Khoi tao Git repository tai: $FullPath" -ForegroundColor Cyan
        & $GitExe -C $FullPath init -b $Branch | Out-Null
    }

    if ($UserName) { & $GitExe -C $FullPath config user.name $UserName }
    if ($UserEmail) { & $GitExe -C $FullPath config user.email $UserEmail }
    & $GitExe -C $FullPath config core.autocrlf true

    $authRemoteUrl = Build-AuthenticatedUrl -Url $RemoteUrl -Token $Token
    $currentRemotes = & $GitExe -C $FullPath remote
    if ($currentRemotes -contains "origin") {
        & $GitExe -C $FullPath remote set-url origin $authRemoteUrl
    } else {
        & $GitExe -C $FullPath remote add origin $authRemoteUrl
    }
}

# 4. Kiểm tra xác thực Token / Remote
function Test-Auth {
    param([string]$GitExe, $Config)
    Write-Host "`n=== KIEM TRA KET NOI VA QUYEN GITHUB ===" -ForegroundColor Cyan
    $token = $Config.settings.github_token
    foreach ($repo in $Config.repositories) {
        $fullPath = Resolve-FullPath $repo.local_path
        Write-Host "`n[Kiem tra] $($repo.name): $($repo.remote_url)" -ForegroundColor Yellow
        $authUrl = Build-AuthenticatedUrl -Url $repo.remote_url -Token $token
        $check = & $GitExe ls-remote $authUrl HEAD 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   [OK] Ket noi & quyen truy cap hop le!" -ForegroundColor Green
        } else {
            Write-Host "   [LOI] Khong the truy cap hoac tu choi quyen (403/401)." -ForegroundColor Red
            Write-Host "   Chi tiet: $check" -ForegroundColor DarkRed
        }
    }
}

# 5. Đồng bộ 1 repository
function Sync-SingleRepo {
    param(
        [string]$GitExe,
        $Repo,
        $Config
    )
    $name = $Repo.name
    $fullPath = Resolve-FullPath $Repo.local_path
    $branch = if ($Repo.branch) { $Repo.branch } else { "main" }
    $token = $Config.settings.github_token

    Write-Host "`n-------------------------------------------------------" -ForegroundColor Gray
    Write-Host "[DONG BO] $name" -ForegroundColor Cyan
    Write-Host "   Thu muc: $fullPath" -ForegroundColor DarkGray
    Write-Host "   Remote:  $($Repo.remote_url) ($branch)" -ForegroundColor DarkGray

    Ensure-RepositoryConfigured -GitExe $GitExe `
                               -FullPath $fullPath `
                               -RemoteUrl $Repo.remote_url `
                               -Branch $branch `
                               -Token $token `
                               -UserName $Config.settings.git_user_name `
                               -UserEmail $Config.settings.git_user_email

    # Pull rebase trước nếu bật cấu hình
    if ($Config.settings.auto_pull_before_push) {
        Write-Host "   -> Dang keo cap nhat tu remote (pull --rebase)..." -ForegroundColor Yellow
        $pullOut = & $GitExe -C $fullPath pull --rebase origin $branch 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "   [CANH BAO] Khong the pull (co the la repo moi chua co commit tren remote)." -ForegroundColor DarkYellow
        }
    }

    # Kiểm tra thay đổi
    $status = & $GitExe -C $fullPath status --porcelain
    if ($status) {
        Write-Host "   -> Phat hien thay doi tap tin:" -ForegroundColor Yellow
        $status | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkYellow }

        & $GitExe -C $fullPath add .
        $commitMsg = "Auto sync: $(Get-Date -Format 'dd/MM/yyyy HH:mm:ss')"
        & $GitExe -C $fullPath commit -m $commitMsg | Out-Null
        
        Write-Host "   -> Dang day code len GitHub (push)..." -ForegroundColor Yellow
        $pushOut = & $GitExe -C $fullPath push -u origin $branch 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   [OK] Day code len GitHub thanh cong!" -ForegroundColor Green
        } else {
            Write-Host "   [LOI] Khong the push len GitHub:" -ForegroundColor Red
            Write-Host "   $pushOut" -ForegroundColor Red
        }
    } else {
        Write-Host "   [OK] Ma nguon da dong bo, khong co thay doi moi." -ForegroundColor Green
    }
}

# MAIN EXECUTION
$config = Get-SyncConfig
$gitExe = Get-GitExecutable -CustomPath $config.settings.git_path
if (-not $gitExe) {
    $gitExe = Install-GitAutomatically
}

switch ($Action) {
    "SyncAll" {
        Write-Host "=======================================================" -ForegroundColor Cyan
        Write-Host "        HE THONG DONG BO GITHUB DA REPOSITORY" -ForegroundColor Cyan
        Write-Host "=======================================================" -ForegroundColor Cyan
        foreach ($repo in $config.repositories) {
            if ($repo.enabled -ne $false) {
                Sync-SingleRepo -GitExe $gitExe -Repo $repo -Config $config
            }
        }
        Write-Host "`n=======================================================" -ForegroundColor Cyan
        Write-Host "            HOAN TAT TOAN BO DONG BO!" -ForegroundColor Green
        Write-Host "=======================================================" -ForegroundColor Cyan
    }
    "TestAuth" {
        Test-Auth -GitExe $gitExe -Config $config
    }
    "SyncRepo" {
        $target = $config.repositories | Where-Object { $_.name -eq $RepoName -or $_.local_path -eq $LocalPath } | Select-Object -First 1
        if (-not $target) {
            throw "Khong tim thay repository: $RepoName"
        }
        Sync-SingleRepo -GitExe $gitExe -Repo $target -Config $config
    }
    "InstallGit" {
        $gitExe = Install-GitAutomatically
    }
}
