using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace GitSyncHub
{
    public class RepoModel
    {
        public string name { get; set; }
        public string local_path { get; set; }
        public string remote_url { get; set; }
        public string branch { get; set; }
        public bool enabled { get; set; }

        public RepoModel()
        {
            branch = "main";
            enabled = true;
        }
    }

    public class SettingsModel
    {
        public string git_path { get; set; }
        public bool auto_pull_before_push { get; set; }
        public string default_branch { get; set; }
        public string github_token { get; set; }
        public string git_user_name { get; set; }
        public string git_user_email { get; set; }

        public SettingsModel()
        {
            auto_pull_before_push = true;
            default_branch = "main";
        }
    }

    public class ConfigModel
    {
        public SettingsModel settings { get; set; }
        public List<RepoModel> repositories { get; set; }

        public ConfigModel()
        {
            settings = new SettingsModel();
            repositories = new List<RepoModel>();
        }
    }

    public class MainForm : Form
    {
        private string appDir;
        private string activeConfigPath;
        private ConfigModel currentConfig;
        private string cachedGitExe;

        // UI Controls
        private Panel headerPanel;
        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblConfigFile;
        private TableLayoutPanel actionsPanel;

        private Button btnSyncAll;
        private Button btnTestAuth;
        private Button btnInstallGit;
        private Button btnInstallPs;
        private Button btnOpenFolder;
        private Button btnBrowseConfig;

        private DataGridView gridRepos;
        private Panel repoToolbar;
        private Button btnAddRepo;
        private Button btnDeleteRepo;
        private Button btnConfig;

        private RichTextBox txtLog;
        private Panel logHeaderPanel;
        private Label lblLogTitle;
        private Button btnClearLog;

        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            activeConfigPath = FindConfigFile();

            InitializeComponent();
            cachedGitExe = LocateGitExecutable();
            LoadConfiguration();
        }

        private string FindConfigFile()
        {
            string[] searchPaths = new string[]
            {
                Path.Combine(appDir, "sync-config.json"),
                Path.Combine(Directory.GetParent(appDir) != null ? Directory.GetParent(appDir).FullName : appDir, "sync-config.json"),
                @"D:\PO\sync-config.json",
                @"D:\PO\Nghiên cứu lập app\sync-config.json"
            };

            foreach (string p in searchPaths)
            {
                if (File.Exists(p)) return p;
            }
            return Path.Combine(appDir, "sync-config.json");
        }

        private string LocateGitExecutable()
        {
            if (currentConfig != null && currentConfig.settings != null && !string.IsNullOrEmpty(currentConfig.settings.git_path))
            {
                if (File.Exists(currentConfig.settings.git_path)) return currentConfig.settings.git_path;
            }

            string[] common = new string[]
            {
                @"C:\Program Files\Git\cmd\git.exe",
                @"C:\Program Files\Git\bin\git.exe",
                @"C:\Program Files (x86)\Git\cmd\git.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\cmd\git.exe")
            };

            foreach (string p in common)
            {
                if (File.Exists(p)) return p;
            }

            try
            {
                Process p = new Process();
                p.StartInfo.FileName = "where.exe";
                p.StartInfo.Arguments = "git";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                string output = p.StandardOutput.ReadLine();
                p.WaitForExit();
                if (!string.IsNullOrEmpty(output) && File.Exists(output.Trim()))
                {
                    return output.Trim();
                }
            }
            catch { }

            return "git";
        }

        private void InitializeComponent()
        {
            this.Text = "Git AutoSync Hub - Ứng Dụng Đồng Bộ Git Độc Lập";
            this.Size = new Size(1080, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Icon = SystemIcons.Application;

            // 1. Header Panel
            headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 78;
            headerPanel.BackColor = Color.White;
            headerPanel.Padding = new Padding(24, 12, 24, 8);
            headerPanel.Paint += (s, e) => {
                using (Pen p = new Pen(Color.FromArgb(226, 232, 240))) {
                    e.Graphics.DrawLine(p, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
                }
            };

            lblTitle = new Label();
            lblTitle.Text = "⚡ Git AutoSync Hub (Standalone .EXE)";
            lblTitle.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(20, 8);

            lblSubtitle = new Label();
            lblSubtitle.Text = "Bộ điều khiển trực tiếp - Tự động hóa kéo & đẩy Git không phụ thuộc script ngoài";
            lblSubtitle.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblSubtitle.AutoSize = true;
            lblSubtitle.Location = new Point(22, 38);

            lblConfigFile = new Label();
            lblConfigFile.Text = "Cấu hình: " + activeConfigPath;
            lblConfigFile.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            lblConfigFile.ForeColor = Color.FromArgb(79, 70, 229);
            lblConfigFile.AutoSize = true;
            lblConfigFile.Location = new Point(22, 56);

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(lblConfigFile);
            this.Controls.Add(headerPanel);

            // 2. Action Buttons Panel
            actionsPanel = new TableLayoutPanel();
            actionsPanel.Dock = DockStyle.Top;
            actionsPanel.Height = 84;
            actionsPanel.ColumnCount = 6;
            actionsPanel.RowCount = 1;
            actionsPanel.Padding = new Padding(20, 12, 20, 8);

            for (int i = 0; i < 6; i++) {
                actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
            }

            btnSyncAll = CreateActionButton("⚡ Đồng Bộ Tất Cả", Color.FromArgb(79, 70, 229), Color.White);
            btnSyncAll.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnSyncAll.Click += (s, e) => ActionSyncAll();

            btnTestAuth = CreateActionButton("🔑 Kiểm Tra Quyền", Color.FromArgb(16, 185, 129), Color.White);
            btnTestAuth.Click += (s, e) => ActionTestAuth();

            btnInstallGit = CreateActionButton("📦 Kiểm Tra / Cài Git", Color.FromArgb(245, 158, 11), Color.White);
            btnInstallGit.Click += (s, e) => ActionCheckInstallGit();

            btnInstallPs = CreateActionButton("📥 Cài PowerShell 7", Color.FromArgb(14, 165, 233), Color.White);
            btnInstallPs.Click += (s, e) => ActionInstallPowerShell();

            btnBrowseConfig = CreateActionButton("📄 Đổi File Config", Color.FromArgb(139, 92, 246), Color.White);
            btnBrowseConfig.Click += (s, e) => ActionBrowseConfig();

            btnOpenFolder = CreateActionButton("📂 Mở Thư Mục Máy", Color.FromArgb(100, 116, 139), Color.White);
            btnOpenFolder.Click += (s, e) => ActionOpenFolder();

            actionsPanel.Controls.Add(btnSyncAll, 0, 0);
            actionsPanel.Controls.Add(btnTestAuth, 1, 0);
            actionsPanel.Controls.Add(btnInstallGit, 2, 0);
            actionsPanel.Controls.Add(btnInstallPs, 3, 0);
            actionsPanel.Controls.Add(btnBrowseConfig, 4, 0);
            actionsPanel.Controls.Add(btnOpenFolder, 5, 0);

            this.Controls.Add(actionsPanel);

            // 3. Middle Content: Repositories List
            Panel repoContainer = new Panel();
            repoContainer.Dock = DockStyle.Top;
            repoContainer.Height = 220;
            repoContainer.Padding = new Padding(20, 5, 20, 5);

            repoToolbar = new Panel();
            repoToolbar.Dock = DockStyle.Top;
            repoToolbar.Height = 36;

            Label lblRepoTitle = new Label();
            lblRepoTitle.Text = "Danh sách Repository đang quản lý:";
            lblRepoTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblRepoTitle.ForeColor = Color.FromArgb(30, 41, 59);
            lblRepoTitle.AutoSize = true;
            lblRepoTitle.Location = new Point(0, 8);
            repoToolbar.Controls.Add(lblRepoTitle);

            btnAddRepo = new Button();
            btnAddRepo.Text = "+ Thêm Repo";
            btnAddRepo.Size = new Size(110, 30);
            btnAddRepo.Location = new Point(540, 2);
            btnAddRepo.BackColor = Color.White;
            btnAddRepo.FlatStyle = FlatStyle.Flat;
            btnAddRepo.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnAddRepo.Cursor = Cursors.Hand;
            btnAddRepo.Click += (s, e) => ActionAddRepo();
            repoToolbar.Controls.Add(btnAddRepo);

            btnDeleteRepo = new Button();
            btnDeleteRepo.Text = "- Xóa Repo";
            btnDeleteRepo.Size = new Size(95, 30);
            btnDeleteRepo.Location = new Point(660, 2);
            btnDeleteRepo.BackColor = Color.White;
            btnDeleteRepo.FlatStyle = FlatStyle.Flat;
            btnDeleteRepo.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnDeleteRepo.Cursor = Cursors.Hand;
            btnDeleteRepo.Click += (s, e) => ActionDeleteRepo();
            repoToolbar.Controls.Add(btnDeleteRepo);

            btnConfig = new Button();
            btnConfig.Text = "⚙️ Cài Đặt Token / Git";
            btnConfig.Size = new Size(160, 30);
            btnConfig.Location = new Point(765, 2);
            btnConfig.BackColor = Color.White;
            btnConfig.FlatStyle = FlatStyle.Flat;
            btnConfig.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnConfig.Cursor = Cursors.Hand;
            btnConfig.Click += (s, e) => ActionConfig();
            repoToolbar.Controls.Add(btnConfig);

            repoContainer.Controls.Add(repoToolbar);

            gridRepos = new DataGridView();
            gridRepos.Dock = DockStyle.Fill;
            gridRepos.BackgroundColor = Color.White;
            gridRepos.BorderStyle = BorderStyle.None;
            gridRepos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridRepos.MultiSelect = false;
            gridRepos.ReadOnly = true;
            gridRepos.AllowUserToAddRows = false;
            gridRepos.RowHeadersVisible = false;
            gridRepos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridRepos.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            gridRepos.GridColor = Color.FromArgb(241, 245, 249);
            gridRepos.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            gridRepos.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            gridRepos.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(71, 85, 105);
            gridRepos.ColumnHeadersHeight = 32;
            gridRepos.EnableHeadersVisualStyles = false;

            gridRepos.Columns.Add("name", "Tên Repository");
            gridRepos.Columns.Add("local", "Thư Mục Cục Bộ");
            gridRepos.Columns.Add("remote", "Link GitHub (Remote URL)");
            gridRepos.Columns.Add("branch", "Nhánh");

            gridRepos.Columns[0].FillWeight = 22;
            gridRepos.Columns[1].FillWeight = 30;
            gridRepos.Columns[2].FillWeight = 38;
            gridRepos.Columns[3].FillWeight = 10;

            repoContainer.Controls.Add(gridRepos);
            gridRepos.BringToFront();

            this.Controls.Add(repoContainer);

            // 4. Bottom Content: Terminal Log Box
            Panel logContainer = new Panel();
            logContainer.Dock = DockStyle.Fill;
            logContainer.Padding = new Padding(20, 10, 20, 10);

            logHeaderPanel = new Panel();
            logHeaderPanel.Dock = DockStyle.Top;
            logHeaderPanel.Height = 36;
            logHeaderPanel.BackColor = Color.FromArgb(30, 41, 59);

            lblLogTitle = new Label();
            lblLogTitle.Text = "Nhật ký thực thi (Terminal Output):";
            lblLogTitle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblLogTitle.ForeColor = Color.FromArgb(226, 232, 240);
            lblLogTitle.AutoSize = true;
            lblLogTitle.Location = new Point(12, 10);
            logHeaderPanel.Controls.Add(lblLogTitle);

            btnClearLog = new Button();
            btnClearLog.Text = "Xóa Log";
            btnClearLog.Size = new Size(80, 26);
            btnClearLog.Location = new Point(935, 5);
            btnClearLog.BackColor = Color.FromArgb(51, 65, 85);
            btnClearLog.ForeColor = Color.White;
            btnClearLog.FlatStyle = FlatStyle.Flat;
            btnClearLog.FlatAppearance.BorderSize = 0;
            btnClearLog.Cursor = Cursors.Hand;
            btnClearLog.Click += (s, e) => txtLog.Clear();
            logHeaderPanel.Controls.Add(btnClearLog);

            txtLog = new RichTextBox();
            txtLog.Dock = DockStyle.Fill;
            txtLog.BackColor = Color.FromArgb(15, 23, 42);
            txtLog.ForeColor = Color.FromArgb(241, 245, 249);
            txtLog.Font = new Font("Consolas", 10f, FontStyle.Regular);
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.ReadOnly = true;

            logContainer.Controls.Add(txtLog);
            logContainer.Controls.Add(logHeaderPanel);

            this.Controls.Add(logContainer);
            logContainer.BringToFront();

            // 5. Status Strip
            statusStrip = new StatusStrip();
            statusStrip.BackColor = Color.White;
            lblStatus = new ToolStripStatusLabel("Sẵn sàng");
            lblStatus.ForeColor = Color.FromArgb(71, 85, 105);
            statusStrip.Items.Add(lblStatus);
            this.Controls.Add(statusStrip);

            AppendLog("Ứng dụng Git AutoSync Hub đã sẵn sàng!", Color.FromArgb(52, 211, 153));
            AppendLog("File cấu hình đang dùng: " + activeConfigPath, Color.FromArgb(148, 163, 184));
        }

        private Button CreateActionButton(string text, Color bg, Color fg)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Dock = DockStyle.Fill;
            btn.Margin = new Padding(4);
            btn.BackColor = bg;
            btn.ForeColor = fg;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private void AppendLog(string message, Color? color = null)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AppendLog(message, color)));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = Color.FromArgb(148, 163, 184);
            txtLog.AppendText(string.Format("[{0}] ", timestamp));

            txtLog.SelectionColor = color ?? Color.FromArgb(241, 245, 249);
            txtLog.AppendText(message + "\r\n");
            txtLog.ScrollToCaret();
        }

        private void SetStatus(string text)
        {
            if (statusStrip.InvokeRequired) {
                statusStrip.Invoke(new Action(() => SetStatus(text)));
                return;
            }
            lblStatus.Text = text;
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (this.InvokeRequired) {
                this.Invoke(new Action(() => SetControlsEnabled(enabled)));
                return;
            }
            btnSyncAll.Enabled = enabled;
            btnTestAuth.Enabled = enabled;
            btnInstallGit.Enabled = enabled;
            btnInstallPs.Enabled = enabled;
            btnAddRepo.Enabled = enabled;
            btnDeleteRepo.Enabled = enabled;
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(activeConfigPath))
                {
                    string json = File.ReadAllText(activeConfigPath, Encoding.UTF8);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    currentConfig = serializer.Deserialize<ConfigModel>(json);
                }
                else
                {
                    currentConfig = new ConfigModel();
                }

                if (currentConfig.settings == null) currentConfig.settings = new SettingsModel();
                if (currentConfig.repositories == null) currentConfig.repositories = new List<RepoModel>();

                gridRepos.Rows.Clear();
                foreach (var repo in currentConfig.repositories)
                {
                    gridRepos.Rows.Add(repo.name, repo.local_path, repo.remote_url, repo.branch ?? "main");
                }
                lblConfigFile.Text = "Cấu hình: " + activeConfigPath;
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi nạp file cấu hình: " + ex.Message, Color.FromArgb(248, 113, 113));
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(currentConfig);
                File.WriteAllText(activeConfigPath, json, Encoding.UTF8);
                LoadConfiguration();
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi lưu file cấu hình: " + ex.Message, Color.FromArgb(248, 113, 113));
            }
        }

        private string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

            string configDir = Path.GetDirectoryName(activeConfigPath);
            return Path.GetFullPath(Path.Combine(configDir, path));
        }

        private string BuildAuthUrl(string url, string token)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("https://github.com/"))
            {
                return url.Replace("https://github.com/", string.Format("https://x-access-token:{0}@github.com/", token));
            }
            return url;
        }

        // ================= DIRECT ACTIONS (NO SCRIPT DEPENDENCY) =================

        private void ActionSyncAll()
        {
            SetControlsEnabled(false);
            SetStatus("Đang đồng bộ tất cả repository...");
            AppendLog("=======================================================", Color.FromArgb(96, 165, 250));
            AppendLog("    BẮT ĐẦU ĐỒNG BỘ TẤT CẢ REPOSITORY TRỰC TIẾP", Color.FromArgb(96, 165, 250));
            AppendLog("=======================================================", Color.FromArgb(96, 165, 250));

            Task.Factory.StartNew(() => {
                try
                {
                    string git = LocateGitExecutable();
                    if (!File.Exists(git) && git != "git")
                    {
                        AppendLog("[LỖI] Không tìm thấy Git trên máy! Bấm nút 'Kiểm Tra / Cài Git' để cài đặt.", Color.FromArgb(248, 113, 113));
                        return;
                    }

                    string token = currentConfig.settings.github_token;

                    foreach (var repo in currentConfig.repositories)
                    {
                        if (!repo.enabled) continue;

                        string name = repo.name;
                        string fullPath = ResolvePath(repo.local_path);
                        string branch = string.IsNullOrEmpty(repo.branch) ? "main" : repo.branch;

                        AppendLog(string.Format("\r\n-------------------------------------------------------"), Color.FromArgb(148, 163, 184));
                        AppendLog(string.Format("[ĐỒNG BỘ] {0}", name), Color.FromArgb(56, 189, 248));
                        AppendLog(string.Format("   Thư mục: {0}", fullPath), Color.FromArgb(148, 163, 184));
                        AppendLog(string.Format("   Remote:  {0} ({1})", repo.remote_url, branch), Color.FromArgb(148, 163, 184));

                        if (!Directory.Exists(fullPath))
                        {
                            Directory.CreateDirectory(fullPath);
                        }

                        // Ensure Git initialized
                        string gitFolder = Path.Combine(fullPath, ".git");
                        if (!Directory.Exists(gitFolder))
                        {
                            AppendLog("   -> Khởi tạo Git repository...", Color.FromArgb(251, 191, 36));
                            RunCommand(git, string.Format("init -b {0}", branch), fullPath);
                        }

                        // Set User Info
                        if (!string.IsNullOrEmpty(currentConfig.settings.git_user_name))
                            RunCommand(git, string.Format("config user.name \"{0}\"", currentConfig.settings.git_user_name), fullPath);
                        if (!string.IsNullOrEmpty(currentConfig.settings.git_user_email))
                            RunCommand(git, string.Format("config user.email \"{0}\"", currentConfig.settings.git_user_email), fullPath);
                        RunCommand(git, "config core.autocrlf true", fullPath);

                        // Set Remote
                        string authUrl = BuildAuthUrl(repo.remote_url, token);
                        string remotes = RunCommand(git, "remote", fullPath);
                        if (remotes.Contains("origin"))
                        {
                            RunCommand(git, string.Format("remote set-url origin {0}", authUrl), fullPath);
                        }
                        else
                        {
                            RunCommand(git, string.Format("remote add origin {0}", authUrl), fullPath);
                        }

                        // Pull Rebase
                        if (currentConfig.settings.auto_pull_before_push)
                        {
                            AppendLog("   -> Đang kéo cập nhật từ GitHub (pull --rebase)...", Color.FromArgb(251, 191, 36));
                            string pullRes = RunCommand(git, string.Format("pull --rebase origin {0}", branch), fullPath);
                            if (pullRes.Contains("fatal") || pullRes.Contains("error"))
                            {
                                AppendLog("   [Lưu ý] Chưa có nhánh hoặc commit trên remote để pull.", Color.FromArgb(148, 163, 184));
                            }
                        }

                        // Check status
                        string status = RunCommand(git, "status --porcelain", fullPath);
                        if (!string.IsNullOrEmpty(status.Trim()))
                        {
                            AppendLog("   -> Phát hiện thay đổi tập tin, đang đóng gói commit...", Color.FromArgb(251, 191, 36));
                            RunCommand(git, "add .", fullPath);
                            string commitMsg = string.Format("Auto sync: {0}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                            RunCommand(git, string.Format("commit -m \"{0}\"", commitMsg), fullPath);

                            AppendLog("   -> Đang đẩy code lên GitHub (push)...", Color.FromArgb(251, 191, 36));
                            string pushRes = RunCommand(git, string.Format("push -u origin {0}", branch), fullPath);
                            if (pushRes.Contains("Everything up-to-date") || pushRes.Contains("->") || !pushRes.Contains("fatal"))
                            {
                                AppendLog(string.Format("   [OK] Đã đẩy thành công lên {0}!", repo.remote_url), Color.FromArgb(52, 211, 153));
                            }
                            else
                            {
                                AppendLog("   [LỖI PUSH] " + pushRes, Color.FromArgb(248, 113, 113));
                            }
                        }
                        else
                        {
                            AppendLog("   [OK] Mã nguồn đã đồng bộ, không có thay đổi mới.", Color.FromArgb(52, 211, 153));
                        }
                    }

                    AppendLog("\r\n=======================================================", Color.FromArgb(52, 211, 153));
                    AppendLog("         HOÀN TẤT ĐỒNG BỘ TẤT CẢ REPOSITORIES!", Color.FromArgb(52, 211, 153));
                    AppendLog("=======================================================", Color.FromArgb(52, 211, 153));
                }
                catch (Exception ex)
                {
                    AppendLog("Lỗi ngoại lệ khi đồng bộ: " + ex.Message, Color.FromArgb(248, 113, 113));
                }
                finally
                {
                    SetStatus("Đồng bộ hoàn tất.");
                    SetControlsEnabled(true);
                }
            });
        }

        private void ActionTestAuth()
        {
            SetControlsEnabled(false);
            SetStatus("Đang kiểm tra kết nối & quyền Token GitHub...");
            AppendLog("\r\n=== KIỂM TRA KẾT NỐI VÀ QUYỀN GITHUB ===", Color.FromArgb(251, 191, 36));

            Task.Factory.StartNew(() => {
                try
                {
                    string git = LocateGitExecutable();
                    string token = currentConfig.settings.github_token;

                    foreach (var repo in currentConfig.repositories)
                    {
                        AppendLog(string.Format("[Kiểm tra] {0}: {1}", repo.name, repo.remote_url), Color.FromArgb(251, 191, 36));
                        string authUrl = BuildAuthUrl(repo.remote_url, token);
                        string res = RunCommand(git, string.Format("ls-remote {0} HEAD", authUrl), appDir);

                        if (res.Contains("HEAD") || (!res.Contains("fatal") && !res.Contains("403") && !res.Contains("denied")))
                        {
                            AppendLog("   [OK] Kết nối & quyền hạn GitHub hợp lệ!", Color.FromArgb(52, 211, 153));
                        }
                        else
                        {
                            AppendLog("   [LỖI] Từ chối quyền truy cập hoặc sai token (403/401).", Color.FromArgb(248, 113, 113));
                            AppendLog("   Chi tiết: " + res, Color.FromArgb(248, 113, 113));
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Lỗi kiểm tra quyền: " + ex.Message, Color.FromArgb(248, 113, 113));
                }
                finally
                {
                    SetStatus("Kiểm tra quyền hoàn tất.");
                    SetControlsEnabled(true);
                }
            });
        }

        private void ActionCheckInstallGit()
        {
            string git = LocateGitExecutable();
            if (File.Exists(git) || git == "git")
            {
                string ver = RunCommand(git, "--version", appDir);
                if (!string.IsNullOrEmpty(ver))
                {
                    AppendLog(string.Format("[OK] Git đã được cài đặt trên máy: {0} ({1})", ver.Trim(), git), Color.FromArgb(52, 211, 153));
                    return;
                }
            }

            AppendLog("Không tìm thấy Git. Đang chạy cài đặt Git qua winget...", Color.FromArgb(251, 191, 36));
            SetControlsEnabled(false);
            Task.Factory.StartNew(() => {
                try
                {
                    RunProcessVisible("winget", "install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements");
                    cachedGitExe = LocateGitExecutable();
                    AppendLog("Cài đặt Git hoàn tất!", Color.FromArgb(52, 211, 153));
                }
                catch (Exception ex)
                {
                    AppendLog("Lỗi cài đặt Git: " + ex.Message, Color.FromArgb(248, 113, 113));
                }
                finally
                {
                    SetControlsEnabled(true);
                }
            });
        }

        private void ActionInstallPowerShell()
        {
            SetControlsEnabled(false);
            SetStatus("Đang tự động cài đặt PowerShell 7...");
            AppendLog(">> Đang kích hoạt cài đặt PowerShell mới nhất từ Microsoft qua winget...", Color.FromArgb(14, 165, 233));

            Task.Factory.StartNew(() => {
                try
                {
                    RunProcessVisible("winget", "install --id Microsoft.PowerShell -e --source winget --accept-package-agreements --accept-source-agreements");
                    AppendLog("Cài đặt PowerShell hoàn tất!", Color.FromArgb(52, 211, 153));
                }
                catch (Exception ex)
                {
                    AppendLog("Lỗi cài đặt PowerShell: " + ex.Message, Color.FromArgb(248, 113, 113));
                }
                finally
                {
                    SetStatus("Cài đặt hoàn tất.");
                    SetControlsEnabled(true);
                }
            });
        }

        private void ActionBrowseConfig()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "JSON Config (*.json)|*.json|All Files (*.*)|*.*";
            ofd.Title = "Chọn file cấu hình sync-config.json";
            ofd.InitialDirectory = appDir;

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                activeConfigPath = ofd.FileName;
                LoadConfiguration();
                AppendLog("Đã chuyển sang file cấu hình: " + activeConfigPath, Color.FromArgb(52, 211, 153));
            }
        }

        private void ActionOpenFolder()
        {
            try
            {
                string targetDir = Path.GetDirectoryName(activeConfigPath);
                if (string.IsNullOrEmpty(targetDir)) targetDir = appDir;
                Process.Start("explorer.exe", targetDir);
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi mở thư mục: " + ex.Message, Color.FromArgb(248, 113, 113));
            }
        }

        private void ActionAddRepo()
        {
            Form dlg = new Form();
            dlg.Text = "Thêm Repository Mới";
            dlg.Size = new Size(580, 290);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;

            Label l1 = new Label { Text = "Tên Repository:", Location = new Point(20, 20), AutoSize = true };
            TextBox tName = new TextBox { Location = new Point(160, 18), Size = new Size(380, 24) };

            Label l2 = new Label { Text = "Thư mục trên máy:", Location = new Point(20, 60), AutoSize = true };
            TextBox tPath = new TextBox { Location = new Point(160, 58), Size = new Size(290, 24) };
            Button bBrowse = new Button { Text = "Chọn...", Location = new Point(460, 56), Size = new Size(80, 28) };
            bBrowse.Click += (s, e) => {
                FolderBrowserDialog fbd = new FolderBrowserDialog();
                if (fbd.ShowDialog() == DialogResult.OK) {
                    tPath.Text = fbd.SelectedPath;
                    if (string.IsNullOrEmpty(tName.Text)) {
                        tName.Text = Path.GetFileName(fbd.SelectedPath);
                    }
                }
            };

            Label l3 = new Label { Text = "Link GitHub (URL):", Location = new Point(20, 100), AutoSize = true };
            TextBox tUrl = new TextBox { Location = new Point(160, 98), Size = new Size(380, 24) };

            Label l4 = new Label { Text = "Nhánh (Branch):", Location = new Point(20, 140), AutoSize = true };
            TextBox tBranch = new TextBox { Text = "main", Location = new Point(160, 138), Size = new Size(120, 24) };

            Button btnSave = new Button {
                Text = "Lưu & Cấu Hình",
                Location = new Point(220, 190),
                Size = new Size(130, 36),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnSave.Click += (s, e) => {
                if (string.IsNullOrEmpty(tName.Text) || string.IsNullOrEmpty(tPath.Text) || string.IsNullOrEmpty(tUrl.Text)) {
                    MessageBox.Show("Vui lòng điền đầy đủ các thông tin!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                currentConfig.repositories.Add(new RepoModel {
                    name = tName.Text.Trim(),
                    local_path = tPath.Text.Trim(),
                    remote_url = tUrl.Text.Trim(),
                    branch = string.IsNullOrEmpty(tBranch.Text) ? "main" : tBranch.Text.Trim(),
                    enabled = true
                });
                SaveConfiguration();
                AppendLog(string.Format("Đã thêm repository mới: {0} ({1})", tName.Text, tUrl.Text), Color.FromArgb(52, 211, 153));
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[] { l1, tName, l2, tPath, bBrowse, l3, tUrl, l4, tBranch, btnSave });
            dlg.ShowDialog(this);
        }

        private void ActionDeleteRepo()
        {
            if (gridRepos.SelectedRows.Count == 0) {
                MessageBox.Show("Vui lòng chọn một repository trong danh sách để xóa!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string name = gridRepos.SelectedRows[0].Cells[0].Value.ToString();
            if (MessageBox.Show(string.Format("Bạn có chắc muốn gỡ bỏ repo '{0}' khỏi danh sách quản lý? (Không làm mất file trên máy)", name), "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                currentConfig.repositories.RemoveAll(r => r.name == name);
                SaveConfiguration();
                AppendLog(string.Format("Đã xóa repo '{0}' khỏi danh sách quản lý.", name), Color.FromArgb(248, 113, 113));
            }
        }

        private void ActionConfig()
        {
            Form dlg = new Form();
            dlg.Text = "Cài Đặt Token & Định Danh Git";
            dlg.Size = new Size(620, 250);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;

            Label l1 = new Label { Text = "GitHub Token (PAT):", Location = new Point(20, 20), AutoSize = true };
            TextBox tToken = new TextBox { Text = currentConfig.settings.github_token ?? "", Location = new Point(170, 18), Size = new Size(410, 24) };

            Label l2 = new Label { Text = "Git User Name:", Location = new Point(20, 60), AutoSize = true };
            TextBox tUser = new TextBox { Text = currentConfig.settings.git_user_name ?? "", Location = new Point(170, 58), Size = new Size(410, 24) };

            Label l3 = new Label { Text = "Git User Email:", Location = new Point(20, 100), AutoSize = true };
            TextBox tEmail = new TextBox { Text = currentConfig.settings.git_user_email ?? "", Location = new Point(170, 98), Size = new Size(410, 24) };

            Button btnSave = new Button {
                Text = "Lưu Cài Đặt",
                Location = new Point(250, 150),
                Size = new Size(130, 36),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnSave.Click += (s, e) => {
                currentConfig.settings.github_token = tToken.Text.Trim();
                currentConfig.settings.git_user_name = tUser.Text.Trim();
                currentConfig.settings.git_user_email = tEmail.Text.Trim();
                SaveConfiguration();
                AppendLog("Đã lưu cài đặt Token và Git User thành công.", Color.FromArgb(52, 211, 153));
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[] { l1, tToken, l2, tUser, l3, tEmail, btnSave });
            dlg.ShowDialog(this);
        }

        private string RunCommand(string exe, string args, string workingDir)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (Process proc = Process.Start(psi))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    string combined = (stdout + "\r\n" + stderr).Trim();
                    return combined;
                }
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        private void RunProcessVisible(string exe, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi chạy tiến trình: " + ex.Message, Color.FromArgb(248, 113, 113));
            }
        }
    }
}
