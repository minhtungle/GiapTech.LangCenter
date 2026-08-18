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

**Chọn đối thủ khi thêm trận (FR-10)**
- Ba đường vào trong cùng một dropdown: chọn từ sổ · gõ tên lạ tạo ngay · tra mã CLB 7 ký tự.
- `GET /doi-thu/tra-cuu-clb/{maDoi}` — endpoint **duy nhất** đọc ngoài tenant, giới hạn chặt:
  chỉ khớp mã chính xác, không trả `id`, và 404 **giống hệt nhau** cho mã sai định dạng / không
  tồn tại / của chính mình (phân biệt được là giúp người dò thu hẹp không gian mã).
- `DOI_THU.ma_doi_he_thong` lưu **mã đội, không phải FK**: FK cho phép join xuyên tenant, và
  sẽ Cascade mất lịch sử đối đầu nếu CLB kia xoá tài khoản.
- Canh bởi `TraCuuClbTests` (12 test, 5 phản chứng) + `e2e/chon-doi-thu.spec.ts` (5 test).
- **Nợ kỹ thuật:** cần rate limit ở tầng Caddy cho endpoint này trước khi lên production.

**Cờ tính năng**
- `GET /api/v1/tinh-nang` (ẩn danh) — API khai những gì đang bật, để frontend không vẽ ra lối
  vào dẫn tới ngõ cụt. Chỉ khai `dangKyClb`, **không khai tên môi trường**.
- Trang đăng nhập ẩn link "Tạo câu lạc bộ" và trang `/dang-ky` báo "Đăng ký chưa mở" khi cờ tắt.
- `ApiFactory` cho ghi đè môi trường (`MoiTruong`) — không có nó thì test "cờ khớp hành vi thật"
  là vô nghĩa: ở Development cả cờ lẫn endpoint đều bật nên `IsDevelopment()` và `true` không
  phân biệt được. Phản chứng này đã lọt một lần.

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
- **Link "Tạo câu lạc bộ" hiện cả trên production**, nơi `/dang-ky-clb` trả 404 vì chỉ bật ở
  Development. Người dùng bấm vào, điền tên, rồi nhận "Đã có lỗi xảy ra" — trông như app hỏng
  chứ không phải "chức năng chưa mở". Gõ thẳng `/dang-ky` cũng mở được form.
- **Bốn lỗi giao diện của dropdown chọn đối thủ**, tất cả chỉ nhìn màn hình mới thấy (220 test
  backend đều xanh khi chúng còn): "Không tìm thấy" hiện lúc sổ đối thủ rỗng sẵn (chưa ai tìm gì);
  placeholder `font-mono` chồng chữ trong ô hẹp; **thông báo lỗi nằm dưới lớp dropdown** nên bấm
  "Tra" mà không thấy phản hồi nào; và cùng một câu in hai lần khi sổ rỗng.
- `MA_DOI_KHONG_HOP_LE` bị pipeline FluentValidation gộp thành `DU_LIEU_KHONG_HOP_LE` — chuyển
  kiểm định dạng mã đội xuống handler (cùng vết đã gặp với `THANH_VIEN_TRUNG`).
- **Rò rỉ dữ liệu chéo tenant**: query filter trỏ ra object ngoài `DbContext` bị EF "nướng cứng" vào
  context đầu tiên, khiến tenant B đọc được dữ liệu tenant A. Lỗi im lặng, không exception.
- Màn Phân quyền hỏng khi lưu do API chỉ nhận enum dạng số.
- `DesignTimeDbContextFactory` hard-code connection string nên không chạy migration lên DB thật được.
- Gỡ tham chiếu tới hai file `.docx` không tồn tại trong repo.
