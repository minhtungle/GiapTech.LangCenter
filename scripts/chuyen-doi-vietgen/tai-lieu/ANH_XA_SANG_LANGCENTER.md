# Ánh xạ dữ liệu VIETGEN cũ → GiapTech.LangCenter

> **Nguyên tắc: tuyệt đối tuân theo LangCenter.** Schema, định nghĩa nghiệp vụ, phân quyền và quy tắc của dự án đích là chuẩn. Dữ liệu cũ phải được biến đổi cho khớp. Thứ gì LangCenter **không có chỗ chứa thì không chuyển**. Không sửa code, không thêm bảng, không thêm cột bên LangCenter.
>
> Phạm vi: **tài khoản · phân quyền · đơn hàng & doanh thu**. Chỉ tenant cũ **VIETGEN Academy** (`F4B89D3A…`); tenant LOCALHOST bỏ.
> Căn cứ: `CLAUDE.md` (11 quy tắc), `docs/05-database/erd.md`, `docs/06-nghiep-vu/crm.md`, `thong-ke-crm.md`, `Domain/Common/ChucNang.cs`, `NhomQuyenMacDinh.cs`, `Domain/Entities/*`, cấu hình EF (migration mới nhất `20261005133522_ThemDuoiTenDangNhap`).

---

## 0. Hệ quả của việc theo LangCenter (đã chấp nhận, không bù)

| Hệ thống cũ có | LangCenter | Khi chuyển |
|---|---|---|
| Doanh thu = tiền **đã đóng**, quy cho người tạo thanh toán, theo tháng thanh toán | Doanh thu = **cam kết** `DANG_KY_KHOA_HOC.so_tien × ty_gia_ve_vnd`, quy cho **người tạo khách** `KHACH_HANG.created_by_id`, theo `ngay_dang_ky` (crm.md FR-18, thong-ke-crm.md) | Theo LangCenter. Tổng doanh thu prod sẽ là **15,27 tỷ** (cam kết), không phải 7,80 tỷ (đã đóng). Tiền đã đóng vẫn xem được ở cột "đã thu" |
| `QuyenTruyCap`: mỗi khách chỉ 1–3 user xem được | Không có phạm vi dữ liệu CRM: ai có `KhachHang.Xem` / `DoanhThu.Xem` thấy toàn trung tâm | Không chuyển. Người phụ trách khách giữ qua `created_by_id` |
| KPI tháng (`tbNguoiDung_DoanhThu`, `tbCoCauToChuc_DoanhThu`), cấp độ doanh thu NS-1…NS-5 | Không có bảng | Không chuyển. Lưu file JSON gốc làm lưu trữ ngoài hệ thống |
| Giới tính, cấp độ doanh thu của user | Không có cột | Không chuyển |
| Mật khẩu MD5 | PBKDF2 (ASP.NET Identity), tối thiểu 12 ký tự (`ChinhSachMatKhau`) | Không chuyển hash. Cấp mật khẩu mới, bắt buộc đổi |
| Giai đoạn khách (Đang tư vấn / Chờ xếp lớp / Đang học…) | Trạng thái phễu = `LICH_SU_CHAM_SOC.trang_thai_sau` mới nhất (Moi/DangTuVan/DaMua/TuChoi) | Ngoài phạm vi tài liệu này |

**Quy tắc #1 (không ảnh hưởng dữ liệu hiện có):** nạp vào **một tenant mới, riêng cho VIETGEN**. Script chỉ `INSERT`, không `UPDATE`/`DELETE` hàng có sẵn của tenant khác. Nếu buộc phải nạp vào tenant đang có dữ liệu thì **dừng lại hỏi trước**.

---

## 1. Tài khoản & người dùng

### 1.1 Cấu trúc đích

| Bảng | Vai trò | Ràng buộc |
|---|---|---|
| `NGUOI_DUNG` | Con người | `ho_ten` NOT NULL ≤ 200, `email` ≤ 256, `ghi_chu` ≤ 1000, `so_tai_khoan` ≤ 50 |
| `TAI_KHOAN` | Đăng nhập | `UNIQUE(tenant_id, username)`, `UNIQUE(nguoi_dung_id) WHERE NOT NULL`, `username` ≤ 100 |
| `NGUOIDUNG_QUYEN` | Gán nhóm quyền **cho tài khoản** | `UNIQUE(tai_khoan_id, quyen_id)` |
| `LIEN_KET_MXH` | Link MXH (nhiều dòng) | `loai`: 0 Facebook · 1 Zalo · 2 LinkedIn · 3 Telegram · 4 Khac |
| `PHONG_BAN` | Cây cơ cấu | `UNIQUE(tenant, cha, ten)` + partial `UNIQUE(tenant, ten) WHERE cha IS NULL` |
| `HO_SO_GIAO_VIEN` | Hồ sơ người dạy (1–1) | `UNIQUE(nguoi_dung_id)` |

