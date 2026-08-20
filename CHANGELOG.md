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
- Chốt tên `GiapTech.SoccerRoom` cho namespace/solution/image.
- **`ma_doi` đổi từ chuỗi người dùng tự đặt sang mã 7 ký tự sinh tự động** (bộ 31 ký tự bỏ `0/O`
  và `1/I/L`, không phân biệt hoa/thường). Tên dạng "FC ..." rất dễ trùng giữa các CLB.
- CI bỏ bước build image frontend — frontend là static do Caddy phục vụ (ADR-0004).
- API nhận và trả enum dạng **chuỗi** thay vì số.

### Fixed
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
