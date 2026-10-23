// Git AutoSync Hub v2 - ứng dụng Windows độc lập (.NET Framework 4.x, không cần PowerShell).
// Biên dịch: xem build.bat
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace GitSyncHub
{
    // ======================= MÔ HÌNH CẤU HÌNH =======================
    public class RepoModel
    {
        public string name { get; set; }
        public string local_path { get; set; }
        public string remote_url { get; set; }
        public string branch { get; set; }
        public bool enabled { get; set; }
        public RepoModel() { branch = "main"; enabled = true; }
    }

    public class SettingsModel
    {
        public string git_path { get; set; }
        public bool auto_pull_before_push { get; set; }
        public string default_branch { get; set; }
        public string github_token { get; set; }       // cũ: chữ thường (nên chuyển sang mã hóa)
        public string github_token_enc { get; set; }   // mới: mã hóa DPAPI theo tài khoản Windows của máy này
        public string git_user_name { get; set; }
        public string git_user_email { get; set; }
        public int auto_sync_minutes { get; set; }      // 0 = tắt tự động đồng bộ
        public string commit_message { get; set; }      // {time} = thời điểm

        public SettingsModel()
        {
            git_path = "auto";
            auto_pull_before_push = true;
            default_branch = "main";
            commit_message = "Auto sync: {time}";
        }
    }

    public class ConfigModel
    {
        public SettingsModel settings { get; set; }
        public List<RepoModel> repositories { get; set; }
        public ConfigModel() { settings = new SettingsModel(); repositories = new List<RepoModel>(); }
    }

    // ======================= TIỆN ÍCH =======================
    public static class Util
    {
        static readonly byte[] Salt = Encoding.UTF8.GetBytes("GitSyncHub.v2");

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            byte[] b = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Salt, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(b);
        }

        public static string Unprotect(string enc)
        {
            if (string.IsNullOrEmpty(enc)) return "";
            try
            {
                byte[] b = ProtectedData.Unprotect(Convert.FromBase64String(enc), Salt, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(b);
            }
            catch { return null; } // sai máy / sai tài khoản Windows
        }

        public static string PrettyJson(string json)
        {
            StringBuilder sb = new StringBuilder();
            int indent = 0; bool inStr = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inStr)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < json.Length) { sb.Append(json[++i]); }
                    else if (c == '"') inStr = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inStr = true; sb.Append(c); break;
                    case '{': case '[':
                        sb.Append(c); indent++; sb.Append("\r\n").Append(' ', indent * 2); break;
                    case '}': case ']':
                        indent--; sb.Append("\r\n").Append(' ', indent * 2).Append(c); break;
                    case ',': sb.Append(c).Append("\r\n").Append(' ', indent * 2); break;
                    case ':': sb.Append(": "); break;
                    default: if (!char.IsWhiteSpace(c)) sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        public static string Quote(string s) { return "\"" + (s ?? "").Replace("\"", "\\\"") + "\""; }

        public static bool IsGitHubHttps(string url)
        {
            return !string.IsNullOrEmpty(url) && url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase);
        }

        // Bỏ token nhúng trong URL (https://token@github.com/...) để không lưu bí mật trong .git/config
        public static string CleanUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            int s = url.IndexOf("://"); int at = url.IndexOf('@');
            int slash = s >= 0 ? url.IndexOf('/', s + 3) : -1;
            if (s >= 0 && at > s && (slash < 0 || at < slash)) return url.Substring(0, s + 3) + url.Substring(at + 1);
            return url;
        }

        [DllImport("kernel32.dll")] public static extern bool AttachConsole(int pid);
    }

    // ======================= LÕI GIT (dùng chung cho giao diện và dòng lệnh) =======================
    public class GitResult
    {
        public int Code; public string Out;
        public bool Ok { get { return Code == 0; } }
    }

    public class GitEngine
    {
        public ConfigModel Cfg;
        public string ConfigDir;
        public Action<string, Color?> Log = delegate { };
        public volatile bool Cancel;

        public static readonly Color Ok = Color.FromArgb(52, 211, 153);
        public static readonly Color Err = Color.FromArgb(248, 113, 113);
        public static readonly Color Warn = Color.FromArgb(251, 191, 36);
        public static readonly Color Info = Color.FromArgb(56, 189, 248);
        public static readonly Color Dim = Color.FromArgb(148, 163, 184);

        string Token()
        {
            string t = Util.Unprotect(Cfg.settings.github_token_enc);
            if (!string.IsNullOrEmpty(t)) return t;
            return Cfg.settings.github_token ?? "";
        }
        public bool TokenUnreadable()
        {
            return !string.IsNullOrEmpty(Cfg.settings.github_token_enc) && Util.Unprotect(Cfg.settings.github_token_enc) == null;
        }

        public string ResolvePath(string p)
        {
            if (string.IsNullOrEmpty(p)) return p;
            if (Path.IsPathRooted(p)) return Path.GetFullPath(p);
            return Path.GetFullPath(Path.Combine(ConfigDir, p));
        }

        public string FindGit()
        {
            string cfg = Cfg.settings.git_path;
            if (!string.IsNullOrEmpty(cfg) && cfg != "auto" && File.Exists(cfg)) return cfg;
            string pf = Environment.GetEnvironmentVariable("ProgramFiles");
            string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            string la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            List<string> c = new List<string>();
            if (pf != null) { c.Add(Path.Combine(pf, @"Git\cmd\git.exe")); c.Add(Path.Combine(pf, @"Git\bin\git.exe")); }
            if (pf86 != null) c.Add(Path.Combine(pf86, @"Git\cmd\git.exe"));
            c.Add(Path.Combine(la, @"Programs\Git\cmd\git.exe"));
            foreach (string p in c) if (File.Exists(p)) return p;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string d in path.Split(';'))
            {
                try { string f = Path.Combine(d.Trim(), "git.exe"); if (File.Exists(f)) return f; } catch { }
            }
            return null;
        }

        string Mask(string s)
        {
            string t = Token();
            if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(s)) return s;
            s = s.Replace(t, "***");
            s = s.Replace(Convert.ToBase64String(Encoding.ASCII.GetBytes("x-access-token:" + t)), "***");
            return s;
        }

        public GitResult Run(string exe, string args, string cwd, bool auth, int timeoutMs)
        {
            GitResult r = new GitResult();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe; psi.Arguments = args; psi.WorkingDirectory = cwd;
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8; psi.StandardErrorEncoding = Encoding.UTF8;
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                psi.EnvironmentVariables["LC_ALL"] = "C.UTF-8";
                string tok = Token();
                if (auth && !string.IsNullOrEmpty(tok))
                {
                    // Token chuyển qua biến môi trường của tiến trình con, không nằm trên dòng lệnh và không ghi vào .git/config
                    psi.EnvironmentVariables["GIT_CONFIG_COUNT"] = "1";
                    psi.EnvironmentVariables["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader";
                    psi.EnvironmentVariables["GIT_CONFIG_VALUE_0"] = "AUTHORIZATION: basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("x-access-token:" + tok));
                }
                StringBuilder sb = new StringBuilder();
                using (Process p = Process.Start(psi))
                {
                    p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } r.Code = -1; r.Out = "Hết thời gian chờ (" + (timeoutMs / 1000) + "s)"; return r; }
                    p.WaitForExit();
                    r.Code = p.ExitCode;
                }
                r.Out = Mask(sb.ToString().Trim());
            }
            catch (Exception ex) { r.Code = -2; r.Out = "ERROR: " + ex.Message; }
            return r;
        }

        GitResult G(string git, string args, string cwd, bool auth)
        {
            return Run(git, args, cwd, auth, auth ? 180000 : 60000);
        }

        void Show(GitResult r, Color c)
        {
            if (!string.IsNullOrEmpty(r.Out))
                foreach (string line in r.Out.Split('\n')) { string l = line.TrimEnd(); if (l.Length > 0) Log("      " + l, c); }
        }

        // ---- đồng bộ một repository. Trả về chuỗi trạng thái ngắn; ok=false nếu lỗi ----
        public string SyncRepo(RepoModel r, out bool ok)
        {
            ok = false;
            string git = FindGit();
            if (git == null) { Log("[LỖI] Chưa có Git. Vào Công cụ → Kiểm tra / Cài Git.", Err); return "Thiếu Git"; }
            string path = ResolvePath(r.local_path);
            string branch = string.IsNullOrEmpty(r.branch) ? (Cfg.settings.default_branch ?? "main") : r.branch;
            string url = Util.CleanUrl(r.remote_url);

            Log("", null);
            Log("▶ " + r.name, Info);
            Log("   Thư mục: " + path, Dim);
            Log("   Remote:  " + url + "  (" + branch + ")", Dim);

            if (string.IsNullOrEmpty(path)) { Log("   [LỖI] Chưa đặt thư mục.", Err); return "Thiếu thư mục"; }
            if (string.IsNullOrEmpty(url)) { Log("   [LỖI] Chưa đặt link GitHub.", Err); return "Thiếu link"; }
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            if (!Directory.Exists(Path.Combine(path, ".git")))
            {
                Log("   → Khởi tạo Git repository…", Warn);
                GitResult i = G(git, "init", path, false);
                if (!i.Ok) { Show(i, Err); return "Lỗi init"; }
                G(git, "symbolic-ref HEAD refs/heads/" + branch, path, false);
            }

            if (!string.IsNullOrEmpty(Cfg.settings.git_user_name)) G(git, "config user.name " + Util.Quote(Cfg.settings.git_user_name), path, false);
            if (!string.IsNullOrEmpty(Cfg.settings.git_user_email)) G(git, "config user.email " + Util.Quote(Cfg.settings.git_user_email), path, false);

            GitResult cur = G(git, "remote get-url origin", path, false);
            if (!cur.Ok) G(git, "remote add origin " + Util.Quote(url), path, false);
            else if (cur.Out.Trim() != url) G(git, "remote set-url origin " + Util.Quote(url), path, false);

            if (Util.IsGitHubHttps(url) && string.IsNullOrEmpty(Token()))
                Log("   [Lưu ý] Chưa có token GitHub; chỉ chạy được nếu máy đã đăng nhập GitHub (Git Credential Manager).", Warn);

            // 1) commit thay đổi cục bộ trước, để pull --rebase không bị chặn bởi file đang sửa dở
            GitResult st = G(git, "status --porcelain", path, false);
            bool committed = false;
            if (st.Ok && st.Out.Trim().Length > 0)
            {
                Log("   → Có thay đổi, đang commit…", Warn);
                GitResult a = G(git, "add -A", path, false);
                if (!a.Ok) { Show(a, Err); return "Lỗi add"; }
                string msg = (Cfg.settings.commit_message ?? "Auto sync: {time}").Replace("{time}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                GitResult c = G(git, "commit -m " + Util.Quote(msg), path, false);
                if (!c.Ok) { Show(c, Err); return "Lỗi commit"; }
                committed = true;
            }

            // 2) kéo bản mới nhất (nếu nhánh đã có trên remote)
            if (Cfg.settings.auto_pull_before_push)
            {
                GitResult has = G(git, "ls-remote --exit-code --heads origin " + Util.Quote(branch), path, true);
                if (has.Code == 0)
                {
                    Log("   → Kéo cập nhật (pull --rebase)…", Warn);
                    GitResult p = G(git, "pull --rebase --autostash origin " + Util.Quote(branch), path, true);
                    if (!p.Ok)
                    {
                        G(git, "rebase --abort", path, false);
                        Log("   [LỖI PULL] Có xung đột hoặc lỗi mạng/quyền. Đã hủy rebase, dữ liệu cục bộ giữ nguyên.", Err);
                        Show(p, Err);
                        return "Lỗi pull";
                    }
                }
                else if (has.Code == 2) Log("   (Nhánh chưa có trên remote, bỏ qua pull)", Dim);
                else { Log("   [LỖI] Không kết nối được remote (sai link/token/quyền hoặc mất mạng).", Err); Show(has, Err); return "Lỗi kết nối"; }
            }

            // 3) đẩy lên
            Log("   → Đẩy lên GitHub (push)…", Warn);
            GitResult ps = G(git, "push -u origin HEAD:refs/heads/" + branch, path, true);
            if (!ps.Ok) { Log("   [LỖI PUSH]", Err); Show(ps, Err); return "Lỗi push"; }
            bool upToDate = ps.Out.Contains("Everything up-to-date");
            Log(upToDate ? "   [OK] Đã đồng bộ, không có gì mới." : "   [OK] Đã đẩy thành công.", Ok);
            ok = true;
            return upToDate && !committed ? "Đã đồng bộ" : "Đã đẩy lúc " + DateTime.Now.ToString("HH:mm");
        }

        public string TestRepo(RepoModel r, out bool ok)
        {
            ok = false;
            string git = FindGit();
            if (git == null) { Log("[LỖI] Chưa có Git.", Err); return "Thiếu Git"; }
            string url = Util.CleanUrl(r.remote_url);
            Log("🔑 " + r.name + ": " + url, Warn);
            GitResult res = Run(git, "ls-remote --heads " + Util.Quote(url), ConfigDir, true, 60000);
            if (res.Ok) { Log("   [OK] Kết nối và quyền đọc hợp lệ.", Ok); ok = true; return "Kết nối tốt"; }
            Log("   [LỖI] Từ chối truy cập hoặc không kết nối được (401/403/404/mạng).", Err); Show(res, Err);
            return "Lỗi quyền";
        }
    }

    // ======================= CHƯƠNG TRÌNH =======================
    public static class Program
    {
        public static string FindConfig(string explicitPath)
        {
            if (!string.IsNullOrEmpty(explicitPath)) return Path.GetFullPath(explicitPath);
            string app = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string parent = Directory.GetParent(app) != null ? Directory.GetParent(app).FullName : app;
            string roam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitSyncHub");
            string[] cands = new string[] { Path.Combine(app, "sync-config.json"), Path.Combine(parent, "sync-config.json"), Path.Combine(roam, "sync-config.json") };
            foreach (string p in cands) if (File.Exists(p)) return p;
            // chưa có: tạo cạnh file .exe nếu ghi được, nếu không thì vào %APPDATA%
            try { string t = Path.Combine(app, ".write-test"); File.WriteAllText(t, "x"); File.Delete(t); return cands[0]; }
            catch { Directory.CreateDirectory(roam); return cands[2]; }
        }

        public static ConfigModel Load(string path)
        {
            ConfigModel c = null;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                c = new JavaScriptSerializer().Deserialize<ConfigModel>(json);
            }
            if (c == null) c = new ConfigModel();
            if (c.settings == null) c.settings = new SettingsModel();
            if (c.repositories == null) c.repositories = new List<RepoModel>();
            return c;
        }

        public static void Save(string path, ConfigModel c)
        {
            string json = Util.PrettyJson(new JavaScriptSerializer().Serialize(c));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        [STAThread]
        public static int Main(string[] args)
        {
            string cfgArg = null, logFile = null; bool cli = false, test = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--config" && i + 1 < args.Length) cfgArg = args[++i];
                else if (args[i] == "--log" && i + 1 < args.Length) logFile = args[++i];
                else if (args[i] == "--sync-all") cli = true;
                else if (args[i] == "--test-auth") { cli = true; test = true; }
            }
            if (cli) return RunCli(cfgArg, logFile, test);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(FindConfig(cfgArg)));
            return 0;
        }

        // Chế độ dòng lệnh: GitSyncHub.exe --sync-all [--config file] [--log file]  (dùng cho Task Scheduler, không cần PowerShell)
        static int RunCli(string cfgArg, string logFile, bool test)
        {
            Util.AttachConsole(-1);
            string cfgPath = FindConfig(cfgArg);
            GitEngine eng = new GitEngine();
            eng.Cfg = Load(cfgPath); eng.ConfigDir = Path.GetDirectoryName(cfgPath);
            StreamWriter lw = null;
            if (!string.IsNullOrEmpty(logFile)) lw = new StreamWriter(logFile, true, new UTF8Encoding(false));
            eng.Log = delegate (string m, Color? c)
            {
                string line = (m.Length == 0 ? "" : "[" + DateTime.Now.ToString("HH:mm:ss") + "] ") + m;
                try { Console.WriteLine(line); } catch { }
                if (lw != null) { lw.WriteLine(line); lw.Flush(); }
            };
            int bad = 0;
            foreach (RepoModel r in eng.Cfg.repositories)
            {
                if (!r.enabled) continue;
                bool ok;
                try { if (test) eng.TestRepo(r, out ok); else eng.SyncRepo(r, out ok); }
                catch (Exception ex) { eng.Log("[LỖI] " + ex.Message, null); ok = false; }
                if (!ok) bad++;
            }
            if (lw != null) lw.Dispose();
            return bad == 0 ? 0 : 1;
        }
    }

    // ======================= CONTROL TÙY BIẾN (giao diện mềm, bo góc) =======================
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(244, 246, 251);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(228, 232, 240);
        public static readonly Color Text = Color.FromArgb(30, 41, 59);
        public static readonly Color Sub = Color.FromArgb(100, 116, 139);
        public static readonly Color Primary = Color.FromArgb(99, 102, 241);
        public static readonly Color PrimaryDark = Color.FromArgb(79, 70, 229);
        public static readonly Color Green = Color.FromArgb(16, 185, 129);
        public static readonly Color Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color Red = Color.FromArgb(239, 68, 68);

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            int d = rad * 2; GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    public class RoundButton : Button
    {
        public Color Fill = Theme.Primary; public Color Fore = Color.White; public bool Outline;
        bool hover, down;
        public RoundButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = new Font("Segoe UI Semibold", 9.5f); Height = 38; Margin = new Padding(0, 0, 8, 0);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            if (Parent != null) g.Clear(Parent.BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bg = Outline ? Theme.Card : Fill, fg = Fore;
            if (!Enabled) { bg = Outline ? Theme.Card : Theme.Mix(Fill, Color.White, 0.65f); fg = Theme.Mix(Fore, Color.White, 0.5f); if (Outline) fg = Color.FromArgb(180, 188, 200); }
            else if (down) bg = Outline ? Color.FromArgb(224, 228, 255) : Theme.Mix(Fill, Color.Black, 0.15f);
            else if (hover) bg = Outline ? Color.FromArgb(238, 240, 255) : Theme.Mix(Fill, Color.White, 0.12f);
            using (GraphicsPath p = Theme.Round(r, 10))
            {
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                if (Outline) using (Pen pen = new Pen(hover ? Theme.Primary : Theme.Border, 1.2f)) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), Outline ? (Enabled ? Theme.Text : fg) : fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    public class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Padding = new Padding(16);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Parent != null) g.Clear(Parent.BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 2, Height - 2);
            using (GraphicsPath p = Theme.Round(r, 14))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(10, 15, 23, 42))) { g.TranslateTransform(0, 1.5f); g.FillPath(b, p); g.ResetTransform(); }
                using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
            }
        }
    }

    // ======================= CỬA SỔ CHÍNH =======================
    public class MainForm : Form
    {
        string cfgPath; ConfigModel cfg; GitEngine eng = new GitEngine();
        DataGridView grid; RichTextBox txtLog; Label lblStatus, lblCfg, lblWarn;
        ProgressBar bar; RoundButton btnSyncAll, btnSyncSel, btnTest, btnSettings, btnTools, btnAdd, btnEdit, btnDel;
        CheckBox chkAuto; NumericUpDown numMin; System.Windows.Forms.Timer timer;
        NotifyIcon tray; bool busy, reallyExit, loading;

        public MainForm(string configPath)
        {
            cfgPath = configPath;
            Build();
            Reload();
            eng.Log = AppendLog;
            timer = new System.Windows.Forms.Timer(); timer.Tick += delegate { if (!busy) SyncAll(false); };
            ApplyTimer();
            Log("Sẵn sàng. File cấu hình: " + cfgPath, GitEngine.Ok);
            string g = eng.FindGit();
            Log(g != null ? "Git: " + g : "Chưa tìm thấy Git → vào Công cụ → Kiểm tra / Cài Git.", g != null ? GitEngine.Dim : GitEngine.Warn);
        }

        // ---------- dựng giao diện ----------
        void Build()
        {
            Text = "Git AutoSync Hub"; Size = new Size(1120, 780); MinimumSize = new Size(920, 640);
            StartPosition = FormStartPosition.CenterScreen; BackColor = Theme.Bg; Font = new Font("Segoe UI", 9.5f);
            Icon = MakeIcon();

            // Header
            Panel head = new Panel(); head.Dock = DockStyle.Top; head.Height = 92;
            head.Paint += delegate (object s, PaintEventArgs e)
            {
                using (LinearGradientBrush b = new LinearGradientBrush(head.ClientRectangle, Color.FromArgb(99, 102, 241), Color.FromArgb(139, 92, 246), 0f)) e.Graphics.FillRectangle(b, head.ClientRectangle);
            };
            Label t = new Label(); t.Text = "⚡  Git AutoSync Hub"; t.Font = new Font("Segoe UI Semibold", 18f); t.ForeColor = Color.White; t.BackColor = Color.Transparent; t.AutoSize = true; t.Location = new Point(28, 12);
            Label st = new Label(); st.Text = "Đồng bộ nhiều repository lên GitHub chỉ với một cú bấm. Không cần PowerShell."; st.Font = new Font("Segoe UI", 9.5f); st.ForeColor = Color.FromArgb(224, 231, 255); st.BackColor = Color.Transparent; st.AutoSize = true; st.Location = new Point(31, 50);
            lblCfg = new Label(); lblCfg.Font = new Font("Segoe UI", 8.5f); lblCfg.ForeColor = Color.FromArgb(199, 210, 254); lblCfg.BackColor = Color.Transparent; lblCfg.AutoSize = true; lblCfg.Location = new Point(31, 70);
            head.Controls.AddRange(new Control[] { t, st, lblCfg });

            // Cảnh báo token chữ thường
            lblWarn = new Label(); lblWarn.Dock = DockStyle.Top; lblWarn.Height = 0; lblWarn.Visible = false; lblWarn.BackColor = Color.FromArgb(254, 243, 199); lblWarn.ForeColor = Color.FromArgb(146, 64, 14);
            lblWarn.TextAlign = ContentAlignment.MiddleLeft; lblWarn.Padding = new Padding(28, 0, 0, 0); lblWarn.Cursor = Cursors.Hand;
            lblWarn.Click += delegate { Settings(); };

            // Thanh nút
            FlowLayoutPanel bars = new FlowLayoutPanel(); bars.Dock = DockStyle.Top; bars.Height = 64; bars.Padding = new Padding(24, 14, 24, 8); bars.WrapContents = false;
            btnSyncAll = Btn("⚡  Đồng bộ tất cả", 170, Theme.Primary, false, delegate { SyncAll(true); });
            btnSyncSel = Btn("↻  Đồng bộ mục chọn", 175, Theme.Primary, true, delegate { SyncSelected(); });
            btnTest = Btn("🔑  Kiểm tra quyền", 160, Theme.Primary, true, delegate { TestAuth(); });
            btnSettings = Btn("⚙  Cài đặt", 115, Theme.Primary, true, delegate { Settings(); });
            btnTools = Btn("🧰  Công cụ  ▾", 135, Theme.Primary, true, null);
            btnTools.Click += delegate { ToolsMenu().Show(btnTools, new Point(0, btnTools.Height + 2)); };
            bars.Controls.AddRange(new Control[] { btnSyncAll, btnSyncSel, btnTest, btnSettings, btnTools });

            // Thân: 2 thẻ
            TableLayoutPanel body = new TableLayoutPanel(); body.Dock = DockStyle.Fill; body.Padding = new Padding(20, 4, 20, 8);
            body.RowCount = 2; body.ColumnCount = 1; body.RowStyles.Add(new RowStyle(SizeType.Percent, 38f)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 62f));

            CardPanel c1 = new CardPanel(); c1.Dock = DockStyle.Fill; c1.Margin = new Padding(4, 4, 4, 8);
            Panel tb = new Panel(); tb.Dock = DockStyle.Top; tb.Height = 42; tb.BackColor = Theme.Card;
            Label l1 = new Label(); l1.Text = "Repository đang quản lý"; l1.Font = new Font("Segoe UI Semibold", 11f); l1.ForeColor = Theme.Text; l1.AutoSize = true; l1.Location = new Point(0, 8);
            FlowLayoutPanel rt = new FlowLayoutPanel(); rt.Dock = DockStyle.Right; rt.AutoSize = true; rt.WrapContents = false; rt.BackColor = Theme.Card; rt.FlowDirection = FlowDirection.LeftToRight;
            chkAuto = new CheckBox(); chkAuto.Text = "Tự đồng bộ mỗi"; chkAuto.AutoSize = true; chkAuto.Margin = new Padding(0, 8, 2, 0); chkAuto.ForeColor = Theme.Sub;
            numMin = new NumericUpDown(); numMin.Minimum = 1; numMin.Maximum = 1440; numMin.Value = 30; numMin.Width = 56; numMin.Margin = new Padding(0, 5, 2, 0);
            Label lm = new Label(); lm.Text = "phút"; lm.AutoSize = true; lm.ForeColor = Theme.Sub; lm.Margin = new Padding(0, 9, 14, 0);
            chkAuto.CheckedChanged += delegate { SaveAutoSetting(); };
            numMin.ValueChanged += delegate { SaveAutoSetting(); };
            btnAdd = Btn("＋ Thêm", 86, Theme.Primary, true, delegate { EditRepo(null); }); btnAdd.Height = 32;
            btnEdit = Btn("✎ Sửa", 76, Theme.Primary, true, delegate { EditRepo(Selected()); }); btnEdit.Height = 32;
            btnDel = Btn("🗑 Gỡ", 72, Theme.Primary, true, delegate { DeleteRepo(); }); btnDel.Height = 32; btnDel.Margin = new Padding(0);
            rt.Controls.AddRange(new Control[] { chkAuto, numMin, lm, btnAdd, btnEdit, btnDel });
            tb.Controls.Add(rt); tb.Controls.Add(l1);

            grid = new DataGridView(); grid.Dock = DockStyle.Fill; grid.BackgroundColor = Theme.Card; grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false; grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; grid.GridColor = Color.FromArgb(241, 245, 249);
            grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersHeight = 34; grid.RowTemplate.Height = 34;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252); grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Sub;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f); grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255); grid.DefaultCellStyle.SelectionForeColor = Theme.Text; grid.DefaultCellStyle.ForeColor = Theme.Text;
            DataGridViewCheckBoxColumn cb = new DataGridViewCheckBoxColumn(); cb.HeaderText = "Bật"; cb.FillWeight = 6; cb.Name = "on"; grid.Columns.Add(cb);
            AddCol("name", "Tên", 17, true); AddCol("local", "Thư mục trên máy", 27, true); AddCol("remote", "Link GitHub", 31, true); AddCol("branch", "Nhánh", 8, true); AddCol("state", "Trạng thái", 17, true);
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.ColumnIndex == 0 && e.RowIndex >= 0 && e.RowIndex < cfg.repositories.Count)
                { cfg.repositories[e.RowIndex].enabled = (bool)grid.Rows[e.RowIndex].Cells[0].Value; SaveCfg(); }
            };
            grid.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && e.ColumnIndex > 0) EditRepo(Selected()); };
            c1.Controls.Add(grid); c1.Controls.Add(tb); grid.BringToFront();

            CardPanel c2 = new CardPanel(); c2.Dock = DockStyle.Fill; c2.Margin = new Padding(4, 0, 4, 0);
            Panel lh = new Panel(); lh.Dock = DockStyle.Top; lh.Height = 38; lh.BackColor = Theme.Card;
            Label l2 = new Label(); l2.Text = "Nhật ký"; l2.Font = new Font("Segoe UI Semibold", 11f); l2.ForeColor = Theme.Text; l2.AutoSize = true; l2.Location = new Point(0, 6);
            FlowLayoutPanel lr = new FlowLayoutPanel(); lr.Dock = DockStyle.Right; lr.AutoSize = true; lr.WrapContents = false; lr.BackColor = Theme.Card;
            RoundButton bCopy = Btn("Sao chép", 88, Theme.Primary, true, delegate { try { if (txtLog.TextLength > 0) Clipboard.SetText(txtLog.Text); } catch { } }); bCopy.Height = 30;
            RoundButton bClear = Btn("Xóa", 62, Theme.Primary, true, delegate { txtLog.Clear(); }); bClear.Height = 30; bClear.Margin = new Padding(0);
            lr.Controls.AddRange(new Control[] { bCopy, bClear });
            lh.Controls.Add(lr); lh.Controls.Add(l2);
            txtLog = new RichTextBox(); txtLog.Dock = DockStyle.Fill; txtLog.ReadOnly = true; txtLog.BorderStyle = BorderStyle.None;
            txtLog.BackColor = Color.FromArgb(15, 23, 42); txtLog.ForeColor = Color.FromArgb(226, 232, 240); txtLog.Font = new Font("Consolas", 10f); txtLog.DetectUrls = false;
            c2.Controls.Add(txtLog); c2.Controls.Add(lh); txtLog.BringToFront();

            body.Controls.Add(c1, 0, 0); body.Controls.Add(c2, 0, 1);

            // Thanh trạng thái
            Panel sp = new Panel(); sp.Dock = DockStyle.Bottom; sp.Height = 30; sp.BackColor = Color.White;
            lblStatus = new Label(); lblStatus.Text = "Sẵn sàng"; lblStatus.ForeColor = Theme.Sub; lblStatus.AutoSize = true; lblStatus.Location = new Point(24, 6);
            bar = new ProgressBar(); bar.Style = ProgressBarStyle.Marquee; bar.MarqueeAnimationSpeed = 28; bar.Width = 160; bar.Height = 8; bar.Anchor = AnchorStyles.Top | AnchorStyles.Right; bar.Visible = false;
            sp.Controls.Add(lblStatus); sp.Controls.Add(bar);
            sp.Resize += delegate { bar.Location = new Point(sp.Width - bar.Width - 24, 11); };

            Controls.Add(body); Controls.Add(bars); Controls.Add(lblWarn); Controls.Add(head); Controls.Add(sp);
            body.BringToFront();

            // Khay hệ thống
            tray = new NotifyIcon(); tray.Icon = Icon; tray.Text = "Git AutoSync Hub";
            ContextMenuStrip tm = new ContextMenuStrip();
            tm.Items.Add("Mở cửa sổ", null, delegate { ShowFromTray(); });
            tm.Items.Add("Đồng bộ ngay", null, delegate { if (!busy) SyncAll(false); });
            tm.Items.Add("Thoát", null, delegate { reallyExit = true; Close(); });
            tray.ContextMenuStrip = tm; tray.DoubleClick += delegate { ShowFromTray(); };
            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                if (!reallyExit && chkAuto.Checked && e.CloseReason == CloseReason.UserClosing)
                { e.Cancel = true; Hide(); tray.Visible = true; tray.ShowBalloonTip(2500, "Git AutoSync Hub", "Vẫn đang chạy nền để tự đồng bộ. Bấm đúp biểu tượng để mở lại.", ToolTipIcon.Info); }
                else if (busy) { if (MessageBox.Show("Đang đồng bộ. Thoát bây giờ có thể làm dang dở thao tác Git. Vẫn thoát?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) e.Cancel = true; }
            };
            FormClosed += delegate { tray.Visible = false; tray.Dispose(); };
        }

        void ShowFromTray() { Show(); WindowState = FormWindowState.Normal; Activate(); tray.Visible = false; }

        void AddCol(string n, string h, float w, bool ro)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn(); c.Name = n; c.HeaderText = h; c.FillWeight = w; c.ReadOnly = ro; grid.Columns.Add(c);
        }

        RoundButton Btn(string text, int w, Color fill, bool outline, EventHandler click)
        {
            RoundButton b = new RoundButton(); b.Text = text; b.Width = w; b.Fill = fill; b.Outline = outline;
            if (click != null) b.Click += click; return b;
        }

        static Icon MakeIcon()
        {
            Bitmap bmp = new Bitmap(64, 64);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = Theme.Round(new Rectangle(2, 2, 60, 60), 14))
                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, 64, 64), Color.FromArgb(99, 102, 241), Color.FromArgb(139, 92, 246), 45f)) g.FillPath(b, p);
                using (Font f = new Font("Segoe UI Symbol", 30f, FontStyle.Bold)) TextRenderer.DrawText(g, "⚡", f, new Rectangle(0, 0, 64, 64), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        // ---------- cấu hình ----------
        void Reload()
        {
            try { cfg = Program.Load(cfgPath); }
            catch (Exception ex) { cfg = new ConfigModel(); Log("Lỗi đọc file cấu hình: " + ex.Message, GitEngine.Err); }
            eng.Cfg = cfg; eng.ConfigDir = Path.GetDirectoryName(cfgPath);
            lblCfg.Text = "Cấu hình: " + cfgPath;
            grid.Rows.Clear();
            foreach (RepoModel r in cfg.repositories) grid.Rows.Add(r.enabled, r.name, r.local_path, r.remote_url, r.branch, "");
            loading = true;
            bool a = cfg.settings.auto_sync_minutes > 0;
            if (a) numMin.Value = Math.Max(1, Math.Min(1440, cfg.settings.auto_sync_minutes));
            chkAuto.Checked = a;
            loading = false;
            UpdateWarn();
        }

        void UpdateWarn()
        {
            bool plain = !string.IsNullOrEmpty(cfg.settings.github_token) && string.IsNullOrEmpty(cfg.settings.github_token_enc);
            bool bad = eng.TokenUnreadable();
            lblWarn.Visible = plain || bad; lblWarn.Height = (plain || bad) ? 32 : 0;
            lblWarn.Text = bad ? "⚠ Token đã mã hóa trên máy khác nên không đọc được ở máy này. Bấm đây để nhập lại token."
                               : "⚠ Token đang lưu dạng chữ thường trong file cấu hình. Bấm đây để mã hóa (khuyến nghị).";
        }

        bool SaveCfg()
        {
            try { Program.Save(cfgPath, cfg); return true; }
            catch (Exception ex) { Log("Lỗi lưu cấu hình: " + ex.Message, GitEngine.Err); return false; }
        }

        void SaveAutoSetting()
        {
            if (loading || timer == null) return;
            cfg.settings.auto_sync_minutes = chkAuto.Checked ? (int)numMin.Value : 0;
            SaveCfg(); ApplyTimer();
        }

        void ApplyTimer()
        {
            timer.Stop();
            if (cfg.settings.auto_sync_minutes > 0)
            {
                timer.Interval = cfg.settings.auto_sync_minutes * 60000; timer.Start();
                SetStatus("Tự động đồng bộ mỗi " + cfg.settings.auto_sync_minutes + " phút");
            }
        }

        RepoModel Selected()
        {
            if (grid.CurrentRow == null || grid.CurrentRow.Index >= cfg.repositories.Count) { MessageBox.Show("Hãy chọn một repository trong danh sách.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }
            return cfg.repositories[grid.CurrentRow.Index];
        }

        // ---------- log & trạng thái ----------
        void Log(string m, Color? c) { AppendLog(m, c); }
        void AppendLog(string m, Color? c)
        {
            if (InvokeRequired) { BeginInvoke(new Action(delegate { AppendLog(m, c); })); return; }
            if (m.Length > 0)
            {
                txtLog.SelectionStart = txtLog.TextLength; txtLog.SelectionColor = Color.FromArgb(100, 116, 139); txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  ");
                txtLog.SelectionColor = c ?? Color.FromArgb(226, 232, 240);
            }
            txtLog.AppendText(m + "\r\n"); txtLog.SelectionColor = txtLog.ForeColor; txtLog.ScrollToCaret();
        }
        void SetStatus(string s) { if (InvokeRequired) { BeginInvoke(new Action(delegate { SetStatus(s); })); return; } lblStatus.Text = s; }
        void SetRow(int i, string s, bool ok)
        {
            if (InvokeRequired) { BeginInvoke(new Action(delegate { SetRow(i, s, ok); })); return; }
            if (i < 0 || i >= grid.Rows.Count) return;
            DataGridViewCell c = grid.Rows[i].Cells["state"]; c.Value = s; c.Style.ForeColor = ok ? Theme.Green : (s.StartsWith("Đang") ? Theme.Amber : Theme.Red);
        }
        void SetBusy(bool b)
        {
            if (InvokeRequired) { BeginInvoke(new Action(delegate { SetBusy(b); })); return; }
            busy = b; bar.Visible = b;
            foreach (Control c in new Control[] { btnSyncAll, btnSyncSel, btnTest, btnSettings, btnAdd, btnEdit, btnDel }) c.Enabled = !b;
        }

        // ---------- hành động ----------
        void RunJob(string title, List<int> idx, bool test)
        {
            if (busy) return;
            SetBusy(true); SetStatus(title);
            Log("═══ " + title.ToUpper() + " ═══", GitEngine.Info);
            ThreadPool.QueueUserWorkItem(delegate
            {
                int good = 0, bad = 0;
                try
                {
                    foreach (int i in idx)
                    {
                        RepoModel r = cfg.repositories[i];
                        if (!r.enabled && !test) { SetRow(i, "Đã tắt", false); continue; }
                        SetRow(i, "Đang chạy…", false);
                        bool ok; string s;
                        try { s = test ? eng.TestRepo(r, out ok) : eng.SyncRepo(r, out ok); }
                        catch (Exception ex) { eng.Log("[LỖI] " + ex.Message, GitEngine.Err); s = "Lỗi"; ok = false; }
                        SetRow(i, s, ok); if (ok) good++; else bad++;
                    }
                }
                finally
                {
                    Log("", null);
                    Log(string.Format("Hoàn tất: {0} thành công, {1} lỗi.", good, bad), bad == 0 ? GitEngine.Ok : GitEngine.Warn);
                    SetStatus(bad == 0 ? "Hoàn tất lúc " + DateTime.Now.ToString("HH:mm:ss") : "Xong, có " + bad + " lỗi");
                    SetBusy(false);
                    if (bad > 0 && !Visible) tray.ShowBalloonTip(3000, "Git AutoSync Hub", "Có " + bad + " repository đồng bộ lỗi.", ToolTipIcon.Warning);
                }
            });
        }

        void SyncAll(bool manual)
        {
            List<int> l = new List<int>(); for (int i = 0; i < cfg.repositories.Count; i++) l.Add(i);
            if (l.Count == 0) { Log("Chưa có repository nào. Bấm “＋ Thêm”.", GitEngine.Warn); return; }
            RunJob(manual ? "Đồng bộ tất cả" : "Tự động đồng bộ", l, false);
        }
        void SyncSelected()
        {
            if (Selected() == null) return;
            RunJob("Đồng bộ " + Selected().name, new List<int>(new int[] { grid.CurrentRow.Index }), false);
        }
        void TestAuth()
        {
            List<int> l = new List<int>(); for (int i = 0; i < cfg.repositories.Count; i++) l.Add(i);
            RunJob("Kiểm tra quyền", l, true);
        }

        void DeleteRepo()
        {
            RepoModel r = Selected(); if (r == null) return;
            if (MessageBox.Show("Gỡ “" + r.name + "” khỏi danh sách? (File trên máy và trên GitHub không bị xóa)", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            cfg.repositories.Remove(r); SaveCfg(); Reload(); Log("Đã gỡ “" + r.name + "” khỏi danh sách.", GitEngine.Warn);
        }

        // ---------- hộp thoại ----------
        Form Dialog(string title, int w, int h)
        {
            Form f = new Form(); f.Text = title; f.ClientSize = new Size(w, h); f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
            f.BackColor = Color.White; f.Font = new Font("Segoe UI", 9.5f); return f;
        }
        Label Lbl(string t, int x, int y) { Label l = new Label(); l.Text = t; l.Location = new Point(x, y + 4); l.AutoSize = true; l.ForeColor = Theme.Sub; return l; }
        TextBox Txt(string v, int x, int y, int w) { TextBox t = new TextBox(); t.Text = v ?? ""; t.Location = new Point(x, y); t.Width = w; t.BorderStyle = BorderStyle.FixedSingle; return t; }

        string RelOrAbs(string p)
        {
            try
            {
                string full = Path.GetFullPath(p).TrimEnd('\\'); string cd = eng.ConfigDir.TrimEnd('\\');
                if (full.StartsWith(cd + "\\", StringComparison.OrdinalIgnoreCase)) return ".\\" + full.Substring(cd.Length + 1);
                return full;
            }
            catch { return p; }
        }

        void EditRepo(RepoModel r)
        {
            bool isNew = r == null;
            if (!isNew && r == null) return;
            Form d = Dialog(isNew ? "Thêm repository" : "Sửa repository", 640, 330);
            TextBox tName = Txt(isNew ? "" : r.name, 160, 22, 450);
            TextBox tPath = Txt(isNew ? "" : r.local_path, 160, 66, 360);
            RoundButton bb = new RoundButton(); bb.Text = "Chọn…"; bb.Outline = true; bb.Width = 80; bb.Height = 28; bb.Location = new Point(530, 64); bb.Margin = new Padding(0);
            TextBox tUrl = Txt(isNew ? "" : r.remote_url, 160, 110, 450);
            TextBox tBranch = Txt(isNew ? (cfg.settings.default_branch ?? "main") : r.branch, 160, 154, 140);
            CheckBox on = new CheckBox(); on.Text = "Bật đồng bộ repository này"; on.Checked = isNew || r.enabled; on.Location = new Point(160, 192); on.AutoSize = true;
            Label hint = new Label(); hint.Text = "Thư mục để trống hoặc dùng “.\\tên” = tính từ nơi đặt file cấu hình (đổi máy vẫn chạy). Link ví dụ: https://github.com/tai-khoan/ten-repo.git"; hint.Location = new Point(20, 226); hint.Size = new Size(600, 38); hint.ForeColor = Theme.Sub; hint.Font = new Font("Segoe UI", 8.5f);
            bb.Click += delegate
            {
                FolderBrowserDialog fb = new FolderBrowserDialog(); fb.Description = "Chọn thư mục repository trên máy";
                if (fb.ShowDialog() == DialogResult.OK) { tPath.Text = RelOrAbs(fb.SelectedPath); if (tName.Text.Trim().Length == 0) tName.Text = Path.GetFileName(fb.SelectedPath); }
            };
            RoundButton ok = new RoundButton(); ok.Text = "Lưu"; ok.Width = 110; ok.Location = new Point(400, 280);
            RoundButton cancel = new RoundButton(); cancel.Text = "Hủy"; cancel.Outline = true; cancel.Width = 100; cancel.Location = new Point(520, 280);
            cancel.Click += delegate { d.Close(); };
            ok.Click += delegate
            {
                string url = Util.CleanUrl(tUrl.Text.Trim());
                if (tName.Text.Trim().Length == 0 || tPath.Text.Trim().Length == 0 || url.Length == 0) { MessageBox.Show("Vui lòng điền Tên, Thư mục và Link GitHub.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (!(url.StartsWith("https://") || url.StartsWith("http://") || url.StartsWith("git@"))) { MessageBox.Show("Link phải bắt đầu bằng https:// (hoặc git@).", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                RepoModel m = isNew ? new RepoModel() : r;
                m.name = tName.Text.Trim(); m.local_path = tPath.Text.Trim(); m.remote_url = url; m.branch = tBranch.Text.Trim().Length == 0 ? "main" : tBranch.Text.Trim(); m.enabled = on.Checked;
                if (isNew) cfg.repositories.Add(m);
                SaveCfg(); Reload(); Log((isNew ? "Đã thêm: " : "Đã cập nhật: ") + m.name, GitEngine.Ok); d.Close();
            };
            d.AcceptButton = null;
            d.Controls.AddRange(new Control[] { Lbl("Tên hiển thị", 20, 22), tName, Lbl("Thư mục trên máy", 20, 66), tPath, bb, Lbl("Link GitHub", 20, 110), tUrl, Lbl("Nhánh", 20, 154), tBranch, on, hint, ok, cancel });
            d.ShowDialog(this);
        }

        void Settings()
        {
            Form d = Dialog("Cài đặt", 640, 470);
            TextBox tTok = Txt("", 160, 22, 450); tTok.UseSystemPasswordChar = true;
            bool has = !string.IsNullOrEmpty(cfg.settings.github_token_enc) || !string.IsNullOrEmpty(cfg.settings.github_token);
            Label tokHint = new Label(); tokHint.Location = new Point(160, 50); tokHint.Size = new Size(450, 34); tokHint.ForeColor = Theme.Sub; tokHint.Font = new Font("Segoe UI", 8.5f);
            tokHint.Text = has ? "Đã có token (để trống = giữ nguyên). Token mới được mã hóa theo tài khoản Windows của máy này, không đọc được nếu sao chép sang máy khác." : "Chưa có token. Nhập token dạng github_pat_… (quyền Contents: Read and write). Được mã hóa theo máy này.";
            TextBox tUser = Txt(cfg.settings.git_user_name, 160, 94, 450);
            TextBox tMail = Txt(cfg.settings.git_user_email, 160, 138, 450);
            TextBox tGit = Txt(cfg.settings.git_path, 160, 182, 360);
            RoundButton gb = new RoundButton(); gb.Text = "Chọn…"; gb.Outline = true; gb.Width = 80; gb.Height = 28; gb.Location = new Point(530, 180);
            gb.Click += delegate { OpenFileDialog o = new OpenFileDialog(); o.Filter = "git.exe|git.exe|Tất cả|*.*"; if (o.ShowDialog() == DialogResult.OK) tGit.Text = o.FileName; };
            TextBox tBr = Txt(cfg.settings.default_branch, 160, 226, 140);
            TextBox tMsg = Txt(cfg.settings.commit_message, 160, 270, 450);
            CheckBox pull = new CheckBox(); pull.Text = "Kéo bản mới (pull --rebase) trước khi đẩy"; pull.Checked = cfg.settings.auto_pull_before_push; pull.Location = new Point(160, 312); pull.AutoSize = true;
            CheckBox enc = new CheckBox(); enc.Text = "Mã hóa token đang lưu dạng chữ thường (script .ps1/.bat cũ sẽ không đọc được nữa)"; enc.Location = new Point(160, 340); enc.AutoSize = true;
            enc.Visible = !string.IsNullOrEmpty(cfg.settings.github_token) && string.IsNullOrEmpty(cfg.settings.github_token_enc); enc.Checked = enc.Visible;
            Label ghLink = new Label(); ghLink.Text = "Tạo token: github.com/settings/tokens?type=beta"; ghLink.Location = new Point(160, 372); ghLink.AutoSize = true; ghLink.ForeColor = Theme.Primary; ghLink.Cursor = Cursors.Hand;
            ghLink.Click += delegate { try { Process.Start("https://github.com/settings/tokens?type=beta"); } catch { } };
            RoundButton ok = new RoundButton(); ok.Text = "Lưu cài đặt"; ok.Width = 130; ok.Location = new Point(380, 415);
            RoundButton cancel = new RoundButton(); cancel.Text = "Hủy"; cancel.Outline = true; cancel.Width = 100; cancel.Location = new Point(520, 415);
            cancel.Click += delegate { d.Close(); };
            ok.Click += delegate
            {
                string nt = tTok.Text.Trim();
                if (nt.Length > 0) { cfg.settings.github_token_enc = Util.Protect(nt); cfg.settings.github_token = ""; }
                else if (enc.Visible && enc.Checked) { cfg.settings.github_token_enc = Util.Protect(cfg.settings.github_token); cfg.settings.github_token = ""; }
                cfg.settings.git_user_name = tUser.Text.Trim(); cfg.settings.git_user_email = tMail.Text.Trim();
                cfg.settings.git_path = tGit.Text.Trim().Length == 0 ? "auto" : tGit.Text.Trim();
                cfg.settings.default_branch = tBr.Text.Trim().Length == 0 ? "main" : tBr.Text.Trim();
                cfg.settings.commit_message = tMsg.Text.Trim().Length == 0 ? "Auto sync: {time}" : tMsg.Text.Trim();
                cfg.settings.auto_pull_before_push = pull.Checked;
                SaveCfg(); UpdateWarn(); Log("Đã lưu cài đặt.", GitEngine.Ok); d.Close();
            };
            d.Controls.AddRange(new Control[] { Lbl("GitHub token (PAT)", 20, 22), tTok, tokHint, Lbl("Git user.name", 20, 94), tUser, Lbl("Git user.email", 20, 138), tMail, Lbl("Đường dẫn git.exe", 20, 182), tGit, gb, Lbl("Nhánh mặc định", 20, 226), tBr, Lbl("Nội dung commit", 20, 270), tMsg, pull, enc, ghLink, ok, cancel });
            d.ShowDialog(this);
        }

        // ---------- công cụ ----------
        ContextMenuStrip ToolsMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip(); m.Font = Font;
            m.Items.Add("Kiểm tra / Cài Git", null, delegate { CheckInstallGit(); });
            m.Items.Add("Cài PowerShell 7 (tùy chọn, ứng dụng không cần)", null, delegate { InstallTool("PowerShell 7", "Microsoft.PowerShell", "PowerShell/PowerShell", "win-x64.msi", true); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Mở Command Prompt tại repository đang chọn", null, delegate { OpenCmd(); });
            m.Items.Add("Mở thư mục repository đang chọn", null, delegate { OpenRepoFolder(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Chọn file cấu hình khác…", null, delegate { BrowseConfig(); });
            m.Items.Add("Mở thư mục chứa file cấu hình", null, delegate { try { Process.Start("explorer.exe", "/select,\"" + cfgPath + "\""); } catch { } });
            m.Items.Add("Tạo lệnh chạy nền (Task Scheduler)…", null, delegate { ShowSchedulerHelp(); });
            return m;
        }

        void CheckInstallGit()
        {
            string g = eng.FindGit();
            if (g != null)
            {
                GitResult v = eng.Run(g, "--version", eng.ConfigDir, false, 15000);
                Log("[OK] " + v.Out.Trim() + "  (" + g + ")", GitEngine.Ok); return;
            }
            InstallTool("Git", "Git.Git", "git-for-windows/git", "64-bit.exe", false);
        }

        void InstallTool(string name, string wingetId, string ghRepo, string assetSuffix, bool msi)
        {
            if (busy) return;
            if (MessageBox.Show("Cài đặt " + name + " bây giờ? Windows có thể hiện hộp thoại xin quyền quản trị.", "Cài đặt " + name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            SetBusy(true); SetStatus("Đang cài " + name + "…"); Log("Đang cài " + name + "…", GitEngine.Warn);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    bool done = false;
                    string winget = FindOnPath("winget.exe");
                    if (winget != null)
                    {
                        Log("   Dùng winget…", GitEngine.Dim);
                        GitResult r = eng.Run(winget, "install --id " + wingetId + " -e --source winget --accept-package-agreements --accept-source-agreements", eng.ConfigDir, false, 900000);
                        foreach (string ln in r.Out.Split('\n')) { string s = ln.Trim(); if (s.Length > 3 && !s.StartsWith("-") && !s.StartsWith("\\") && !s.StartsWith("|") && !s.StartsWith("/")) Log("   " + s, GitEngine.Dim); }
                        done = r.Ok;
                    }
                    if (!done)
                    {
                        Log("   winget không dùng được, tải trực tiếp từ GitHub Releases…", GitEngine.Warn);
                        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                        WebClient wc = new WebClient(); wc.Headers["User-Agent"] = "GitSyncHub";
                        string json = wc.DownloadString("https://api.github.com/repos/" + ghRepo + "/releases/latest");
                        Dictionary<string, object> rel = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                        string url = null;
                        foreach (object a in (System.Collections.ArrayList)rel["assets"])
                        {
                            Dictionary<string, object> da = (Dictionary<string, object>)a; string n = (string)da["name"];
                            if (n.EndsWith(assetSuffix, StringComparison.OrdinalIgnoreCase) && !n.Contains("busybox") && !n.Contains("arm") && !n.Contains("Portable")) { url = (string)da["browser_download_url"]; break; }
                        }
                        if (url == null) throw new Exception("Không tìm thấy gói cài đặt phù hợp.");
                        string tmp = Path.Combine(Path.GetTempPath(), Path.GetFileName(url));
                        Log("   Tải " + url, GitEngine.Dim); wc.DownloadFile(url, tmp);
                        ProcessStartInfo psi = new ProcessStartInfo();
                        if (msi) { psi.FileName = "msiexec.exe"; psi.Arguments = "/i \"" + tmp + "\" /passive /norestart"; }
                        else { psi.FileName = tmp; psi.Arguments = "/VERYSILENT /NORESTART"; }
                        psi.UseShellExecute = true; psi.Verb = "runas";
                        using (Process p = Process.Start(psi)) { p.WaitForExit(); done = p.ExitCode == 0 || p.ExitCode == 3010; }
                        try { File.Delete(tmp); } catch { }
                    }
                    Log(done ? "Cài " + name + " xong. Nếu app chưa nhận ra, hãy mở lại ứng dụng." : "Cài " + name + " chưa thành công. Hãy cài thủ công.", done ? GitEngine.Ok : GitEngine.Err);
                }
                catch (Exception ex) { Log("Lỗi cài " + name + ": " + ex.Message, GitEngine.Err); }
                finally { SetBusy(false); SetStatus("Sẵn sàng"); }
            });
        }

        static string FindOnPath(string exe)
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string d in path.Split(';')) { try { string f = Path.Combine(d.Trim(), exe); if (File.Exists(f)) return f; } catch { } }
            string wa = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps", exe);
            return File.Exists(wa) ? wa : null;
        }

        void OpenCmd()
        {
            RepoModel r = Selected(); if (r == null) return;
            string p = eng.ResolvePath(r.local_path);
            if (!Directory.Exists(p)) { MessageBox.Show("Thư mục chưa tồn tại: " + p); return; }
            try { Process.Start(new ProcessStartInfo("cmd.exe", "/k") { WorkingDirectory = p }); } catch (Exception ex) { Log("Lỗi mở CMD: " + ex.Message, GitEngine.Err); }
        }
        void OpenRepoFolder()
        {
            RepoModel r = Selected(); if (r == null) return;
            string p = eng.ResolvePath(r.local_path);
            if (!Directory.Exists(p)) { MessageBox.Show("Thư mục chưa tồn tại: " + p); return; }
            try { Process.Start("explorer.exe", "\"" + p + "\""); } catch { }
        }
        void BrowseConfig()
        {
            OpenFileDialog o = new OpenFileDialog(); o.Filter = "JSON (*.json)|*.json|Tất cả|*.*"; o.Title = "Chọn file cấu hình";
            if (o.ShowDialog() == DialogResult.OK) { cfgPath = o.FileName; Reload(); ApplyTimer(); Log("Đã chuyển sang file cấu hình: " + cfgPath, GitEngine.Ok); }
        }
        void ShowSchedulerHelp()
        {
            string exe = Application.ExecutablePath;
            string cmd = "schtasks /Create /SC MINUTE /MO 30 /TN \"GitSyncHub\" /TR \"\\\"" + exe + "\\\" --sync-all --config \\\"" + cfgPath + "\\\" --log \\\"" + Path.Combine(Path.GetDirectoryName(cfgPath), "sync.log") + "\\\"\"";
            try { Clipboard.SetText(cmd); } catch { }
            MessageBox.Show("Ứng dụng chạy được không cần giao diện:\r\n\r\n  GitSyncHub.exe --sync-all [--config file] [--log file]\r\n\r\nĐã sao chép lệnh tạo tác vụ chạy mỗi 30 phút bằng Task Scheduler (dán vào Command Prompt):\r\n\r\n" + cmd, "Chạy nền không cần mở app", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
