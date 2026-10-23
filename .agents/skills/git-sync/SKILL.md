---
name: git-sync
description: >-
  Use this skill when the user asks to sync, commit, or push changes to GitHub for po-data and po-dashboard repositories (e.g. "sync git", "đẩy git", "push code", "đồng bộ git").
---

# Git Sync Skill for Multi-Repository Hub

Kỹ năng đồng bộ tự động, linh hoạt đa repository dựa trên cấu hình tập trung `sync-config.json`. Tự động nhận diện đường dẫn máy tính, link GitHub, kiểm tra token và giải quyết xung đột (pull rebase).

## Cơ chế hoạt động động (Dynamic & Config-driven)
Skill không hardcode bất kỳ đường dẫn nào, mà đọc toàn bộ từ file `sync-config.json`. Bất kể dự án được chuyển sang máy khác, đổi tên thư mục, hay thêm bao nhiêu repo mới, skill đều tự động nhận diện và xử lý chính xác.

## Các bước thực hiện tự động

1. **Chạy bộ điều khiển đồng bộ trung tâm**:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -File "d:\PO\Nghiên cứu lập app\scripts\git_manager.ps1" -Action SyncAll
   ```

2. **Nếu cần kiểm tra quyền truy cập hoặc Token**:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -File "d:\PO\Nghiên cứu lập app\scripts\git_manager.ps1" -Action TestAuth
   ```

3. **Tùy biến hoặc thêm repository**:
   - Chỉnh sửa trực tiếp file [sync-config.json](file:///d:/PO/Nghiên%20cứu%20lập%20app/sync-config.json) hoặc mở ứng dụng giao diện [GitSyncApp.bat](file:///d:/PO/Nghiên%20cứu%20lập%20app/GitSyncApp.bat).

4. **Cập nhật nhật ký vào `TODO.md`** và tóm tắt kết quả cho người dùng.
