# git_gui.ps1 - Giao dien quan ly va dong bo Git Repository
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$script:BaseDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$script:ConfigPath = Join-Path $script:BaseDir "sync-config.json"
$script:ManagerScript = Join-Path $script:BaseDir "scripts\git_manager.ps1"

function Load-Config {
    if (Test-Path $script:ConfigPath) {
        $raw = Get-Content $script:ConfigPath -Raw -Encoding UTF8
        return ($raw | ConvertFrom-Json)
    }
    return $null
}

function Save-Config ($cfg) {
    $json = $cfg | ConvertTo-Json -Depth 5
    Set-Content -Path $script:ConfigPath -Value $json -Encoding UTF8
}

# Khoi tao Form chinh
$form = New-Object System.Windows.Forms.Form
$form.Text = "Git AutoSync Hub - Quan ly & Dong bo da Repository"
$form.Size = New-Object System.Drawing.Size(950, 680)
$form.StartPosition = "CenterScreen"
$form.BackColor = [System.Drawing.Color]::FromArgb(245, 247, 250)
$form.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)

# Tieu de Header
$lblHeader = New-Object System.Windows.Forms.Label
$lblHeader.Text = "Git AutoSync Hub"
$lblHeader.Font = New-Object System.Drawing.Font("Segoe UI", 16, [System.Drawing.FontStyle]::Bold)
$lblHeader.ForeColor = [System.Drawing.Color]::FromArgb(24, 43, 73)
$lblHeader.Location = New-Object System.Drawing.Point(20, 15)
$lblHeader.Size = New-Object System.Drawing.Size(400, 32)
$form.Controls.Add($lblHeader)

$lblSub = New-Object System.Windows.Forms.Label
$lblSub.Text = "Quan ly linh hoat moi thu muc tren may & repo GitHub - 1 click la dong bo toan bo"
$lblSub.ForeColor = [System.Drawing.Color]::FromArgb(100, 110, 125)
$lblSub.Location = New-Object System.Drawing.Point(22, 48)
$lblSub.Size = New-Object System.Drawing.Size(650, 25)
$form.Controls.Add($lblSub)

# DataGridView hien thi danh sach Repo
$grid = New-Object System.Windows.Forms.DataGridView
$grid.Location = New-Object System.Drawing.Point(20, 80)
$grid.Size = New-Object System.Drawing.Size(895, 210)
$grid.BackgroundColor = [System.Drawing.Color]::White
$grid.BorderStyle = "Fixed3D"
$grid.SelectionMode = "FullRowSelect"
$grid.MultiSelect = $false
$grid.AllowUserToAddRows = $false
$grid.ReadOnly = $true
$grid.RowHeadersVisible = $false
$grid.AutoSizeColumnsMode = "Fill"

$colName = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colName.HeaderText = "Ten Repository"
$colName.FillWeight = 20
$grid.Columns.Add($colName) | Out-Null

$colLocal = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colLocal.HeaderText = "Thu muc tren may (Local)"
$colLocal.FillWeight = 30
$grid.Columns.Add($colLocal) | Out-Null

$colRemote = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colRemote.HeaderText = "Link GitHub (Remote)"
$colRemote.FillWeight = 40
$grid.Columns.Add($colRemote) | Out-Null

$colBranch = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colBranch.HeaderText = "Nhanh"
$colBranch.FillWeight = 10
$grid.Columns.Add($colBranch) | Out-Null

$form.Controls.Add($grid)

function Refresh-Grid {
    $grid.Rows.Clear()
    $c = Load-Config
    if ($c -and $c.repositories) {
        foreach ($r in $c.repositories) {
            $grid.Rows.Add($r.name, $r.local_path, $r.remote_url, $r.branch) | Out-Null
        }
    }
}
Refresh-Grid

# Log Box
$lblLog = New-Object System.Windows.Forms.Label
$lblLog.Text = "Nhat ky dong bo (Log Output):"
$lblLog.Location = New-Object System.Drawing.Point(20, 350)
$lblLog.Size = New-Object System.Drawing.Size(250, 20)
$lblLog.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$form.Controls.Add($lblLog)

