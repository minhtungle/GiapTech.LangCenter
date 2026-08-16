# CHANGELOG

Định dạng dựa theo [Keep a Changelog](https://keepachangelog.com/), phiên bản theo
[Semantic Versioning](https://semver.org/).

Bối cảnh chi tiết từng ngày: [`docs/nhat-ky/`](./docs/nhat-ky/README.md).
Tiến độ và lộ trình: [`docs/ke-hoach.md`](./docs/ke-hoach.md).

## [Unreleased]

### Added

**Tài liệu**
- Tổ chức lại `docs/` theo module: nghiệp vụ (16 mã FR) · database · backend · frontend · hạ tầng.
- `docs/ke-hoach.md` — bảng tiến độ 16 FR, lộ trình, nợ kỹ thuật.
- `docs/nhat-ky/` — nhật ký làm việc theo ngày.

**Nền tảng**
- Solution .NET 8, Clean Architecture 4 lớp + 2 project test.
- `LuatPhuThuocTests` canh luật phụ thuộc — `Domain` giữ 0 package.
- 16 entity, `AppDbContext` đa tenant với 2 tầng phòng vệ tự động (Global Query Filter theo
  reflection + tự gán `tenant_id` khi ghi).
- 17 bảng qua 3 migration, đã kiểm chứng áp sạch trên PostgreSQL thật.

**Xác thực (FR-01, FR-02)**
- Đăng nhập bằng bộ ba {mã đội, username, mật khẩu}; đổi mật khẩu; quên mật khẩu qua email.
- Refresh token có **xoay vòng** và **phát hiện tái sử dụng** — dùng lại token đã thu hồi thì thu
  hồi toàn bộ phiên.
- Middleware buộc đổi mật khẩu lần đầu, chặn ở tầng API.
- Cả refresh token lẫn token đặt lại mật khẩu đều lưu **hash**, không lưu token thô.

**Quản trị hệ thống (FR-03 → FR-06)**
- CRUD tài khoản · hồ sơ cầu thủ · nhóm quyền · thiết lập chung.
- Phân quyền động: `[RequirePermission]` + policy sinh động + handler đọc `QUYEN_CHUC_NANG` có cache.
- Seeder tạo CLB mới kèm admin mặc định và nhóm quyền "Quản trị viên" đầy đủ.

**Frontend**
- Vite + React + TS + Tailwind + TanStack Query; 8 màn hình.
- Interceptor tự làm mới token có khử đua (tránh kích hoạt nhầm cơ chế chống đánh cắp).
- i18n với bảng dịch mã lỗi; design token (xanh sân cỏ + cam nhấn + 3 màu trạng thái, có dark mode).

### Changed
- Chuyển bộ khung từ `repo-scaffold/` lên gốc repo — sửa link hỏng và đường dẫn sai trong CI.
- Chốt tên `GiapTech.SoccerRoom` cho namespace/solution/image.
- **`ma_doi` đổi từ chuỗi người dùng tự đặt sang mã 7 ký tự sinh tự động** (bộ 31 ký tự bỏ `0/O`
  và `1/I/L`, không phân biệt hoa/thường). Tên dạng "FC ..." rất dễ trùng giữa các CLB.
- CI bỏ bước build image frontend — frontend là static do Caddy phục vụ (ADR-0004).
- API nhận và trả enum dạng **chuỗi** thay vì số.

### Fixed
- **Rò rỉ dữ liệu chéo tenant**: query filter trỏ ra object ngoài `DbContext` bị EF "nướng cứng" vào
  context đầu tiên, khiến tenant B đọc được dữ liệu tenant A. Lỗi im lặng, không exception.
- Màn Phân quyền hỏng khi lưu do API chỉ nhận enum dạng số.
- `DesignTimeDbContextFactory` hard-code connection string nên không chạy migration lên DB thật được.
- Gỡ tham chiếu tới hai file `.docx` không tồn tại trong repo.
