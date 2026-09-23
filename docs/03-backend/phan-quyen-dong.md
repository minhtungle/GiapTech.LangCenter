# Phân quyền động theo chức năng + thao tác

> **Quy tắc bất di bất dịch #9.** **Không** dùng `[Authorize(Roles = "...")]` với role cố định. Quyền
> đọc động từ bảng `QUYEN_CHUC_NANG` tại runtime.

## Vì sao không dùng role cố định

Nghiệp vụ (FR-05) cho phép Admin của **mỗi trung tâm tự định nghĩa nhóm quyền riêng** — tên nhóm và tập quyền
do người dùng tạo ra lúc chạy, không biết trước lúc biên dịch. Role cố định trong attribute không biểu
diễn được điều này.

## Mô hình quyền

```
NGUOI_DUNG ──N:N── QUYEN ──1:N── QUYEN_CHUC_NANG
                                  ├─ ten_chuc_nang  (vd "LichThiDau", "TaiChinh")
                                  └─ hanh_dong      (xem | them | sua | xoa)
```

Một tài khoản gán **nhiều nhóm quyền**; quyền hiệu lực = **hợp (union)** của tất cả các nhóm. Không có
khái niệm "deny" ghi đè — chỉ cộng dồn quyền.

## Cách dùng trên endpoint

```csharp
[RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
public async Task<IActionResult> CapNhatTranDau(...)
```

Dùng hằng số `ChucNang.*` và enum `HanhDong` thay vì chuỗi thô — gõ sai chuỗi sẽ tạo ra một policy
không bao giờ khớp, và lỗi chỉ lộ ra lúc chạy.

### Cơ chế (đã triển khai)

| Thành phần | Vai trò |
|---|---|
| `RequirePermissionAttribute` | Sinh tên policy `Quyen:{chucNang}:{hanhDong}` |
| `QuyenPolicyProvider` | Sinh policy **khi gặp lần đầu** — không phải đăng ký sẵn từng tổ hợp trong `Program.cs` (số tổ hợp = số chức năng × 4 và còn tăng theo mỗi module) |
| `QuyenAuthorizationHandler` | Đọc `tenant_id` + `NameIdentifier` từ claim, hỏi `IQuyenService` |
| `QuyenService` | Truy vấn `NGUOIDUNG_QUYEN → QUYEN → QUYEN_CHUC_NANG`, cache 5 phút |

### Đã kiểm chứng

`PhanQuyenVaCachLyTests` chạy qua API thật. Đã chứng minh test bắt được vi phạm bằng phản chứng: thay
`[RequirePermission]` bằng `[Authorize]` thường → test "thiếu quyền → 403" đỏ ngay.

## Danh mục chức năng và thao tác

