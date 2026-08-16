# CHANGELOG

Định dạng dựa theo [Keep a Changelog](https://keepachangelog.com/), phiên bản theo
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Khởi tạo repo, tài liệu nền tảng (README, CLAUDE.md, CONTRIBUTING.md, SECURITY.md).
- Tách nghiệp vụ 16 mã FR thành 5 file theo module trong `docs/nghiep-vu/`.
- `docs/database/`: ERD 16 bảng (kèm sơ đồ mermaid) + quy ước đặt tên/migration EF Core.
- `docs/backend/`: Clean Architecture, CQRS/MediatR, multi-tenant, phân quyền động.
- `docs/frontend/`: nguyên tắc UI/UX, cấu trúc design token.
- `docs/ha-tang/`: cài đặt VPS lần đầu, giải thích biến môi trường, runbook 7 sự cố.

### Changed
- Chuyển toàn bộ bộ khung từ `repo-scaffold/` lên thư mục gốc — sửa link hỏng trong README và để
  docker-compose/CI/scripts chạy đúng đường dẫn.
- Chốt tên chuẩn `GiapTech.SoccerRoom` cho namespace/solution/image (trước đó lẫn lộn giữa
  `GiapTech.QuanLyCLB` và `ClubMgmt.*`).
- CLAUDE.md rút gọn còn quy tắc bắt buộc + bản đồ điều hướng; chi tiết chuyển vào `docs/`.
- CI: bỏ bước build image frontend (frontend là static do Caddy phục vụ theo ADR-0004), thay bằng
  upload artifact + copy sang VPS.

### Fixed
- Gỡ tham chiếu tới hai file `.docx` không tồn tại trong repo (5 vị trí trong `docs/`).
- Sửa docstring `scripts/check-doc-links.py` trỏ tới `docs/BASEDUNGCHUNG.md` không tồn tại.
