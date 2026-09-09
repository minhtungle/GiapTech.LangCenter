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

## Danh mục chức năng

`ten_chuc_nang` là **danh mục đóng** — định nghĩa bằng hằng số trong code (không để người dùng tự nhập
chuỗi tùy ý), tương ứng các module nghiệp vụ:

Từ 08/09/2026, mỗi chức năng thuộc **một trong ba hệ thống** (HRM · CRM · LMS) hoặc nhóm
**dùng chung** — xem mục [Ba hệ thống con](#ba-hệ-thống-con-hrm--crm--lms) bên dưới.

| `ten_chuc_nang` | Hệ thống | Module | FR |
|---|---|---|---|
| `NhanSu` | HRM | [Hồ sơ nhân sự](../nghiep-vu/hrm.md) — nhân viên · giáo viên · trợ giảng. Đổi tên từ `GiaoVienNhanSu` (09/09) vì nó gác cả ba vai trò | FR-03, FR-23 |
| `ChucVu` | HRM | [Danh mục chức vụ](../nghiep-vu/hrm.md#fr-24--danh-mục-chức-vụ) — "Ban quản lý", "Trưởng phòng"… | FR-24 |
| `PhongBan` | HRM | [Cơ cấu tổ chức](../nghiep-vu/hrm.md#fr-22--cơ-cấu-tổ-chức): cây phòng ban, người quản lý, xếp nhân sự | FR-22 |
| `KhachHang` | CRM | [Khách hàng](../nghiep-vu/crm.md#fr-17--khách-hàng) — tất cả khách, kể cả chưa mua | FR-17 |
| `DoanhThu` | CRM | [Doanh thu](../nghiep-vu/crm.md) — đơn hàng, sổ thu, yêu cầu xếp lớp | FR-18, FR-21 |
| `KhoaHoc` | CRM | [Khoá học](../nghiep-vu/crm.md) — danh mục khoá bán ra | FR-19 |
| `SanPham` | CRM | [Sản phẩm](../nghiep-vu/crm.md) — sách, học cụ | FR-20 |
| `TaiKhoan` | Dùng chung | [Quản trị](../nghiep-vu/quan-tri-he-thong.md) | FR-03 |
| `PhanQuyen` | Dùng chung | [Quản trị](../nghiep-vu/quan-tri-he-thong.md) | FR-05 |
| `ThietLapChung` | Dùng chung | [Quản trị](../nghiep-vu/quan-tri-he-thong.md) | FR-06 |
| `Anh` | Dùng chung | Ảnh dùng chung (logo, ảnh bìa, QR, ảnh đại diện) | — |
| `DoiMatKhauNguoiKhac` | Dùng chung | [Quản trị](../nghiep-vu/quan-tri-he-thong.md) | FR-03 |
| `LopHoc` | LMS | [Lớp học](../nghiep-vu/lop-hoc.md) | FR-07, FR-08 |
| `BuoiHoc` | LMS | [Buổi học](../nghiep-vu/buoi-hoc-diem-danh.md) | FR-09 |
| `DiemDanh` | LMS | [Điểm danh](../nghiep-vu/buoi-hoc-diem-danh.md) | FR-10 |
| `BaiTap` | LMS | [Bài tập](../nghiep-vu/hoc-lieu.md) giao trong buổi | FR-11 |
| `BaiNopBaiTap` | LMS | [Bài học viên nộp](../nghiep-vu/hoc-lieu.md) | FR-12 |
| `BaiKiemTra` | LMS | Bài kiểm tra | *(có schema, chưa API/UI)* |
| `BaiLamKiemTra` | LMS | Bài làm của học viên | *(có schema, chưa API/UI)* |
| `TaiLieu` | LMS | [Tài liệu giảng dạy](../nghiep-vu/hoc-lieu.md) | FR-13 |
| `HocPhi` | LMS | [Học phí](../nghiep-vu/hoc-phi.md) | FR-14 |
| `ThongKe` | LMS | Thống kê / dashboard | *(chưa làm)* |
| `LopHocToanTrungTam` | LMS | **Phạm vi**, không phải module — xem dưới | — |
| `NhatKyHeThong` | Dùng chung | [Nhật ký thao tác](../nghiep-vu/nhat-ky-he-thong.md). Chỉ `Xem` có nghĩa — nhật ký chỉ ghi thêm | FR-16 |

**24 chức năng** — danh sách này phải khớp `ChucNang.TatCa`; canh bởi
`NhomHeThongTests.Moi_chuc_nang_phai_duoc_khai_he_thong_hoac_dung_chung`.

### Vì sao tách nhỏ tới mức này

Tiêu chí gộp/tách: **so cột-theo-cột trong ma trận phân quyền; khác một ô là tách**.

- `BaiTap` vs `BaiKiemTra`: trợ giảng **toàn quyền** với bài tập nhưng **chỉ xem** bài kiểm tra.
  Gộp lại thì không diễn đạt nổi khác biệt đó.
- `BaiTap` vs `BaiNopBaiTap`: học viên **tạo** bài nộp nhưng **không tạo** bài tập.

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

### Vì sao có nhóm "dùng chung"

`TaiKhoan`, `PhanQuyen`… **không thuộc hệ thống nào**. Ép chúng vào một hệ thống sẽ sai theo cả
hai hướng: người quản trị nhân sự cần sửa tài khoản nhưng không cần vào LMS, còn nhật ký thì ghi
thao tác của cả ba hệ thống. Nhóm này hiện ở sidebar và ở **mọi tab** của màn phân quyền.

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

## Cache

Truy vấn quyền chạy ở **mọi request** → cần cache **ngắn hạn** (in-memory hoặc Redis, TTL vài phút),
khóa theo `{tenant_id}:{nguoi_dung_id}`.

**Bắt buộc invalidate cache khi:** sửa nhóm quyền (FR-05), gán/gỡ quyền khỏi tài khoản (FR-03), vô hiệu
hóa tài khoản. Quyền bị thu hồi mà cache còn sống là lỗ hổng bảo mật, không phải chỉ là chuyện dữ liệu cũ.

## Quan hệ với multi-tenant

Hai tầng độc lập, **cả hai đều phải đúng**:

- **Tenant** trả lời "được thấy dữ liệu của trung tâm nào" — xem [multi-tenant.md](./multi-tenant.md).
- **Quyền** trả lời "được làm gì với dữ liệu trong trung tâm đó".

Bảng `QUYEN` cũng có `tenant_id` — nhóm quyền của trung tâm A không áp dụng cho trung tâm B.

## Admin mặc định

Tài khoản `admin` mỗi tenant có toàn quyền. Cách triển khai: seed một nhóm quyền "Quản trị viên" đầy đủ
`xem/them/sua/xoa` cho mọi chức năng (xem [seed data](../database/quy-uoc-migration.md#seed-data)) —
**không** hard-code nhánh `if (user.IsAdmin) return true` bỏ qua hệ phân quyền, để một cơ chế duy nhất
quyết định mọi truy cập.

Riêng thao tác **đổi mật khẩu cho tài khoản khác** là đặc quyền chỉ Admin có (FR-03) — biểu diễn bằng
một `ten_chuc_nang`/`hanh_dong` riêng, không bằng ngoại lệ trong code.
