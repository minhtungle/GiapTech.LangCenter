# Module HRM (FR-22 → FR-24)

Quản lý nhân sự: cơ cấu tổ chức, hồ sơ, chức vụ.

> **Nhân sự ≠ học viên.** Học viên là *khách*, nằm bên [LMS](./lop-hoc.md). Người phụ trách
> tuyển sinh cần thêm học viên nhưng không nên thấy hồ sơ giáo viên — chốt 08/09/2026.

## Ba tầng, đừng gộp

Đây là điểm dễ sai nhất của module này. Ba thứ nghe giống nhau nhưng khác tầng hoàn toàn:

| Tầng | Là gì | Ai quyết định | Ảnh hưởng gì |
|---|---|---|---|
| `LoaiNguoiDung` | **Loại nghiệp vụ**: NhanVien · GiaoVien · TroGiang · HocVien | Cố định trong code (enum) | Ai được gán dạy lớp, ai được ghi danh học, hồ sơ con nào áp dụng |
| **Chức vụ** (FR-24) | **Chức danh**: Ban quản lý · Trưởng phòng · Kế toán… | Admin tự thêm/sửa (bảng danh mục) | Chỉ hiển thị và báo cáo. **Không** gác gì |
| **Quyền** (FR-04) | Làm được gì trong hệ thống | Admin gán qua nhóm quyền | Mọi endpoint, qua `QUYEN_CHUC_NANG` |

### Vì sao KHÔNG nhét chức vụ vào `LoaiNguoiDung`

Chủ sản phẩm đề xuất "vai trò mặc định (chức vụ): ban quản lý, nhân viên kinh doanh, giáo viên,
trợ giảng" — danh sách này **trộn hai tầng**:

- `GiaoVien`/`TroGiang` đã là `LoaiNguoiDung`, và nó **load-bearing**: handler lọc theo nó để
  biết ai gán được vào lớp (`LopHocDtos`), ai ghi danh được (`HocVienTrongLopDtos`), và hồ sơ
  con nào được tạo (`NguoiDungDtos.GhiHoSo`). Thêm giá trị mới vào enum này là buộc 6+ chỗ phải
  biết xử lý giá trị đó.
- "Ban quản lý" **không** phải loại nghiệp vụ mà là chức danh. Nhét vào enum sẽ tạo một giá trị
  mà không màn nghiệp vụ nào biết làm gì với nó.
