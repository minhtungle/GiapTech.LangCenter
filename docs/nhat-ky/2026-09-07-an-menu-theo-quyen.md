# 07/09/2026 — Ẩn menu và nút theo quyền (đóng nợ N2)

Nợ N2 tồn từ giai đoạn 0: frontend không có bất kỳ cơ chế kiểm quyền nào, nên giáo viên vẫn
thấy menu "Học phí" rồi bấm vào mới biết mình không được phép. Nay đóng dứt điểm.

## Endpoint `GET /toi/quyen`

Trả toàn bộ quyền hiệu lực của phiên hiện tại, dạng `[{chucNang, hanhDong}]`.

**Không gác `[RequirePermission]`** — ai đăng nhập cũng phải biết quyền của chính mình, nếu
không frontend không dựng nổi menu. An toàn vì nó **không nhận tham số id**: không có đường dò
quyền người khác.

Không viết logic mới: `QuyenService.LayQuyenHieuLucAsync` đã tồn tại (private, phục vụ
`CoQuyenAsync`), chỉ công khai hoá. Nhờ vậy nó dùng chung cache 5 phút và chung cơ chế xoá
cache khi admin sửa nhóm quyền.

## Hook `useQuyen()`

```ts
coQuyen('HocPhi', 'Them')   // cho nút
an: !coQuyen('TaiLieu', 'Xoa')   // cho mục trong MenuThaoTac
xemTienCaLop()              // HocPhi.Xem VÀ LopHocToanTrungTam.Xem
```

`xemTienCaLop()` lặp đúng điều kiện `IPhamViHocPhi.ThayToanBoSo` ở backend. Học viên có
`HocPhi.Xem` nhưng không có `LopHocToanTrungTam` nên trả false — họ vẫn vào tab Học phí tra
công nợ của mình, chỉ không thấy con số của cả lớp.

**Hai quyết định về trải nghiệm:**

- Trong lúc **chưa biết quyền** thì hiện đủ menu. Ẩn trước rồi hiện lại làm menu nhấp nháy mỗi
  lần tải trang; bấm nhầm lúc đó cùng lắm nhận 403.
- Ẩn hết mục trong một nhóm thì **bỏ luôn nhóm**. Tiêu đề "Quản trị hệ thống" không có mục nào
  bên dưới trông như giao diện hỏng.

## Phạm vi áp dụng

| Chỗ | Trước | Sau |
|---|---|---|
| Menu sidebar | 6 mục cho mọi người | Lọc theo `<ChucNang>.Xem`, bỏ nhóm rỗng |
| Tab trong chi tiết lớp | 6 tab cho mọi người | Lọc theo quyền; gõ thẳng `?tab=hoc-phi` không có quyền thì rơi về Tổng quan |
| Nút Thêm | luôn hiện | `coQuyen(..., 'Them')` — 5 màn |
| Mục Sửa/Xoá trong menu | luôn hiện | `an: !coQuyen(...)` — 6 màn |

Kết quả đo trên hệ thống thật:

| Vai trò | Menu thấy được | Xem tiền cả lớp |
|---|---|---|
| Admin | Lớp học, Tài liệu, Học phí, Tài khoản, Phân quyền, Thiết lập | CÓ |
| Giáo viên / trợ giảng | Lớp học, Tài liệu, Tài khoản | không |
| Học viên | Lớp học, Tài liệu, Học phí | không |

## Dọn thêm hai thứ sót

- **`BaiTapCuaLop` và `TaiLieu` vẫn dùng nút icon rời** — hai màn này bị bỏ sót ở lượt gom
  `MenuThaoTac` trước. Nay đã gom.
- **`PhanQuyen` dùng `confirm()` của trình duyệt** để xác nhận xoá — trái nguyên tắc UI/UX của
  dự án (không style được, không dịch được, chặn luồng). Thay bằng `HopXacNhan`.

Rà lại toàn bộ `src/`: không còn `confirm()`/`alert()`, không còn nút icon rời trong bảng,
không còn màn nào có nút hành động mà chưa gác quyền.

## Ranh giới phải nhớ

**Ẩn ở frontend là tiện lợi, không phải bảo vệ.** Mọi endpoint vẫn tự gác quyền của nó, và dữ
liệu nhạy cảm phải được backend che TRƯỚC khi rời máy chủ — như vừa làm với học phí hôm nay.
Nếu chỉ ẩn ở client thì mở DevTools là thấy hết.

## Kiểm chứng

- **239 test xanh** (54 unit + 185 integration), build 0 warning, frontend sạch.
- `QuyenCuaToiTests` — 5 test: chưa đăng nhập nhận 401; admin đủ quyền; giáo viên không có
  `HocPhi` nhưng vẫn đủ quyền dạy; học viên có `HocPhi.Xem` mà không có `LopHocToanTrungTam`;
  mỗi người nhận đúng quyền của mình.

## Còn nợ

- Hook chưa xử lý trường hợp admin **vừa đổi quyền** của người đang đăng nhập: backend đã xoá
  cache nhưng frontend giữ `staleTime: Infinity`, người dùng phải tải lại trang. Chấp nhận
  được vì đổi quyền là việc hiếm.
