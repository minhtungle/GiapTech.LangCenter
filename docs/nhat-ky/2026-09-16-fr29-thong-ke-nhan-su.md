# 16/09/2026 — FR-29: thống kê nhân sự + module tiêu chí đánh giá

## Yêu cầu

> - tôi cần thêm phần thống kê tại HRM
> - cũng tương tự thống kê tại crm nhưng chỉ cho nhân viên kinh doanh, giáo viên và trợ giảng
> - kinh doanh: xếp hạng theo doanh thu, số học viên, chất lượng chăm sóc khách hàng
> - giáo viên: số lớp, chất lượng giảng dạy (do học viên đánh giá với lớp), số buổi dạy đủ
> - trợ giảng: tương tự giáo viên
> - thêm các tiêu chí để học viên chấm theo thang 5 thay vì chỉ nhận xét — có thể tạo riêng 1
>   module các tiêu chí đánh giá trong HRM

## Rà trước khi thiết kế

Kiểm từng chỉ số xem đã có dữ liệu chưa, thay vì tin bảng trạng thái:

| Chỉ số | Có sẵn? |
|---|---|
| Doanh thu, số học viên | ✅ CRM (`DANG_KY_KHOA_HOC`, `KHACH_HANG.nguoi_dung_id`) |
| Số lớp (GV/TG) | ✅ `LOP_HOC.giao_vien_chinh_id`, `LOP_HOC_TRO_GIANG` |
| Số buổi dạy | ✅ `BUOI_HOC` + `TrangThai` |
| **Chất lượng chăm sóc** | ❌ không có bảng nào |
| **Chất lượng giảng dạy** | ⚠️ có `MucHaiLong` 1–5 nhưng **một điểm duy nhất**, không theo tiêu chí |

Nên phần "tiêu chí thang 5" đúng là thứ phải làm mới, còn hai cái đầu chỉ là tổng hợp lại.

## Ba câu hỏi phải chốt trước khi code

**Ai chấm nhân viên kinh doanh?** Cân ba phương án, chủ sản phẩm chốt **quản lý chấm theo kỳ**:

| Phương án | Vì sao không chọn |
|---|---|
| Khách hàng chấm (ghi kèm lần chăm sóc) | chính nhân viên ghi hộ ⇒ tự chấm mình |
| Học viên chấm | 63/65 học viên **chưa có tài khoản** ⇒ gần như không có phiếu |

**Chấm giảng dạy ở đâu?** Giữ **theo buổi** (mở rộng chỗ đã có) thay vì phiếu cuối khoá — dữ liệu
dày hơn (mỗi lớp ~12 buổi), và phiếu cuối khoá vướng nợ N26 (chưa có endpoint kết thúc lớp).

## Ba chỗ tính sai mà không có gì báo

**1. `BUOI_HOC.giao_vien_id = null` nghĩa là "giáo viên chính của lớp"**, không phải "không có
giáo viên". Đây là cái bẫy nặng nhất: đếm thẳng cột đó thì **mọi giáo viên ra 0 buổi**, mà trên dữ
liệu thật W686AE9 cả **12/12 buổi đều null**. Không ngoại lệ nào ném ra — chỉ là bảng toàn số 0,
và người đọc sẽ kết luận "giáo viên không dạy buổi nào". Đột biến bỏ fallback làm đỏ 3 test.

**2. Chỉ buổi `DaHoanThanh` là "dạy đủ"** — buổi mới lên lịch chưa phải công.

**3. Chưa ai chấm thì trả `null`, không phải `0`.** Trên bảng xếp hạng, "chưa có đánh giá" và "bị 0
điểm" dẫn tới hai kết luận trái ngược về cùng một người.

## Test canh kiến trúc bắt đúng chỗ

`RanhGioiHeThongConTests` đỏ ngay khi tôi vừa viết handler: HRM đọc `db.KhachHangs`,
`db.DangKyKhoaHocs` và cả `DaoTao`. Đây **không phải** lỗi cần lách — nó là cầu nối nghiệp vụ thật,
vì bản chất yêu cầu là *"đánh giá con người bằng kết quả công việc"* mà công việc nằm ở hai hệ
thống kia. Đã khai vào `CauNoiDuocPhep` kèm lý do và ghi vào ADR-0005 (kèm bảng liệt kê **cả bốn**
cầu nối hiện có, để sau này tách source thì đếm được phải cắt mấy sợi dây).

FR-29 là cầu nối rộng nhất tới nay. Giới hạn tự đặt: chỉ ĐỌC qua `IAppDbContext`, không gọi handler
hệ thống khác, và không đọc cột tiền nào của LMS.

## Bug tự gây: trường rơi ở ranh giới controller