`ten_chuc_nang` là **danh mục đóng** — định nghĩa bằng hằng số trong code (không để người dùng
tự nhập chuỗi tùy ý). Mỗi chức năng thuộc **một trong ba hệ thống** (HRM · CRM · LMS) hoặc nhóm
**dùng chung** — xem mục [Ba hệ thống con](#ba-hệ-thống-con-hrm--crm--lms).

### Ma trận THƯA: mỗi chức năng có tập thao tác riêng (14/09/2026)

Trước 14/09/2026, ma trận phân quyền hiện **đủ bốn ô** `Xem · Thêm · Sửa · Xoá` cho **mọi**
chức năng. Hệ quả: **31 trên 108 ô tick cũng không làm gì** — `NhatKyHeThong.Xoa` (nhật ký chỉ
ghi thêm, không xoá được), `ChucVu.Xem` (bị `NhanSu.Xem` thay), `HocOnline.Sua`… Người cấu hình
quyền không có cách nào biết ô nào có tác dụng, nên **tick bừa cho chắc** — đúng thứ làm phân
quyền mất ý nghĩa.

Đồng thời bốn thao tác CRUD không diễn đạt nổi nghiệp vụ thật: "chốt buổi học" và "sửa điểm
danh" đều phải mượn `Sua`, nên không tách được quyền của giáo viên chính với trợ giảng.

Nay `ChucNang.ThaoTacTheoChucNang` khai **từng chức năng có đúng những thao tác nào**, và màn
phân quyền chỉ hiện đúng bấy nhiêu ô. Ô không áp dụng để **trống** (không phải checkbox mờ —
checkbox mờ vẫn là checkbox, người đọc phải thử mới biết). Số ô: **540 → 106**.

### 18 thao tác

Bốn thao tác cơ bản, mười bốn thao tác đặc thù. `QUYEN_CHUC_NANG.hanh_dong` lưu **số nguyên**
nên **giá trị 0–3 không bao giờ được đổi** — đổi `Sua` từ 2 thành 3 là âm thầm biến quyền "Sửa"
của mọi nhóm thành "Xoá". Canh bởi `MaTranQuyenPhaiKhopThucTeTests.Gia_tri_so_cua_hanh_dong_co_ban_khong_doi`.

| Thao tác | Số | Nghĩa |
|---|---|---|
| `Xem` | 0 | Đọc danh sách / chi tiết |
| `Them` | 1 | Tạo mới |
| `Sua` | 2 | Sửa bản ghi đã có |
| `Xoa` | 3 | Xoá hẳn |
| `Duyet` | 10 | Duyệt / từ chối đơn từ hệ thống khác. Tách khỏi `Sua` vì duyệt đơn xếp lớp **không đảo ngược được** và ảnh hưởng tới bên bán |
| `Chot` | 11 | Chốt sổ — sau đó không sửa được. Chốt buổi học sinh bản ghi **Vắng mặc định** cho mọi người chưa điểm danh |
| `Huy` | 12 | Huỷ nhưng **giữ bản ghi** (khác `Xoa` là mất hẳn) |
| `SinhLich` | 13 | Sinh hàng loạt buổi học theo thứ trong tuần |
| `ThuTien` | 14 | Ghi nhận tiền đã thu |
| `Cham` | 15 | Chấm điểm bài nộp |
| `TuLam` | 16 | Làm việc đó **cho chính mình** — endpoint lấy người dùng từ token, không nhận id |
| `DocTep` | 17 | Đọc / tải tệp đính kèm |
| `QuanLyTep` | 18 | Tải lên và xoá tệp |
| `CauHinhTien` | 19 | Cấu hình thông tin thanh toán (QR chuyển khoản…) |
| `CauHinhQuyen` | 20 | Sửa ma trận quyền của một nhóm |
| `HoanTat` | 21 | Hoàn tất **wizard tạo lớp** — lớp rời trạng thái nháp. Một chiều. KHÔNG phải "kết thúc lớp" (nợ N26, chưa có endpoint) |
| `XepNhanSu` | 22 | Xếp nhân sự vào phòng ban |
| `GuiXepLop` | 23 | Gửi yêu cầu xếp lớp sang LMS |

#### Cặp `Xem` / `TuLam` — chỗ dễ cấp sai nhất

`TuLam` **không phải** phiên bản yếu của `Xem`; hai cái trả lời hai câu hỏi khác nhau:

| | `TuLam` | `Xem` |
|---|---|---|
| `NhanXetBuoiHoc` | Gửi và đọc lại nhận xét **của chính mình** | Đọc nhận xét **của mọi người** trong buổi |
| `DiemDanh` | Tự khai có mặt | Xem bảng điểm danh cả lớp |
| `BaiNopBaiTap` | Nộp bài của mình | Xem bài nộp của cả lớp |

Nên **học viên chỉ được `TuLam`, không được `Xem`**. Cấp nhầm `NhanXetBuoiHoc.Xem` cho học viên
là cho họ đọc phản hồi riêng của bạn cùng lớp — lỗi đã mắc và sửa ngày 14/09/2026.

Hệ quả về cách gác endpoint: `GET /buoi-hoc/{id}/nhan-xet` gác bằng **`TuLam`** (thao tác hẹp
nhất — ai cũng đọc được nhận xét của mình), rồi *handler* mới đọc thêm `Xem` để quyết định trả
về của mọi người hay chỉ của mình. Gác endpoint bằng `Xem` sẽ chặn học viên đọc nhận xét chính
họ. `[RequirePermission]` **chỉ nhận một quyền**, không có ngữ nghĩa OR.

### Bảng đầy đủ: chức năng × thao tác

**Dùng chung cả ba hệ thống**

| Chức năng | Thao tác | Module | FR |
|---|---|---|---|
| `TaiKhoan` | `Xem` `Them` `Sua` `Xoa` | Tài khoản đăng nhập | FR-03 |
| `HoSoNguoiDung` | `Xem` `Them` `Sua` `Xoa` | Hồ sơ con người (tách khỏi `TaiKhoan` 14/09 — người sống lâu hơn tài khoản) | FR-03 |
| `PhanQuyen` | `Xem` `Them` `CauHinhQuyen` `Xoa` | [Nhóm quyền](../06-nghiep-vu/quan-tri-he-thong.md). `CauHinhQuyen` thay `Sua`: sửa ma trận quyền khác hẳn đổi tên nhóm | FR-05 |
| `ThietLapChung` | `Xem` `Sua` `CauHinhTien` | [Thiết lập](../06-nghiep-vu/quan-tri-he-thong.md). `CauHinhTien` gác QR chuyển khoản | FR-06 |
| `Anh` | `Xem` `Them` `Xoa` | Ảnh dùng chung (logo, ảnh bìa, QR). Không `Sua` — thay ảnh là xoá rồi tải lên | — |
| `DoiMatKhauNguoiKhac` | `Sua` | Đặt lại mật khẩu người khác | FR-03 |
| `NhatKyHeThong` | `Xem` | [Nhật ký thao tác](../06-nghiep-vu/nhat-ky-he-thong.md) — chỉ ghi thêm, không sửa không xoá | FR-16 |

**HRM — Nhân sự**

| Chức năng | Thao tác | Module | FR |
|---|---|---|---|
| `NhanSu` | `Xem` `Them` `Sua` `Xoa` `DocTep` `QuanLyTep` | [Hồ sơ nhân sự](../06-nghiep-vu/hrm.md). Tệp tách riêng: xem hợp đồng ≠ thay hợp đồng | FR-03, FR-23 |
| `ChucVu` | `Them` `Sua` `Xoa` | [Danh mục chức vụ](../06-nghiep-vu/hrm.md#fr-24--danh-mục-chức-vụ). Không `Xem` — đọc qua `NhanSu.Xem` | FR-24 |
| `PhongBan` | `Xem` `Them` `Sua` `Xoa` `XepNhanSu` | [Cơ cấu tổ chức](../06-nghiep-vu/hrm.md#fr-22--cơ-cấu-tổ-chức) | FR-22 |

**CRM — Khách hàng**

| Chức năng | Thao tác | Module | FR |
|---|---|---|---|
| `KhachHang` | `Xem` `Them` `Sua` `Xoa` | [Khách hàng](../06-nghiep-vu/crm.md#fr-17--khách-hàng) — kể cả chưa mua | FR-17 |
| `ChamSocKhachHang` | `Xem` `Them` `Sua` `Xoa` | Lịch sử chăm sóc. Tách khỏi `KhachHang`: sale ghi chăm sóc nhưng không sửa hồ sơ khách | FR-17 |
| `DoanhThu` | `Xem` `Them` `Sua` `Xoa` `ThuTien` `GuiXepLop` | [Doanh thu](../06-nghiep-vu/crm.md). Lên đơn ≠ thu tiền ≠ đẩy sang LMS | FR-18, FR-21 |
| `ThongKeDoanhThu` | `Xem` | [Thống kê CRM](../06-nghiep-vu/thong-ke-crm.md) — số liệu toàn trung tâm, không phải ai bán cũng được xem | FR-28 |
| `KhoaHoc` | `Xem` `Them` `Sua` `Xoa` | [Khoá học](../06-nghiep-vu/crm.md) bán ra | FR-19 |
| `SanPham` | `Xem` `Them` `Sua` `Xoa` | [Sản phẩm](../06-nghiep-vu/crm.md) — sách, học cụ | FR-20 |

**LMS — Đào tạo**

| Chức năng | Thao tác | Module | FR |
|---|---|---|---|
| `LopHoc` | `Xem` `Them` `Sua` `Xoa` `HoanTat` `Huy` `SinhLich` | [Lớp học](../06-nghiep-vu/lop-hoc.md) | FR-07, FR-08 |
| `GhiDanhLop` | `Xem` `Them` `Xoa` | Ghi danh học viên vào lớp. Không `Sua`: đổi học phí là gỡ rồi thêm lại để còn dấu vết | FR-08 |
| `XepLop` | `Xem` `Duyet` | [Duyệt yêu cầu xếp lớp từ CRM](../06-nghiep-vu/lop-hoc.md) | FR-21 |
| `BuoiHoc` | `Xem` `Them` `Sua` `Xoa` `Huy` | [Buổi học](../06-nghiep-vu/buoi-hoc-diem-danh.md) | FR-09 |
| `DiemDanh` | `Xem` `Sua` `Chot` `TuLam` | [Điểm danh](../06-nghiep-vu/buoi-hoc-diem-danh.md). Không `Them`/`Xoa`: bản ghi sinh cùng buổi học, sai thì ghi lại | FR-10 |
| `NhanXetBuoiHoc` | `Xem` `TuLam` | Nhận xét sau buổi — xem [cặp `Xem`/`TuLam`](#cặp-xem--tulam--chỗ-dễ-cấp-sai-nhất) | FR-09 |
| `BaiTap` | `Xem` `Them` `Sua` `Xoa` | [Bài tập](../06-nghiep-vu/hoc-lieu.md) | FR-11 |
| `BaiNopBaiTap` | `Xem` `TuLam` `Cham` | [Bài học viên nộp](../06-nghiep-vu/hoc-lieu.md) | FR-12 |
| `TaiLieu` | `Xem` `Them` `Sua` `Xoa` | [Tài liệu giảng dạy](../06-nghiep-vu/hoc-lieu.md) | FR-13 |
| `HocPhi` | `Xem` `ThuTien` `Sua` `Xoa` | [Học phí](../06-nghiep-vu/hoc-phi.md) — ẩn khỏi LMS từ 12/09, chỉ CRM nắm tiền | FR-14 |
| `KhoaOnline` | `Xem` `Them` `Sua` `Xoa` | [Soạn khoá trực tuyến](../06-nghiep-vu/hoc-tap-truc-tuyen.md). `Xem` **không gác endpoint** — nó là quyền phạm vi, `PhamViKhoaOnline.LocKhoa` đọc để phân biệt người SOẠN (thấy mọi khoá kể cả nháp) với người HỌC. Endpoint đọc gác bằng `HocOnline.Xem` để học viên đọc được mà không soạn được | FR-26 |
| `GhiDanhKhoaOnline` | `Xem` `Them` `Sua` `Xoa` | Cấp quyền học khoá trực tuyến — việc điều phối, tách khỏi việc soạn | FR-26 |
| `HocOnline` | `Xem` `TuLam` | Học viên đọc bài và ghi tiến độ | FR-27 |

**Phạm vi dữ liệu** — xem [mục riêng](#lophoctoantrungtam--cách-nhận-ra-người-quản-trị-mà-không-hard-code-vai-trò)

| Chức năng | Thao tác | Nghĩa |
|---|---|---|
| `LopHocToanTrungTam` | `Xem` `Sua` | Không mở thêm chức năng mà **mở rộng phần dữ liệu thấy được** |

**Chưa có API** — `BaiKiemTra`, `BaiLamKiemTra`, `ThongKe` khai **rỗng** trong
`ThaoTacTheoChucNang`, nên **không hiện** trên màn phân quyền và không nhóm nào được cấp. Giữ
hằng lại để dữ liệu cũ không vỡ. Khi có API thật thì khai thao tác vào bảng **trước**, rồi mới
cấp cho nhóm — canh bởi `MaTranQuyenPhaiKhopThucTeTests.Nhom_quyen_mac_dinh_khong_cap_o_khong_hien_tren_man_phan_quyen`.

**33 chức năng** (30 có thao tác + 3 chưa có API) — phải khớp `ChucNang.TatCa`.

### Vì sao tách nhỏ tới mức này

Tiêu chí gộp/tách: **so cột-theo-cột trong ma trận phân quyền; khác một ô là tách**.

- `BaiTap` vs `BaiKiemTra`: trợ giảng **toàn quyền** với bài tập nhưng **chỉ xem** bài kiểm tra.
- `BaiTap` vs `BaiNopBaiTap`: học viên **tạo** bài nộp nhưng **không tạo** bài tập.
- `DoanhThu.ThuTien` vs `DoanhThu.Them`: sale lên đơn được nhưng **không tự xác nhận đã thu tiền**.
- `DiemDanh.Chot` vs `DiemDanh.Sua`: trợ giảng ghi điểm danh nhưng **không chốt** — chốt là
  quyết định của giáo viên chính và sinh "Vắng mặc định" cho người chưa khai.

Ngược lại, **không tách** khi attribute không thể tách thật: `KhoaHoc` giữ nguyên CRUD dù giá
là dữ liệu tiền, vì `LuuKhoaHocCommand` ghi tên, ghi chú và giá trong **một lệnh** — muốn tách
`CauHinhTien` thì phải tách lệnh trước. Khai một ô không gác gì chính là lỗi vừa dọn.

### ⚠️ Thêm chức năng mới: bắt buộc khai quyền trong cùng PR

Từ 14/09/2026 đây là **một phần của định nghĩa "xong"**, không phải việc làm sau:

1. Thêm hằng vào `ChucNang` và phân loại hệ thống trong `HeThongCua`.
2. **Khai thao tác vào `ChucNang.ThaoTacTheoChucNang`** — liệt kê đúng những thao tác thật
   của nghiệp vụ, không mặc định bốn ô CRUD. Cần thao tác chưa có thì thêm giá trị mới vào
   `enum HanhDong` với **số ≥ 10** (không bao giờ chèn vào giữa 0–3).
3. Gác endpoint bằng `[RequirePermission(ChucNang.X, HanhDong.Y)]` — đúng cặp vừa khai.
4. Thêm nhãn tiếng Việt vào **`chucNang`** và **`hanhDong`** trong `frontend/src/lib/i18n.ts`,
   và giá trị mới vào **`export type HanhDong`** trong `frontend/src/lib/quyen.ts`.
5. Cân nhắc cấp cho nhóm mặc định trong `NhomQuyenMacDinh.cs` (không bắt buộc — nhóm quản trị
   tự có mọi chức năng).
6. Cập nhật bảng ở trên trong tài liệu này.

Bốn chốt chặn tự động, chạy cùng `dotnet test` và script:

| Canh gì | Ở đâu |
|---|---|
| Endpoint gác bằng quyền chưa khai → ô không hiện, **403 cho cả quản trị** | `MaTranQuyenPhaiKhopThucTeTests.Moi_quyen_endpoint_dang_dung_phai_duoc_khai` |
| Quyền đã khai mà không endpoint nào dùng (ô chết) | `...Moi_quyen_da_khai_phai_co_endpoint_dung_hoac_khai_ly_do` |
| Nhóm mặc định cấp ô màn phân quyền không hiện | `...Nhom_quyen_mac_dinh_khong_cap_o_khong_hien_tren_man_phan_quyen` |
| Chức năng mà **tầng phạm vi** đọc bị xoá khỏi bảng khai | `MaTranQuyenPhaiKhopThucTeTests.Chuc_nang_tang_pham_vi_doc_phai_con_trong_bang_khai` |
| Thiếu nhãn tiếng Việt hoặc thiếu giá trị trong kiểu TS | `python3 scripts/check-nhan-phan-quyen.py` |

Quyền **không** gác endpoint mà đọc trong handler (quyền phạm vi) phải khai lý do trong
`ChuaDungNhungCoLyDo` — danh sách này có test chiều ngược để không lạc hậu.

> ⚠️ **Quyền phạm vi trông giống ô chết.** Test "ô chết" chỉ quét `[RequirePermission]` trong
> `Controllers/`, nên quyền đọc ở tầng phạm vi bị nó báo là không ai dùng. Ngày 14/09/2026 tôi
> nghe theo và xoá mất `KhoaOnline.Xem` — hậu quả là `PhamViKhoaOnline.LocKhoa` không còn phân
> biệt được người soạn với người học, và khoá vừa tạo **biến mất khỏi màn của chính người tạo**.
> 489 test backend xanh hết; chỉ E2E bắt được. Trước khi xoá một ô, `grep CoQuyenAsync` xem
> tầng phạm vi có đọc nó không — nay đã có test canh tự động.


## Ba hệ thống con: HRM · CRM · LMS

Từ 08/09/2026, danh mục chức năng được **nhóm** thành ba hệ thống. Đây là cách nhóm để lọc
sidebar, **không phải ba ứng dụng**: vẫn một API, một database, một lần đăng nhập.

| Hệ thống | Chức năng |
|---|---|
| **HRM** | `NhanSu`, `ChucVu`, `PhongBan` |
| **CRM** | `KhachHang`, `DoanhThu`, `KhoaHoc`, `SanPham` |
| **LMS** | `LopHoc`, `BuoiHoc`, `DiemDanh`, `BaiTap`, `BaiNopBaiTap`, `BaiKiemTra`, `BaiLamKiemTra`, `TaiLieu`, `HocPhi`, `ThongKe`, `LopHocToanTrungTam` |
| **Dùng chung** | `TaiKhoan`, `PhanQuyen`, `ThietLapChung`, `Anh`, `DoiMatKhauNguoiKhac`, `NhatKyHeThong` |

Nguồn sự thật duy nhất: `ChucNang.HeThongCua()` trong `Domain/Common/ChucNang.cs`. **Không lưu
xuống DB** — hệ thống của một chức năng là thuộc tính của mã nguồn (`LopHoc` thuộc LMS là bất
biến), lưu xuống DB thì mỗi tenant nhóm một kiểu và sidebar hết xác định.

### Đường dẫn frontend: mỗi hệ thống một tiền tố

`/hrm/...` · `/crm/...` · `/lms/...` — **cả ba cùng khuôn** (thống nhất 10/09/2026). Quản trị
dùng chung nằm ở `/quan-tri/...`.

`Layout.tsx` suy ra đang ở hệ thống con nào **từ tiền tố đường dẫn**, và URL **thắng** lựa chọn
lưu trong `localStorage`: mở bookmark `/crm/...` thì sidebar phải nhảy sang CRM, không hiện menu
của hệ thống lưu từ phiên trước.

> Trước 10/09, route LMS **không có tiền tố** (`/lop-hoc`, `/hoc-vien`, `/buoi-hoc`, `/tai-lieu`,
> `/hoc-phi` — vì LMS dựng trước khi tách ba hệ thống). Cái giá: `Layout.tsx` phải giữ một **danh
> sách 5 đường hardcode**, và thêm một màn LMS mới mà quên khai vào đó thì sidebar hiện **sai hệ
> thống con** — lỗi im lặng, không có lỗi biên dịch. Nay chỉ còn một bảng tra tiền tố.
>
> Đường cũ vẫn **chuyển tiếp** sang `/lms/...` (`DoiSangLms` trong `App.tsx`) để bookmark và link
> đã gửi cho nhau không chết. Chuyển tiếp **giữ nguyên `:id` và query** — dùng `<Navigate to>`
> tĩnh sẽ làm mất chúng và link tới đúng một lớp/buổi cụ thể sẽ lặng lẽ rơi về danh sách.
> Canh bởi `e2e/url-he-thong-con.spec.ts`.

**Đừng lẫn route với endpoint API**: `/lms/hoc-vien` là đường frontend, còn API vẫn là
`/api/v1/hoc-vien`. Ở `NguoiDung.tsx` hai thứ này là hai trường riêng (`duong` = endpoint,
`duongChiTiet` = route) đúng vì lý do đó — đổi tiền tố route **không** được đụng tới lời gọi API.

### Vì sao có nhóm "dùng chung"

`TaiKhoan`, `PhanQuyen`… **không thuộc hệ thống nào**. Ép chúng vào một hệ thống sẽ sai theo cả
hai hướng: người quản trị nhân sự cần sửa tài khoản nhưng không cần vào LMS, còn nhật ký thì ghi
thao tác của cả ba hệ thống. Nhóm này hiện ở sidebar và ở **mọi tab** của màn phân quyền.

### Thao tác KHÔNG mở lối vào hệ thống của chính nó (18/09/2026)

Khác mục dưới (chức năng *dùng chung*, không thuộc hệ thống nào), đây là: chức năng **có** hệ
thống rõ ràng, nhưng **một thao tác** của nó không đủ để coi là "làm việc trong hệ thống đó".

Ca đầu tiên — `TieuChiDanhGia.TuLam`. `TieuChiDanhGia` thuộc **HRM** (module cấu hình nằm ở đó),
nhưng học viên giữ `TuLam` chỉ để **đọc tên tiêu chí mà chấm giáo viên trên phiếu nhận xét buổi
học ở LMS**. Tính nó là "vào được HRM" thì `Layout` đưa học viên sang sidebar nhân sự và họ
**mất luôn menu Lớp học** — chỉ còn thấy "Tổng quan".

Khai ở `ChucNang.KhongMoLoiVao`, hỏi qua `ChucNang.MoLoiVaoHeThong(chucNang, hanhDong)`. Đặt ở
`Domain` để API và màn phân quyền dùng cùng một chỗ. Danh sách phải **hẹp** —
`MoLoiVaoHeThongTests` chốt đúng một ngoại lệ và kiểm cả chiều ngược (`Xem`/`Them`/`Sua` của
chính chức năng đó **vẫn** mở lối vào HRM, để người phụ trách danh mục tiêu chí không bị khoá
ngoài).

> Lỗi này do E2E `doi-nick-khong-giu-quyen-cu` bắt được, không phải tsc hay lint: cả hai thứ đó
> không biết gì về việc một ô quyền làm đổi sidebar.

### Quyền dùng chung KHÔNG mở lối vào hệ thống

`GET /toi/he-thong` trả hệ thống mà người dùng có ít nhất một quyền — **bỏ qua** nhóm dùng
chung. Nếu tính cả thì người chỉ quản trị tài khoản sẽ "vào được" cả ba hệ thống, mà cả ba đều
chỉ hiện đúng cụm Quản trị — ba lối vào giống hệt nhau, bộ chuyển thành vô nghĩa.
Canh bởi `BaHeThongTests.Chuc_nang_dung_chung_khong_mo_loi_vao_he_thong`.

### Thêm chức năng mới

Phải khai hệ thống của nó trong `TheoHeThong` **hoặc** thêm vào `DungChung`. Quên thì
`NhomHeThongTests.Moi_chuc_nang_phai_duoc_khai_he_thong_hoac_dung_chung` đỏ ngay kèm tên hằng —
bỏ qua thì chức năng đó hiện ở sidebar của **mọi** hệ thống.

### `LopHocToanTrungTam` — cách nhận ra "người quản trị" mà không hard-code vai trò

Giáo viên có `LopHoc.Xem` nhưng **không** có `LopHocToanTrungTam` → handler giới hạn họ trong lớp
được phân công. Admin có cả hai → thấy hết.

Suy từ **dữ liệu quyền**, không suy từ **tên nhóm quyền**: tên là chuỗi người dùng tự sửa được,
đổi tên nhóm "Quản trị viên" thành "Ban giám hiệu" không được phép làm mất quyền quản trị.

Phân biệt theo thao tác: cấp `Xem` mà không cấp `Sua` = xem được mọi lớp nhưng chỉ sửa lớp mình.

## Bốn nhóm quyền dựng sẵn

`TenantSeeder` tạo sẵn 4 nhóm khi lập trung tâm mới — xem
`Infrastructure/Persistence/Seed/NhomQuyenMacDinh.cs`:

| Nhóm | Tinh thần |
|---|---|
| **Quản trị viên** | Toàn quyền. Sinh bằng vòng lặp `ChucNang.TatCa` × `HanhDong` nên module thêm sau tự thuộc về nhóm này |
| **Giáo viên** | Trọn vẹn lớp mình dạy: toàn quyền buổi học, bài tập, bài kiểm tra |
| **Trợ giảng** | Như giáo viên nhưng **không xoá buổi học** và **không ra đề kiểm tra** |
| **Học viên** | Chỉ dữ liệu của mình; `DiemDanh.Them` để tự điểm danh |

Đây là **điểm khởi đầu, không phải luật cứng** — admin vào màn Phân quyền sửa từng ô, hoặc tạo
nhóm khác hẳn. Canh bởi `NhomQuyenMacDinhTests`.

### ⚠️ Thêm chức năng mới và trung tâm đã tồn tại

`TenantSeeder` chỉ chạy **một lần lúc tạo trung tâm**. Thêm hằng vào `ChucNang` thì trung tâm lập
trước đó **không có hàng nào** trong `QUYEN_CHUC_NANG` cho hằng mới → admin của họ nhận 403 trên
toàn bộ tính năng mới. Triệu chứng rất khó chẩn: đăng nhập được, mọi màn cũ chạy bình thường, chỉ
màn mới hỏng — dễ bị quy oan cho frontend.

`BoKhuyetQuyenQuanTri` chạy lúc khởi động vá chuyện này: cấp cho nhóm **"Quản trị viên"** mọi cặp
(chức năng, thao tác) còn thiếu. **Idempotent, chỉ thêm không xoá** — admin đã cố ý bỏ bớt một ô
thì lần khởi động sau không được lặng lẽ cấp lại.

Nó **cố tình không đụng** ba nhóm còn lại: sửa nhóm "Giáo viên" là quyết định của admin, hệ thống
tự nới quyền cho họ là lỗ hổng. Hệ quả cần biết: thêm module mới thì **giáo viên/trợ giảng/học
viên của trung tâm cũ phải được admin cấp quyền bằng tay** ở màn Phân quyền.

Thêm module mới → thêm hằng số **và** cập nhật bảng này trong cùng PR.

## Frontend PHẢI gác nút, không phó mặc backend (17/09/2026)

Backend chặn đủ thì dữ liệu vẫn an toàn — nhưng **nút hiện ra mà bấm vào nhận 403 vẫn là lỗi**:
người dùng không hiểu vì sao, và tệ hơn, họ tưởng mình vừa làm hỏng dữ liệu.

Chủ sản phẩm báo: *"học viên vẫn có thể sinh lịch học và điểm danh trong buổi học"*. Kiểm bằng tài
khoản học viên thật: mọi endpoint ghi đều trả **403** (`sinh-lich`, `chot`, `huy`) — backend đúng.
Lỗi nằm ở frontend: `LichVaDiemDanh.tsx` và `BangDiemDanh.tsx` **không gọi `coQuyen` một lần nào**,
nên học viên thấy đủ "Sinh lịch", "Sinh lại lịch", "Chốt buổi", "Lưu điểm danh" và ô chọn trạng
thái điểm danh của **cả lớp**.

### Lối vào dễ sót nhất: tab gác bằng chức năng, không kèm thao tác

```
{ ma: 'diem-danh', khoa: '...', can: 'DiemDanh' }   // mặc định là `Xem`
```

`DiemDanh.Xem` là quyền **học viên CÓ** (để xem điểm danh của mình), nên tab mở được và bảng hiện
ra với đủ ô sửa. Gác tab bằng chức năng thì phải hỏi tiếp: *bên trong tab có gì cần quyền GHI?*

### Gác đúng thao tác của ĐÚNG endpoint, đừng gom một cờ

Suýt sai ở bản sửa đầu: gom "Thêm buổi" chung cờ với "Sinh lịch". Hai nút gọi hai endpoint khác
nhau, gác hai quyền khác nhau:

| Nút | Endpoint | Quyền | Giáo viên có? |
|---|---|---|---|
| Sinh lịch / Sinh lại lịch | `sinh-lich` | `LopHoc.SinhLich` | ❌ (việc của điều phối) |
| Thêm buổi | `sinh-them-buoi` | `BuoiHoc.Them` | ✅ |
| Mở bảng điểm danh | `diem-danh` | `DiemDanh.Sua` | ✅ |
| Chốt buổi | `chot` | `DiemDanh.Chot` | ✅ |

Gom một cờ thì giáo viên mất nút "Thêm buổi" — sửa một lỗi, tạo một lỗi khác.

### Test phải kiểm CẢ HAI CHIỀU

"Ẩn hết cho chắc" cũng làm test học viên xanh. `e2e/hoc-vien-chi-xem-buoi-hoc.spec.ts` kiểm trong
cùng một file: học viên **không** thấy nút ghi nào và bảng điểm danh bị khoá; giáo viên **vẫn**
thấy "Thêm buổi", "Chốt buổi", "Lưu điểm danh" và sửa được bảng.

## Cache

Truy vấn quyền chạy ở **mọi request** → cần cache **ngắn hạn** (in-memory hoặc Redis, TTL vài phút),
khóa theo `{tenant_id}:{nguoi_dung_id}`.

**Bắt buộc invalidate cache khi:** sửa nhóm quyền (FR-05), gán/gỡ quyền khỏi tài khoản (FR-03), vô hiệu
hóa tài khoản. Quyền bị thu hồi mà cache còn sống là lỗ hổng bảo mật, không phải chỉ là chuyện dữ liệu cũ.

### Cache Ở FRONTEND: đổi phiên phải xoá sạch (17/09/2026)

Cache phía trình duyệt là một **cái bẫy riêng**, không dính gì tới cache backend ở trên. Mọi
`queryKey` của TanStack Query trong app đều là **hằng** — `['toi-quyen']`, `['toi-he-thong']`,
`['nguoi-dung-ngan']`… — tức **không mang danh tính người đăng nhập**. Cộng với
`staleTime: Infinity` ở `useQuyen`, đăng xuất rồi đăng nhập nick khác sẽ **dùng lại nguyên cache
của nick cũ**: học viên nhìn thấy menu quản trị, phải Ctrl+Shift+R mới đúng.

Chữa ở **một chỗ** — `queryClient.clear()` trong cả `dangNhap` và `dangXuat`
(`frontend/src/lib/auth.tsx`) — chứ không thêm `username` vào từng khoá: ~20 chỗ phải nhớ, và
chỗ thứ 21 thêm sau này sẽ quên. Hai lần gọi là **thừa có chủ ý**: `dangNhap` lo *đúng* (vào
thẳng không qua `dangXuat` — phiên hết hạn ở tab khác), `dangXuat` lo *kín* (máy dùng chung,
dữ liệu người trước không nằm lại trong RAM tab).

> ⚠️ **Test cho lỗi này không được dùng `page.goto('/dang-nhap')`.** `goto` tải lại trang ⇒ cache
> mất sạch ⇒ test xanh **kể cả khi gỡ hết bản sửa** — nó kiểm đúng cái đường đã lành. Phải **bấm
> nút Đăng xuất** rồi điền form, đường người dùng thật đi. Xem
> `frontend/e2e/doi-nick-khong-giu-quyen-cu.spec.ts`.

Nhắc lại cho rõ: đây **không phải lỗ hổng bảo mật** — backend vẫn 403 mọi endpoint ngoài quyền.
Nhưng menu bấm vào chỉ nhận lỗi là sai với người dùng, và dữ liệu nghiệp vụ đã tải sẵn thì đúng
là của phiên trước.

## Quan hệ với multi-tenant

Hai tầng độc lập, **cả hai đều phải đúng**:

- **Tenant** trả lời "được thấy dữ liệu của trung tâm nào" — xem [multi-tenant.md](multi-tenant.md).
- **Quyền** trả lời "được làm gì với dữ liệu trong trung tâm đó".

Bảng `QUYEN` cũng có `tenant_id` — nhóm quyền của trung tâm A không áp dụng cho trung tâm B.

## Admin mặc định

Tài khoản `admin` mỗi tenant có toàn quyền. Cách triển khai: seed một nhóm quyền "Quản trị viên" đầy đủ
`xem/them/sua/xoa` cho mọi chức năng (xem [seed data](../05-database/quy-uoc-migration.md#seed-data)) —
**không** hard-code nhánh `if (user.IsAdmin) return true` bỏ qua hệ phân quyền, để một cơ chế duy nhất
quyết định mọi truy cập.

Riêng thao tác **đổi mật khẩu cho tài khoản khác** là đặc quyền chỉ Admin có (FR-03) — biểu diễn bằng
một `ten_chuc_nang`/`hanh_dong` riêng, không bằng ngoại lệ trong code.
