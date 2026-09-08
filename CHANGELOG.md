# CHANGELOG

Định dạng dựa theo [Keep a Changelog](https://keepachangelog.com/), phiên bản theo
[Semantic Versioning](https://semver.org/).

Bối cảnh chi tiết từng ngày: [`docs/nhat-ky/`](./docs/nhat-ky/README.md).
Tiến độ và lộ trình: [`docs/ke-hoach.md`](./docs/ke-hoach.md).

## [Unreleased]

### Fixed — Ngày khai giảng của lớp bị ghi năm 0001, và không tính lại khi xoá buổi (08/09/2026)

Hai lỗi tìm ra khi **chạy hệ thống và lái UI thật**, không lỗi nào bị test cũ bắt.

- **`TuNgay` / `NgayKhaiGiang` không có validation.** `DateOnly` không nullable nên client gửi
  thiếu trường — hoặc gửi đúng dữ liệu nhưng **sai tên trường** (`ngayKhaiGiang` cho lệnh dùng
  `tuNgay`) — sẽ nhận `default` = 01/01/0001. Handler ghi ngày đó vào `LOP_HOC.ngay_khai_giang`,
  PostgreSQL lưu thành **`-infinity`**, UI hiện **"1/1/1"**, và **không có lỗi nào ở giữa**.
  Nay cả hai validator chặn bằng `NGAY_KHONG_HOP_LE` (có bản dịch).
- **`XoaBuoiHocHandler` không tính lại mốc ngày của lớp.** Xoá buổi đầu — hoặc xoá hết buổi —
  mà lớp vẫn khai ngày khai giảng/kết thúc của buổi **không còn tồn tại**. Trái đúng quy ước
  "ngày của lớp suy từ lịch, không cho sửa tay". Nay tính lại từ buổi còn lại; không còn buổi
  nào thì về `null`.
- Vá dữ liệu dev: một lớp có `ngay_khai_giang = -infinity` được tính lại từ buổi học thật
  (`pg_dump` trước, chạy trong transaction, chỉ đụng hàng đang hỏng).
- 4 test mới, đã thử bỏ cả hai bản vá để chắc chúng đỏ đúng chỗ.

### Changed — Đổi tên token trạng thái: `win/lose/draw` → `ok/loi/cho` (08/09/2026)

Đóng nợ **N8**. Tên cũ là di sản của dự án tiền thân và **đọc lên trái hẳn nghĩa thật**:
`variant="win"` cho "học phí đã đủ", `'lose'` cho "tài khoản bị vô hiệu hoá" — không có
"thắng/thua" nào trong nghiệp vụ đào tạo.

- Biến CSS `--status-ok` · `--status-loi` · `--status-cho`; ánh xạ Tailwind và `Badge variant`
  dùng cùng ba tên. Đổi ở 16 file.
- Nghĩa ghi thẳng vào JSDoc của `Badge`: xong·đạt·đủ / hỏng·quá hạn·bị từ chối / đang chờ·cần chú ý.
- `--primary` bỏ lý do "gợi sân cỏ" → "trung tính, đủ tương phản với chữ trắng (WCAG AA)".
  **Giá trị hex không đổi** — đổi màu là redesign, không ai yêu cầu.
- Kiểm chứng: `npm run build` **không đủ** (đổi tên biến CSS mà quên một chỗ thì build vẫn xanh,
  chỉ màu biến mất). Đã grep bundle CSS trong `dist/` để chắc 3 biến + 6 class sinh ra và không
  còn tên cũ, rồi chạy một test Playwright tạm đọc `getComputedStyle` trên app đang chạy.

### Removed — Sáu nhật ký của dự án tiền thân (08/09/2026)

- Gỡ `2026-08-16` → `2026-08-21` (1.014 dòng): sàn đối thủ, quỹ CLB, đăng ký đá trận qua link.
  Chúng cạnh tranh chú ý với nhật ký thật của dự án, và ba mã nợ **N3/N4/N9** trong đó **đánh số
  khác** bảng nợ hiện tại — ai đối chiếu là hiểu sai. Nội dung còn trong git history.
- Trích bài học **code hiện tại còn dẫn chiếu tới** vào
  `docs/nhat-ky/2026-08-bai-hoc-du-an-tien-than.md`: sự cố mất địa chỉ 16/08 (nguồn của quy tắc
  #1), hạn mức phải kiểm được bằng **con số** chứ không chỉ tên policy, chốt còn người quản trị cuối.
- Sửa **thứ tự bảng mục lục nhật ký** — entry 08/09 nằm giữa danh sách, 09/06 nằm dưới 09/05.

### Removed — ADR-0005 (lời mời thách đấu qua link) (08/09/2026)

- Nghiệp vụ này **không còn dòng code nào** trong repo (gỡ 05/09 cùng nghiệp vụ bóng đá), nên ADR
  mô tả nó chỉ gây nhiễu cho người đọc tài liệu. Nội dung còn trong git history.
- CLAUDE.md: 5 → **4 ADR**. Tham chiếu trong CHANGELOG chuyển thành ghi chú không-liên-kết để
  bản ghi phát hành cũ vẫn đọc được.

### Changed — ADR-0001/0002/0004 viết lại theo dự án này (08/09/2026)

Chủ sản phẩm yêu cầu tài liệu bám sát dự án hiện tại, **bác** lập luận "giữ ADR làm bản ghi lịch
sử" của tôi ở lượt trước. Ba ADR còn hiệu lực nay mô tả LMS:

- **ADR-0001**: bối cảnh "quản lý CLB đá bóng" → "quản lý trung tâm ngoại ngữ". Lý do kỹ thuật
  (.NET 8, PostgreSQL, shared-schema) **không đổi** — có ghi chú nói rõ quyết định chốt 16/08 cho
  dự án khác trên cùng nền tảng và vẫn giữ hiệu lực.
- **ADR-0004**: **sửa quyết định thật** — reverse proxy Caddy → **Nginx + certbot**. Bản gốc chọn
  Caddy; khi triển khai thật thì VPS đích đã có Nginx phục vụ nhiều domain, thêm Caddy sẽ tranh
  port 80/443. Tệ hơn: ADR cũ liệt kê "Nginx + Certbot" ở mục **phương án đã loại bỏ** — đúng
  ngược thực tế. Ghi sửa đổi tại chỗ vì đây là đổi một lựa chọn công cụ do ràng buộc môi trường,
  không phải đổi hướng kiến trúc.
- **ADR-0002**: bỏ ví dụ "sơ đồ chiến thuật kéo-thả".

### Changed — Viết lại tài liệu triển khai và FR-06 (08/09/2026)

- **`prompt-trien-khai-vps.md` viết lại toàn bộ.** Bản cũ mô tả ứng dụng là "quản lý CLB bóng đá
  phong trào", ghi sẵn một domain không phải của dự án, và **hướng dẫn khởi động Caddy** — làm
  theo là tranh port với Nginx đang chạy. Bản mới: bỏ domain cứng (dùng `<domain>`), cảnh báo
  không cài Caddy, thêm bước cấu hình Nginx + certbot, luồng thử nghiệm theo nghiệp vụ LMS
  (tạo giáo viên/học viên → lớp → lịch → điểm danh → học phí), và nhắc rate limit tầng Nginx.
  Sửa số liệu bịa: "399 test + 57 E2E" → **306 test + 11 E2E** (đếm thật).
- **FR-06 Thiết lập chung viết lại** theo đúng 17 cột của bảng `TENANT`. Bản cũ có cả một mục
  **"Bộ áo đấu"** (8 màu, `mau_ao_json`, `MauAoDongBoTests`) — không tồn tại trong LMS. Bản mới
  ghi bốn nhóm trường thật, trong đó `mui_gio` và `so_ngay_canh_bao_no_hoc_phi` là hai trường
  vận hành hay bị coi nhẹ.
- **`multi-tenant.md`**: bảng "5 endpoint ngoài tenant" có **3 endpoint đã bị gỡ** (`/cong-dong`,
  `/cong-dong/loi-moi`, `/moi-qua-link/xem`). Viết lại thành **6 endpoint ẩn danh thật**, mỗi cái
  kèm giới hạn đã kiểm chứng trong code.
- Bỏ hai ghi chú "*FR này vẫn còn trong code nhưng từ ngữ đã đổi*" ở `dang-nhap.md` và
  `quan-tri-he-thong.md` — dùng thẳng từ ngữ đúng thay vì bắt người đọc tự dịch.
- Đổi từ ngữ còn sót ở `README.md` (bỏ câu "**đây là BASE, chưa có nghiệp vụ**" — sai, 14/15 FR
  đã chạy, và câu "tài liệu trong `docs/` vẫn là của dự án cũ"), `SECURITY.md`, `CONTRIBUTING.md`,
  `phan-quyen-dong.md`, `design-tokens.md`, `ui-ux-nguyen-tac.md`, `trien-khai-pull-code.md`.

### Security — `/dang-ky-trung-tam` thiếu giới hạn tần suất (08/09/2026)

- Endpoint ẩn danh **GHI** dữ liệu (tạo tenant + tài khoản admin) mà **không có
  `[EnableRateLimiting]`** — một script sinh tenant rác không giới hạn. `AuthController` đã có
  hạn mức từ 21/08, endpoint đăng ký bị bỏ sót.
- Nguyên nhân gốc là **tài liệu sai**: nợ N3 ghi "đang chặn bằng `IsDevelopment()`", và danh
  sách miễn trừ trong `GioiHanTanSuatTests` chép lại đúng tiền đề đó ("chỉ bật ở Development").
  Nhưng controller nói rõ **MỞ Ở MỌI MÔI TRƯỜNG**. Test canh endpoint ẩn danh thiếu rate limit
  đã có sẵn — nó xanh vì endpoint này nằm trong danh sách miễn trừ dựa trên tiền đề sai.
- Nay có `[EnableRateLimiting(XacThuc)]` (10 req/phút mỗi IP) và **đã gỡ khỏi miễn trừ**, nên
  test sẽ đỏ nếu ai bỏ nó đi. Rate limit ở reverse proxy vẫn bắt buộc — nợ N3 viết lại cho đúng.

### Docs — Dọn di sản dự án bóng đá và đồng bộ số liệu (08/09/2026)

- **Sửa mô tả sai gây hiểu nhầm khi triển khai**: `/dang-ky-clb` → `/dang-ky-trung-tam`, "tạo
  CLB đầu tiên" → "tạo trung tâm đầu tiên", bỏ tham chiếu FR-18 (lời mời thách đấu — không tồn
  tại trong LMS).
- **Sửa tên sai trong tài liệu frontend**: claim JWT `ten_doi` → `ten_trung_tam`, hàm
  `capNhatTenDoi()` → `capNhatTenTrungTam()` (tên thật trong `auth.tsx`).
- **Ghi rõ nghĩa hiện tại của `--status-win/lose/draw`**: xong·đạt·đủ / hỏng·quá hạn / đang chờ
  — kèm cảnh báo tên token là di sản bóng đá (nợ N8), đọc theo bảng chứ không theo kết quả trận.
- **Cập nhật nguyên tắc xác nhận thao tác** theo quyết định 07/09: hỏi trước **mọi** thao tác
  ghi (trước đó tài liệu ghi "không hỏi khi không mất gì"), kèm quy tắc một xác nhận cho một
  đơn vị công việc — bài học từ bảng chấm điểm 20 hộp thoại.
- **Đồng bộ số liệu**: 16 → **20 chức năng**, 7 → **25/26 bảng** có `tenant_id`, 8 → **13 nợ**,
  cập nhật cuối 05/09 → 08/09.
- **Bổ sung ba hệ thống con** vào `TONG-QUAN-KIEN-TRUC.md`, `docs/nghiep-vu/README.md`,
  `tong-thuat.md`, `ke-hoach.md` (giai đoạn 5b) — trước đó chỉ có trong `phan-quyen-dong.md`.
- **Thêm 5 thuật ngữ dễ nhầm** vào `THUAT-NGU.md`: hệ thống con, chức năng dùng chung, Nhân sự
  vs Học viên, `trang_thai_nhan_su` vs `TAI_KHOAN.trang_thai`, `UserId` vs `TaiKhoanId`.
- Lượt đầu tôi **giữ nguyên ADR** với lý do "bản ghi lịch sử" (quy tắc #7). Chủ sản phẩm **bác
  lại**: tài liệu phải bám sát dự án này. Xem mục dưới.

### Changed — Tách màn hồ sơ con người: Nhân sự (HRM) và Học viên (LMS) (08/09/2026)

- Màn "Người dùng" cũ quản cả 4 vai trò, nay tách đôi theo hệ thống:
  **HRM → Hồ sơ nhân sự** (nhân viên, giáo viên, trợ giảng) · **LMS → Học viên**.
  Học viên là **khách, không phải nhân sự**: người phụ trách tuyển sinh cần thêm học viên
  nhưng không nên thấy hợp đồng, lương của giáo viên.
- **Tài khoản** (tên đăng nhập, mật khẩu, nhóm quyền) ở lại cụm Quản trị dùng chung — đó là
  quyền ĐĂNG NHẬP, việc của quản trị, không phải của nhân sự. Nó vẫn gán được cho **cả bốn**
  vai trò nên vẫn gọi `/nguoi-dung`.
- Endpoint mới `/nhan-su` (gác `GiaoVienNhanSu`) và `/hoc-vien` (gác `TaiKhoan`). Lọc vai trò
  ở **server** — lọc trên trang đã tải thì phân trang và tổng số bản ghi đều sai.
- Chặn hai chiều: không tạo được học viên qua `/nhan-su` (`KHONG_PHAI_NHAN_SU`) và không tạo
  được nhân sự qua `/hoc-vien` (`KHONG_PHAI_HOC_VIEN`) — UI ẩn gì thì gọi API trực tiếp vẫn
  lách được, nên phải chặn ở handler.
- Một component `NguoiDung` dùng cho cả hai màn qua prop `phamVi`; chép thành hai file là chép
  ~600 dòng form ba loại hồ sơ rồi hai bản trôi khỏi nhau.

### Fixed — Học viên đọc được danh sách mọi học viên (08/09/2026)

- Ban đầu tôi gác `/hoc-vien` bằng `LopHoc` — nhưng **`LopHoc.Xem` là quyền học viên cũng có**
  (để xem lớp mình học), nên học viên đọc được họ tên, số điện thoại, địa chỉ, tên và số điện
  thoại phụ huynh của mọi học viên khác.
- Nay gác bằng `TaiKhoan`: nhóm Giáo viên có `TaiKhoan.Xem` sẵn ("xem học viên lớp mình"), nhóm
  Học viên không có chức năng `TaiKhoan` nào. **Không** dùng `LopHocToanTrungTam` — giáo viên cố
  ý không có nó, gác bằng nó sẽ chặn oan chính người cần dùng màn này nhất.
- Phát hiện khi kiểm tay bằng tài khoản `hv1` thật. Test viết cùng lượt **không bắt được vì chỉ
  dùng admin** — nay có test bằng tài khoản học viên và tài khoản giáo viên (hai chiều).

### Added — Ba hệ thống con HRM · CRM · LMS (08/09/2026)

Chia hệ thống lớn thành ba hệ thống nhỏ theo nhóm quyền. **Không tách service**: vẫn một API,
một database, một lần đăng nhập — "hệ thống" là cách NHÓM chức năng phân quyền để lọc sidebar.

- `HeThong` enum (Hrm · Crm · Lms) + `ChucNang.HeThongCua()` là **nguồn sự thật duy nhất** cho
  việc nhóm. Không lưu xuống DB: hệ thống của một chức năng là thuộc tính của mã nguồn.
- **Ba chức năng mới**: `NhanVienKinhDoanh`, `GiaoVienNhanSu` (HRM), `DoanhThu` (CRM). Nhóm
  "Quản trị viên" tự có chúng — seeder lặp `ChucNang.TatCa`, và `BoKhuyetQuyenQuanTri` cấp bù
  cho tenant đã tồn tại lúc khởi động.
- **Nhóm dùng chung** (`TaiKhoan`, `PhanQuyen`, `ThietLapChung`, `Anh`,
  `DoiMatKhauNguoiKhac`, `NhatKyHeThong`) không thuộc hệ thống nào — hiện ở sidebar và ở mọi
  tab phân quyền. Ép vào một hệ thống thì người quản trị nhân sự phải sang LMS mới sửa được
  tài khoản.
- `GET /toi/he-thong` — hệ thống người dùng vào được. **Bỏ qua nhóm dùng chung**: tính cả thì
  người chỉ quản trị tài khoản "vào được" cả ba mà ba lối vào giống hệt nhau.
- `GET /quyen/danh-muc` trả thêm `heThongs` — màn phân quyền dựng **tab HRM · CRM · LMS**.
  Chuyển tab **không mất** ô đã tích ở tab khác (một tập `oDaChon` duy nhất); tab có số đếm ô
  đã tích để thấy ngay hệ thống nào đang được cấp quyền.
- **Bộ chuyển hệ thống** ngay trên nút Đăng xuất, chỉ hiện khi vào được từ 2 hệ thống trở lên.
  Sidebar lọc theo hệ thống đang chọn — không gộp quyền của hệ thống khác.
- Lựa chọn hệ thống lưu `localStorage`, nhưng **chỉ dùng khi còn quyền**: admin thu quyền HRM
  thì lần vào sau người đó rơi về hệ thống khác, không kẹt ở sidebar trống.
- Ba module mới hiện là **khung trống** (`DangPhatTrien`) — phân quyền và điều hướng chạy thật,
  nghiệp vụ bổ sung sau.

### Added — View chi tiết buổi học + nhận xét hai chiều (07/09/2026)

- **Route mới `/buoi-hoc/:id`** — 5 tab: Thông tin buổi · Điểm danh · Nhận xét · Bài tập ·
  Tài liệu. Bấm một buổi ở bảng lịch hoặc trên lịch dạng calendar đều vào đây.
- **Chuyển buổi không cần về lịch**: nút trước/sau để chấm lần lượt cả khoá, danh sách thả
  xuống để nhảy tới một buổi cụ thể. **Giữ nguyên tab đang xem** khi đổi buổi — điểm danh 20
  buổi không phải bấm lại tab 20 lần.
- **Nhận xét hai chiều** (`GET`/`POST /buoi-hoc/{id}/nhan-xet` + bảng `NHAN_XET_BUOI_HOC`):
  - Giáo viên nhận xét **từng học viên** trong buổi → cột `DIEM_DANH.nhan_xet`, nhập ở tab
    Điểm danh (bảng đã có đúng một dòng cho mỗi học viên).
  - Học viên nhận xét **về buổi** → bảng riêng, kèm `muc_hai_long` 1–5 tuỳ chọn.
  - **Học viên chỉ đọc nhận xét của mình**; giáo viên của lớp và quản trị đọc tất cả. Lọc ở
    handler, không ở frontend.
- `GET /bai-tap` nhận thêm `?buoiHocId=` — tab Bài tập lọc ở **server**, không `.filter()` trên
  mảng đã tải (lọc phía client thì hai view dùng chung cache và danh sách của lớp bị cắt).
- `BangDiemDanh` tách khỏi `LichVaDiemDanh.tsx` thành file riêng, bọc `KhungNoiDung` để dùng
  được cả dạng modal lẫn dạng tab. Thêm **cột nhận xét của giáo viên**.

### Fixed — Tab Thông tin buổi: thiếu trợ giảng, hiện khoá i18n thô (07/09/2026)

- Nhãn hiện ra chuỗi `buoiHoc.linkHoc` vì khối `buoiHoc` trong `i18n.ts` **chưa có** ba khoá
  `phongHoc`, `linkHoc`, `ghiChu` (quy tắc #3). Thêm đủ.
- **Thiếu hẳn dòng Trợ giảng.** `BuoiHocDto` thêm `TenTroGiangs` — lấy từ `LOP_HOC_TRO_GIANG`
  vì **không có** bảng phân công trợ giảng theo buổi; UI ghi rõ "trợ giảng của lớp".
- Phòng học/link của buổi sinh theo lịch luôn là `null` (quy ước "null = theo lớp", cùng kiểu
  `GiaoVienId`), nên UI hiện dấu gạch — đọc thành "không có phòng" thay vì "P.101 theo lớp".
  DTO thêm `PhongHocHieuLuc`/`LinkHocHieuLuc`/`DiaDiemRieng`, tính ở backend để mọi màn hình
  hiểu giống nhau.
- **Thêm `scripts/check-i18n-keys.py` vào CI.** Thiếu bản dịch không làm `npm run build`,
  oxlint hay test API đỏ — chỉ người dùng thấy chuỗi khoá trên màn hình. Đây là lần thứ hai
  trong ngày, nên đưa vào máy kiểm thay vì dặn nhau nhớ.

### Fixed — Giáo viên bị "bạn không thuộc lớp này" khi viết nhận xét buổi (07/09/2026)

- Tab Nhận xét hiện form gửi cho **mọi** vai trò, nhưng `NHAN_XET_BUOI_HOC` là kênh riêng của
  học viên (giáo viên gửi vào đó sẽ làm lệch thống kê hài lòng) nên backend chặn bằng
  `KHONG_THUOC_LOP_NAY`. Giáo viên của lớp nhập xong mới nhận lỗi — vô lý với người đang dạy.
- `BuoiHocDto` thêm cờ **`toiLaHocVien`**; UI chỉ hiện form cho học viên của lớp, còn giáo viên
  / trợ giảng / quản trị thấy câu giải thích **chỉ đúng chỗ ghi nhận xét của họ** (tab Điểm
  danh, mỗi học viên một ô) thay vì chỉ ẩn form đi.
- Backend **không đổi hành vi** — vẫn chặn như trước. Lỗi nằm ở UI mời người dùng làm việc
  chắc chắn thất bại.

### Fixed — Xoá buổi đã có nhận xét trả 500 thay vì mã lỗi (07/09/2026)

- `NHAN_XET_BUOI_HOC → BUOI_HOC` là Restrict, nhưng `XoaBuoiHocHandler` chỉ kiểm `DIEM_DANH`
  nên FK nổ ở tầng DB → API trả `500 LOI_HE_THONG`. Người dùng không hiểu vì sao và cũng không
  biết việc cần làm là **huỷ** buổi chứ không phải xoá.
- Nay trả `400 BUOI_HOC_DA_CO_NHAN_XET` (đã có bản dịch). Phát hiện khi kiểm tay trên
  PostgreSQL thật — không test nào đỏ vì chưa có test nào xoá buổi có nhận xét.

### Changed — Gộp "Thêm buổi" và "Sinh thêm buổi" thành một (07/09/2026)

- Hai lệnh làm cùng một việc ở hai mức số lượng, và hai nút cạnh nhau với tên gần giống nhau
  gây nhầm. Nay chỉ còn `POST /lop-hoc/{id}/sinh-them-buoi`; **thêm một buổi = để `SoBuoi = 1`**.
- Form mặc định **1 buổi** khi thêm (ca hay dùng nhất), vẫn 24 buổi khi sinh lịch mới.
- Các trường của buổi lẻ chuyển sang lệnh còn lại: `LaHocBu`, `GiaoVienId`, `PhongHoc`,
  `LinkHoc`, `GhiChu`. Bỏ chúng sẽ mất hẳn khả năng ghi buổi dạy bù.
- `POST /lop-hoc/{id}/buoi-hoc` **đã gỡ** (nay trả 405).


### Changed — Bảng chấm điểm lưu một lần cho cả bảng (07/09/2026)

- Nhập điểm và nhận xét cho mọi bài nộp rồi bấm **Lưu điểm** một lần, thay vì gọi API ở
  `onBlur` từng ô. Việc hỏi xác nhận trước mọi thao tác ghi làm cách cũ thành hỏi mỗi lần rời
  một ô — chấm lớp 20 học viên là 20 hộp thoại.
- Chỉ gửi dòng đã sửa, gửi tuần tự (mỗi lượt là một bản ghi nhật ký và một lần `SaveChanges`).
- **Thêm ô nhận xét** — trước đây `nhanXet` chỉ được gửi lại giá trị cũ nên giáo viên không có
  đường nhập.


### Added — Nhật ký hệ thống (FR-16, 07/09/2026)

- **Bảng `NHAT_KY_HE_THONG`** ghi lịch sử thao tác mọi module: ai · lúc nào · lệnh gì · tham số
  · trường nào đổi từ giá trị gì sang gì · IP · thời gian xử lý · thành công hay mã lỗi.
- **Ghi tự động ở pipeline MediatR** (`NhatKyBehavior`) nên 46 lệnh hiện có và mọi lệnh thêm
  sau này đều được ghi, không phải nhớ thêm code. Chỉ ghi lệnh GHI, không ghi truy vấn đọc.
- **Một bản ghi cho mỗi LỆNH**, không phải mỗi dòng dữ liệu: `GhiDiemDanhCommand` ghi 20–30
  dòng một lần, log từng dòng sẽ làm bảng này lớn hơn cả `DIEM_DANH`.
- Giá trị trước/sau lấy từ `ChanBatThayDoi` — một `SaveChangesInterceptor` chụp ChangeTracker
  TRƯỚC khi EF ghi.
- **Chỉ ghi thêm**: không có endpoint sửa hay xoá, có test canh. Chức năng phân quyền mới
  `NhatKyHeThong` — chỉ nhóm quản trị có.
- Màn Quản trị → Nhật ký hệ thống: lọc theo module / hành động / chỉ-thất-bại, mở rộng từng
  dòng để xem trường đã đổi và tham số lệnh.

### Added — Xác nhận trước mọi thao tác ghi

- Hook **`useXacNhan()`** dùng chung cho 11 màn. Hỏi trước mọi thao tác thêm/sửa/xoá, kể cả bấm
  Lưu trong form.
- Lời văn nói **cụ thể đổi gì hoặc mất gì**, không dùng "Bạn có chắc không?" — hỏi nhiều thì
  rủi ro lớn nhất là người dùng bấm Đồng ý theo phản xạ.
- `HopXacNhan` thêm prop `nguyHiem`: nút đỏ và biểu tượng cảnh báo chỉ dành cho thao tác phá
  huỷ, để màu đỏ không mất nghĩa.

### Fixed

- **Nhật ký ghi `chi_tiet` rỗng**: bản đầu đọc ChangeTracker SAU `SaveChanges`, lúc EF đã đặt
  `OriginalValue = CurrentValue`. Phát hiện khi chạy thật. Sửa bằng `SaveChangesInterceptor`.
- **Mật khẩu lộ nguyên văn trong `tham_so`**: cột đó là command THÔ nên mang mật khẩu dạng chữ,
  và mật khẩu còn LỒNG trong khối `TaiKhoan`. Nay che đệ quy cả đối tượng lồng và mảng, thay
  giá trị bằng `***` nhưng giữ tên trường. Phát hiện khi kiểm tay; test cũ bỏ sót vì chỉ kiểm
  cột `chi_tiet`.
- `ApiFactory` (test) thay hoàn toàn cấu hình DbContext nên **mất interceptor** — phải đăng ký
  lại, nếu không mọi test về nhật ký xanh sai.

### Migration

`ThemNhatKyHeThong` — chỉ tạo bảng mới, không đụng dữ liệu hiện có.


### Added — View lịch dạng calendar (07/09/2026)

- **Lịch tháng / tuần / danh sách** cho buổi học, dựng trên **FullCalendar 6.1.21** (MIT).
  Chọn nó thay `react-big-calendar` vì có `timeZone` sẵn và chỉ kéo theo `preact`, trong khi
  bên kia mang cả moment, luxon, lodash và globalize. Dùng 6.1.21 vì v7 các gói view còn beta.
- **`GET /toi/cau-hinh`** trả múi giờ trung tâm — mọi vai trò gọi được. Trước đây frontend
  hiển thị giờ theo múi giờ **máy người xem**; với lịch thì lệch giờ làm buổi nhảy sang ô ngày
  khác.
- Màu trên lịch khớp badge ở bảng; buổi bù dùng viền nhấn. Trục giờ giới hạn 6h–22h.
- Lịch **tải theo yêu cầu** (`lazy`): bundle chính 865 → 631 kB, lịch thành chunk riêng 234 kB.
- CSS map `--fc-*` sang design token nên lịch theo đúng bảng màu và chế độ tối của dự án.


### Added — Bổ sung buổi học (07/09/2026)

- **Thêm buổi lẻ** (`POST /lop-hoc/{id}/buoi-hoc`) — dạy bù, ôn tập. Có cờ "học bù".
- **Sinh thêm buổi** (`POST /lop-hoc/{id}/sinh-them-buoi`) — nối tiếp lịch đang có, **không
  xoá buổi nào**. Trước đây chỉ có `sinh-lich` vốn xoá sạch rồi sinh lại.
- **Xoá buổi** (`DELETE /buoi-hoc/{id}`) — khác `huy` vốn giữ bản ghi. Chặn nếu đã có điểm danh.
- Cả hai đường bổ sung đều chặn trùng giờ (`BUOI_HOC_TRUNG_GIO`), đánh số tiếp theo `MAX(ThuTu)`.
- Giao diện: ba nút riêng với ba hộp xác nhận riêng; nút "Sinh lại" viền đỏ vì là nút duy nhất
  xoá dữ liệu.

### Fixed — Buổi đã chốt bị sửa và huỷ được

- `CapNhatBuoiHoc` và `HuyBuoiHoc` **không kiểm trạng thái**, nên sửa được giờ và huỷ được cả
  buổi ĐÃ CHỐT — làm bản ghi điểm danh nói về một thời điểm không còn tồn tại.
- Thêm `BuoiHoc.DaKhoa` ở Domain (một chỗ duy nhất), bốn handler hỏi qua nó: sửa, huỷ, xoá,
  sinh lịch. `sinh-lich` nay **giữ nguyên buổi đã chốt** thay vì xoá sạch hoặc từ chối.
- `ThemBuoiHoc` ban đầu quên chặn trùng giờ (trong khi `SinhThemBuoi` có) — bấm nút hai lần ra
  hai buổi y hệt. Phát hiện khi kiểm tay, đã vá.


### Added — Ẩn menu và nút theo quyền (07/09/2026, đóng nợ N2)

- **`GET /toi/quyen`** trả toàn bộ quyền hiệu lực của phiên hiện tại. Không gác
  `[RequirePermission]` (ai cũng phải biết quyền của mình) nhưng không nhận tham số id nên
  không dò được quyền người khác. Dùng lại `QuyenService` sẵn có, chung cache.
- **Hook `useQuyen()`** — `coQuyen(chucNang, hanhDong)` và `xemTienCaLop()`. Áp cho menu
  sidebar, tab trong chi tiết lớp, và nút Thêm/Sửa/Xoá ở 6 màn. Giáo viên không còn thấy menu
  "Học phí"; học viên vẫn thấy (để tra công nợ của mình) nhưng không thấy số của cả lớp.
- Ẩn ở frontend là **tiện lợi, không phải bảo vệ** — mọi endpoint vẫn tự gác quyền của nó.

### Fixed

- `BaiTapCuaLop` và `TaiLieu` còn dùng nút icon rời (sót ở lượt gom `MenuThaoTac` trước) — nay
  đã gom vào menu.
- `PhanQuyen` dùng `confirm()` của trình duyệt để xác nhận xoá, trái nguyên tắc UI/UX (không
  style được, không dịch được). Thay bằng `HopXacNhan`.


### Fixed — Rò rỉ học phí qua DTO module lớp học (07/09/2026)

- **Giáo viên và trợ giảng đọc được học phí**, và **học viên đọc được học phí của bạn cùng
  lớp**. Số tiền nằm trong `LopHocDto.HocPhi` và `HocVienTrongLopDto.HocPhiApDung` — hai DTO
  gác bằng `LopHoc.Xem`, quyền mà cả ba vai trò đều có, nên hoàn toàn vòng qua cổng học phí.
  Ma trận quyền và tầng `IPhamViHocPhi` đều đúng; lỗi là trường tiền nằm sai module.
- Vá bằng `IPhamViHocPhi.DuocXemTienCuaLop()` — **che cột** (trả `null`), khác với
  `LocTheoPhamVi` vốn chỉ **lọc hàng**. Học viên vẫn thấy mức áp dụng của chính mình; giáo
  viên vẫn thấy đủ danh sách học viên để điểm danh.
- `RoRiHocPhiTests` — 7 test canh, gồm cả chiều ngược (admin phải vẫn thấy đủ).

### Changed — Gom thông tin học phí về một tab

- Tab Học phí thêm tổng phải thu / đã thu / còn nợ + thanh tiến độ ở đầu trang.
- Tab Tổng quan bỏ hai ô tiền và dòng học phí; bảng danh sách lớp bỏ cột Học phí; form thông
  tin lớp bỏ ô học phí khi sửa (giữ khi tạo, vì lúc đó chưa có tab Học phí).
- Tab Học viên ẩn cột mức áp dụng khi người xem không được phép.
- Vào tab Học phí không có quyền thì hiện thông báo rõ ràng thay vì bảng trống.


### Added — Menu thao tác và view chi tiết lớp học (07/09/2026)

- **`MenuThaoTac`** — nút thao tác trong bảng gom vào một menu có chữ, thay cho dải icon trần.
  Áp cho Lớp học (6 nút → menu), Tài khoản, Người dùng, Học phí. Menu định vị `fixed` vì
  `Table` bọc trong `overflow-x-auto` sẽ cắt mất menu `absolute` ở dòng cuối.
- **View chi tiết lớp học tại `/lop-hoc/:id`** với 6 tab: Tổng quan (mới), Học viên, Lịch &
  điểm danh, Bài tập, Học phí, Tài liệu. Tab lưu ở query `?tab=` nên gửi link được và F5 không
  mất chỗ. Trước đây mỗi thứ là một modal riêng.
- **`KhungNoiDung`** — cho phép một component vừa mở dạng modal vừa nhúng làm tab, không phải
  chép nội dung sang file thứ hai.
- Màn Tài liệu và Học phí nhận prop `lopHocId` để lọc theo lớp. API đã hỗ trợ sẵn, không sửa
  backend dòng nào.
- Tên lớp trong bảng danh sách nay là liên kết tới trang chi tiết.
- **Sửa thông tin lớp ngay trong tab Tổng quan**, không phải mở modal. `FormLopHoc` tách thành
  component dùng chung cho cả modal ở danh sách lẫn form tại chỗ — form có 12 trường và ba quy
  ước null tinh tế, chép sang file thứ hai là mời gọi lỗi mất dữ liệu.

### Fixed

- **Nhãn tab hiện ra chuỗi khoá i18n** (`lopHoc.tab_tong-quan` thay vì "Tổng quan"): mã tab
  dùng gạch ngang cho URL còn khoá i18n dùng gạch dưới, nên ghép động ra khoá không tồn tại.
  Nay nhãn gắn thẳng vào định nghĩa tab.


### Changed — Tách người dùng khỏi tài khoản (07/09/2026)

- **`NGUOI_DUNG` nay là bảng "con người", `TAI_KHOAN` là bảng đăng nhập.** Trước đó một cột
  `TrangThai` gánh hai nghĩa — "còn đăng nhập được" và "còn làm ở trung tâm" — nên vô hiệu hoá
  tài khoản một giáo viên đã nghỉ thì không phân công được họ vào lớp cũ nữa. Nay hai cột
  riêng: `NGUOI_DUNG.trang_thai_nhan_su` và `TAI_KHOAN.trang_thai`.
- Ba bảng hồ sơ theo vai trò: `HO_SO_GIAO_VIEN` (bằng cấp, chuyên môn, ngày vào làm),
  `HO_SO_HOC_VIEN` (trường/lớp, tên và SĐT phụ huynh), `HO_SO_NHAN_VIEN` (chức vụ, phòng ban).
  Đổi vai trò **không xoá** hồ sơ cũ — bằng cấp là sự thật lịch sử.
- **12 khoá ngoại nghiệp vụ không đổi một dòng nào** — `NGUOI_DUNG` giữ nguyên tên và id. Chỉ
  3 khoá ngoại đăng nhập (`NGUOIDUNG_QUYEN`, `REFRESH_TOKEN`, `TOKEN_DATLAI_MATKHAU`) chuyển
  sang `TAI_KHOAN`.
- Màn Quản trị → Tài khoản nay có **hai tab**: Người dùng và Tài khoản. Tạo người mới có thể
  tích ô tạo luôn tài khoản — một lượt gọi, một giao dịch.
- Tài khoản có thể **không gắn ai** (tài khoản kỹ thuật) và người có thể **không có tài khoản**
  (học viên nhỏ tuổi, giáo viên thỉnh giảng).
- JWT thêm claim `tai_khoan_id`; `NameIdentifier` **giữ nguyên là id người**. Token cũ không có
  claim mới → ba thao tác trên chính tài khoản từ chối rõ ràng thay vì đoán nhầm.

### Fixed

- **Việc lọc theo vai trò trước nay chỉ nằm ở frontend** — gọi API trực tiếp là gán được một
  học viên làm giáo viên chính. Nay `KiemNhanSu` kiểm cả vai trò.
- Ba chỗ tra quyền vẫn truyền id người sau khi quyền chuyển sang khoá theo tài khoản:
  `QuyenAuthorizationHandler` (cổng quyền toàn hệ thống), `PhamViLopHoc` và `PhamViHocPhi`
  (admin không thấy sổ học phí nào). Ranh giới nay ghi vào `CLAUDE.md`.
- Dropdown chọn giáo viên/học viên nay lọc `DangLamViec` ở server, không còn hiện người đã nghỉ
  rồi mới bị backend từ chối.

### Migration

`TachNguoiDungVaTaiKhoan` — **viết tay phần chuyển dữ liệu**. Bản EF tự sinh xoá thẳng
`username`/`password_hash` rồi tạo `TAI_KHOAN` rỗng: mọi tài khoản mất mật khẩu và không ai
đăng nhập được (quy tắc #1). Thứ tự đúng: tạo bảng → chuyển dữ liệu → đổi khoá ngoại → xoá cột.
Đã `pg_dump` trước khi áp; kiểm chứng đăng nhập bằng mật khẩu cũ sau migration.


### Added — LMS giai đoạn 4: Học phí (05/09/2026)

- `KHOAN_THU_HOC_PHI` — sổ thu từng khoản: số tiền, ngày thu, hình thức, số phiếu đối chiếu,
  người thu. `numeric(18,2)` + `CHECK (so_tien > 0)` ở tầng DB. Không UNIQUE: nộp nhiều đợt là
  chuyện thường.
- **Công nợ tính động, không có cột `da_thu`** — `hoc_phi_ap_dung − SUM(so_tien)`, cộng dồn
  trong SQL. Lưu cột là mở cửa cho sai lệch khi ai đó sửa hoặc xoá một khoản thu.
- Cảnh báo quá hạn theo `TENANT.so_ngay_canh_bao_no_hoc_phi` (mặc định 14 ngày) tính từ ngày
  khai giảng. Lớp chưa khai giảng không bao giờ quá hạn.
- Màn Học phí hai tab: Công nợ và Sổ thu. Học viên vào cùng màn này để tự tra nợ.
- `IPhamViHocPhi` — **tầng phạm vi riêng cho dữ liệu tiền**, tách khỏi `IPhamViLopHoc`. Giáo
  viên thấy lớp mình dạy nhưng học phí là quan hệ giữa học viên và trung tâm; dùng chung một
  tầng lọc là mở sổ thu cho toàn bộ giáo viên.

### Fixed

- **Đường ghi sổ thu lỏng hơn đường sửa.** `ThuHocPhiHandler` ban đầu cổng bằng phạm vi *lớp*,
  nghĩa là giáo viên ghi được tiền vào sổ — trong khi họ không sửa hay xoá lại được, tạo ra
  khoản thu không ai gỡ nổi. Nay ghi đi qua đúng điều kiện với sửa (`DuocGhiSo`).
- **DTO trả `nguoiThu`, UI đọc `tenNguoiThu`** — cột "Người thu" luôn trống. 11 test tích hợp
  xanh hết vì không test nào chạm cột đó; chỉ lộ khi chạy tay trên PostgreSQL thật. Đổi tên cho
  nhất quán và thêm hai test khoá hợp đồng JSON.

### Changed

- **Dọn dứt điểm tài liệu dự án cũ.** `docs/` trước đó phần lớn vẫn là hệ quản lý CLB bóng đá,
  mang cảnh báo "tài liệu dự án cũ" ở đầu file, và bảng tra FR còn **trùng mã** (FR-07 vừa là
  Lớp học vừa là Lọc trận đấu). Xoá 5 file nghiệp vụ cũ; viết lại `nghiep-vu/README.md`,
  `tong-thuat.md`, `ke-hoach.md`, và `database/erd.md` (20 bảng, **đối chiếu từ
  `information_schema` của DB thật**). Liên kết chết trong CHANGELOG/ADR/nhật ký cũ được gỡ
  nhưng giữ nguyên chữ — lịch sử không sửa.


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
- Xử lý **13 trường hợp** — xem FR-18 và ADR-0005 (*cả hai đã gỡ 08/09/2026 cùng nghiệp vụ này;
  còn trong git history*). Gồm: link bị chuyển tiếp (xác nhận
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
  4 lời mời thách đấu — dựng trong ~8 giây. Tài liệu đã gỡ cùng nghiệp vụ cũ.
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
  FR-17.
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
- **Rà soát toàn hệ thống 20/08** — tài liệu đã gỡ cùng nghiệp vụ cũ. Năm
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