Đăng nhập = **{mã trung tâm, username, mật khẩu}**. Tenant phải được tạo bằng luồng của LangCenter (chủ hệ thống tạo trung tâm). Luồng này sinh `ma_trung_tam` và 4 nhóm quyền mặc định (Quản trị viên / Giáo viên / Trợ giảng / Học viên).

### 1.2 `tbNguoiDung` → `NGUOI_DUNG` + `TAI_KHOAN` (174 user)

| Cũ | → Đích | Quy tắc |
|---|---|---|
| IdNguoiDung | `NGUOI_DUNG.id` | Giữ GUID, để `created_by_id` của khách/đơn map thẳng |
| TenNguoiDung | `ho_ten` | |
| Email | `email` | |
| SoDienThoai | `so_dien_thoai` | |
| NgaySinh | `ngay_sinh` | `1900-01-01` (19 dòng) → NULL |
| SoTaiKhoanNganHang (text tự do) | `so_tai_khoan` ≤ 50 + `ten_ngan_hang` | Tách số / tên ngân hàng; 9 dòng > 50 ký tự phải tách tay |
| LinkLienHe | `LIEN_KET_MXH` (loai = 0 Facebook) | |
| GhiChu | `ghi_chu` | |
| IdCoCauToChuc | `phong_ban_id` | §1.4 |
| IdChucVu | `chuc_vu_id` | Bản export thiếu tbChucVu → NULL (hoặc export bổ sung rồi nạp `CHUC_VU` trước) |
| IdKieuNguoiDung | `loai_nguoi_dung` (§1.3) + nhóm quyền (§2) | |
| — | `trang_thai_nhan_su = 0` DangLamViec | Cũ không phân biệt nghỉ việc với khoá đăng nhập |
| TenDangNhap | `TAI_KHOAN.username` | Giữ **phần tên** cũ, **đổi đuôi** sang `@vietgeneducation.edu.vn` cho cả 173 tài khoản (chủ sản phẩm chốt 06/10/2026 — hệ cũ dùng `@vietgenacademy.edu.vn`). Validator `^[a-zA-Z0-9._-]+(@[a-zA-Z0-9.-]+)?$`, đăng nhập so khớp chính xác. Đã kiểm: 173 username mới đều duy nhất. 1 username hỏng mã hoá → sinh lại từ họ tên viết thường không dấu (quy tắc 137/174 username cũ đang theo) |
| MatKhau | `password_hash` | **Không chép.** Băm qua chính LangCenter (§1.5) |
| — | `phai_doi_mat_khau = true` | |
| KichHoat = 0 hoặc TrangThai = 0 | `TAI_KHOAN.trang_thai = 1` VoHieuHoa | 29 user → còn **145 tài khoản hoạt động** |
| GioiTinh, IdCapDo_DoanhThu, SoLanDangNhap, Online, ThongTinThietBi_TruyCap | — | Không chuyển |

### 1.3 `loai_nguoi_dung` (chỉ để lọc, **không** cấp quyền – quy tắc #9)

| Vai trò cũ (số user) | `loai_nguoi_dung` |
|---|---|
| Giáo viên (101) | `1` GiaoVien (+ 1 dòng `HO_SO_GIAO_VIEN` rỗng) |
| Nhân viên kinh doanh (61), NVKD – master (5), Trưởng phòng kinh doanh (1) | `4` NhanVienKinhDoanh |
| SUPER ADMIN (1), Tổng giám đốc (1), Điều phối lớp (2), Điều phối lớp – master (1) | `0` NhanVien |
| Khách (1) – tài khoản không có thao tác nào | Không chuyển (xác nhận lại) |

### 1.4 `tbCoCauToChuc` → `PHONG_BAN` (10 dòng còn dùng)

