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

Màn cây ở `/hrm?tab=so-do` (đường cũ `/hrm/co-cau` vẫn chuyển hướng tới đây), dùng `@headless-tree` (MIT, ~13.5 KB gzip, 0 dependency): thu/mở nhánh,
sĩ số riêng/cả nhánh, menu thao tác mỗi dòng. Chọn thư viện *headless* để dùng lại `MenuThaoTac`
và `Badge` sẵn có, và để có sẵn điều hướng bàn phím + ARIA `tree`/`treeitem`.

> **Bẫy của thư viện** (mất thời gian nhất khi làm): `useTree` gọi `createTree` **đúng một lần**,
> nên cấu trúc cây bị cache — dữ liệu về sau phải gọi `rebuildTree()` mới hiện. Và mở nhánh phải
> qua `item.expand()`, **không** phải `setState({ expandedItems })`: bản kia đổi state mà
> `getItems()` vẫn không trả con. Đã đo bằng script độc lập chứ không đoán.

## Vai trò Nhân viên kinh doanh (16/09/2026)

Yêu cầu: *"vai trò nhân viên => nhân viên kinh doanh. tránh nhầm lẫn"*.

**Làm thành vai trò RIÊNG, không đổi tên `NhanVien`.** Rà dữ liệu thật trước khi sửa: trong 6
người mang vai trò `NhanVien` chỉ 4 là sale, còn lại là **nhân sự** và **quản trị hệ thống**. Thêm
nữa `NhanVien` là giá trị mặc định của `NguoiDung` **và** là vai trò mà `TenantSeeder` gán cho tài
khoản quản trị — đổi nhãn nó sẽ gọi chính người quản trị là nhân viên kinh doanh ở **mọi trung tâm
mới**, tức tạo ra một nhầm lẫn mới thay vì bỏ nhầm lẫn cũ.

| Giá trị | Nhãn | Nghĩa |
|---|---|---|
| `NhanVien = 0` | **Nhân viên khác** | Không dạy, không học, không thuộc kinh doanh: hành chính, nhân sự, kế toán, IT. Mặc định của `NguoiDung` + vai trò của admin |
| `NhanVienKinhDoanh = 4` | **Nhân viên kinh doanh** | Sale / tư vấn tuyển sinh |

Nhãn của `NhanVien` đổi thành "Nhân viên **khác**": để trần "Nhân viên" thì hai tùy chọn đọc như
lồng nhau và người dùng phải đoán chọn cái nào cho sale — đúng sự nhầm lẫn mà vai trò mới sinh ra
để bỏ.

