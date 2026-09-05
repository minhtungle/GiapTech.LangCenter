# CHANGELOG

Định dạng dựa theo [Keep a Changelog](https://keepachangelog.com/), phiên bản theo
[Semantic Versioning](https://semver.org/).

Bối cảnh chi tiết từng ngày: [`docs/nhat-ky/`](./docs/nhat-ky/README.md).
Tiến độ và lộ trình: [`docs/ke-hoach.md`](./docs/ke-hoach.md).

## [Unreleased]

### Added — LMS giai đoạn 3: Học liệu (06/09/2026)

- `TEP_DINH_KEM` một bảng dùng chung với năm cột FK loại trừ nhau + `CHECK` constraint. Mảng
  jsonb thì không dọn được tệp mồ côi và không có `tenant_id`; năm bảng riêng thì job dọn rác
  phải UNION cả năm.
- `BAI_TAP` + `BAI_NOP` (nộp nhiều lần, giữ lịch sử), `BAI_KIEM_TRA` + `BAI_LAM` (schema sẵn,
  API sau), `TAI_LIEU` + `TAI_LIEU_LOP_HOC`. **Có giao diện** cho bài tập và tài liệu.
- Chấm điểm là endpoint riêng, command không có trường nội dung — quyền `Sua` trên bài nộp là
  để chấm, không phải sửa bài học viên.

### Fixed

- **Quyền trên tệp không kiểm chủ sở hữu.** `Anh.Xoa` chỉ nói "được xoá tệp", không nói "xoá
  tệp nào" — học viên gỡ được tệp trong bài nộp của bạn cùng lớp chỉ cần đoán đúng id (mà id
  nằm ngay trong danh sách bài nộp). Phát hiện khi kiểm tay, đã vá và có test canh cả hai chiều.
- Ma trận quyền mặc định thiếu `Anh.Them` cho cả bốn nhóm — học viên không đính kèm được bài
  nộp, giáo viên không đính kèm được đề bài. Lộ ra ngay khi chạy thật.


### Added — LMS giai đoạn 2: Buổi học & Điểm danh (06/09/2026)

- `BUOI_HOC` + `DIEM_DANH`, sinh lịch tự động theo tần suất, **có giao diện**.
- `SinhLichBuoiHoc` — hàm thuần ở Domain, 19 unit test phủ mọi ca biên. Ngày lễ ảnh hưởng
  **khác nhau** tuỳ điều kiện dừng: theo số buổi thì vẫn đủ buổi (ngày kết thúc lùi ra), theo
  ngày thì ít buổi đi.
- **Điểm danh hai nguồn, hai cột trạng thái.** Giáo viên luôn thắng và ghi đè là một chiều,
  nhưng lời khai của học viên được GIỮ để đối chiếu khi tranh chấp. Gộp một cột thì sau khi ghi
  đè không còn biết học viên khai gì — mà "em có điểm danh mà sao bị tính vắng" là tình huống
  thường xuyên.
- Endpoint tự điểm danh **không nhận id học viên** (lấy từ token) — không có tham số nào để lạm
  dụng, dù có quyền `DiemDanh.Them`.
- `IMuiGioTrungTam` — bọc `TimeZoneInfo` với cache và fallback. Không có nó thì một id múi giờ
  gõ sai làm sập cả module lịch.
- **Không có cột "ngày học"**: lọc theo khoảng thời gian tuyệt đối. Cột riêng là dữ liệu thừa và
  sai âm thầm khi trung tâm đổi múi giờ.


### Added — LMS giai đoạn 1: Lớp học (06/09/2026)

- `LOP_HOC` + `LOP_HOC_HOC_VIEN` + `LOP_HOC_TRO_GIANG`, CRUD đầy đủ, **có giao diện**.
- `IPhamViLopHoc` — tầng giới hạn "chỉ lớp mình phụ trách". `[RequirePermission]` chỉ quyết
  định có gọi được endpoint hay không, Query Filter chỉ lọc tenant; không có tầng này thì giáo
  viên đọc được mọi lớp của trung tâm kèm học phí và ghi chú nội bộ.
- Vòng đời lớp: Nháp → Sắp khai giảng → Đang học → Đã kết thúc, nhánh phụ Đã huỷ. Hai trạng
  thái giữa **suy từ ngày lúc đọc**, không lưu cột — tránh phải có job đổi trạng thái lúc nửa
  đêm (job chết là lớp kẹt sai trạng thái mà không ai biết).
- Học phí theo từng học viên (`hoc_phi_ap_dung`, snapshot lúc vào lớp) — cho phép miễn giảm,
  và sửa học phí lớp không đổi hồi tố công nợ người đã đóng.

### Fixed

- **Dockerfile thiếu `tzdata` + `icu-libs`** — lỗi chỉ nổ trên production. Lịch học lưu UTC
  nhưng "buổi học ngày nào" là câu hỏi theo giờ địa phương, nên code cần
  `TimeZoneInfo.FindSystemTimeZoneById`. Alpine không có sẵn cả hai gói; máy dev chạy hoàn hảo
  và mọi test xanh. Đã kiểm chứng bằng cách dựng thử 4 tổ hợp container.


### Added — LMS giai đoạn 0 (06/09/2026)

Đặt nền cho nghiệp vụ LMS theo đặc tả Vietgenedu. Chưa có entity nghiệp vụ; giai đoạn này vá hai
lỗ hổng của base và dựng danh mục quyền.

- `NguoiDung`: thêm `HoTen` (bắt buộc), `NgaySinh`, `AnhDaiDienUrl`, `LoaiNguoiDung`. Mọi màn LMS
  hiển thị họ tên, không ai hiển thị username. Theo quy tắc #1, trường mới vào **cả** DTO, form và
  payload trong cùng PR.
- `Tenant`: thêm `MuiGio` (tiền đề để tính "buổi học ngày nào" — lớp 6h sáng giờ VN rơi sang ngày
  UTC hôm trước) và `SoNgayCanhBaoNoHocPhi` (mỗi trung tâm một chính sách thu).
- `ILuuTruTep` + `MinioLuuTruTep`: kho tệp PDF/Office/ZIP, hạn mức 20 MB. **Tách khỏi**
  `ILuuTruAnh` chứ không nới nó — nới ra là cho phép tải PDF lên làm logo.
- Danh mục `ChucNang`: 5 → **16 chức năng**. Tách nhỏ vì ma trận đặc tả lệch cột-theo-cột (trợ
  giảng toàn quyền bài tập nhưng chỉ xem bài kiểm tra).
- `LopHocToanTrungTam` — không phải module mà là **phạm vi**, cách nhận ra người quản trị mà không
  hard-code vai trò (suy từ dữ liệu quyền, không từ tên nhóm).
- `TenantSeeder` tạo sẵn **4 nhóm quyền** (Quản trị viên / Giáo viên / Trợ giảng / Học viên) đúng
  ma trận đặc tả — giữ phân quyền động (quy tắc #9) thay vì đổi sang role enum cứng.
- `BoKhuyetQuyenQuanTri`: cấp bù quyền cho nhóm quản trị của trung tâm **đã tồn tại** khi danh mục
  chức năng dài ra. Không có bước này thì thêm module = admin trung tâm cũ bị 403 trên toàn bộ
  tính năng mới, âm thầm và rất khó chẩn.

### Fixed

- Suýt làm hẹp cột `NGUOI_DUNG.dia_chi` từ `text` xuống `varchar(300)` — EF cảnh báo "may result
  in the loss of data", đã bỏ `HasMaxLength` và tạo lại migration (quy tắc #1).
- Sửa hai `defaultValue` EF sinh sai trong migration: `mui_gio` `""` → `Asia/Ho_Chi_Minh` (rỗng
  làm mọi buổi học lệch ngày), `so_ngay_canh_bao_no_hoc_phi` `0` → `14` (0 = cảnh báo nợ ngay hôm
  khai giảng).

### Kiểm chứng

143 test xanh (35 unit + 108 integration, tăng 15), build 0 warning, frontend typecheck sạch.
Trên PostgreSQL thật: migration áp sạch, trung tâm mới có 4 nhóm với 64/25/18/14 quyền, xoá 96
hàng quyền rồi khởi động lại thì bổ khuyết cấp lại đúng 44 quyền và không đụng nhóm khác.


## [2.0.0] — 2026-09-05 — Tách base cho dự án LMS

Repo chuyển từ **quản lý CLB đá bóng** (`GiapTech.SoccerRoom`) thành **base cho hệ thống quản
lý trung tâm ngoại ngữ** (`GiapTech.LangCenter.LMS`): giữ toàn bộ tầng hệ thống, bỏ hết nghiệp
vụ bóng đá. Bản bóng đá đầy đủ vẫn còn ở repo cũ.

### Removed

- **Nghiệp vụ bóng đá** (~55.000 dòng): lịch thi đấu, chi tiết trận, đội hình + sơ đồ chiến
  thuật, mẫu đội hình, video, đối thủ, Cộng đồng/sàn bắt đối, lời mời (thách đấu · qua link ·
  đăng ký nhanh), tài chính (quỹ · khoản chi), thống kê, hồ sơ cầu thủ, dữ liệu mẫu.
- `NguoiDung`: bỏ `CauThuId` (liên kết hồ sơ) và `LaTruongNhom`.
- `Tenant`: bỏ `MauAoJson`, `SanNha`, `NgayThanhLap`. Giữ nhóm chung, nhóm liên hệ và nhóm
  ngân hàng/QR (trung tâm cũng cần thu học phí).
- 14 migration cũ → tạo lại một `InitialCreate` gồm 7 bảng hệ thống.
- 5 thư viện frontend không còn ai dùng: `recharts`, `@calendarjs/ce`, `qrcode`,
  `@tanstack/react-table`, `class-variance-authority`.

### Changed

- **Đổi tên đồng bộ** toàn source: 4 project + 2 test project, solution, namespace, Dockerfile,
  compose, CI, docs.
- **Thống nhất 4 lược đặt tên** vốn lẫn lộn từ trước — `GiapTech.SoccerRoom` (code),
  `soccercity` (compose + image), `soccerroom` (JWT issuer · MinIO bucket · SMTP), `clubmgmt`
  (DB name) — về một tên `langcenter-lms`. CI trước đây push
  `ghcr.io/$owner/soccerroom-api` còn compose kéo `soccercity-api`: **hai bên đã lệch nhau**,
  nay khớp.
- **Từ ngữ định danh**: `MaDoi`/`TenDoi` → `MaTrungTam`/`TenTrungTam`; claim JWT
  `ma_doi`/`ten_doi` → `ma_trung_tam`/`ten_trung_tam`; route `/dang-ky-clb` →
  `/dang-ky-trung-tam`, `/auth/ten-doi/{ma}` → `/auth/ten-trung-tam/{ma}`, `/anh/clb/*` →
  `/anh/trung-tam/*`. `KhuVuc` → `DiaChi`, `LienHeCongKhai` → `LienHe`.
- `ChucNang`: còn `TaiKhoan`, `PhanQuyen`, `ThietLapChung`, `DoiMatKhauNguoiKhac` và **thêm
  `Anh`** — xem phần Fixed.
- i18n 814 → 207 dòng (bỏ 17 namespace + 58 mã lỗi nghiệp vụ, giữ 29 mã hệ thống).
- Frontend: 15 → 4 route bảo vệ, 13 → 4 mục menu; `index.css` 276 → 89 dòng. Màn Tổng quan
  thành khung dẫn tới các màn quản trị, chờ thiết kế mới.
- localStorage `sr_*` → `lms_*` (người đang có phiên sẽ phải đăng nhập lại một lần).

### Fixed

- **Endpoint đọc ảnh dùng chung không còn gác bằng quyền của một module cụ thể.** Trước đây nó
  dùng `[RequirePermission(ChucNang.CauThu, Xem)]`; bỏ danh mục đó mà không đổi thì **mọi ảnh
  kể cả logo trả 403** — âm thầm, không test nào bắt được. Nay có `ChucNang.Anh` riêng.
- **Quy tắc #1 — cập nhật thiết lập không còn xoá trường không gửi.** `TenVietTat`, `MoTa`,
  `LogoUrl`, `AnhBiaUrl` gán trực tiếp (`t.MoTa = request.MoTa`) trong khi các trường khác dùng
  `is { }` để phân biệt "không gửi" với "gửi rỗng" — hai quy ước trái ngược trong cùng một
  handler. Phát hiện khi kiểm tay trên PostgreSQL thật, đã đưa về một quy ước và có
  `CapNhatKhongMatDuLieuTests` canh (đã kiểm bằng phản chứng).
- **Route tra tên trung tâm khớp lại giữa BE và FE.** Backend còn `ten-doi` trong khi frontend
  đã gọi `ten-trung-tam`; test cũng dùng tên cũ nên **vẫn xanh dù thực tế đã lệch**. Đã đồng bộ
  và soát toàn bộ endpoint FE gọi so với Swagger.

### Added

- `DongThoiTests` viết lại trên 7 ràng buộc UNIQUE hệ thống, **thêm hai test chiều ngược** mà
  bản gốc chưa có: `UNIQUE(username)` phải gồm `tenant_id`, và cùng một username tạo được ở hai
  tenant khác nhau.
- Test E2E quy tắc #1 cho màn Thiết lập — nơi quy ước `null`/`''` khác màn Tài khoản.
- Ghi chú trong `playwright.config.ts` về việc làm lại endpoint dọn tenant test khi có nghiệp vụ.

### Kiểm chứng

- 128 test backend xanh (35 unit + 93 integration), `dotnet build` 0 warning.
- Frontend `tsc -b` + `vite build` sạch.
- **PostgreSQL thật**: migration áp sạch, đúng 7 bảng, `ix_nguoi_dung_tenant_id_username` gồm
  `tenant_id`; chạy đầu-cuối tạo trung tâm → đăng nhập → bị chặn buộc đổi mật khẩu → đổi → vào
  hệ thống → tải logo (MinIO) đọc lại 200; tenant 2 đọc ảnh của tenant 1 trả 404 đúng.


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
- **Trang đăng nhập tra tên đội theo mã** — gõ đủ 7 ký tự là hiện tên CLB, sai thì hiện "Không
  tìm thấy đội tương ứng". Trước đó gõ sai mã chỉ biết sau khi điền hết form và nhận "sai thông
  tin đăng nhập", không phân biệt được sai mã hay sai mật khẩu.
  `GET /auth/ten-doi/{maDoi:length(7)}` ẩn danh, chỉ trả `tenDoi`, chỉ khớp mã **chính xác** —
  **không** có đường tìm theo tên (quyết định 20/08, xem nhật ký).

**Đăng ký đá trận qua link/QR (FR-19)**
- Trưởng nhóm sinh link + QR cho lời mời đăng ký; người **không có tài khoản** mở link, chọn tên
  mình, bấm Tham gia / Chưa chắc / Không. Cập nhật thẳng vào bảng phản hồi.
- Tab **Đăng ký** trong chi tiết trận (trước đó nút tạo nằm ở Hòm thư, tách rời khỏi trận).
- Chọn/bỏ ai được mời — trước đó luôn mời tất cả, không có cách bỏ ai. Bỏ người **đã trả lời** bị
  chặn cho tới khi xác nhận, kèm tên cụ thể (quy tắc #1).
- Bốn mốc hạn link (5 phút · 1 giờ · tới giờ đá · 7 ngày) chặn ở validator, cộng nút thu hồi.
- **Không** áp cho vote MVP: "chọn tên mình" phá vỡ tính bí mật của bầu chọn.

**Màn Tổng quan (FR-20)**
- **Việc cần làm** — lời mời thách đấu chờ trả lời · trận chưa mời đăng ký · khoản quỹ chưa thu ·
  việc của chính mình. Mỗi dòng bấm được để tới đúng chỗ xử lý.
- **Trận kế tiếp + trận vừa rồi**, kèm tiến độ đăng ký (`chưa mời` khác `0/16`).
- Phân vai: trưởng nhóm thấy việc của đội, cầu thủ thường chỉ thấy việc của mình.
- Không bắt quyền: đây là màn đầu tiên sau đăng nhập, bắt quyền thì người không có quyền nào rơi
  vào trang trắng.

**Cầu thủ nghỉ thi đấu (FR-04 bổ sung)**
- Cho cầu thủ dừng hoạt động với nhóm — **không xoá**: hồ sơ và toàn bộ lịch sử (bàn thắng, phiếu
  MVP, đóng quỹ) giữ nguyên và vẫn tính vào thống kê. Chỉ ẩn khỏi các chỗ chọn người cho việc sắp
  tới (mời đăng ký, xếp đội hình).
- Trước đó chỉ có xoá cứng, mà xoá bị chặn nếu cầu thủ từng đóng quỹ — nên người đá lâu năm rồi
  nghỉ thì không xoá được, cũng không có cách đánh dấu.
- Bộ lọc `Đang đá / Đã nghỉ / Tất cả`, mặc định "đang đá". Cho đá lại bất cứ lúc nào.
- Tuỳ chọn khoá kèm tài khoản, **không** tự động: có người nghỉ đá mà vẫn làm thủ quỹ. Chặn tự
  khoá tài khoản của chính người đang đăng nhập.

**Hạ tầng & chất lượng**
- **Giới hạn tần suất** cho 7 endpoint ẩn danh (`Microsoft.AspNetCore.RateLimiting`, cửa sổ trượt
  theo IP): tra cứu 30/phút · lời mời theo token 10/phút · xác thực 10/phút. Trả 429
  `QUA_NHIEU_YEU_CAU` kèm `Retry-After`. Có test quét **toàn bộ** controller để bắt endpoint ẩn
  danh mới mà quên gắn giới hạn.
- `EnableRetryOnFailure` cho Npgsql — không có nó thì mỗi lần PostgreSQL restart, request **đầu
  tiên** sau đó trả 500 "Đã có lỗi xảy ra" còn các request sau tự lành.
- `POST /du-lieu-mau/don-tenant-test` (chỉ Development) + `globalTeardown` của Playwright — E2E
  không còn để lại CLB rác trên trang Cộng đồng.

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

**Thông tin chuyển khoản quỹ (FR-16)**
- Khai số tài khoản · ngân hàng · chủ tài khoản · ảnh QR ở Thiết lập chung; checkbox **theo từng
  đợt quỹ** để chọn có hiện lên màn thu tiền hay không (đợt thu tiền mặt tại sân thì tắt).
- **Hệ thống KHÔNG xử lý tiền** — chỉ hiển thị. Thủ quỹ vẫn nhập tay số đã nhận. Không cổng
  thanh toán, không webhook, không đối chiếu sao kê; màn thu tiền nói rõ điều này để người
  chuyển không tưởng thất bại rồi chuyển lại lần nữa.
- Bốn trường này là dữ liệu **NỘI BỘ**, khác `lien_he_cong_khai`: không lên Cộng đồng. Ảnh QR
  dùng thư mục riêng `qr-chuyen-khoan/`, tách khỏi logo và ảnh bìa (hai loại đó có lên Cộng đồng).
- Lỗi đã TRÁNH: hai handler ảnh dùng `default:` cho ảnh bìa — thêm `LoaiAnh.AnhQr` mà không đổi
  thành `case` tường minh sẽ khiến QR ghi lên `anh_bia_url`, mất ảnh bìa và làm QR hiện công khai.
- Canh bởi 8 test tích hợp mới (6 phản chứng) + 1 test E2E.

**Lời mời thách đấu qua link/QR (FR-18)**
- Sinh link + QR mời đối thủ **chưa liên kết**. Họ mở link (KHÔNG cần đăng nhập để xem), đăng
  nhập hoặc **tạo đội mới ngay tại đó**, chấp nhận → đối thủ "chỉ là cái tên" **nâng cấp thành
  CLB có ID thật**, trận vào lịch cả hai bên.
- Xử lý **13 trường hợp** — xem [FR-18](./docs/nghiep-vu/loi-moi-qua-link.md) và
  [ADR-0005](./docs/kien-truc/adr/0005-loi-moi-qua-link.md). Gồm: link bị chuyển tiếp (xác nhận
  danh tính + huỷ liên kết được) · hết hạn · thu hồi · dùng token hai lần · mời chéo (gộp trận) ·
  trận đã đá xong · đối thủ trùng lặp (gộp).
- Bảng `LOI_MOI_LINK` RIÊNG, không mở rộng `LOI_MOI_BAT_DOI`: bảng đó cần cả hai tenant, còn
  lời mời link chưa biết bên nhận là ai.
- Token lưu **hash** (SHA-256), dùng lại cơ chế token đặt lại mật khẩu. Truyền trong **body**
  không phải URL — URL đi vào log Caddy, history trình duyệt, và header `Referer`.
- **Mở đăng ký CLB ở production** (nợ N4): luồng "đối thủ chưa có tài khoản" là ca phổ biến nhất
  và không chạy được nếu đăng ký bị chặn. ⚠️ Rate limit ở Caddy giờ là **bắt buộc** trước khi lên
  Internet (nợ N3).
- 20 test tích hợp, 9 phản chứng.

**Bộ dữ liệu mẫu để test tay**
- `POST /api/v1/du-lieu-mau/seed` (chỉ Development): 7 CLB, 39 cầu thủ, 64 trận, 8 đợt quỹ,
  4 lời mời thách đấu — dựng trong ~8 giây. Xem [docs/du-lieu-mau.md](./docs/du-lieu-mau.md).
- Hai CLB đầy đủ NGANG NHAU để test cách ly dữ liệu và lời mời hai chiều; ba vai mỗi CLB
  (admin/manager/player) để test phân quyền.
- 6 tháng quá khứ + 1 tháng tương lai; đủ mọi trạng thái quỹ, lời mời, trận đấu.
- Tỷ số nhà **cộng từ đánh giá cầu thủ** qua `DongBoTySoNha()`, không gán tay: gán tay tạo ra
  dữ liệu hệ thống không sinh nổi (tỷ số 3-1 mà tổng bàn cầu thủ = 0).
- `xoaDuLieuCu` mặc định `false` — xoá dữ liệu là lựa chọn tường minh (quy tắc #1).
- 14 test tích hợp, 6 phản chứng.

**Cộng đồng (FR-17)** *(đổi tên từ "Sàn đối thủ" theo yêu cầu)*
- **Trang chi tiết CLB** `/cong-dong/{maDoi}`: mô tả, khu vực, sân nhà, bộ áo, thành tích đầy đủ
  (kèm bàn thắng/thua), 10 trận gần nhất, số trận đối đầu với ta, và nút thách đấu.
  Vẫn KHÔNG lộ tên cầu thủ, ghi chú/nhận xét từng trận, quỹ, hay số điện thoại.
- Route dùng `{maDoi:length(7)}`: `{maDoi}` trần cũng khớp `khu-vuc` và `loi-moi` — cả hai **đúng
  7 ký tự**, nên chỉ thứ tự literal của ASP.NET Core đang giữ. Test canh bằng chuỗi 6 và 8 ký tự.
- Đổi tên: URL `/san-doi-thu` → `/cong-dong`, entity `LoiMoiBatDoi` → `LoiMoiThachDau`.
  **Tên bảng `LOI_MOI_BAT_DOI` giữ nguyên** — đổi là mất dữ liệu đang có (quy tắc #1); đã kiểm
  bằng `dotnet ef migrations add` thử: `Up()` rỗng, không đụng schema.
- Danh sách CLB đã đăng ký hệ thống + gửi lời mời thách đấu. Lọc theo tên/mã và theo khu vực.
- `LOI_MOI_BAT_DOI` — **bảng duy nhất thuộc HAI tenant cùng lúc**, nên không có Global Query
  Filter. Mọi truy vấn tự lọc `TenantGuiId == toi || TenantNhanId == toi`; FK dùng **Restrict**
  chứ không Cascade (xoá một CLB không được xoá lời mời khỏi hòm thư CLB kia).
- Đồng ý tạo **hai trận độc lập**, một ở lịch mỗi bên — trận dùng chung sẽ buộc một CLB sửa dữ
  liệu nằm trong tenant của CLB kia.
- Ba ô mới ở Thiết lập chung: khu vực · sân nhà · liên hệ thách đấu, tách thành nhóm và ghi rõ
  "hiện CÔNG KHAI".
- ⚠️ **Quyết định của chủ sản phẩm:** mọi CLB tự động lên cộng đồng, không tắt được, kèm thành tích
  thắng/hoà/thua. Cố ý đi ngược thiết kế của `tra-cuu-clb` (vốn dựng để chặn liệt kê CLB) — xem
  [FR-17](./docs/nghiep-vu/lich-thi-dau.md#fr-17--cộng-đồng-bổ-sung-18082026).
- Canh bởi `CongDongTests` (16 test, 8 phản chứng) + `e2e/cong-dong.spec.ts` (5 test).
- `CachLyTenantTests` thêm test **chiều ngược**: entity KHÔNG có Query Filter phải là ngoại lệ
  đã khai lý do. Thêm entity không kế thừa `TenantEntity` giờ làm test đỏ thay vì lọt im lặng.

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
- Chốt tên `GiapTech.LangCenter.LMS` cho namespace/solution/image.
- **`ma_doi` đổi từ chuỗi người dùng tự đặt sang mã 7 ký tự sinh tự động** (bộ 31 ký tự bỏ `0/O`
  và `1/I/L`, không phân biệt hoa/thường). Tên dạng "FC ..." rất dễ trùng giữa các CLB.
- CI bỏ bước build image frontend — frontend là static do Caddy phục vụ (ADR-0004).
- API nhận và trả enum dạng **chuỗi** thay vì số.

### Fixed
- **Quân đối thủ trên bảng chiến thuật hiện dấu "?"** — bộ dữ liệu mẫu sinh quân đối thủ không có
  số áo nên `SoDoSan` rơi về fallback. Tìm thấy khi chạy hệ thống và xem ảnh chụp.
- **Ba luồng tạo bản ghi TRÙNG khi request đồng thời** (rà soát vòng hai 20/08). 5 request song
  song cho ra: 5 trận + 5 đối thủ trùng (chấp nhận lời mời link) · 5 lời mời thách đấu · 5 link
  mời. Nguyên nhân chung: ràng buộc "chỉ một" chỉ kiểm ở tầng ứng dụng (`AnyAsync` rồi `Add`) nên
  hai request song song đều thấy "chưa có". Vote MVP không bị vì đã có UNIQUE ở DB.
  Thêm ba UNIQUE index có filter riêng, và middleware trả **409 `THAO_TAC_TRUNG`** cho SQLSTATE
  23505 thay vì 500 "Lỗi hệ thống".
- **Rà soát toàn hệ thống 20/08** — xem [docs/ra-soat-20-08.md](./docs/ra-soat-20-08.md). Năm
  thiếu sót, tất cả đã sửa:
  - **Thu quỹ QUÁ số phải đóng** được nhận: thu 999.000.000₫ cho khoản 100.000₫ → tiến độ hiện
    `999000000 / 100000`, người đó tính là đã đóng đủ, và số sai lan vào mọi thẻ ở màn Tài chính.
    Giờ chặn `THU_QUA_SO_PHAI_DONG`, trả kèm số phải đóng để UI nói rõ.
  - **Xoá cầu thủ làm tỷ số trận mất căn cứ**: cầu thủ ghi 2 bàn trong trận thắng 2-1 → xoá →
    trận vẫn 2-1 với **0 bàn trong đánh giá**. `DongBoTySoNha` chỉ được gọi khi lưu đánh giá.
    Giờ tính lại tỷ số của đúng những trận có liên quan.
  - **Chỉ số kỹ năng ngoài thang 1–10** được nhận (`{"tanCong": 99}`) → radar vẽ ra ngoài khung.
    Backend trước đây KHÔNG biết danh sách 6 chỉ số — nó chỉ có ở frontend. Thêm
    `Domain/Common/ChiSoKyNang.cs` + test đồng bộ hai tầng (như `MauAoDongBoTests`).
  - **Bàn thắng một cầu thủ không có giới hạn trên** (500 → tỷ số 500-1). Giới hạn mềm 50.
  - **Ô nhập giữ con số vừa bị từ chối** — phát sinh khi sửa lỗi đầu, phát hiện bằng ảnh chụp:
    `key={id}-${soTien}` không đổi khi bị từ chối (số tiền trong DB không đổi) nên React giữ
    nguyên ô, mâu thuẫn với cột "Còn thiếu".
- **Đối thủ trùng lặp khi liên kết** (ca 13 của FR-18): bên mời vừa gõ tay tên đội, vừa đã tra mã
  đội đó từ Cộng đồng → hai bản ghi cùng `ma_doi_he_thong`, thành tích đối đầu đếm sai. Giờ gộp
  trận sang một bản ghi. Phát hiện khi **xem màn Đối thủ** sau khi chạy luồng thật.
- **CLB khác thu hồi được link của mình** nếu bỏ Query Filter — test cách ly cũ chỉ canh chiều
  ĐỌC (danh sách), không canh chiều GHI qua id trực tiếp. Đã thêm test.
- **Bốn lỗi trong bộ dữ liệu mẫu**, cả bốn chỉ lộ khi xem con số trên màn hình: 18 người "đóng"
  1₫–17₫ vì `_ => canDong` trong switch **lồng** bị C# hiểu là pattern gán biến; trận hiện 22:00
  vì ghi `TimeSpan.Zero` thay vì UTC+7; bảng xếp hạng thiếu 4 người và **cột "Cứu thua" trống
  hoàn toàn** vì đội hình xoay `i % 4`; và test "số tiền" vẫn xanh khi tổng quỹ là 153₫.
- **Không hoàn tác được khi bấm nhầm "đã đóng đủ".** Backend vốn đã cho (gửi số tiền 0), nhưng UI
  chỉ sửa được bằng cách tự xoá ô rồi gõ `0` — không ai đoán ra. Thêm nút **↺ Hoàn tác** kèm hộp
  xác nhận nói rõ tên + số tiền + hậu quả.
- **Ô "Đã đóng" không cập nhật sau khi bấm ✓.** `defaultValue` chỉ có tác dụng ở render đầu, nên ô
  vẫn hiện 0 trong khi cột "Còn thiếu" báo đã đủ — người dùng tưởng chưa lưu được. Thêm `key` buộc
  React dựng lại ô khi số tiền đổi từ phía server.
- Nút ✓ và ↺ **không dùng chung vị trí** nữa: sau khi bấm ✓, nút ↺ nhảy vào đúng toạ độ đó và cú
  bấm tiếp theo theo quán tính sẽ xoá mất khoản vừa ghi.
- **Lưu đợt quỹ không làm mới cache chi tiết.** Chỉ `invalidateQueries(['quy'])`, thiếu
  `['quy-chi-tiet']` — nên tắt hiển thị chuyển khoản xong mở lại màn thu tiền vẫn thấy số tài
  khoản. Lỗi có sẵn từ trước, tính năng chuyển khoản làm nó lộ ra. Phát hiện khi xem màn hình.
- **Cộng đồng trả `lienHeCongKhai` của mọi CLB.** Một lần gọi API là thu được số điện thoại
  toàn hệ thống — đúng cửa spam mà việc "chỉ hiện liên hệ sau khi chấp nhận" ở hòm thư định chặn.
  Phát hiện khi gọi API thật và đọc kết quả, không test nào bắt được lúc đó.
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