| Cũ | Đích |
|---|---|
| IdCoCauToChuc | `id` |
| TenCoCauToChuc | `ten` |
| IdCha (GUID rỗng = gốc) | `phong_ban_cha_id` (NULL = gốc) |
| IdQuanLy (CSV, prod ≤ 1 người) | `nguoi_quan_ly_id`: chỉ là thông tin, **không cấp quyền** |
| — | `thu_tu` theo thứ tự cũ |
| — | `tag_vai_tro`: **0 KinhDoanh** cho "Phòng kinh doanh" + 6 team "Leader …"; **1 GiaoVien** cho "Tiếng Anh", "Tiếng Đức" (chứa giáo viên); NULL cho "phòng sản phẩm" |
| TrangThai = 0 ("nắng team", không ai thuộc) | Không chuyển |

Không có `tag_vai_tro = KinhDoanh` thì ô *Đội nhóm* ở màn Doanh thu/Thống kê CRM không hiện các team, và doanh thu bị gom vào mục `KHAC`.

### 1.5 Mật khẩu – theo đúng cách LangCenter làm

- **Không ghi hash tay.** Theo `scripts/dong-bo-mat-khau-dev.sh`, LangCenter luôn gọi API để băm.
- Sau khi nạp `TAI_KHOAN` (cột `password_hash` để tạm một chuỗi không hợp lệ: `KiemTra` bắt `FormatException` → đăng nhập bị từ chối, không lộ lỗi), Quản trị viên đặt lại mật khẩu từng tài khoản qua chức năng `DoiMatKhauNguoiKhac.Sua`. API băm PBKDF2; mật khẩu ≥ 12 ký tự.
- Giữ `phai_doi_mat_khau = true` để người dùng tự đổi ở lần đăng nhập đầu.
- Gửi mỗi người: mã trung tâm + username + mật khẩu tạm.

---

## 2. Phân quyền

### 2.1 Mô hình đích (quy tắc #9)

`TAI_KHOAN ─< NGUOIDUNG_QUYEN >─ QUYEN ─< QUYEN_CHUC_NANG(ten_chuc_nang, hanh_dong)`

- Quyền hiệu lực = **hợp** các nhóm, không có deny.
- Chỉ dùng cặp (chức năng, hành động) **đã khai trong `ChucNang.ThaoTacTheoChucNang`**. Ghi cặp khác là ô chết, `MaTranQuyenPhaiKhopThucTeTests` sẽ báo.
- `hanh_dong` lưu số nguyên: 0 Xem · 1 Them · 2 Sua · 3 Xoa · 10 Duyet · 12 Huy · 13 SinhLich · 14 ThuTien · 21 HoanTat · 23 GuiXepLop.
- `QUYEN`: `UNIQUE(tenant_id, ten_quyen)`. Không tạo trùng tên với 4 nhóm seed.
- Quyền được cache 5 phút → nạp bằng SQL xong phải restart API.

### 2.2 Nhóm quyền đích (mọi cặp đều có trong danh mục LangCenter)

| Nhóm (`QUYEN.ten_quyen`) | Gán cho (vai trò cũ) | `QUYEN_CHUC_NANG` |
|---|---|---|
| **Quản trị viên** *(seed sẵn, không tạo lại)* | SUPER ADMIN | giữ nguyên seed |
| **Giáo viên** *(seed sẵn)* | Giáo viên (101) | giữ nguyên `NhomQuyenMacDinh.CuaGiaoVien` |
| **Ban giám đốc** | Tổng giám đốc | `KhachHang`: Xem, Them, Sua, Xoa · `ChamSocKhachHang`: Xem, Them, Sua · `DoanhThu`: Xem, Them, Sua · `ThongKeDoanhThu`: Xem · `KhoaHoc`: Xem · `SanPham`: Xem |
| **Nhân viên kinh doanh** | NVKD (61) | `KhachHang`: Xem, Them · `ChamSocKhachHang`: Xem, Them, Sua · `DoanhThu`: Xem, Them, ThuTien, GuiXepLop · `KhoaHoc`: Xem · `SanPham`: Xem · `LopHoc`: Xem |
| **Nhân viên kinh doanh – master** | NVKD – master (5) | Như NVKD + `KhachHang`: Sua, Xoa · `DoanhThu`: Sua |
| **Trưởng phòng kinh doanh** | Trưởng phòng KD (1) | Như NVKD + `ThongKeDoanhThu`: Xem · `TaiKhoan`: Xem · `HoSoNguoiDung`: Xem |
| **Điều phối lớp** | Điều phối lớp (2) | `KhoaHoc`: Xem, Them, Sua · `LopHoc`: Xem, Them, Sua, SinhLich · `GhiDanhLop`: Xem, Them, Xoa · `XepLop`: Xem, Duyet · `BuoiHoc`: Xem, Them, Sua · `LopHocToanTrungTam`: Xem, Sua · `NhatKyHeThong`: Xem |
| **Điều phối lớp – master** | Điều phối lớp – master (1) | Như Điều phối lớp + `KhoaHoc`: Xoa · `LopHoc`: Xoa, Huy, HoanTat · `BuoiHoc`: Xoa, Huy |