Thêm `DiemTieuChis` vào `GuiNhanXetBuoiHocCommand`, sửa handler, sửa DTO đọc, sửa frontend — nhưng
`BuoiHocController` dùng một **DTO riêng cho thân request** (`GuiNhanXetBody`, để `id` lấy từ
route). Quên khai trường ở đó nên:

- request của học viên có `diemTieuChis` hợp lệ,
- controller dựng command với `DiemTieuChis = null`,
- handler thấy `null` → hiểu là "giữ nguyên điểm cũ" → không ghi gì,
- API trả **200**, không lỗi nào, `diemTieuChis` trong response là `[]`.

Tôi phát hiện vì test đầu-cuối đỏ, rồi xác nhận bằng Swagger schema (`GuiNhanXetBody` chỉ có
`noiDung`, `mucHaiLong`). Đã ghi cảnh báo ngay tại DTO đó. Đây là loại bug mà build xanh, lint
xanh, unit test xanh — chỉ chuỗi thật bắt được.

## Một chi tiết của fixture làm test đỏ sai chỗ

Ba test buổi học đỏ với "expected 2, actual 0". Không phải logic sai: fixture sinh lịch từ
`06/10/2026`, mà **kỳ mặc định của endpoint là 12 tháng gần nhất tính đến hôm nay (16/09/2026)** —
buổi học nằm ở *tương lai*. Đã thêm hằng `KyCoLich` truyền khoảng tường minh, kèm chú thích để lần
sau không ai mất thời gian lần lại.

## Kiểm bằng đột biến

| Đột biến | Kết quả |
|---|---|
| Bỏ fallback `?? LopHoc.GiaoVienChinhId` | ĐỎ 3 test |
| Đếm mọi buổi thay vì chỉ `DaHoanThanh` | ĐỎ 2 test |
| Điểm `null` → `0` | ĐỎ 1 test |
| Frontend gửi `diems: []` | ĐỎ E2E |

## Kiểm chứng trên dữ liệu thật

Tạo 4 tiêu chí mẫu trong W686AE9 rồi chụp màn: bảng kinh doanh ra đúng 4 sale với doanh thu
187/164/136/135 tr; chấm 5 và 4 qua UI thì cột chất lượng hiện **4.5 (2)** ngay. Bảng giáo viên ra
4 lớp mỗi người và `0` buổi dạy đủ — **đúng**, vì tenant thật chưa chốt buổi nào; hai khung biểu đồ
hiện câu giải thích thay vì con số 0 trơ.

Migration **chỉ thêm 3 bảng**, không đụng cột nào đang có: đã backup, dry-run trên DB bản sao
(kiểm cả hai chiều của `CHECK` thang 5: điểm 9 bị chặn, điểm 4 vào được) rồi mới chạy thật.

## Tách thành hai module riêng (cùng ngày)

Bản đầu tôi làm hai tab thứ tư và thứ năm của `/hrm`. Chủ sản phẩm yêu cầu tách riêng ngay sau đó —
và khi làm mới thấy lý do mạnh hơn thẩm mỹ:

**Mỗi module gác bằng quyền riêng của nó.** `/hrm` gác `NhanSu`, nên trưởng phòng có
`ThongKeNhanSu.Xem` mà không có `NhanSu.Xem` sẽ **không thấy mục nào để vào**. Làm tab là vô tình
buộc hai quyền phải đi cùng nhau.

Ba tab còn lại vẫn gộp: cơ cấu · hồ sơ · chức vụ nói về *cùng một tập người*, đó là lý do gộp chúng
từ đầu. Hai màn mới khác hẳn — một là báo cáo toàn trung tâm, một là cấu hình danh mục.

### Một bẫy tôi mắc trong lúc tách

Mục sidebar `/hrm` có `cuoi: true` (chỉ khớp đúng đường dẫn, không khớp route con). Tôi bỏ cờ này
khi thêm hai mục mới — và `startsWith('/hrm')` lập tức khớp luôn `/hrm/thong-ke`, làm mục "Nhân sự"
sáng lên và **tiêu đề thanh trên hiện sai**. May là tôi đọc lại định nghĩa `MucMenu` trước khi
chạy, chứ ảnh chụp cũng khó thấy: cả hai mục đều thuộc nhóm "NHÂN SỰ".

Kéo theo một chi tiết nhỏ: `TieuChiDanhGia.tsx` phải bỏ `<h2>` tên màn, vì `Layout` đã hiện tiêu đề
ở thanh trên — giữ lại là hiện hai lần cùng một chữ. Kiểm bằng cách so với các màn đứng riêng của
CRM (Khách hàng, Sản phẩm đều không có `<h2>`), không đoán.

Link `?tab=thong-ke` / `?tab=tieu-chi` cũ **vẫn chuyển hướng** sang đường mới, có test canh: rơi về
tab đầu thì người dùng tưởng tính năng bị xoá.

## Kết quả

554 test backend · 28 frontend · 36 E2E — xanh hết.
