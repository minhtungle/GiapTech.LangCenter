# 16/09/2026 — HRM gộp một trang ba tab, bấm sĩ số ra danh sách người

## Yêu cầu

Chủ sản phẩm nêu hai việc:

> - cơ cấu tổ chức đang chưa xem được chi tiết danh sách nhân sự
> - hồ sơ nhân viên, cơ cấu, chức vụ đang bị tách biệt, cần tìm cách bố trí hợp lý để thuận tiện
>   thao tác

Hai việc này là **một gốc**: ba màn nói về cùng một tập người (`NGUOI_DUNG` mang cả
`phong_ban_id` lẫn `chuc_vu_id`) nhưng giao diện tách rời, nên cây cơ cấu hiện sĩ số mà không có
đường nào xem *ai*, và muốn đổi chức vụ một người thì phải đi qua sidebar hai ba lần.

## Đã làm

**Backend** — `/nhan-su` thêm ba tham số lọc: `phongBanId`, `gomPhongBanCon`, `chucVuId`.

`gomPhongBanCon` đi xuống **mọi cấp**, dựng tập id trong bộ nhớ (BFS có chặn số vòng) chứ không
recursive CTE. Lý do không dùng CTE: EF Core không sinh được nếu không viết SQL thô, mà SQL thô sẽ
**mất Global Query Filter** của multi-tenant (quy tắc #2). Cây phòng ban một trung tâm cỡ vài chục
dòng nên một truy vấn lấy hết rồi lan trong bộ nhớ vừa rẻ vừa dịch ra `WHERE ... IN (...)`.

Tham số truyền theo **tên** vào `LayDanhSachNguoiDungQuery`: query nay có bốn tham số `Guid?` liền
nhau, truyền theo vị trí thì chèn thêm một bộ lọc vào giữa là lệch im lặng — "lọc theo phòng" hoá
ra "lọc theo chức vụ", không có lỗi biên dịch.

**Frontend** — sidebar 3 mục còn 1 (`/hrm`), ba màn thành ba tab lưu ở `?tab=`. Đường cũ chuyển
hướng sang tab tương ứng. Sĩ số trên cây thành link sang tab Nhân sự đã lọc sẵn.

Ba màn con **giữ nguyên là component riêng**, không nhồi vào một file. `key={tab}` để đổi tab là
dựng lại màn con — cần vì `CoCauToChuc` giữ state cây trong thư viện ngoài React.

## Lỗi tự gây, và vì sao chỉ E2E bắt được

Cây **chỉ đếm người `DangLamViec`** (`PhongBanDtos.Handle`), còn `/nhan-su` mặc định trả cả người
đã nghỉ. Nên bấm vào sĩ số `1` lại ra `2` dòng.

Đứt ở **hai** mắt, mà mỗi mắt tự nó trông vẫn đúng:

1. Link trên cây không mang `trangThaiNhanSu=DangLamViec`.
2. Màn Nhân sự khởi tạo `locNhanSu` bằng `null`, **không đọc** tham số đó từ URL — nên kể cả khi
   link mang theo, nó vẫn bị bỏ qua.

**Không test backend nào đỏ** vì backend hoàn toàn đúng: API lọc chính xác theo tham số nó nhận
được. Chuỗi hỏng nằm ở chỗ bắc qua ba lớp — link → router → state khởi tạo → tham số API → số dòng
— và chỉ E2E đi hết chuỗi đó mới thấy.

Đáng ghi lại: lần đầu sửa mắt (1) **bị mất** do hai lần ghi file đè lên nhau; test E2E chạy lại vẫn
đỏ đúng chỗ cũ nên phát hiện ngay. Nếu tin vào "đã sửa rồi" mà không chạy lại thì lỗi đã lọt.

## Một bẫy test suýt mắc

Bản đầu có test *"lọc phòng ban không làm lọt học viên vào màn nhân sự"*. Nhưng học viên **không
bao giờ** có `phong_ban_id`: cả `KiemTraPhongBan` (lúc tạo/sửa) lẫn `XepNhanSuVaoPhongBan` (lúc xếp
từ cây) đều ném `HOC_VIEN_KHONG_VAO_CO_CAU`. Tập "học viên có phòng ban" luôn rỗng ⇒ assert đó
**xanh kể cả khi gỡ sạch bộ lọc** — test rỗng.

Đã thay bằng test kiểm **chính chốt chặn**, cả hai đường vào cơ cấu.

## Kiểm bằng đột biến

| Đột biến | Kết quả |
|---|---|
| Gom con chỉ đi một cấp (bỏ enqueue) | ĐỎ — `Gom_phong_ban_con_di_xuong_du_moi_cap` |
| Bỏ bộ lọc chức vụ | ĐỎ — `Loc_theo_chuc_vu` |
| `GomPhongBanCon` mặc định `true` | ĐỎ — 2 test |
| Gỡ `trangThaiNhanSu` khỏi link trên cây | ĐỎ — E2E |
| Màn Nhân sự không đọc tham số từ URL | ĐỎ — E2E |

Cây **ba tầng** (Khối → Phòng → Tổ) là bắt buộc cho đột biến thứ nhất: cây hai tầng không phân biệt
được "một cấp con" với "mọi cấp con".

## Một test cũ phải sửa

`sidebar.spec.ts` khẳng định sau khi chuyển sang HRM thì đường dẫn khớp `/^\/hrm\//`. Nay trang đích
của HRM là `/hrm` (không có đoạn sau) nên đỏ. Đây là **assert lạc hậu, không phải lỗi sản phẩm**:
ý định của test là "URL đi theo bộ chuyển hệ thống", mà `Layout` suy hệ thống con bằng
`startsWith('/hrm')` nên `/hrm` nhận đúng. Đã nới thành `/^\/hrm(\/|$)/` kèm lý do.

## Kết quả

514 test backend · 28 frontend · 32 E2E — xanh hết.