Căn cứ đối chiếu với quyền cũ:

| Quyền cũ | Thao tác cũ | → Chức năng LangCenter |
|---|---|---|
| QuanLyKhachHang | themmoi / capnhat / xoabo / capnhatdonhang | `KhachHang` Them/Sua/Xoa · `DoanhThu` Them/Sua |
| ChamSocKhachHang | themmoi / capnhat / xoabo / lichsuchamsoc-* | `ChamSocKhachHang` |
| QuanLyDoanhThu | xuatfile, themmuctieu | `DoanhThu.Xem`, `ThongKeDoanhThu.Xem`. Không có tương đương cho "mục tiêu" |
| QuanLySanPham | themmoi / capnhat / xoabo | `KhoaHoc` (sản phẩm cũ là khoá học) |
| QuanLyLopHoc | giaodienquanly, themmoi/capnhat/xoabo-lophoc, capnhat-lichhoc | `LopHoc`, `BuoiHoc`, `GhiDanhLop`, `XepLop`, `LopHocToanTrungTam` |
| QuanLyLopHoc | giaodiengiaovien | Nhóm seed *Giáo viên* |
| History | — | `NhatKyHeThong.Xem` |
| QuanLyLuong, Guide, Report, ThongTinCaNhan, SystemUtilities | — | Không có tương đương → bỏ |

Các cặp sau thuộc `ChucNang.CanCanNhac` (không đảo ngược được / dính tiền / mở rộng phạm vi). Chúng có trong bảng trên vì vai trò cũ đã làm việc tương đương, nhưng cần chủ sản phẩm duyệt: `DoanhThu.ThuTien`, `XepLop.Duyet`, `LopHoc.HoanTat`, `LopHoc.Huy`, `BuoiHoc.Huy`, `LopHocToanTrungTam.Xem/Sua`.

---

## 3. Đơn hàng & doanh thu

### 3.1 Bảng đích

| Cũ | → LangCenter | Ghi chú |
|---|---|---|
| tbSanPham (thực chất là khoá học) | **`KHOA_HOC`** | `SAN_PHAM` của LangCenter là sách/học cụ, không dùng |
| tbKhachHang | `KHACH_HANG` | |
| tbKhachHang_DonHang | `DANG_KY_KHOA_HOC` (= đơn hàng) | `CHECK` đúng một trong `khoa_hoc_id` / `san_pham_id` |
| tbKhachHang_DonHang_ThanhToan | `THU_TIEN_DANG_KY` | `CHECK so_tien > 0`; cùng đơn vị tiền với đơn |

### 3.2 `tbSanPham` → `KHOA_HOC` (36)

| Cũ | Đích |
|---|---|
| IdSanPham | `id` |
| TenSanPham | `ten`: `UNIQUE(tenant, ten)`; "Chỉ luyện đề Telc/Goethe B2" trùng 2 bản → thêm hậu tố cho 1 bản |
| GiaTien | `gia_tien`, `don_vi_tien = 0` VND |
| SoBuoi | `so_buoi` (số niêm yết) |
| GhiChu, loại (Anh/Đức), GiaTienTungBuoi, ThoiGianBuoiHoc | `ghi_chu` ≤ 1000 |
| TrangThai = 0 | `dang_ban = false` (FK Restrict nên không bỏ; đơn cũ phải giữ được tên khoá) |

### 3.3 `tbKhachHang` → `KHACH_HANG` (939; bỏ 13 khách xoá mềm + gộp 18 khách trùng SĐT cùng tên)

