# CONTRIBUTING.md — Git Flow, Commit Convention, PR Checklist

## 1. Nhánh & an toàn

- Phát triển trên nhánh riêng theo tính năng/phiên làm việc: `feature/<mo-ta-ngan>`,
  `fix/<mo-ta-ngan>`, `chore/<mo-ta-ngan>` — **không push thẳng vào `main`**.
- **Không bao giờ** (trừ khi được yêu cầu tường minh):
  - force-push nhánh `main`
  - `git reset --hard` / `git clean -f` khi chưa kiểm tra `git status`
  - amend commit đã publish (đã push lên remote)
  - bỏ qua pre-commit hook bằng `--no-verify`
- Khi hook chặn commit: sửa lỗi gốc rồi tạo **commit mới**, không amend đè lên commit trước đó.
- Trước thao tác có thể mất dữ liệu chưa commit: luôn `git status` trước; `git stash -u` nếu cần giữ lại
  thay đổi.

## 2. Quy ước commit

Định dạng: `<loại>: <mô tả ngắn — vì sao>`

Loại: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `perf`, `ci`.

Ví dụ:
```
feat: thêm ràng buộc unique vote MVP theo trận — tránh 1 người vote nhiều lần
fix: sửa query filter thiếu tenant_id ở endpoint thống kê — rò rỉ dữ liệu chéo CLB
docs: cập nhật ERD sau khi thêm bảng LOI_MOI_DOI_THU
```

Commit message tập trung **"vì sao"** hơn "làm gì" — diff đã tự nói "làm gì" rồi.

## 3. Checklist bắt buộc trước khi mở PR

- [ ] Tài liệu liên quan đã cập nhật cùng đợt (ERD/API doc/checklist tính năng trong `docs/`) —
      **không merge tính năng thiếu tài liệu tương ứng**.
- [ ] Test tương ứng đã viết (unit cho Application layer, integration cho API endpoint mới/đổi).
- [ ] Nếu đổi schema DB: đã tạo migration, đã cập nhật `docs/database/`.
- [ ] Nếu đổi hợp đồng API (breaking change): đã tăng version theo `docs/kien-truc/adr/0003-api-versioning.md`.
- [ ] Nếu là quyết định kiến trúc lớn/khó đảo ngược: đã viết ADR mới trong `docs/kien-truc/adr/`.
- [ ] `CHANGELOG.md` đã ghi nhận thay đổi đáng chú ý.

## 4. Review trước khi merge

- Code review có cấu trúc (không chỉ đọc lướt diff).
- Security review bắt buộc nếu PR đụng tới: xác thực/phân quyền, dữ liệu tài chính (quỹ), dữ liệu cá
  nhân cầu thủ, cấu hình hạ tầng (Docker/Caddy/secrets).
- Ít nhất 1 approval trước khi merge vào `main`.

## 5. Kiểm tra liên kết tài liệu

Sau mỗi đợt sửa nhiều file `.md`, chạy script quét liên kết nội bộ hỏng trước khi merge:

```bash
python3 scripts/check-doc-links.py
```

Exit code 0 nếu không có link hỏng, 1 nếu có (dùng được trong CI).
