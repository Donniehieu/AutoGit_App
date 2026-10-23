# TODO & Nhật ký quản lý Git - Dự án PO

> File này dùng để theo dõi tiến độ thiết lập, đồng bộ mã nguồn giữa máy tính cục bộ và GitHub, cũng như làm checklist phối hợp công việc.

---

## 1. Thông tin cấu hình hệ thống & Repositories

| Thành phần | Đường dẫn thư mục cục bộ | Repository GitHub | Trạng thái đồng bộ |
| :--- | :--- | :--- | :--- |
| **PO Data** | `./po-data-main` | `https://github.com/Donniehieu/po-data.git` | ✅ **Đã đồng bộ mới nhất** (`commit 0aa315c`) |
| **PO Dashboard** | `./po-dashboard-main` | `https://github.com/Donniehieu/po-dashboard.git` | ✅ **Đã đẩy thành công lên GitHub** (`commit 19e43fd`) |

- **Quản lý tập trung đa Repo**: Cấu hình tại [`sync-config.json`](file:///d:/PO/Nghiên%20cứu%20lập%20app/sync-config.json) (hỗ trợ đường dẫn tương đối `./` nên copy sang máy khác hoặc ổ đĩa khác vẫn chạy trơn tru).
- **Ứng dụng Native EXE độc lập (1-Click Run)**: [**`GitSyncHub.exe`**](file:///d:/PO/Nghiên%20cứu%20lập%20app/GitSyncHub.exe) (Ứng dụng Windows `.exe` độc lập, tích hợp đầy đủ 6 nút bấm chức năng, quản lý repo, kiểm tra quyền, console log đổi màu thời gian thực).
- **Ứng dụng giao diện web hiện đại (Modern Web App)**: [`GitSyncApp.bat`](file:///d:/PO/Nghiên%20cứu%20lập%20app/GitSyncApp.bat) hoặc [`GitSyncApp.vbs`](file:///d:/PO/Nghiên%20cứu%20lập%20app/GitSyncApp.vbs) (Giao diện web desktop bo tròn, hiệu ứng mượt).
- **Kịch bản đồng bộ CMD thuần túy (100% Zero PowerShell)**: [`sync-pure-cmd.bat`](file:///d:/PO/Nghiên%20cứu%20lập%20app/sync-pure-cmd.bat) (Chạy thuần bằng lệnh Windows CMD & Git).
- **Kịch bản tự động tải & cài đặt PowerShell**: [`install-powershell.bat`](file:///d:/PO/Nghiên%20cứu%20lập%20app/install-powershell.bat) (Tự động tải PowerShell mới nhất).
- **Kịch bản đồng bộ nhanh 1-Click**: [`sync-git.bat`](file:///d:/PO/Nghiên%20cứu%20lập%20app/sync-git.bat)
- **Tự động hóa qua Chat (AI Skill)**: [`.agents/skills/git-sync/SKILL.md`](file:///d:/PO/Nghiên%20cứu%20lập%20app/.agents/skills/git-sync/SKILL.md)

---

## 2. Kiểm tra & Hướng dẫn toàn diện về Quyền (Permissions)

| Quyền | Cần thiết khi nào? | Trạng thái hiện tại | Lưu ý & Cách cấp |
| :--- | :--- | :--- | :--- |
| **Quyền Admin trên máy tính (Windows)** | Khi cài đặt phần mềm mới. | ✅ **Không cần** | Git đã được cài đặt. Ứng dụng chạy ở chế độ người dùng bình thường (`User`), không đòi hỏi Administrator. |
| **Quyền thực thi Script (PowerShell ExecutionPolicy)** | Khi chạy file script `.ps1`. | ✅ **Đã tự động xử lý** | Đã cấu hình cờ `-ExecutionPolicy Bypass` trong `.bat` và `.vbs`, bạn không cần gõ lệnh quyền trong Windows PowerShell. |
| **Quyền GitHub Token hiện tại (`po-data`, `po-dashboard`)** | Khi kéo (`pull`) và đẩy (`push`) code. | ✅ **Đã cấp đủ** | Token hiện tại trong `keygithub.txt` đã xác thực thành công cả 2 repo. |
| **Quyền GitHub khi THÊM REPO MỚI trong tương lai** | Khi bạn bấm `Thêm Repo` một dự án mới trên GitHub của bạn. | ⚠️ **Cần lưu ý** | Token hiện tại là **Fine-grained Token**. Khi tạo thêm repo mới trên GitHub, bạn vào [GitHub Token Settings](https://github.com/settings/tokens?type=beta) -> Nhấn vào tên token -> Mục **Repository access** -> Tích chọn thêm repo mới (hoặc chọn **All repositories** để vĩnh viễn không cần cấp lại). |

---

## 2. Checklist công việc (Task List)

### Giai đoạn 1: Thiết lập môi trường & Chuẩn hóa
- [x] Cài đặt Git tự động trên máy tính (`winget install Git.Git`).
- [x] Cấu hình định danh Git (`user.name`, `user.email`, `autocrlf`).
- [x] Tạo file `.gitignore` chung ngăn file cài đặt nặng (`.exe`, `.msi`) và rác hệ thống.
- [x] Chuẩn hóa thư mục giải nén (loại bỏ tầng thư mục lồng `po-*-main/po-*-main` thành thư mục phẳng trực diện).

### Giai đoạn 2: Thiết lập & Đẩy Git - `po-data`
- [x] Khởi tạo / liên kết Git remote cho `po-data-main`.
- [x] Xác thực qua Personal Access Token.
- [x] Đóng gói commit cập nhật file `data/PO-data.xlsx`.
- [x] Đẩy (`git push origin main`) thành công lên `Donniehieu/po-data.git`.

### Giai đoạn 3: Thiết lập & Đẩy Git - `po-dashboard`
- [x] Khởi tạo / liên kết Git remote cho `po-dashboard-main`.
- [x] Kiểm tra trạng thái mã nguồn cục bộ (mã nguồn hiện tại đã đồng bộ với branch `main`).
- [x] **Cấp quyền Token hoàn tất**: Quyền ghi vào `po-dashboard` đã được kích hoạt thành công (hết lỗi 403).
- [x] Thực hiện kiểm tra đồng bộ và lệnh đẩy `git push` cho `po-dashboard` hoàn tất (`Everything up-to-date`).

### Giai đoạn 4: Đóng gói thành Ứng dụng Quản lý Tự động & Tùy biến Đa Repo
- [x] Xây dựng file cấu hình động tập trung [`sync-config.json`](file:///d:/PO/Nghiên%20cứu%20lập%20app/sync-config.json) (không còn hardcode đường dẫn hay URL).
- [x] Xây dựng nhân xử lý đa nền tảng [`scripts/git_manager.ps1`](file:///d:/PO/Nghiên%20cứu%20lập%20app/scripts/git_manager.ps1) (tự phát hiện Git, tự cài Git qua winget nếu máy chưa có, tự tiêm Token xác thực, tự pull rebase chống xung đột).
- [x] Đóng gói thành ứng dụng giao diện cửa sổ Windows trực quan [`GitSyncApp.bat`](file:///d:/PO/Nghiên%20cứu%20lập%20app/GitSyncApp.bat) (thêm/xóa/sửa repo, chọn thư mục bằng chuột, kiểm tra quyền token).
- [x] Cập nhật kịch bản 1-click [`sync-git.bat`](file:///d:/PO/Nghiên%20cứu%20lập%20app/sync-git.bat) tự động đọc từ cấu hình.
- [x] Nâng cấp Skill AI [`git-sync`](file:///d:/PO/Nghiên%20cứu%20lập%20app/.agents/skills/git-sync/SKILL.md) hoạt động hoàn toàn linh hoạt theo cấu hình.

---

### Giai đoạn 5: Làm mới GitSyncHub.exe (23/10/2026)
Mã nguồn: `src/GitSyncHub.cs`; biên dịch bằng `build.bat` (dùng trình biên dịch C# có sẵn của Windows, không cần cài gì). Bản cũ lưu ở `backup/` (đã che token).
- [x] Giao diện mới: header gradient, nút/thẻ bo góc, nhật ký màu, hỗ trợ DPI cao, biểu tượng khay hệ thống, cửa sổ co giãn được.
- [x] Quản lý repo đầy đủ: bật/tắt từng repo (tick ngay trên bảng), thêm / **sửa** / gỡ, đồng bộ cả danh sách hoặc chỉ repo đang chọn, cột trạng thái từng repo.
- [x] Tùy biến theo máy: đường dẫn repo nhập tay hoặc chọn thư mục (nằm trong thư mục cấu hình thì lưu dạng `.\tên` nên đổi máy vẫn chạy), link GitHub, nhánh, đường dẫn `git.exe`, nội dung commit. Đã **bỏ các đường dẫn cứng `D:\PO\...`**; file cấu hình tìm theo thứ tự: tham số `--config`, cạnh .exe, thư mục cha, `%APPDATA%\GitSyncHub`; chưa có thì tự tạo.
- [x] Không cần PowerShell: app gọi thẳng `git`. Công cụ → Kiểm tra / Cài Git (winget, nếu không có thì tải từ GitHub Releases); Công cụ → Cài PowerShell 7 chỉ là tùy chọn.
- [x] Chạy không giao diện: `GitSyncHub.exe --sync-all [--config file] [--log file]` (dùng với Task Scheduler; Công cụ → Tạo lệnh chạy nền sao chép sẵn lệnh `schtasks`). Có ô **Tự đồng bộ mỗi N phút** (đóng cửa sổ thì thu vào khay hệ thống).
- [x] Sửa quy trình: trước đây pull trước commit nên dễ lỗi "unstaged changes"; giờ commit → pull --rebase --autostash → push, lỗi pull thì tự hủy rebase, kiểm tra thành công bằng mã thoát thay vì tìm chữ.
- [x] **Bảo mật token**: token mới được mã hóa DPAPI theo tài khoản Windows (`github_token_enc`), truyền cho git qua biến môi trường nên không nằm trong `.git/config` hay dòng lệnh; app tự bỏ token cũ nhúng trong remote URL; log che token. Sang máy khác phải nhập lại token (đúng thiết kế). Nếu file cấu hình còn token dạng chữ, app hiện dải cảnh báo, bấm để mã hóa (script `.ps1`/`.bat` cũ sẽ không đọc được nữa).
- [x] `sync-pure-cmd.bat` viết lại: không còn token/tên tài khoản cứng, tự quét thư mục con có `.git`, đăng nhập qua Git Credential Manager; `sync-git.bat` ưu tiên .exe.
- 🟡 Đã thử: biên dịch OK; chạy `--sync-all` trên repo giả + remote cục bộ (commit mới → pull → push, 2 lần đúng); chụp màn hình giao diện. **Chưa thử** với GitHub/token thật, cài Git/PowerShell tự động, khay hệ thống, hộp thoại Cài đặt/Thêm repo, Task Scheduler.
- ✅ `GitSyncHub.exe` đã là bản mới (biên dịch lại từ `src/GitSyncHub.cs`); muốn sửa thêm thì chạy `build.bat` (đóng app trước).
- 🟡 Chưa làm: biểu tượng cho file .exe (chỉ có biểu tượng cửa sổ), chọn nhánh từ danh sách, xem lịch sử commit, tiếng Anh.
- ⚠️ **CẦN LÀM NGAY (bảo mật)**: `keygithub.txt` và `keyodoo.txt` đang **được commit và có trên nhánh main của repo po-data** (cả lịch sử). Hãy (1) thu hồi/tạo lại token GitHub và khóa API Odoo vì xem như đã lộ; (2) rồi `git rm --cached keygithub.txt keyodoo.txt` và thêm vào `.gitignore` của po-data. Token cũng từng nằm ở `sync-config.json`, `.git/config` của 2 repo và `sync-pure-cmd.bat` (bản cũ). Chưa tự sửa vì thao tác này đổi repo trên GitHub.

---

## 3. Hướng dẫn sửa lỗi quyền 403 cho `po-dashboard`

Khi thực hiện lệnh đẩy code lên `po-dashboard`, GitHub trả về:
```text
remote: Permission to Donniehieu/po-dashboard.git denied to Donniehieu.
fatal: unable to access 'https://github.com/Donniehieu/po-dashboard.git/': The requested URL returned error: 403
```

### Cách xử lý (chỉ mất 1 phút trên GitHub):
1. Truy cập vào trang quản lý Token của GitHub: [GitHub Fine-grained Personal Access Tokens](https://github.com/settings/tokens?type=beta).
2. Nhấp vào tên Token bạn đang dùng (mã token bắt đầu bằng `github_pat_...`).
3. Cuộn đến phần **Repository access**:
   - Tích chọn **Only select repositories** -> Nhấn nút **Select repositories** -> Chọn thêm repo **`po-dashboard`**.
   - *(Hoặc bạn có thể chọn **All repositories** nếu muốn dùng chung token này cho mọi dự án của bạn)*.
4. Ở phần **Permissions** -> **Repository permissions**:
   - Đảm bảo mục **Contents** được chọn là **Read and write**.
5. Cuộn xuống cuối trang và nhấn **Save changes** (hoặc **Update token**).
6. Sau khi cập nhật xong, bạn chỉ cần nhắn lại cho tôi: *"Đã cập nhật token"* để tôi bấm lệnh push hoàn tất!

---

## 4. Các lệnh Git thao tác nhanh khi làm việc

### Đối với thư mục `po-data-main`:
```powershell
cd "d:\PO\Nghiên cứu lập app\po-data-main"
git status
git add .
git commit -m "Cập nhật dữ liệu PO"
git push origin main
```

### Đối với thư mục `po-dashboard-main`:
```powershell
cd "d:\PO\Nghiên cứu lập app\po-dashboard-main"
git status
git add .
git commit -m "Cập nhật giao diện Dashboard"
git push origin main
```