| Cũ | Đích |
|---|---|
| IdKhachHang | `id` |
| TenKhachHang | `ho_ten` |
| Email | `email` |
| SoDienThoai | `so_dien_thoai`, partial `UNIQUE(tenant, so_dien_thoai) WHERE <> ''` → xem §4 |
| LienKet | `link_facebook` ≤ 500 |
| IdPhuongThucThanhToan | `phuong_thuc_thanh_toan`: Tiền mặt → 0 TienMat · Techcombank, MB Bank, Tk Đức, Tk CAD, Tk Thụy Sỹ, Paypal, Remitly → 1 ChuyenKhoan · Chưa đóng → 2 Khac |
| IdNguoiTao | **`created_by_id`**. Đây là mốc LangCenter dùng để tính doanh số cá nhân/đội. Khớp người tạo thanh toán cũ ở 1147/1161 dòng |
| — | `nguon = 0` NhanVienTao |
| GhiChu + NgheNghiep + DoTuoi + DiaChi + NguonKhachHang + LienKetSale | Gộp vào `ghi_chu` ≤ 1000 (1 khách vượt độ dài) |
| NgayTao | `created_at` (lọc khách theo ngày tạo hồ sơ dùng cột này) |
| TrangThai = 0 (xoá mềm) | Không chuyển |
| QuyenTruyCap, IdGoiChamSoc, IdQuocGiaSinhSong | Không chuyển |

### 3.4 `tbKhachHang_DonHang` → `DANG_KY_KHOA_HOC` (1056; bỏ 10 đơn TrangThai = 0)

| Cũ | Đích | Quy tắc LangCenter |
|---|---|---|
| IdDonHang | `id` | |
| IdKhachHang | `khach_hang_id` | |
| IdSanPham | `khoa_hoc_id`; `san_pham_id = NULL` | |
| — | `so_luong = 1` | Khoá học luôn 1 |
| SanPham.GiaTien | `gia_goc` | Snapshot giá niêm yết (= TongSoTien ở 1071/1077 đơn) |
| TongSoTien | **`so_tien`** | **Cam kết khách trả = cơ sở doanh thu** |
| — | `don_vi_tien = 0` VND, `ty_gia_ve_vnd = 1` | TongSoTien cũ luôn là VND |
| NgayTao | `ngay_dang_ky` | Mốc thời gian của doanh thu |
| (phương thức của khách) | `phuong_thuc` | |
| GhiChu + trình độ đầu vào/đầu ra | `ghi_chu` ≤ 1000 (1 đơn vượt) | |
| IdNguoiTao | `created_by_id` | Người nhập đơn. Báo cáo không dùng cột này |

### 3.5 `tbKhachHang_DonHang_ThanhToan` → `THU_TIEN_DANG_KY` (1161 → 1115)

| Cũ | Đích | Quy tắc |
|---|---|---|
| IdThanhToan | `id` | |
| IdDonHang | `dang_ky_id` | |
| SoTienDaDong (VND) | `so_tien` | Cùng đơn vị VND với đơn |
| Tiền gốc ngoại tệ (SoTienDaDong_ChuaQuyDoi, EUR/CAD/USD/CHF) | `ghi_chu` ≤ 500 | Ví dụ "Đóng 460 CAD" |
| NgayTao | `ngay_thu` | |
| IdNguoiTao | `nguoi_thu_id`, `created_by_id` | |
| (phương thức của khách) | `phuong_thuc` | |
| — | `xac_nhan_du_tien = false` | Xác nhận đủ tiền là quyết định chủ động của người thu, không tick sẵn (crm.md 05/10/2026). Không tự đặt khi migrate |
| SoTienDaDong ≤ 0 (33 dòng) | **Không chuyển** | Vi phạm `CHECK so_tien > 0` |
| TrangThai = 0 (14 dòng) | Không chuyển | |
| IdKhachHang, IdSanPham, ThuTuThanhToan, PhanTramDaDong | — | Suy được |

### 3.6 Số liệu sẽ thấy trên LangCenter sau khi chuyển

| Chỉ số | Giá trị (prod) |
|---|---|
| Doanh thu (cam kết, `Σ so_tien × ty_gia`) | ≈ **15,27 tỷ** / 1056 đơn |
| Đã thu (`Σ THU_TIEN_DANG_KY.so_tien`) | ≈ **7,80 tỷ** |
| Đơn đóng đủ | 142 |
| Đơn còn thiếu (hiện "Thiếu X", nằm trong nhắc nợ) | 887 |
| Đơn chưa đóng | 27 |

Đây là kết quả đúng theo định nghĩa của LangCenter. Đơn nào trung tâm không còn đòi nữa thì người thu tự đánh dấu "Đã nhận đủ tiền" trên hệ thống sau khi chuyển.

---

## 4. Làm sạch bắt buộc để qua ràng buộc LangCenter

