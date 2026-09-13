# 2026-09-14 — Thống kê CRM: ba thứ chỉ máy móc mới bắt được

Dựng màn thống kê CRM với biểu đồ. Ba lần trong ngày, thứ bắt được lỗi **không phải mắt tôi**:
một script kiểm màu, một lượt tiêm đột biến, và một ảnh chụp màn hình.

## Quyết định trước tiên: doanh số tính cho ai

Trước khi vẽ gì, phải chốt **mốc doanh số**. Hệ thống đang có ba cột, mỗi cột trả lời một câu
khác nhau — chọn sai thì mọi con số trên dashboard đều sai mà không gì báo:

| Cột | Câu hỏi | |
|---|---|---|
| `KHACH_HANG.created_by_id` | ai **mang khách về** | ✅ đã chọn |
| `DANG_KY.created_by_id` | ai **nhập đơn** | lệch khi kế toán nhập hộ |
| `LICH_SU_CHAM_SOC.nguoi_phu_trach_id` | ai **đang chăm** | xem dưới |

Cột thứ ba là cái bẫy đáng nói: nó phản ánh đúng thực tế bán hàng **hôm nay**, nhưng
**báo cáo tháng trước sẽ tự đổi số** mỗi lần bàn giao khách. Kế toán không chấp nhận một con số
quá khứ biết tự thay đổi.

## Máy bắt lỗi 1 — script kiểm màu

Dựng bảng màu từ token của dự án (xanh lá `--primary`, cam `--accent`) rồi chạy validator thay
vì ước lượng. Nó báo **ba lỗi tôi không nhìn ra**:

| Lỗi | Số đo |
|---|---|
| Xanh lá đọc thành xám | chroma 0.083 < sàn 0.1 |
| Tím lẫn xanh dương khi mù màu đỏ | ΔE 4.3 < 8 |
| Cam thiếu tương phản nền | 2.76:1 < 3:1 |

Sửa xong vẫn còn cặp **cam ↔ lá ΔE 6.9** — hợp lệ nhưng chỉ khi có mã hoá phụ, nên mọi biểu đồ
nhiều chuỗi ở đây bắt buộc kèm nhãn trực tiếp.

**Dark mode phải dò riêng**, không đảo màu sáng: màu tươi trên nền tối luôn vượt trần sáng của
tiêu chuẩn. Mấy bản đầu đều trượt cho tới khi hạ đúng mức.

> Thử dark mode thì phát hiện thêm: **dự án chưa có công tắc nền tối**. CSS `.dark` viết sẵn từ
> lâu nhưng chưa mã nào bật được — class có trên `<html>` mà biến vẫn giá trị sáng. Nợ có sẵn,
> nằm ngoài việc hôm nay nên chỉ ghi nhận.

## Máy bắt lỗi 2 — tiêm đột biến

Viết 6 test, xanh hết. Tiêm ba đột biến:

| Đột biến | Kết quả |
|---|---|
| Bỏ quy đổi tỷ giá | đỏ ✅ |
| Bỏ bước phễu rỗng | đỏ ✅ |
| **Đổi mốc sang "người nhập đơn"** | **xanh** ❌ |

Đột biến thứ ba đúng là thứ quan trọng nhất của cả tính năng, mà test không canh được. Lý do:
test dùng **cùng một tài khoản** cho cả việc tạo khách lẫn nhập đơn — một người thì không phân
biệt nổi hai mốc.

Sửa thành hai người khác nhau: `manager` tạo khách, `ke-toan-nhap-don` nhập đơn. Nay đỏ đúng.

## Máy bắt lỗi 3 — ảnh chụp màn hình

Validator kiểm màu, **không kiểm bố cục**. Render ra nhìn mới thấy bốn thứ:

1. **Một mốc thời gian vẽ ra chấm lơ lửng** giữa khung trống → dưới 3 mốc chuyển sang cột.
2. **Bước phễu 0 khách vẫn vẽ vạch màu** → trông như có dữ liệu.
3. **Hàng ô số cao so le** khi ô nào thiếu dòng phụ.
4. Endpoint mới **chưa được đăng ký** vì API đang chạy bản build cũ — màn báo *"Không tìm thấy
   dữ liệu"* mà log sạch trơn.

Và sau khi thêm bộ lọc theo loại, ảnh chụp bắt được cái thứ năm — nặng hơn cả bốn cái trên:

> Chọn **"Học trực tuyến"** mà hàng ô số vẫn hiện **"Tổng doanh thu 17 tr"** — ngay trên dòng chữ
> *"không đo tiền"*. Người đọc sẽ tưởng 17 triệu đó là doanh thu của khoá trực tuyến.

Nay hàng ô số và đường doanh thu cùng đổi theo loại.

## Elearning không có tiền, và đó là đúng

Chủ sản phẩm yêu cầu thống kê cả elearning. Nhưng theo thiết kế chốt 13/09, khoá trực tuyến
**không có đường nối nào sang đơn hàng** — quản trị cấp quyền học bằng tay.

Nên nó chỉ đo được **số lượng**: khoá đang mở · người đang học · lượt ghi danh · tỷ lệ hoàn
thành. Bịa ra một con số doanh thu cho nó là nói dối về chính thiết kế.

### Ranh giới hệ thống bắt lỗi hai lần

Lần đầu tôi cho handler CRM đọc thẳng `db.KhoaOnlines` → `RanhGioiHeThongConTests` đỏ ngay.
Sửa thành interface `IThongKeHocTrucTuyen`, phần đọc đặt ở phía LMS.

Vẫn đỏ: DTO để ở `Crm/` khiến file LMS phải `using` sang. Chuyển sang `Common/Models` — nó là
**hợp đồng giữa hai hệ thống**, không của riêng bên nào.

## Vài lựa chọn hình thức, mỗi cái một lý do

- **Thanh xếp hạng một màu** — người/đội/mặt hàng không có thứ tự tự nhiên; tô đậm dần theo độ
  lớn là mã hoá hai lần thứ chiều dài thanh đã nói.
- **Phễu dùng dải một màu đậm dần** — ngược lại, các bước **có** thứ tự nên màu phải có thứ tự.
- **Màu gắn cố định với từng loại** — người dùng học được "khoá học màu xanh lá" thì nó phải
  xanh lá ở mọi kỳ, mọi bộ lọc. Đổi màu theo thứ hạng là cách chắc chắn làm người đọc hiểu sai.
- **Không biểu đồ tròn** (so góc khó hơn so chiều dài), **không hai trục tung** (ghép hai thang
  đo vào một khung sẽ bịa ra tương quan không có trong dữ liệu).
- **SVG thuần, không thư viện** — bốn dạng cần dùng đều vài chục dòng, recharts kéo 100-300KB.

## Kết quả

| | Đầu ngày | Cuối ngày |
|---|---|---|
| Test backend | 456 | **464** |
| Mã FR chạy được | 26 | **27** |

Cũng bỏ module Học viên khỏi LMS theo yêu cầu — học viên nay quản lý tập trung ở CRM, cấp tài
khoản từ chính màn Khách hàng nên hồ sơ **tự nối**, không thể tạo trùng một người.

Ghi thêm **nợ N27**: form khoá trực tuyến thiếu hộp xác nhận lưu, phát hiện khi viết E2E.