$txtLog = New-Object System.Windows.Forms.TextBox
$txtLog.Location = New-Object System.Drawing.Point(20, 375)
$txtLog.Size = New-Object System.Drawing.Size(895, 245)
$txtLog.Multiline = $true
$txtLog.ScrollBars = "Vertical"
$txtLog.ReadOnly = $true
$txtLog.BackColor = [System.Drawing.Color]::FromArgb(30, 34, 42)
$txtLog.ForeColor = [System.Drawing.Color]::FromArgb(230, 235, 245)
$txtLog.Font = New-Object System.Drawing.Font("Consolas", 9.5)
$form.Controls.Add($txtLog)

function Append-Log ($msg) {
    $time = (Get-Date).ToString("HH:mm:ss")
    $txtLog.AppendText("[$time] $msg`r`n")
    $txtLog.SelectionStart = $txtLog.Text.Length
    $txtLog.ScrollToCaret()
    [System.Windows.Forms.Application]::DoEvents()
}

# Nut bam chuc nang
$btnSyncAll = New-Object System.Windows.Forms.Button
$btnSyncAll.Text = ">> DONG BO TAT CA <<"
$btnSyncAll.Location = New-Object System.Drawing.Point(20, 302)
$btnSyncAll.Size = New-Object System.Drawing.Size(200, 36)
$btnSyncAll.BackColor = [System.Drawing.Color]::FromArgb(37, 99, 235)
$btnSyncAll.ForeColor = [System.Drawing.Color]::White
$btnSyncAll.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$btnSyncAll.FlatStyle = "Flat"
$form.Controls.Add($btnSyncAll)

$btnAdd = New-Object System.Windows.Forms.Button
$btnAdd.Text = "+ Them Repo"
$btnAdd.Location = New-Object System.Drawing.Point(230, 302)
$btnAdd.Size = New-Object System.Drawing.Size(130, 36)
$btnAdd.BackColor = [System.Drawing.Color]::FromArgb(255, 255, 255)
$btnAdd.FlatStyle = "Flat"
$form.Controls.Add($btnAdd)

$btnRemove = New-Object System.Windows.Forms.Button
$btnRemove.Text = "- Xoa Repo"
$btnRemove.Location = New-Object System.Drawing.Point(370, 302)
$btnRemove.Size = New-Object System.Drawing.Size(110, 36)
$btnRemove.BackColor = [System.Drawing.Color]::FromArgb(255, 255, 255)
$btnRemove.FlatStyle = "Flat"
$form.Controls.Add($btnRemove)

$btnTest = New-Object System.Windows.Forms.Button
$btnTest.Text = "Kiem Tra Token"
$btnTest.Location = New-Object System.Drawing.Point(490, 302)
$btnTest.Size = New-Object System.Drawing.Size(150, 36)
$btnTest.BackColor = [System.Drawing.Color]::FromArgb(255, 255, 255)
$btnTest.FlatStyle = "Flat"
$form.Controls.Add($btnTest)

$btnConfig = New-Object System.Windows.Forms.Button
$btnConfig.Text = "Cai Dat Token"
$btnConfig.Location = New-Object System.Drawing.Point(650, 302)
$btnConfig.Size = New-Object System.Drawing.Size(130, 36)
$btnConfig.BackColor = [System.Drawing.Color]::FromArgb(255, 255, 255)
$btnConfig.FlatStyle = "Flat"
$form.Controls.Add($btnConfig)

