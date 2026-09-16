# FR-15 — Thống kê / Tổng quan

Màn **đầu tiên** người dùng thấy sau khi đăng nhập (`/` → `TongQuan.tsx`). Một endpoint,
một màn, mọi vai trò.

## Nguyên tắc thiết kế

Hai nguyên tắc quyết định *cái gì được vào màn này*, giữ từ bản base và rút ra từ dự án trước:

1. **Mỗi con số phải bấm được để tới đúng chỗ xử lý.** Một con số không kèm đường đi tiếp chỉ
   làm người dùng biết có việc mà không biết làm ở đâu.
2. **Không làm dải số thống kê chỉ để lấp chỗ trống.** Nên ở đây **không có** "tổng số học
   viên", "tổng số lớp", "tổng số bài tập" — đó là thông tin để ngắm, không phải việc để làm.

Hệ quả: DTO chỉ có 5 số, và 3 trong đó là **việc tồn đọng** (hiện khi > 0), 2 là **bối cảnh**.

### Một màn cho mọi vai trò — không phải ba dashboard

Kế hoạch ban đầu ghi *"3 dashboard: Admin / Giáo viên / Học viên"*. Đã **bỏ** hướng đó
(12/09/2026):

`IPhamViLopHoc` đã lọc đúng phạm vi từng người — admin thấy toàn trung tâm, giáo viên thấy lớp
mình dạy, học viên thấy lớp mình học. Nên **cùng một truy vấn** cho ra con số đúng với từng
người, không cần biết vai trò là gì. Ba bản sao là ba chỗ phải sửa khi đổi, và trái với cách
dự án làm phân quyền: suy từ **dữ liệu quyền**, không hard-code vai trò (quy tắc #9).

### Không có số tiền nào

Kế hoạch giai đoạn 5 từng ghi *"cảnh báo nợ học phí dùng `TENANT.so_ngay_canh_bao_no_hoc_phi`"*.
Mục đó **không còn áp dụng**: từ 12/09/2026 LMS không quản lý và không hiển thị tiền học, chỉ
CRM nắm số tiền. → [Học phí](./hoc-phi.md)

Canh bởi test `Khong_co_truong_tien_nao_trong_DTO`.

## Endpoint

```
GET /api/v1/toi/tong-quan  →  TongQuanDto
```

Nằm ở `ToiController` cùng `/toi/lich`, `/toi/quyen` — cụm "dữ liệu của chính tôi". **Không có
`[RequirePermission]`**: mọi người đăng nhập đều thấy màn chủ, và phạm vi đã lọc theo người.
Lý do này khai trong danh sách ngoại lệ của `MoiEndpointPhaiDuocGacTests`.

## Năm con số + danh sách buổi sắp tới

| Trường | Nghĩa | Loại | Dẫn tới |
|---|---|---|---|
| `buoiQuaHanChuaDiemDanh` | Buổi `DaLenLich` đã qua giờ kết thúc mà chưa chốt điểm danh | việc | `/lms/lop-hoc` |
| `baiNopChuaCham` | Bài nộp có `diem == null` | việc | `/lms/lop-hoc` |
| `choXepLop` | Yêu cầu xếp lớp `DangCho` (FR-21) | việc | `/lms/lop-hoc?tab=cho-xep-lop` |
| `buoiHomNay` | Buổi học hôm nay, trừ buổi `DaHuy` | bối cảnh | — |
| `lopDangHoatDong` | Lớp không ở trạng thái `Nhap` / `DaHuy` / `DaKetThuc` | bối cảnh | — |

Dòng việc **chỉ hiện khi > 0**. Hết việc thì hiện đúng một câu, không phải danh sách toàn số 0:
người dùng vào đây để biết **phải làm gì**, và một danh sách số 0 bắt họ đọc để nhận ra không
có gì.

### Danh sách BUỔI SẮP TỚI (17/09/2026)

Yêu cầu chủ sản phẩm: *"ở phần tổng quan của học viên và giáo viên, đối với buổi sắp tới hãy hiện
đủ tên lớp, số buổi, thời gian và khi ấn thì chuyển thẳng tới xem chi tiết buổi học đó"*.

`buoiSapToi` — **ba buổi gần nhất** trong phạm vi người đang đăng nhập, mỗi dòng có:

| Hiện gì | Vì sao cần |
|---|---|
| Tên lớp | giáo viên dạy 4 lớp, con số "buổi hôm nay" không nói được lớp nào |
| Số buổi (`thuTu`) | người dạy và người học đều nói theo số này |
| Thời gian | quyết định có phải chuẩn bị ngay không |
| Phòng học / link online | thứ cần **ngay trước giờ học** |

Bấm cả dòng → `/lms/buoi-hoc/{id}`, **thẳng chi tiết buổi**, không phải màn lớp rồi tự tìm.

Trước đó Tổng quan chỉ có con số `buoiHomNay`: không nói được lớp nào, mấy giờ, và **không bấm
được** — trái đúng nguyên tắc số 1 của màn này (*"mỗi con số phải dẫn tới một màn xử lý"*). Con số
đó vẫn giữ làm bối cảnh.

**Ba chốt của truy vấn**, mỗi cái sai một kiểu khác nhau:

- **Gồm cả hôm nay**, không chỉ "từ ngày mai": buổi 18h tối nay vẫn là việc sắp tới lúc 8h sáng.
- **Mốc là `KetThuc >= bây giờ`, không phải `BatDau`**: buổi đang diễn ra dở vẫn là buổi người dùng
  cần mở (điểm danh, xem tài liệu) — lấy `BatDau` thì nó biến mất ngay khi chuông reo.
- **Bỏ buổi `DaHuy`**: không còn là việc phải làm.

Ba buổi chứ không phải toàn bộ: Tổng quan là chỗ liếc nhanh, danh sách dài thuộc về màn lịch. Khối
này **ẩn hẳn** khi không có buổi nào — khối rỗng chỉ làm loãng màn.

Phép so dùng **thời điểm tuyệt đối hai bên** nên không phụ thuộc múi giờ; chỉ `buoiHomNay` mới cần
cắt theo ngày của múi giờ trung tâm (mục dưới).

Canh bởi `e2e/tong-quan-buoi-sap-toi.spec.ts` — kiểm **đủ ba mẩu thông tin** trên một dòng và
**bấm tới đúng chi tiết buổi**. Ba đột biến kiểm đỏ: link tới màn lớp, bỏ số buổi, backend lấy buổi
đã qua.

### `choXepLop` trả 0 với người không xếp được lớp

Hàng chờ chỉ có nghĩa với người **điều phối được**. Endpoint hàng chờ gác bằng
`LopHoc` + `Sua`, nên handler kiểm đúng quyền đó trước khi đếm; không có thì trả `0`.

Trả số cho người không bấm được là mời họ đi vào ngõ cụt — đúng lỗi menu "Chờ xếp lớp" đã gặp
10/09/2026. Canh bởi `Hang_cho_ve_0_voi_nguoi_khong_duoc_xep_lop`, có **kiểm chiều ngược**:
cùng dữ liệu đó, người có quyền phải thấy số > 0 — nếu không, test sẽ xanh cả khi hàng chờ rỗng
vì lý do khác.

### "Hôm nay" theo múi giờ trung tâm, không phải UTC

`BUOI_HOC.bat_dau` lưu thời điểm tuyệt đối. Buổi 7h sáng giờ Việt Nam là 0h UTC **cùng ngày**,
nhưng buổi 6h sáng là 23h UTC **hôm trước** — lấy ngày theo UTC sẽ đẩy các buổi sáng sớm sang
hôm trước và đếm thiếu. Handler quy về khoảng `[0h, 24h)` của múi giờ trung tâm
(`IMuiGioTrungTam`) rồi mới so.

### Bài nộp đi qua hai chặng

`BAI_TAP` gắn **buổi học**, không gắn lớp trực tiếp. Nên đếm bài chưa chấm phải đi
`BaiNop → BaiTap → BuoiHoc → LopHocId` mới lọc được theo phạm vi.

## Phạm vi — lọc một lần, dùng lại

Handler gọi `phamVi.LocTheoPhamVi(db.LopHocs, HanhDong.Xem)` **một lần**, lấy tập `id` lớp rồi
dùng lại cho cả bốn số còn lại. Đây là lý do một DTO phục vụ được mọi vai trò.

Canh bởi `Giao_vien_chi_thay_lop_minh_day_con_admin_thay_ca_hai`.

## Liên quan

- [Lớp học](./lop-hoc.md) · [Buổi học & Điểm danh](./buoi-hoc-diem-danh.md) ·
  [Học liệu](./hoc-lieu.md) · [CRM — FR-21](./crm.md#fr-21--yêu-cầu-xếp-lớp-crm--lms)
- Ba tầng bảo vệ: [phân quyền động](../backend/phan-quyen-dong.md) ·
  [multi-tenant](../backend/multi-tenant.md)
