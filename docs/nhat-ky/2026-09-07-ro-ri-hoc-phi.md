# 07/09/2026 — Rò rỉ học phí qua DTO của module lớp học

Yêu cầu ban đầu nghe như việc bố cục: *"đưa thông tin nhạy cảm về học phí vào chung tab học
phí"*. Khảo sát ra hai lỗ hổng thật ở **tầng API**, không phải chuyện giao diện.

## Đo trước khi sửa

```
GET /lop-hoc  (giáo viên co.lan)          → hocPhi = 6000000
GET /lop-hoc/{id}/hoc-vien  (giáo viên)   → hocPhiApDung của TỪNG học viên
GET /lop-hoc/{id}/hoc-vien  (học viên 1)  → hocPhiApDung của CẢ BA bạn cùng lớp
```

Giáo viên biết ai được miễn giảm và giảm bao nhiêu. Học viên biết bạn mình đóng bao nhiêu.

## Vì sao `IPhamViHocPhi` không cứu được

Tầng phạm vi học phí được thiết kế đúng và bảo vệ tốt **module `/hoc-phi`** — giáo viên gọi
nhận 403, học viên nhận dữ liệu đã lọc. Nhưng số tiền còn nằm ở hai chỗ khác:

| DTO | Endpoint | Gác bằng |
|---|---|---|
| `LopHocDto.HocPhi` | `GET /lop-hoc`, `GET /lop-hoc/{id}` | `LopHoc.Xem` |
| `HocVienTrongLopDto.HocPhiApDung` | `GET /lop-hoc/{id}/hoc-vien` | `LopHoc.Xem` |

Cả ba vai trò đều có `LopHoc.Xem`. Tiền đi ra cửa của module *lớp học*, hoàn toàn vòng qua
cổng học phí.

Điều đáng nói: **ma trận quyền vẫn đúng** (giáo viên không hề có `ChucNang.HocPhi`), **tầng
phạm vi vẫn đúng**. Lỗi nằm ở chỗ trường tiền ở sai nhà. `IPhamViLopHoc` lọc **hàng nào** thấy
được, nó không có khái niệm **cột nào** — mà đây là vấn đề về cột.

So sánh cho rõ: `CongNoDto` cũng có `HocPhiApDung`, cũng đọc từ bảng `LOP_HOC_HOC_VIEN`, nhưng
đi qua `phamVi.LocHocVienTrongLop` nên an toàn. Cùng một cột, hai đường đi, một đường có gác.

## Vá: che cột, không lọc hàng

Thêm `IPhamViHocPhi.DuocXemTienCuaLop()` — công khai hoá điều kiện `ThayToanBoSo` vốn đã có.
Hai handler trả `null` thay vì số:

```csharp
xemTien ? l.HocPhi : null
xemTien || hv.HocVienId == toi ? hv.HocPhiApDung : null
```

Dòng thứ hai có ba mức: admin thấy tất, học viên thấy **của chính mình**, còn lại `null`. Học
viên cần biết mình phải đóng bao nhiêu — đó là quyền của họ, chỉ không được biết của người khác.

Giáo viên **vẫn thấy đủ danh sách học viên** để điểm danh và chấm bài; chỉ cột tiền là `null`.
Che cột chứ không cắt hàng.

Đo lại sau khi vá:

| Vai trò | `/lop-hoc` hocPhi | `/hoc-vien` hocPhiApDung |
|---|---|---|
| Admin | 6.000.000 | cả ba đều có số |
| Giáo viên | null | null, null, null |
| Trợ giảng | null | null, null, null |
| Học viên 1 | null | **6.000.000**, null, null |

## Bẫy phân quyền phát hiện thêm

`ThayToanBoSo` cần **cả** `HocPhi` **và** `LopHocToanTrungTam`. Nghĩa là cấp
`LopHocToanTrungTam.Xem` cho nhóm Giáo viên — nghe rất vô hại, "cho xem lịch mọi lớp" — sẽ mở
toàn bộ sổ thu nếu nhóm đó cũng có `HocPhi.Xem`. Nhóm mặc định an toàn, nhưng admin sửa được
ma trận. Đã ghi vào [FR-14](../nghiep-vu/hoc-phi.md).

## Bố cục: gom tiền về một tab

- **Tab Học phí** thêm ba con số đầu trang (tổng phải thu · đã thu · còn nợ) + thanh tiến độ.
- **Tab Tổng quan** bỏ hai ô "Đã thu"/"Còn nợ" và dòng học phí — nó không còn gọi API học phí
  nữa, nên cũng hết cảnh giáo viên mở tab và thấy hai ô `0₫` do 403.
- **Bảng danh sách lớp** bỏ cột Học phí.
- **Form thông tin lớp** bỏ ô học phí khi SỬA; giữ khi TẠO vì lúc đó chưa có tab Học phí để
  nhập. Ô ẩn thì **không gửi trường đó** — backend hiểu `null` là "giữ nguyên", nên mức học
  phí không bị xoá (quy tắc #1).
- **Tab Học viên** ẩn cột mức áp dụng khi dữ liệu trả về toàn `null` — hỏi dữ liệu chứ không
  hỏi vai trò, vì backend đã quyết định rồi.
- Vào tab Học phí mà không có quyền thì hiện thông báo rõ ràng, không phải bảng trống — bảng
  trống khiến người dùng tưởng lớp chưa ai đóng tiền.

## Kiểm chứng

- **234 test xanh** (54 unit + 180 integration), build 0 warning, frontend sạch.
- `RoRiHocPhiTests` — 7 test mới, mỗi lỗ hổng một test, cộng test chiều ngược
  `Quan_tri_van_thay_du_moi_so_tien` để không "vá" bằng cách che của tất cả mọi người.
- Chạy thật: cổng `/hoc-phi/*` trả 403 cho giáo viên và trợ giảng, 200 cho học viên nhưng lọc
  còn đúng 1 dòng công nợ và 2 khoản thu của chính họ.

## Còn nợ

- **N2**: menu "Học phí" ở sidebar vẫn hiện với giáo viên — bấm vào sẽ thấy thông báo không có
  quyền thay vì trang lỗi, nhưng đúng ra không nên hiện. Cần hook quyền dùng chung, không vá lẻ.
- Test hiện chỉ canh hai DTO đã biết. Không có cơ chế **tự động** phát hiện trường tiền mới
  lọt vào DTO của module khác — vẫn dựa vào người viết code nhớ quy tắc.