# Xu ly su kien Sync All
$btnSyncAll.Add_Click({
    $btnSyncAll.Enabled = $false
    Append-Log "Bat dau tien trinh dong bo toan bo..."
    try {
        $pinfo = New-Object System.Diagnostics.ProcessStartInfo
        $pinfo.FileName = "powershell.exe"
        $pinfo.Arguments = "-ExecutionPolicy Bypass -File `"$script:ManagerScript`" -Action SyncAll"
        $pinfo.RedirectStandardOutput = $true
        $pinfo.RedirectStandardError = $true
        $pinfo.UseShellExecute = $false
        $pinfo.CreateNoWindow = $true

        $proc = New-Object System.Diagnostics.Process
        $proc.StartInfo = $pinfo
        $proc.Start() | Out-Null
        $stdout = $proc.StandardOutput.ReadToEnd()
        $stderr = $proc.StandardError.ReadToEnd()
        $proc.WaitForExit()

        Append-Log $stdout
        if ($stderr) { Append-Log "[LOI] $stderr" }
        Append-Log "Hoan tat!"
    } catch {
        Append-Log "[NGOAI LE] $_"
    } finally {
        $btnSyncAll.Enabled = $true
    }
})

# Xu ly Test Token
$btnTest.Add_Click({
    Append-Log "Dang kiem tra quyen ket noi voi GitHub..."
    try {
        $res = & powershell.exe -ExecutionPolicy Bypass -File "$script:ManagerScript" -Action TestAuth
        Append-Log ($res -join "`r`n")
    } catch {
        Append-Log "[LOI] $_"
    }
})

# Xu ly Them Repo
$btnAdd.Add_Click({
    $subForm = New-Object System.Windows.Forms.Form
    $subForm.Text = "Them Repository Moi"
    $subForm.Size = New-Object System.Drawing.Size(600, 320)
    $subForm.StartPosition = "CenterParent"
    $subForm.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)

    $l1 = New-Object System.Windows.Forms.Label; $l1.Text = "Ten Repository:"; $l1.Location = "20, 20"; $l1.Size = "150, 20"; $subForm.Controls.Add($l1)
    $tName = New-Object System.Windows.Forms.TextBox; $tName.Location = "180, 18"; $tName.Size = "380, 25"; $subForm.Controls.Add($tName)

    $l2 = New-Object System.Windows.Forms.Label; $l2.Text = "Thu muc tren may:"; $l2.Location = "20, 60"; $l2.Size = "150, 20"; $subForm.Controls.Add($l2)
    $tPath = New-Object System.Windows.Forms.TextBox; $tPath.Location = "180, 58"; $tPath.Size = "290, 25"; $subForm.Controls.Add($tPath)
    $bBrowse = New-Object System.Windows.Forms.Button; $bBrowse.Text = "Chon..."; $bBrowse.Location = "480, 56"; $bBrowse.Size = "80, 28"; $subForm.Controls.Add($bBrowse)

    $bBrowse.Add_Click({
        $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
        if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            $tPath.Text = $dlg.SelectedPath
            if (-not $tName.Text) { $tName.Text = (Split-Path $dlg.SelectedPath -Leaf) }
        }
    })

    $l3 = New-Object System.Windows.Forms.Label; $l3.Text = "Link GitHub (URL):"; $l3.Location = "20, 100"; $l3.Size = "150, 20"; $subForm.Controls.Add($l3)
    $tUrl = New-Object System.Windows.Forms.TextBox; $tUrl.Location = "180, 98"; $tUrl.Size = "380, 25"; $subForm.Controls.Add($tUrl)

    $l4 = New-Object System.Windows.Forms.Label; $l4.Text = "Nhanh (Branch):"; $l4.Location = "20, 140"; $l4.Size = "150, 20"; $subForm.Controls.Add($l4)
    $tBranch = New-Object System.Windows.Forms.TextBox; $tBranch.Text = "main"; $tBranch.Location = "180, 138"; $tBranch.Size = "120, 25"; $subForm.Controls.Add($tBranch)

    $btnSave = New-Object System.Windows.Forms.Button
    $btnSave.Text = "Luu & Khoi tao"
    $btnSave.Location = "220, 200"; $btnSave.Size = "140, 36"
    $btnSave.BackColor = [System.Drawing.Color]::FromArgb(37, 99, 235); $btnSave.ForeColor = "White"; $btnSave.FlatStyle = "Flat"
    $subForm.Controls.Add($btnSave)

    $btnSave.Add_Click({
        if (-not $tName.Text -or -not $tPath.Text -or -not $tUrl.Text) {
            [System.Windows.Forms.MessageBox]::Show("Vui long dien day du cac thong tin!", "Thong bao", "OK", "Warning")
            return
        }
        $c = Load-Config
        $newRepo = [PSCustomObject]@{
            name = $tName.Text
            local_path = $tPath.Text
            remote_url = $tUrl.Text
            branch = if ($tBranch.Text) { $tBranch.Text } else { "main" }
            enabled = $true
        }
        $c.repositories += $newRepo
        Save-Config $c
        Refresh-Grid
        Append-Log "Da them repository moi: $($tName.Text) -> $($tUrl.Text)"
        $subForm.Close()
    })

    $subForm.ShowDialog() | Out-Null
})

# Xu ly Xoa Repo
$btnRemove.Add_Click({
    if ($grid.SelectedRows.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show("Vui long chon 1 repository trong danh sach de xoa!", "Thong bao")
        return
    }
    $selectedName = $grid.SelectedRows[0].Cells[0].Value
    $confirm = [System.Windows.Forms.MessageBox]::Show("Ban co chac muon go bo '$selectedName' khoi danh sach? (Khong lam mat file tren may)", "Xac nhan", "YesNo", "Question")
    if ($confirm -eq [System.Windows.Forms.DialogResult]::Yes) {
        $c = Load-Config
        $c.repositories = @($c.repositories | Where-Object { $_.name -ne $selectedName })
        Save-Config $c
        Refresh-Grid
        Append-Log "Da xoa bo '$selectedName' khoi danh sach quan ly."
    }
})

# Xu ly Cai dat Token
$btnConfig.Add_Click({
    $cfgForm = New-Object System.Windows.Forms.Form
    $cfgForm.Text = "Cai Dat Token & Thong Tin Git"
    $cfgForm.Size = New-Object System.Drawing.Size(650, 260)
    $cfgForm.StartPosition = "CenterParent"
    $cfgForm.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)

    $c = Load-Config

    $l1 = New-Object System.Windows.Forms.Label; $l1.Text = "GitHub Token (PAT):"; $l1.Location = "20, 20"; $l1.Size = "160, 20"; $cfgForm.Controls.Add($l1)
    $tToken = New-Object System.Windows.Forms.TextBox; $tToken.Text = $c.settings.github_token; $tToken.Location = "190, 18"; $tToken.Size = "420, 25"; $cfgForm.Controls.Add($tToken)

    $l2 = New-Object System.Windows.Forms.Label; $l2.Text = "Git User Name:"; $l2.Location = "20, 60"; $l2.Size = "160, 20"; $cfgForm.Controls.Add($l2)
    $tUser = New-Object System.Windows.Forms.TextBox; $tUser.Text = $c.settings.git_user_name; $tUser.Location = "190, 58"; $tUser.Size = "420, 25"; $cfgForm.Controls.Add($tUser)

    $l3 = New-Object System.Windows.Forms.Label; $l3.Text = "Git User Email:"; $l3.Location = "20, 100"; $l3.Size = "160, 20"; $cfgForm.Controls.Add($l3)
    $tEmail = New-Object System.Windows.Forms.TextBox; $tEmail.Text = $c.settings.git_user_email; $tEmail.Location = "190, 98"; $tEmail.Size = "420, 25"; $cfgForm.Controls.Add($tEmail)

    $btnSaveCfg = New-Object System.Windows.Forms.Button
    $btnSaveCfg.Text = "Luu Cau Hinh"
    $btnSaveCfg.Location = "260, 150"; $btnSaveCfg.Size = "140, 36"
    $btnSaveCfg.BackColor = [System.Drawing.Color]::FromArgb(37, 99, 235); $btnSaveCfg.ForeColor = "White"; $btnSaveCfg.FlatStyle = "Flat"
    $cfgForm.Controls.Add($btnSaveCfg)

    $btnSaveCfg.Add_Click({
        $c.settings.github_token = $tToken.Text.Trim()
        $c.settings.git_user_name = $tUser.Text.Trim()
        $c.settings.git_user_email = $tEmail.Text.Trim()
        Save-Config $c
        Append-Log "Da cap nhat GitHub Token va thong tin Git."
        $cfgForm.Close()
    })

    $cfgForm.ShowDialog() | Out-Null
})

# Chay Form
$form.ShowDialog() | Out-Null