| Vấn đề | Số lượng | Ràng buộc | Xử lý |
|---|---|---|---|
| SĐT khách trùng (sau khi bỏ ký tự không phải số) | 25 nhóm / 52 khách | partial `UNIQUE(tenant, so_dien_thoai)`. Mục đích: "chặn hai người bán nhập cùng một khách" | **Cùng tên** (sau khi bỏ dấu và hậu tố số) → **GỘP** về khách tạo sớm nhất, dồn đơn sang: 18 hồ sơ, còn 939 khách. **Khác tên** → giữ SĐT ở khách đầu, 9 khách sau để `NULL` và ghi số vào `ghi_chu` (số dùng chung là chuyện thật: vợ chồng, phụ huynh đăng ký cho con, số rác `00000`) |
| Thanh toán ≤ 0 | 33 | `CHECK so_tien > 0` | Bỏ |
| Tên khoá trùng | 1 cặp | `UNIQUE(tenant, ten)` | Thêm hậu tố |
| `ghi_chu` > 1000 | KH 1, đơn 1 | `varchar(1000)` | Cắt, giữ phần đầu |
| STK ngân hàng > 50 | 9 | `varchar(50)` | Tách tay |
| Username hỏng mã hoá | 1 | — | Sửa tay |
| Ngày sinh 1900-01-01 | 19 | — | NULL |
| GUID rỗng `0000…` | nhiều cột | FK thật | → NULL |

---

## 5. Cách nạp – theo quy ước script của LangCenter

- **Script SQL đặt ở `scripts/`**, cùng khuôn với `chuyen-vai-tro-nhan-vien-kinh-doanh.sql`:
  - một giao dịch `BEGIN … COMMIT`;
  - `\echo` trước/sau để đối chiếu;
  - khối `DO $$ … RAISE EXCEPTION` chặn kết quả sai;
  - chạy bằng `docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/…sql`.
- Nạp bằng SQL thẳng, không qua EF: `AppDbContext.SaveChanges` luôn ghi `created_at = now` khi thêm mới, nên nạp qua EF sẽ mất ngày tạo/ngày đăng ký gốc.
- Mọi hàng phải có `tenant_id` của trung tâm mới. SQL thẳng không có `AppDbContext` gán hộ (quy tắc #2).
- Không tạo migration EF nào cho việc chuyển dữ liệu (quy tắc #1, giống lý do script chuyển vai trò NVKD không nằm trong migration).

**Thứ tự:**

1. Tạo trung tâm bằng luồng chủ hệ thống → lấy `tenant_id`, `ma_trung_tam`.
2. `PHONG_BAN` (gốc trước, con sau; `nguoi_quan_ly_id` để NULL).
3. `NGUOI_DUNG` (`created_by_id`/`updated_by_id` NULL) → `HO_SO_GIAO_VIEN` → `LIEN_KET_MXH`.
4. Cập nhật `PHONG_BAN.nguoi_quan_ly_id` và cột audit của `NGUOI_DUNG`. Các hàng này vừa nạp trong cùng script, không đụng dữ liệu có sẵn.
5. `TAI_KHOAN` (`phai_doi_mat_khau = true`) → `QUYEN` + `QUYEN_CHUC_NANG` (§2.2, trừ 2 nhóm seed) → `NGUOIDUNG_QUYEN`.
6. `KHOA_HOC` → `KHACH_HANG` → `DANG_KY_KHOA_HOC` → `THU_TIEN_DANG_KY`.
7. Restart API (xoá cache quyền) → Quản trị viên đặt mật khẩu tạm qua API (§1.5).

## 6. Đối soát sau khi nạp

- 173 `NGUOI_DUNG`, 173 `TAI_KHOAN` (145 hoạt động) — chưa kể admin do seeder tạo. Mỗi tài khoản đúng 1 nhóm quyền.
- Đăng nhập thử một tài khoản mỗi nhóm; kiểm menu hiện đúng theo quyền.
- Màn Doanh thu không lọc: 1056 đơn, tổng ≈ 15,27 tỷ (15.273.437.000), đã thu ≈ 7,80 tỷ (7.804.775.161), 1115 lần thu. Gộp khách **không** đổi các số này — đơn chỉ chuyển sang hồ sơ giữ lại.
- Lọc theo từng Sale (`KHACH_HANG.created_by_id`): số khách bằng số khách cũ họ tạo.
- Lọc *Đội nhóm*: hiện 7 phòng tag KinhDoanh, mục `KHAC` không phình bất thường.
