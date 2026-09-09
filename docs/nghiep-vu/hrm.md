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

Bổ sung vào `NGUOI_DUNG`: `cccd`, `so_tai_khoan`, `ten_ngan_hang`.
Bảng mới `LIEN_KET_MXH` (nhiều dòng mỗi người): `loai`, `duong_dan`.
Tệp đính kèm: **thêm `nguoi_dung_id` vào `TEP_DINH_KEM`** đã có, không tạo bảng mới.

Chi tiết viết khi làm đợt 2.

## FR-24 — Danh mục chức vụ

Bảng `CHUC_VU` do admin tự quản, thay cột chuỗi `HO_SO_NHAN_VIEN.chuc_vu`.

⚠️ **Cột cũ đang có 30 hàng dữ liệu thật** ("Quản trị hệ thống", "Trưởng phòng nhân sự"…). Khác
`phong_ban` (NULL cả 34 hàng, bỏ được tự do) — ở đây migration phải **sinh danh mục từ các giá
trị đang có** rồi nối lại, không được xoá cột trước khi chuyển (quy tắc #1).

Chi tiết viết khi làm đợt 3.

## Mã lỗi

| Mã | Khi nào |
|---|---|
| `PHONG_BAN_TRUNG_TEN` | Trùng tên trong cùng phòng ban cha |
| `PHONG_BAN_CON_NGUOI` | Xoá phòng ban còn nhân sự |
| `PHONG_BAN_CON_CAP_DUOI` | Xoá phòng ban còn phòng con |
| `PHONG_BAN_CHU_TRINH` | Gán cha là chính nó hoặc hậu duệ của nó |
| `NGUOI_QUAN_LY_KHONG_HOP_LE` | Người quản lý không tồn tại, đã nghỉ, hoặc là học viên |
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
