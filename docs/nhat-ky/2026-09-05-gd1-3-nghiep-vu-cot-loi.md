# 05/09/2026 — LMS giai đoạn 1–3: lớp học, điểm danh, học liệu

Ba giai đoạn liền, ghi gộp. Commit: `d180e79`, `04be01e`, `a295268`.

## Giai đoạn 1 — Lớp học (FR-07, FR-08)

`LOP_HOC` + `LOP_HOC_HOC_VIEN` + `LOP_HOC_TRO_GIANG`.

**Hai bảng trung gian riêng, không gộp** thành một bảng có cột `vai_tro`: học viên có ngày vào
lớp, trạng thái, mức học phí riêng; trợ giảng không có gì trong số đó. Gộp thì nửa số cột luôn
NULL, và mọi query học viên phải nhớ `WHERE vai_tro = 1` — quên một lần là trợ giảng lọt vào
bảng điểm danh.

**`hoc_phi_ap_dung` snapshot lúc ghi danh**: sửa học phí lớp không đổi hồi tố công nợ người đã
đóng. Đây là cột giai đoạn 4 dựa vào.

**Tên lớp duy nhất nhưng lớp nháp không chiếm tên** —
`UNIQUE(tenant_id, ten) WHERE trang_thai <> 0`. Nếu tính cả nháp: admin A tạo nháp rồi bỏ
ngang, ba tháng sau admin B tạo lớp cùng tên và nhận lỗi trùng, trong khi danh sách lớp không
hiện nháp nên B không thấy lớp nào trùng cả.

**`IPhamViLopHoc`** — tầng phạm vi "lớp mình phụ trách". `[RequirePermission]` chỉ gác cửa
endpoint, Query Filter chỉ lọc tenant; phạm vi bên trong tenant là khoảng trống thật. Áp cho
**cả list lẫn detail** — detail là chỗ dễ quên, và quên là IDOR.

## Giai đoạn 2 — Buổi học & điểm danh (FR-09, FR-10)

### Bỏ cột `ngay_hoc` đã định làm

Kế hoạch ban đầu có 3 cột: `ngay_hoc` (để lọc/nhóm theo ngày) + `bat_dau` + `ket_thuc`. Bỏ
`ngay_hoc` sau khi nhận ra nó **âm thầm lệch** nếu trung tâm đổi múi giờ — cột dẫn xuất được
tính một lần rồi đông cứng. Lọc theo khoảng thời gian tuyệt đối dùng cùng index và không bao
giờ trôi.

### Điểm danh giữ hai cột trạng thái

| Cột | Ý nghĩa |
|---|---|
| `trang_thai_tu_khai` (nullable) | Học viên tự khai. Null ≠ Vắng — không tự điểm danh là bình thường |
| `trang_thai_chinh_thuc` (NOT NULL) | **Nguồn sự thật duy nhất cho mọi báo cáo** |

Gộp một cột thì sau khi giáo viên xác nhận, **không còn biết học viên đã khai gì**. Tranh chấp
"em có check-in mà sao bị tính vắng" là tình huống thật, và tỷ lệ khai sai là dấu hiệu gian lận.
Muốn tính lại về sau phải đổi schema *và* không có dữ liệu quá khứ. Chi phí giữ: một cột
`integer` nullable.

### Tự điểm danh: không có tham số để lạm dụng

Endpoint riêng `POST /buoi-hoc/{id}/tu-diem-danh`, command **không mang `hocVienId`** — lấy từ
`ICurrentUser`. Không phải "handler nhớ kiểm" mà là không có chỗ để truyền id người khác.
Khung giờ hợp lệ `[bắt đầu − 15 phút, kết thúc]`.

`SinhLichBuoiHoc` là **hàm thuần** trong Domain, 19 unit test, chặn `SoBuoiToiDa = 500` và
`SoNgayQuetToiDa = 3650` để một tham số sai không sinh ra vòng lặp vô tận.

### Container Alpine thiếu múi giờ

`TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` ném lỗi trong container. Kiểm bằng
cách build 4 container khác nhau: cần **cả `tzdata` lẫn `icu-libs`**. Có tài liệu nói phải đặt
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` — thử trực tiếp thì không cần, `icu-libs` mới là
mảnh thiếu.

## Giai đoạn 3 — Học liệu (FR-11 → FR-13)

### `TEP_DINH_KEM`: một bảng, 5 FK nullable loại trừ nhau

Ba phương án đã cân:

| Phương án | Vì sao loại |
|---|---|
| Mảng `jsonb` trong bảng cha | Không dọn được tệp mồ côi (phải quét mảng của 5 bảng), không mang metadata UI cần, và mảng không có `tenant_id` |
| 5 bảng riêng | 5 config gần giống hệt nhau; job dọn rác phải UNION cả 5 |
| **Một bảng, 5 FK loại trừ** | Được **cả FK cứng thật** (Cascade tự dọn hàng DB) **và** một bảng duy nhất để quét |

Ràng buộc "đúng một cột khác null" bằng `CHECK` ở tầng DB, không dựa validate ở handler.

**Nộp nhiều lần**: `BAI_NOP.lan_nop` + `UNIQUE(bai_tap_id, hoc_vien_id, lan_nop)`. Bài mới nhất
là `MAX(lan_nop)`.

**Chấm điểm qua endpoint riêng**, command **không có trường nội dung** — giáo viên không sửa
được bài của học viên.

### Lỗ hổng: `Anh.Xoa` không nói xoá tệp *nào*

`XoaTepCommand` ban đầu chỉ kiểm quyền, không kiểm chủ sở hữu. Học viên có `Anh.Xoa` (cần, để
gỡ tệp mình đính nhầm) nên **xoá được tệp trong bài nộp của bạn cùng lớp** — và id tệp hiện
ngay trong danh sách bài nộp, không cần đoán.

Vá bằng `BaoDamLaChuTep`: bài nộp/bài làm kiểm theo chủ sở hữu, còn lại kiểm theo phạm vi lớp.
Trả **404 chứ không 403** — 403 xác nhận tệp đó tồn tại.

Cùng đợt: 4 nhóm quyền dựng sẵn chỉ có `Anh.Xem` nên đính kèm tệp trả 403. Sửa ma trận
(giáo viên/trợ giảng `ToanBo`, học viên `Xem/Them/Xoa`).

## Còn nợ sau ba giai đoạn

- **Bài kiểm tra** (`BAI_KIEM_TRA`, `BAI_LAM`): có schema và cách ly tenant, **chưa có API/UI**.
- Kiểm trùng lịch giáo viên có API nhưng chưa nối vào UI.
- Job dọn tệp mồ côi trong MinIO.
- Danh mục ngày nghỉ hệ thống — sinh lịch hiện không né ngày lễ.