- "Nhân viên kinh doanh" đã đặc biệt sẵn — nhưng qua **quyền** `ChucNang.NhanVienKinhDoanh`,
  không qua `LoaiNguoiDung`. Đó là chỗ đúng của nó (quy tắc #9).

Nên: `LoaiNguoiDung` **giữ nguyên 4 giá trị**, chức vụ là bảng danh mục riêng. Một người có cả
hai: `LoaiNguoiDung = GiaoVien` + `ChucVu = "Trưởng bộ môn Anh"`.

## FR-22 — Cơ cấu tổ chức

Bảng `PHONG_BAN`: `ten`, `phong_ban_cha_id` (tự tham chiếu, null = cấp gốc),
`nguoi_quan_ly_id`, `mo_ta`, `thu_tu`.

### Tag vai trò (16/09/2026)

Mỗi phòng ban mang **một** tag vai trò, hoặc không tag:

| Tag | Nghĩa | Hệ quả ở module khác |
|---|---|---|
| `KinhDoanh` | Nhóm bán hàng | **Hiện** ở bộ lọc đội nhóm của Khách hàng · Doanh thu · Thống kê CRM |
| `GiaoVien` | Nhóm giáo viên đứng lớp | Nhận diện nhóm; không hiện ở bộ lọc doanh số |
| `TroGiang` | Nhóm trợ giảng | Nhận diện nhóm; không hiện ở bộ lọc doanh số |
| *(không tag)* | Chỉ **diễn tả cơ cấu** | Hiện trong cây này, xếp được nhân sự, nhưng **không xuất hiện ở bộ lọc của module nào** |

**Vì sao cần.** Trước đó bộ lọc "đội nhóm" ở CRM liệt kê *mọi* phòng ban, kể cả phòng Đào tạo
và các phòng chỉ tồn tại để vẽ sơ đồ. Chọn phòng Đào tạo để xem doanh thu là câu hỏi vô nghĩa —
nó không bán hàng — nhưng người dùng vẫn phải đọc qua nó mỗi lần lọc.

**Một tag, không nhiều.** Nhiều tag thì khi lọc doanh thu theo "nhóm kinh doanh", phòng mang cả
hai tag vẫn hiện, và người đọc không hiểu vì sao phòng Đào tạo nằm trong danh sách đội bán hàng.

**Nguồn duy nhất cho mọi bộ lọc:** `GET /phong-ban/nhom-theo-tag?tag=KinhDoanh`. Lọc ở backend
chứ không để frontend tự lọc cây — "phòng nào xuất hiện ở module nào" là quy tắc nghiệp vụ, để
frontend lọc thì mỗi màn lọc một kiểu và màn mới sẽ quên lọc.

Endpoint đó gác bằng **`DoanhThu.Xem`**, không phải `PhongBan.Xem`: nhóm "Nhân sự & Kế toán" xem
được doanh thu mà không có quyền HRM (đo trên W686AE9) — gác bằng quyền HRM là bộ lọc của họ
rỗng trắng, không lỗi nào hiện ra. `[RequirePermission]` chỉ nhận một quyền, không có OR.

**Biểu đồ Thống kê gom phần còn lại vào "Khác", KHÔNG ẩn.** Đơn của người thuộc phòng không tag
(hoặc tag khác) vẫn được tính, gộp thành một mục. Ẩn đi thì tổng biểu đồ nhỏ hơn ô "tổng doanh
thu" ngay trên cùng màn, và người đọc không biết tiền đi đâu.

**Lọc bằng id phòng không tag vẫn chạy**, không trả lỗi: tag quyết định phòng nào *hiện trong ô
chọn*, không chặn truy vấn — link và bookmark cũ phải tiếp tục dùng được.

Migration **không đoán tag** cho phòng đã có (quy tắc #1): mọi phòng mặc định `null`, chủ trung
tâm tự đánh trên màn Cơ cấu tổ chức. Canh bởi `TagVaiTroPhongBanTests` (6 test, 4 đột biến đã
kiểm đỏ).

### Quy tắc

- **Cây, không phẳng**: `phong_ban_cha_id` tự tham chiếu. Không giới hạn cấp ở schema; thực tế
  2-4 cấp.
- **Chống chu trình**: handler phải kiểm khi đổi cha — gán một phòng làm con của chính hậu duệ
  nó sẽ tạo vòng lặp, và mọi truy vấn đệ quy sau đó treo. Không ép được bằng constraint (cần
  recursive CTE), nên **phải có test**.
- **Một người MỘT phòng ban** (chốt 09/09/2026): cột `phong_ban_id` trên **`NGUOI_DUNG`**, không
  bảng trung gian. Đủ cho trung tâm ngoại ngữ, và sĩ số phòng đếm không bị trùng người. Cần
  nhiều phòng thì thêm bảng trung gian sau — không phá cấu trúc hiện có.
- **Cột nằm trên `NGUOI_DUNG`, KHÔNG trên `HO_SO_NHAN_VIEN`** — chốt 09/09/2026: *giáo viên cũng
  là nhân viên*, nên **mọi vai trò nhân sự** (nhân viên · giáo viên · trợ giảng) xếp được vào
  phòng ban. Ba lý do, kiểm bằng dữ liệu thật:
  1. `HO_SO_NHAN_VIEN` là hồ sơ của **riêng** vai trò `NhanVien`; giáo viên dùng
     `HO_SO_GIAO_VIEN`. Để cột ở đó là buộc form gửi hai khối hồ sơ cùng lúc — `GhiHoSo` đã ghi
     rõ làm vậy sẽ ghi rỗng đè lên hồ sơ vai trò còn lại (quy tắc #1).
  2. Hồ sơ vai trò **sống lâu hơn vai trò**: đổi vai trò không xoá hồ sơ cũ, nên DB thật đã có
     một trợ giảng còn giữ `HO_SO_NHAN_VIEN` từ hồi làm nhân viên. Phòng ban nằm ở đó thì không
     trả lời được "phòng ban HIỆN TẠI của người này".
  3. Backend **không** chặn giáo viên có `HO_SO_NHAN_VIEN` (handler nhận nếu client gửi DTO) —
     nên đây là lựa chọn thiết kế, không phải giới hạn kỹ thuật.

  Canh bởi `PhongBanTests.Moi_vai_tro_nhan_su_deu_xep_duoc_vao_phong_ban` (3 vai trò).
- **Học viên KHÔNG vào cơ cấu** (`HOC_VIEN_KHONG_VAO_CO_CAU`) — họ là khách. Chặn ở **cả hai
  đường vào** (form hồ sơ và xếp từ cây) vì chặn một phía sẽ để lọt, và sĩ số phòng cũng lọc
  học viên ở tầng đọc. Đổi vai trò sang học viên thì **tự rời** cơ cấu.
- **Quy tắc #1 với `Guid?`**: `phong_ban_id` không có hai giá trị trống để phân biệt "không
  gửi" với "gỡ ra", nên lệnh cập nhật có cờ **`DoiPhongBan`** riêng. Thiếu cờ này thì mọi form
  không có ô phòng ban sẽ âm thầm gỡ người khỏi cơ cấu mỗi lần lưu — đúng lỗi 16/08 với ô địa
  chỉ. Chuỗi thì dùng được quy ước `null` vs `''` như `AnhDaiDienUrl`, `Guid?` thì không.
- **`PHONG_BAN` thay cột chuỗi `HO_SO_NHAN_VIEN.phong_ban`** hiện tại. Chuỗi tự do thì "Phòng
  Đào tạo" và "phòng đào tạo" là hai phòng khác nhau, và không cây nào dựng được từ đó.
  Cột cũ **NULL cả 34 hàng** (chưa ai dùng) nên bỏ đi không mất dữ liệu — khác `chuc_vu` đang
  có 30 hàng dữ liệu thật, xem FR-24.
- **Xoá phòng ban còn người → chặn** (`PHONG_BAN_CON_NGUOI`). Xoá phòng còn phòng con → chặn
  (`PHONG_BAN_CON_CAP_DUOI`). Cả hai là Restrict ở tầng DB.
- **Người quản lý là THÔNG TIN, không phải quyền.** Cột `nguoi_quan_ly_id` chỉ để hiển thị và
  liên hệ. Muốn "trưởng phòng xem được hồ sơ phòng mình" thì đó là **tầng phạm vi mới** (như
  `IPhamViLopHoc`), phải làm có ý thức — tuyệt đối không suy ngầm quyền từ cột này.
- `UNIQUE(tenant_id, phong_ban_cha_id, ten)` — trùng tên trong **cùng một cha** thì người dùng
  chọn sai. Khác cha thì cho trùng: "Bộ môn Anh" dưới hai chi nhánh là hợp lệ.

### Hai cách thêm nhân sự vào phòng ban

Cả hai cùng ghi một cột `NGUOI_DUNG.phong_ban_id`:

| Cách | Vào từ đâu | Dùng khi |
|---|---|---|
| 1 | Form tạo/sửa hồ sơ → chọn phòng ban | Tuyển người mới, biết trước họ vào phòng nào |
| 2 | Cây cơ cấu → node phòng ban → *Thêm nhân sự* → chọn người đã có | Sắp xếp lại tổ chức, hoặc lấp phòng mới lập |

Màn cây ở `/hrm/co-cau`, dùng `@headless-tree` (MIT, ~13.5 KB gzip, 0 dependency): thu/mở nhánh,
sĩ số riêng/cả nhánh, menu thao tác mỗi dòng. Chọn thư viện *headless* để dùng lại `MenuThaoTac`
và `Badge` sẵn có, và để có sẵn điều hướng bàn phím + ARIA `tree`/`treeitem`.

> **Bẫy của thư viện** (mất thời gian nhất khi làm): `useTree` gọi `createTree` **đúng một lần**,
> nên cấu trúc cây bị cache — dữ liệu về sau phải gọi `rebuildTree()` mới hiện. Và mở nhánh phải
> qua `item.expand()`, **không** phải `setState({ expandedItems })`: bản kia đổi state mà
> `getItems()` vẫn không trả con. Đã đo bằng script độc lập chứ không đoán.

## FR-23 — Hồ sơ nhân sự (mở rộng)

Bổ sung vào `NGUOI_DUNG`: `cccd`, `so_tai_khoan`, `ten_ngan_hang`, `ghi_chu`.
Bảng mới `LIEN_KET_MXH` (nhiều dòng mỗi người). Tệp: **thêm `nguoi_dung_id` vào `TEP_DINH_KEM`**
đã có, không tạo bảng mới.

Hiện cho **mọi vai trò nhân sự** — CCCD và số tài khoản là thứ trung tâm cần cho hợp đồng và trả
lương, áp cho cả giáo viên. Học viên không có các trường này.

### Quy tắc

- **`cccd` KHÔNG unique.** Dữ liệu nhập tay thường thiếu; ép duy nhất sẽ chặn lưu hồ sơ chỉ vì
  hai người cùng để trống, và một người có thể đổi CCCD (12 số thay 9 số). Trùng CCCD là việc
  **cảnh báo ở UI**, không chặn ở DB. Canh bởi `HoSoNhanSuTests.Hai_nguoi_trung_CCCD_van_luu_duoc`.
- **`LIEN_KET_MXH` là bảng riêng**, không phải vài cột `facebook`/`zalo` trên `NGUOI_DUNG`: thêm
  một mạng là thêm một cột + một migration, và ai chỉ dùng Zalo thì mọi cột khác NULL. Cũng
  không dùng `jsonb` — mảng không mang `tenant_id` nên nằm ngoài Global Query Filter (quy tắc #2).
- **KHÔNG unique theo `(nguoi_dung_id, loai)`**: một người có hai Facebook (cá nhân và công việc)
  là chuyện thật — đó là lý do có cột `ghi_chu` để phân biệt.
- **`duong_dan` không validate là URL**: Zalo thường là số điện thoại. UI chỉ mở tab mới khi giá
  trị bắt đầu bằng `http(s)://`, còn lại hiện dạng chữ để không tạo link hỏng.
- **Quy tắc #1 với `List`**: `lienKetMxhs = null` (không gửi) → **GIỮ NGUYÊN**; danh sách (kể cả
  rỗng) → **THAY THẾ toàn bộ**. Khác `ChucVuId`/`PhongBanId` (cần cờ `DoiChucVu`/`DoiPhongBan`)
  vì `List` có **hai** giá trị trống phân biệt được (`null` vs `[]`), còn `Guid?` chỉ có một.

  > **Bẫy đã gặp 10/09/2026**: handler cập nhật thiếu `.Include(u => u.LienKetMxhs)` nên
  > `RemoveRange(nd.LienKetMxhs)` chạy trên collection rỗng — gửi danh sách rỗng **không** xoá
  > được liên kết cũ, và gửi danh sách mới thì **cộng thêm** thay vì thay thế. Hai test bắt được;
  > đã kiểm bằng đột biến (bỏ `Include` → đúng 2 test đỏ).

### Tệp hồ sơ

- **`TEP_DINH_KEM` thêm cột FK thứ sáu** `nguoi_dung_id`. Thêm cột mới **phải sửa cả `CHECK`**
  `ck_tep_dinh_kem_dung_mot_chu` — constraint đếm "đúng một cột khác null", nên quên là mọi tệp
  hồ sơ bị chặn ở tầng DB (lỗi lộ lúc chạy, không lúc biên dịch). Đã thử ba chiều trên
  PostgreSQL: chỉ `nguoi_dung_id` → vào được; không cột nào → chặn; hai cột → chặn.
- **Handler RIÊNG, không thêm nhánh vào `TaiTepCommand`** của học liệu: lệnh kia nằm ở
  `Application/DaoTao/HocLieu` và phụ thuộc `IPhamViLopHoc` ("lớp mình dạy") — không liên quan
  hồ sơ nhân sự. Thêm nhánh là đặt logic HRM trong handler LMS và `RanhGioiHeThongConTests` sẽ
  đỏ (ADR-0005). Vẫn dùng chung `ILuuTruTep` + bảng `TEP_DINH_KEM`: hạn mức dung lượng là một
  và job dọn tệp mồ côi chỉ phải quét một bảng — nhưng **whitelist định dạng thì KHÔNG dùng
  chung** (xem mục dưới).
- Endpoint xoá tệp HRM lọc `NguoiDungId != null` — chặn việc dùng nó để xoá tệp của bài tập hay
  tài liệu. Canh bởi `HoSoNhanSuTests.Endpoint_HRM_khong_xoa_duoc_tep_cua_hoc_lieu`.
- **Xoá hàng DB trước, xoá tệp sau**: kho lỗi thì còn tệp mồ côi (job dọn rác lo — nợ N5); làm
  ngược lại mà DB lỗi thì hàng còn trỏ tới tệp đã mất và UI hiện một tệp tải về không được.
- Tải tệp làm ở **view chi tiết**, không trong modal sửa: tệp là thao tác từng cái một, ghép vào
  form sẽ phải giữ tệp trong bộ nhớ tới lúc bấm Lưu.

### Xem online + giới hạn định dạng và dung lượng (10/09/2026)

Yêu cầu: *"cho phép xem tệp online, giới hạn định dạng file và kích thước cho phép (pdf, word,
excel)"*.

| Hạng mục | Chốt |
|---|---|
| Định dạng | **PDF · Word (doc/docx) · Excel (xls/xlsx)** — 5 kiểu MIME |
| Dung lượng | **20 MB** mỗi tệp (giữ hạn mức chung của `ILuuTruTep`) |
| Xem online | PDF xem trong modal; Word/Excel **chỉ tải về** |

- **Whitelist HRM hẹp hơn kho lưu trữ, và đặt ở tầng Application** (`LoaiTepHoSo.ChoPhep`), KHÔNG
  sửa `MinioLuuTruTep`. Kho dùng chung với học liệu LMS, mà lớp ngoại ngữ cần file nghe `mp3`,
  ảnh chụp bài nộp và slide `pptx` — siết danh sách chung xuống 5 kiểu sẽ **hỏng nghiệp vụ LMS
  đang chạy** để thoả một yêu cầu của HRM. Hai tầng: HRM (hẹp, theo nghiệp vụ) + kho (rộng, chặn
  SVG/HTML gây XSS và giữ hạn mức 20 MB).
- **Thứ tự kiểm quan trọng**: whitelist chạy **trước** khi stream đi vào kho, nên tệp sai loại
  không bao giờ nằm trong MinIO dù chỉ một lúc. Đảo hai bước vẫn trả 400 đúng và mọi test khác
  vẫn xanh, chỉ để lại tệp mồ côi mỗi lần người dùng chọn sai — canh bởi
  `HoSoNhanSuTests.Tep_sai_loai_khong_de_lai_rac_trong_kho`.
- **Mã lỗi riêng `LOAI_TEP_HO_SO_KHONG_HO_TRO`**, không dùng lại `LOAI_TEP_KHONG_HO_TRO` của kho:
  hai thông điệp liệt kê hai danh sách khác nhau, dùng chung thì người dùng HRM đọc được "chấp
  nhận cả ảnh và file nén" rồi thử và bị từ chối.
- **`accept` ở `<input type=file>` là tiện lợi, KHÔNG phải bảo mật** — người dùng đổi được sang
  "All files". Chốt thật ở handler; test backend canh tầng thật. Client cũng kiểm dung lượng
  trước để không bắt người dùng chờ tải xong 20 MB rồi mới bị từ chối.
  - `File.type` **rỗng** với vài tệp Office cũ trên Windows → lúc đó tin phần mở rộng và để
    backend phán quyết, thay vì chặn oan một `.doc` hợp lệ.
- **Endpoint xem gác bằng `NhanSu.Xem`**, không `Sua`: đọc hợp đồng không phải hành vi sửa hồ sơ.
  Lọc `NguoiDungId != null` — cùng lý do như lệnh xoá, quyền `NhanSu.Xem` không có nghĩa "được
  đọc mọi hàng `TEP_DINH_KEM`". Canh bởi `Endpoint_HRM_khong_xem_duoc_tep_cua_hoc_lieu`.
- **`?taiVe=true` đổi `Content-Disposition`** từ `inline` sang `attachment`. Một endpoint hai chế
  độ chứ không hai route: cùng phép kiểm quyền và cùng truy vấn, tách ra chỉ nhân đôi chỗ có thể
  quên gác.
- **Trả `inline` an toàn được ở đây là NHỜ whitelist hẹp**: `inline` cho tệp người dùng tải lên là
  đường XSS lưu trữ kinh điển nếu loại tệp có thể là SVG/HTML. Danh sách chỉ có PDF/Word/Excel nên
  không nhánh nào chạy script; thêm `X-Content-Type-Options: nosniff` chặn trình duyệt đoán lại
  kiểu. **Nới whitelist này về sau phải xem lại chỗ đó.**
- **Frontend tải blob rồi `createObjectURL`**, không trỏ `<iframe src>` thẳng vào API: endpoint cần
  header `Authorization` mà `<a href>`/`<iframe src>` không gửi được (cùng khuôn `Anh.tsx`,
  `ChonTep.tsx`). `revokeObjectURL` gọi **lúc đóng modal**, không ngay sau khi gán — thu hồi sớm
  thì iframe mất nguồn và hiện khung trắng.
- **Word/Excel không có nút "Xem"**: không trình duyệt nào render chúng, trả `inline` cũng chỉ dẫn
  tới hộp thoại tải về. Cho bấm Xem thì modal mở ra trắng trơn — tệ hơn là không có nút. Nhúng qua
  Microsoft/Google viewer đã **bị loại**: phải public URL tệp ra Internet cho bên thứ ba đọc, vi
  phạm quy tắc #6 và làm lộ hợp đồng, CCCD nhân viên.
- Thanh công cụ của bộ đọc PDF hiện **GUID của blob** chứ không phải tên tệp. Thử thêm
  `#tên-tệp` vào URL blob — **không có tác dụng**, Chrome lấy tiêu đề từ chính blob. Đã bỏ, tên
  gốc hiện ở **tiêu đề modal** ngay phía trên khung xem; không đáng kéo `pdf.js` (~300 KB) chỉ để
  sửa một dòng chữ.

## FR-24 — Danh mục chức vụ

Bảng `CHUC_VU` do admin tự quản: `ten`, `mo_ta`, `thu_tu`, `dang_dung`. Thay cột chuỗi
`HO_SO_NHAN_VIEN.chuc_vu`.

Chủ sản phẩm đề xuất thêm "ban quản lý" vào danh sách vai trò (09/09/2026). Làm ở đây, **không**
thêm vào `LoaiNguoiDung` — xem [Ba tầng, đừng gộp](#ba-tầng-đừng-gộp) ở đầu tài liệu.

### Quy tắc

- **Áp cho MỌI vai trò nhân sự** (nhân viên · giáo viên · trợ giảng): cột `chuc_vu_id` nằm trên
  `NGUOI_DUNG`, cùng lý do với `phong_ban_id` — giáo viên cũng làm trưởng bộ môn. Học viên
  **không** có chức vụ. Canh bởi `ChucVuTests.Moi_vai_tro_nhan_su_gan_duoc_chuc_vu` (3 vai trò).
- `UNIQUE(tenant_id, ten)` (quy tắc #8) — trùng tên thì người dùng chọn sai.
- **Ngừng dùng thay vì xoá**: xoá chức vụ đang có người giữ bị chặn
  (`CHUC_VU_CON_NGUOI_GIU`); bỏ tích `dang_dung` thì nó biến khỏi form chọn nhưng **giữ nguyên**
  ở hồ sơ đã gán. Cùng cơ chế `dang_ban` của `KHOA_HOC` — "từng là trưởng phòng" là sự thật
  không nên xoá.
- **Chức vụ KHÔNG cấp quyền gì** — quyền vẫn đọc từ `QUYEN_CHUC_NANG` (quy tắc #9). Màn Chức vụ
  nói rõ điều này ngay đầu trang để không ai hiểu sai.
- **Quy tắc #1 với `Guid?`**: lệnh cập nhật có cờ **`DoiChucVu`** riêng, cùng lý do với
  `DoiPhongBan` — `Guid?` chỉ có một giá trị trống nên không phân biệt được "không gửi" với
  "bỏ chức vụ".
- Seeder dựng sẵn 5 chức vụ cho tenant mới: **Ban quản lý**, Quản trị hệ thống, Trưởng phòng,
  Nhân viên kinh doanh, Kế toán.

### Chuyển đổi dữ liệu cũ

Cột `HO_SO_NHAN_VIEN.chuc_vu` (chuỗi) có **dữ liệu thật** — 40 hàng, hai giá trị. Migration
`ThemChucVuVaDoiTenChucNang` **sinh danh mục từ chính các giá trị đang có** theo từng tenant, nối
người vào danh mục, **rồi mới** xoá cột. EF sinh `DropColumn` ngay đầu `Up()`; đã chuyển xuống
sau khối SQL bằng tay — để nguyên là mất toàn bộ 40 hàng.

## Bỏ hai màn riêng (09/09/2026)

"Nhân viên kinh doanh" và "Giáo viên (góc nhìn nhân sự)" từng là hai mục menu riêng, cả hai chỉ
là khung trống. Bỏ theo yêu cầu chủ sản phẩm — hồ sơ của cả ba vai trò đã nằm ở màn
**Hồ sơ nhân sự**.

Kéo theo hai đổi tên trong danh mục phân quyền:

| Cũ | Mới | Vì sao |
|---|---|---|
| `GiaoVienNhanSu` | `NhanSu` | Nó **luôn** gác cả ba vai trò nhân sự, tên cũ gây hiểu sai là chỉ dành cho giáo viên |
| `NhanVienKinhDoanh` | `ChucVu` | Không còn màn nào dùng; quyền đã cấp chuyển sang danh mục chức vụ để nhóm quyền cũ không mất tác dụng |

Migration đổi cả **dữ liệu** trong `QUYEN_CHUC_NANG`, không chỉ code. Nhóm quyền nào từng có
**cả hai** thì bỏ trùng trước khi đổi, nếu không sẽ đụng `UNIQUE(quyen_id, ten_chuc_nang,
hanh_dong)` — đo thật: 42/43 nhóm có cả hai, nên 170 hàng bị bỏ và không nhóm nào mất quyền nhân
sự.

## View chi tiết hồ sơ nhân sự

Bấm một dòng ở màn Hồ sơ nhân sự mở `/hrm/nhan-su/{id}` (09/09/2026). Chỉ **đọc**, sửa qua modal
ở màn danh sách — cùng quy ước với chi tiết khách hàng và lớp học
([ui-ux-nguyen-tac.md](../frontend/ui-ux-nguyen-tac.md)).

`GET /nhan-su/{id}` dùng lại `LayDanhSachNguoiDungQuery` với phạm vi ba vai trò nhân sự thay vì
viết query riêng: nhờ đó gõ id **học viên** vào URL nhận 404, không phải hồ sơ học viên. Canh bởi
`ChucVuTests.Chi_tiet_nhan_su_khong_tra_ve_hoc_vien`.

## Mã lỗi

| Mã | Khi nào |
|---|---|
| `PHONG_BAN_TRUNG_TEN` | Trùng tên trong cùng phòng ban cha |
| `PHONG_BAN_CON_NGUOI` | Xoá phòng ban còn nhân sự |
| `PHONG_BAN_CON_CAP_DUOI` | Xoá phòng ban còn phòng con |
| `PHONG_BAN_CHU_TRINH` | Gán cha là chính nó hoặc hậu duệ của nó |
| `NGUOI_QUAN_LY_KHONG_HOP_LE` | Người quản lý không tồn tại, đã nghỉ, hoặc là học viên |
| `CHUC_VU_TRUNG_TEN` | Trùng tên chức vụ trong cùng trung tâm |
| `CHUC_VU_CON_NGUOI_GIU` | Xoá chức vụ đang có người giữ — bỏ tích "Còn dùng" thay vì xoá |
| `CHUC_VU_KHONG_HOP_LE` | Gán chức vụ không tồn tại (hoặc của tenant khác) |
| `PHONG_BAN_KHONG_HOP_LE` | Phòng ban cha (hoặc phòng đích khi xếp người) không tồn tại |
| `NHAN_SU_KHONG_HOP_LE` | Có id người không tồn tại hoặc thuộc tenant khác |
| `HOC_VIEN_KHONG_VAO_CO_CAU` | Xếp học viên vào phòng ban — họ là khách, không phải nhân sự |
| `CHUA_CHON_NHAN_SU` | Bấm xếp mà chưa chọn ai |

## Chưa làm

- **Phạm vi theo phòng ban** (trưởng phòng chỉ thấy người phòng mình) — cần tầng
  `IPhamViPhongBan` riêng, xem quy tắc trên.
- Một người nhiều phòng ban.
- Lịch sử điều chuyển phòng ban (ai chuyển từ đâu sang đâu, lúc nào).
- Hợp đồng, lương, ngày phép — chưa trong phạm vi.