**Giá trị 4, không chen vào giữa**: DB lưu `int`, đổi số của giá trị đang có là làm sai toàn bộ dữ
liệu cũ một cách im lặng (quy tắc #1). Canh bởi
`VaiTroNhanVienKinhDoanhTests.Gia_tri_so_cua_vai_tro_khong_duoc_doi` — chốt cả 5 giá trị.

### Chỗ dễ sai nhất khi thêm vai trò nhân sự

Phải khai vào **`NhanSuController.VaiTroNhanSu`** (phạm vi cố định của màn HRM). Thiếu chỗ đó thì
**tạo người vẫn thành công (200)** nhưng danh sách không hiện ra và `/nhan-su/{id}` trả 404 — không
ngoại lệ nào ném, không test cũ nào đỏ. Đột biến bỏ vai trò mới khỏi mảng này làm đỏ **5** test.

Hai vai trò **dùng chung `HO_SO_NHAN_VIEN`** (`LaNhanVienVanHanh`): khác nhau ở nghiệp vụ, không
khác ở trường hồ sơ. Bảng đó nay chỉ còn FK + `tenant_id` nên **không xuất hiện trong DTO nào** ⇒
phải canh ở **tầng DB**, test qua API không thấy được (đã thử: đột biến sống).

Về phân quyền thì hai vai trò như nhau. Khác biệt nằm ở **lọc và thống kê**: giờ trả lời được "xem
doanh thu theo từng nhân viên kinh doanh" mà không phải suy từ phòng ban. Bộ lọc CRM
(`LocDoiNhom.tsx`) tự nhận vai trò mới vì nó liệt kê *mọi vai trò không phải học viên* — cố ý,
vì giáo vụ cũng tạo được hồ sơ khách.

### Chuyển dữ liệu đang có

`scripts/chuyen-vai-tro-nhan-vien-kinh-doanh.sql` — **không** làm trong EF migration: ai là nhân
viên kinh doanh là quyết định nghiệp vụ của từng trung tâm, migration đoán hộ sẽ gán sai cho mọi
tenant khác.

Nhận diện theo **tên** (`Sale%`), không theo phòng ban có tag Kinh doanh: chủ sản phẩm chốt sau khi
thấy "Sale Online B" đang nằm ở phòng "Đào tạo" — tức phòng ban của người này mới là thứ đặt sai.
Script có hai chốt an toàn (phải còn ít nhất một `NhanVien`; không hồ sơ học viên nào lọt sang) và
đã dry-run trên DB bản sao trước khi chạy thật: `UPDATE 4`, đúng 4 người.

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

### Đặt tên tệp + hạn mức 10 tệp (16/09/2026)

Yêu cầu: *"khi tải tệp hồ sơ nhân sự lên, cho phép đặt tên file để dễ theo dõi, giới hạn tổng số
lượng file là 10"*.

| Hạng mục | Chốt |
|---|---|
| Đặt tên | Lúc **tải lên** (hộp thoại trước khi gửi) **và** đổi tên tệp đã có (`PUT /nhan-su/tep/{id}/ten`) |
| Hạn mức | **10 tệp mỗi hồ sơ nhân sự** — không phải toàn trung tâm |

**Đuôi tệp luôn lấy từ tệp thật, không từ tên người dùng gõ.** Người dùng gõ "Hợp đồng lao động
2026" chứ không gõ `.pdf`; và nếu lấy đuôi họ gõ thì một PDF tải về thành `.exe`/`.html` —
`Content-Disposition` mang tên đó, là đường lừa người dùng từ một hệ thống nội bộ. Canh bởi
`Ten_nguoi_dung_go_khong_doi_duoc_duoi_that` (2 ca).

**Đổi tên chỉ đụng cột `TEN_GOC`** — khoá lưu trữ và object trong MinIO giữ nguyên. Đổi cả khoá
thì mọi tệp đang có phải di chuyển trong kho: việc rủi ro, không đảo lại được, cho một thứ thuần
hiển thị. Canh bởi `Doi_ten_khong_lam_mat_noi_dung_tep` (quy tắc #1).

**Hạn mức theo TỪNG hồ sơ**, và đếm *tệp hiện có* chứ không *số lần đã tải*: xoá một tệp thì mở
lại một chỗ, nếu không hồ sơ dùng lâu sẽ khoá cứng dù đang trống. Hai điều này có test riêng vì
cả hai đều sai được mà không ai thấy ngay.

> **Không nâng hạn mức lên ràng buộc DB.** Quy tắc #8 nói về "chỉ một" (UNIQUE giải quyết được),
> còn "nhiều nhất N" thì PostgreSQL không có ràng buộc khai báo tương đương — phải dùng trigger.
> Hai request song song đều thấy 9 thì hồ sơ thành 11 tệp; chấp nhận được vì hậu quả là **thừa
> một tệp**, không mất dữ liệu, và người dùng xoá bớt được.

**Giới hạn của model binder, đã đo không đoán**: MVC đổi chuỗi rỗng và chuỗi toàn dấu cách của
`[FromForm] string?` thành `null`, nên lệnh **tải lên** không phân biệt được "gửi tên vô dụng" với
"không gửi trường này" (client cũ) ⇒ quay về tên gốc của tệp. Lệnh **đổi tên** đi qua JSON nên
nhận được `"   "` nguyên vẹn và **có** báo `TEN_TEP_KHONG_HOP_LE` — ở đó im lặng bỏ qua mới là
tệ, vì người dùng bấm Lưu, hộp thoại đóng, tên không đổi và không có gì giải thích.

UI: nút *Tải tệp lên* **bị gỡ hẳn `<input>`** khi đủ 10 tệp, không chỉ làm mờ — `<label>` bọc
input không có thuộc tính `disabled`, để nguyên thì vẫn bấm chọn được tệp rồi mới nhận lỗi từ
server. Dòng dưới luôn hiện `đã dùng n/10 tệp`.

## FR-29 — Thống kê nhân sự + tiêu chí đánh giá (16/09/2026)

Yêu cầu chủ sản phẩm: *"thêm phần thống kê, tương tự thống kê tại CRM nhưng chỉ cho nhân viên
kinh doanh, giáo viên và trợ giảng"*, kèm bộ chỉ số cho từng vai trò và *"thêm các tiêu chí để
học viên chấm theo thang 5 thay vì chỉ nhận xét"*.

### Ba bảng xếp hạng (`/hrm/thong-ke`)

| Vai trò | Chỉ số | Nguồn dữ liệu |
|---|---|---|
| Nhân viên kinh doanh | doanh thu · số học viên · chất lượng chăm sóc | CRM (`DANG_KY_KHOA_HOC`, `KHACH_HANG`) + phiếu đánh giá |
| Giáo viên | số lớp · số buổi dạy đủ · chất lượng giảng dạy | LMS (`LOP_HOC`, `BUOI_HOC`, `NHAN_XET_BUOI_HOC`) |
| Trợ giảng | như giáo viên | `LOP_HOC_TRO_GIANG` + buổi của lớp đó |

`NhanVien` (hành chính, nhân sự, IT) **không có bảng** — chủ sản phẩm chốt đúng ba vai trò.

### Ba chỗ tính sai mà không có gì báo

1. **`BUOI_HOC.giao_vien_id = null` nghĩa là *giáo viên chính của lớp***, không phải "không có
   giáo viên". Đếm thẳng cột đó thì **mọi giáo viên ra 0 buổi** — trên dữ liệu thật W686AE9 cả
   12/12 buổi đều null. Phải rơi về `LOP_HOC.giao_vien_chinh_id`. Đột biến bỏ fallback này làm đỏ
   3 test.
2. **Chỉ buổi `DaHoanThanh` là "dạy đủ"** — buổi mới lên lịch chưa phải công.
3. **Doanh thu quy cho người TẠO HỒ SƠ KHÁCH** (`KHACH_HANG.CreatedById`), đúng cách của FR-28.
   Lấy mốc khác thì cùng một người ra hai con số ở hai màn.

Thêm một quy ước hiển thị: **chưa ai chấm thì trả `null`, không phải `0`** — "chưa có đánh giá"
khác "bị 0 điểm", mà trên bảng xếp hạng hai thứ đó dẫn tới hai kết luận trái ngược về một người.

### Module tiêu chí đánh giá (`/hrm/tieu-chi-danh-gia`)

`TIEU_CHI_DANH_GIA` — danh mục **do trung tâm tự cấu hình**, chia hai nhóm (`NhomTieuChi`):

| Nhóm | Ai chấm | Ở đâu |
|---|---|---|
| `KinhDoanh` | **quản lý** | tab Thống kê nhân sự, theo kỳ `yyyy-MM` |
| `GiangDay` | **học viên** | ô nhận xét trong từng buổi học |

Người chấm nhân viên kinh doanh là **quản lý**, chốt sau khi cân nhắc hai phương án khác: khách
hàng chấm (nhưng chính nhân viên ghi hộ ⇒ tự chấm mình) và học viên chấm (nhưng 63/65 học viên
chưa có tài khoản ⇒ gần như không có phiếu).

Điểm lưu ở `DIEM_TIEU_CHI` — bảng riêng, không phải các cột `diem_1`, `diem_2`…: số tiêu chí do
người dùng quyết định nên không cột nào đủ. Mỗi hàng thuộc **đúng một** phiếu
(`nhan_xet_buoi_hoc_id` hoặc `phieu_danh_gia_nhan_vien_id`), canh bằng `CHECK` cùng khuôn
`TEP_DINH_KEM`. Thang 5 cũng ép bằng `CHECK` ở DB, không chỉ validator.

**Không có endpoint xoá tiêu chí** — xoá tiêu chí đã có điểm sẽ làm mọi kỳ đã chấm đổi số một cách
im lặng (quy tắc #1). Ngừng dùng bằng `DangDung = false`: phiếu mới không hiện nữa, phiếu cũ vẫn
đọc được. Cũng **không đổi nhóm** được nếu đã có điểm — đổi nhóm là đổi ý nghĩa của mọi điểm đã
chấm (điểm "Truyền đạt dễ hiểu" bỗng tính vào xếp hạng kinh doanh).

`MucHaiLong` (1–5, hài lòng chung) **giữ nguyên**, không bị thay thế: nó vẫn là một con số để xếp
hạng nhanh. Thống kê ưu tiên điểm tiêu chí, thiếu thì rơi về `MucHaiLong` — nên dữ liệu cũ không
mất ý nghĩa.

### Hai module RIÊNG, không phải tab của `/hrm` (tách 16/09/2026)

Ban đầu làm thành hai tab thứ tư và thứ năm của `/hrm`; chủ sản phẩm yêu cầu tách riêng ngay sau
đó. Tách là đúng, và lý do quan trọng hơn thẩm mỹ:

**Mỗi module gác bằng quyền riêng.** Trang `/hrm` gác `NhanSu`, nên nhồi hai màn này vào đó thì
trưởng phòng có `ThongKeNhanSu.Xem` mà không có `NhanSu.Xem` sẽ **không thấy mục nào để vào** — và
cũng không hiểu vì sao. Nay mỗi mục sidebar gác đúng quyền của nó.

Ba tab còn lại ở `/hrm` **vẫn gộp**: sơ đồ tổ chức, hồ sơ nhân sự và chức vụ nói về *cùng một tập
người*. Còn hai màn mới khác hẳn — một là báo cáo toàn trung tâm, một là cấu hình danh mục.

| Đường | Màn |
|---|---|
| `/hrm` | ba tab: cơ cấu · hồ sơ · chức vụ |
| `/hrm/thong-ke` | Thống kê nhân sự |
| `/hrm/tieu-chi-danh-gia` | Tiêu chí đánh giá |

Link `?tab=thong-ke` và `?tab=tieu-chi` **cũ vẫn chuyển hướng** sang đường mới — rơi về tab đầu thì
người dùng tưởng tính năng bị xoá. Canh bởi `e2e/thong-ke-nhan-su.spec.ts`.

> **Bẫy của `cuoi: true` khi tách.** Mục sidebar `/hrm` **phải** giữ `cuoi: true`: thiếu nó thì
> `startsWith('/hrm')` khớp luôn `/hrm/thong-ke`, nên mục "Nhân sự" sáng lên và **tiêu đề thanh
> trên hiện sai** khi đang ở hai module mới. Tôi đã bỏ cờ này một nhịp trong lúc tách và phải trả
> lại. Hai module mới cũng `cuoi: true` vì chúng không có route con.

Kéo theo: `TieuChiDanhGia.tsx` **bỏ `<h2>` tên màn** — `Layout` đã hiện tiêu đề ở thanh trên (suy
từ mục sidebar), giữ lại là hiện hai lần cùng một chữ. Đúng quy ước các màn đứng riêng của CRM.

### Cầu nối chéo hệ thống — rộng nhất tới nay

FR-29 là cầu nối HRM → **CRM + LMS**, đã khai vào `CauNoiDuocPhep` và ghi vào
[ADR-0005](../kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md). Bản chất yêu cầu là *"đánh giá
con người bằng kết quả công việc"*, mà công việc nằm ở CRM (bán hàng) và LMS (giảng dạy) — HRM chỉ
giữ hồ sơ con người. Giới hạn tự đặt: **chỉ ĐỌC qua `IAppDbContext`, không gọi handler của hệ
thống khác**, và không đọc cột tiền nào của LMS (chốt 12/09: chỉ CRM nắm tiền).

### Một bug đáng ghi lại

`BuoiHocController.GuiNhanXetBody` là DTO riêng cho thân request (để `id` lấy từ route). Thêm
`DiemTieuChis` vào command mà **quên khai ở DTO đó** thì điểm học viên chấm **rơi âm thầm**:
command nhận `null`, handler chạy đúng theo `null`, không lỗi nào. Chỉ integration test đầu-cuối
bắt được. Đã ghi chú cảnh báo ngay tại DTO.

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

## Một trang ba tab + bấm sĩ số ra danh sách người (16/09/2026)

Chủ sản phẩm nêu hai việc: *"cơ cấu tổ chức đang chưa xem được chi tiết danh sách nhân sự"* và
*"hồ sơ nhân viên, cơ cấu, chức vụ đang bị tách biệt"*.

Ba màn này nói về **cùng một tập người** — `NGUOI_DUNG` mang cả `phong_ban_id` lẫn `chuc_vu_id` —
nhưng trước đây là ba mục sidebar riêng, nên một việc thường ngày ("ai trong phòng Kinh doanh?")
phải đi qua sidebar hai ba lần.

| | Trước | Nay |
|---|---|---|
| Sidebar | 3 mục | **1 mục** `/hrm` |
| Đường cũ | `/hrm/co-cau`, `/hrm/nhan-su`, `/hrm/chuc-vu` | vẫn vào được — chuyển hướng sang `?tab=` tương ứng |
| Từ sĩ số trên cây | không bấm được | bấm là sang tab Nhân sự **đã lọc sẵn phòng đó** |

Tab lưu ở `?tab=` (không phải state) để gửi link được và F5 không mất chỗ; `replace: true` để bấm
qua lại ba tab không sinh ba mục lịch sử. Chuyển tab **giữ nguyên `phongBanId`** — mất nó là người
dùng phải chọn lại phòng bằng tay.

### Bộ lọc mới của `/nhan-su`

| Tham số | Nghĩa |
|---|---|
| `phongBanId` | Chỉ người thuộc **chính** phòng đó |
| `gomPhongBanCon` | Thêm người của **mọi cấp dưới**, không chỉ một cấp |
| `chucVuId` | Lọc theo chức vụ — "cho tôi xem mọi trưởng phòng" |

`gomPhongBanCon` dựng tập id **trong bộ nhớ** (BFS, có chặn số vòng chống dữ liệu lỗi tạo chu
trình), không recursive CTE: EF Core không sinh được CTE mà không viết SQL thô, và SQL thô sẽ **mất
Global Query Filter** của multi-tenant (quy tắc #2) — rủi ro không đáng đổi cho một cây cỡ vài chục
dòng.

### Bẫy: cây đếm khác danh sách đếm

Cây **chỉ đếm người `DangLamViec`**, còn `/nhan-su` mặc định trả cả người đã nghỉ. Nên link từ sĩ số
phải mang theo `trangThaiNhanSu=DangLamViec`, và màn Nhân sự phải **đọc tham số đó từ URL**.

Thiếu một trong hai thì bấm vào số `1` lại ra `2` dòng — hai màn nói hai chuyện về cùng một phòng,
mà **không có ngoại lệ nào ném ra**: người dùng chỉ thấy hai con số khác nhau và không biết tin cái
nào. Cả hai mắt đều đã đứt thật trong lúc làm, và **không** làm đỏ test backend nào — chỉ E2E bắt
được, vì chuỗi này bắc qua ba lớp (link → router → state → tham số API → số dòng).

Canh bởi `LocNhanSuTheoCoCauTests` (4 test) và `e2e/hrm-mot-trang-ba-tab.spec.ts`.

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
