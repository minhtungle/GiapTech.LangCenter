# FR-28 — Thống kê CRM

Màn `/crm/thong-ke`, gác bằng **`DoanhThu.Xem`**. Viết 14/09/2026.

## Doanh số của một đơn tính cho ai

Quyết định quan trọng nhất của cả tính năng. Ba cột trong hệ thống đang trả lời **ba câu khác
nhau**, chọn sai thì mọi con số trên dashboard đều sai mà không có gì báo:

| Cột | Câu hỏi nó trả lời | Chọn? |
|---|---|---|
| `KHACH_HANG.created_by_id` | ai **mang khách về** | ✅ |
| `DANG_KY_KHOA_HOC.created_by_id` | ai **nhập đơn** | ❌ lệch khi kế toán nhập hộ |
| `LICH_SU_CHAM_SOC.nguoi_phu_trach_id` | ai **đang chăm** khách | ❌ xem dưới |

Cột thứ ba là cái bẫy đáng nói: nó phản ánh đúng thực tế bán hàng **hôm nay**, nhưng
**báo cáo tháng trước sẽ tự đổi số** mỗi lần bàn giao khách — thứ kế toán không chấp nhận.

Chốt với chủ sản phẩm: **người tạo hồ sơ khách**. Ổn định, không đổi theo thời gian, và khớp
với quy ước đã có từ 12/09 (*"nhân viên kinh doanh = người tạo khách hàng"*).

## Bốn loại thống kê (14/09/2026)

Ô chọn **"Thống kê theo"** đổi phần chia nhỏ; bộ lọc thời gian và phần bối cảnh dùng chung nên
so sánh giữa các loại vẫn cùng một kỳ.

| Loại | Chia theo | Lọc danh sách |
|---|---|---|
| Khoá học | từng khoá bán ra | chọn nhiều khoá |
| Sản phẩm | từng sản phẩm | chọn nhiều sản phẩm |
| **Học trực tuyến** | **không chia doanh thu** — xem dưới | không có |
| Đội nhóm | từng phòng ban | chọn nhiều phòng ban |

### Elearning chỉ đo SỐ LƯỢNG

Khoá trực tuyến **không có đường nối nào sang đơn hàng** (chốt 13/09/2026: quản trị cấp quyền
học bằng tay, LMS không trỏ sang CRM). Nên ở đây không có doanh thu để chia — trả về một con số
tiền cho nó là **nói dối về chính thiết kế**.

Đo được: khoá đang mở · người đang học · lượt ghi danh · tỷ lệ hoàn thành bài.

Khi chọn loại này, **hàng ô số và đường doanh thu cũng đổi theo**: hiện "17 triệu tổng doanh
thu" ngay trên dòng chữ *"không đo tiền"* là tự mâu thuẫn, và người đọc sẽ tưởng số đó là doanh
thu của khoá trực tuyến. Thấy được khi chụp màn hình thật, không thấy khi đọc code.

> **Ranh giới hệ thống**: CRM lấy số liệu này qua `IThongKeHocTrucTuyen`, không đọc thẳng
> `db.KhoaOnlines`. Đọc thẳng là gọi chéo hệ thống con và `RanhGioiHeThongConTests` bắt ngay.
> DTO dùng chung đặt ở `Common/Models` — để ở `Crm/` hay `DaoTao/` thì bên kia phải `using`
> sang, tức lại gọi chéo.

### Bộ lọc thu hẹp CẢ hàng ô số

Chọn "chỉ khoá A" thì tổng doanh thu, số đơn, và đường tăng trưởng đều chỉ tính khoá A. Lọc nửa
vời thì *"tổng doanh thu"* và *"top khoá học"* trên cùng một màn lại nói về hai tập dữ liệu khác
nhau — người đọc không có cách nào biết. Canh bởi `Loc_theo_muc_thu_hep_ca_tong_doanh_thu`.

Đổi loại thì **bỏ lọc cũ**: id khoá học không có nghĩa gì trong danh sách sản phẩm, giữ lại sẽ
lọc ra rỗng mà người dùng không hiểu vì sao.

## Sáu nhóm số liệu

| Nhóm | Dạng hiển thị | Vì sao dạng đó |
|---|---|---|
| 4 con số dẫn | **ô số** | Một giá trị hiện tại — biểu đồ một thanh là thừa |
| Doanh thu theo tháng | **đường** (≥3 mốc) / **cột** (<3) | Đường ngụ ý xu hướng; hai điểm chưa thành xu hướng |
| Theo cá nhân · đội · mặt hàng · nguồn | **thanh ngang, MỘT màu** | Xem "Một màu" dưới |
| Phễu bán hàng | **thanh + dải một màu đậm dần** | Các bước CÓ thứ tự nên màu phải có thứ tự |

### Một màu cho mọi thanh xếp hạng

Người, đội, mặt hàng **không có thứ tự tự nhiên**. Tô mỗi thanh một màu (hoặc tô đậm dần theo
độ lớn) sẽ mã hoá **hai lần** cùng một thông tin mà chiều dài thanh đã nói, và đốt mất kênh màu
vào việc vô ích.

### Những dạng cố ý KHÔNG dùng

- **Biểu đồ tròn** — so sánh góc khó hơn so sánh chiều dài.
- **Hai trục tung** — hai thang đo ghép vào một khung sẽ **bịa ra tương quan** không có trong
  dữ liệu. Cần so hai đại lượng khác đơn vị thì tách hai biểu đồ.

## Bảng màu — kiểm bằng script, không ước lượng

`--chart-1..5` trong `index.css`, dựng từ `--primary` (xanh lá 152) và `--accent` (cam 28) của
dự án rồi chạy `validate_palette.js`:

```
LIGHT  → ALL CHECKS PASS   DARK → ALL CHECKS PASS
```

Bản đầu tôi tự chọn bị báo **ba lỗi không nhìn ra bằng mắt**:

| Lỗi | Số đo |
|---|---|
| Xanh lá đọc thành xám | chroma 0.083 < sàn 0.1 |
| Tím lẫn xanh dương khi mù màu đỏ | ΔE 4.3 < 8 |
| Cam thiếu tương phản nền | 2.76:1 < 3:1 |

Cặp **cam ↔ lá còn ΔE 6.9** — hợp lệ nhưng chỉ khi có mã hoá phụ, nên mọi biểu đồ nhiều chuỗi
ở đây **bắt buộc kèm nhãn trực tiếp**.

Dark mode phải **dò riêng**, không đảo màu sáng: màu tươi trên nền tối luôn vượt trần sáng của
tiêu chuẩn.

> ⚠️ Dự án **chưa có công tắc nền tối** — CSS `.dark` viết sẵn từ trước nhưng chưa mã nào bật
> được. Token biểu đồ dark đã đặt đúng chỗ, sẽ tự chạy khi nào có công tắc.

## Bốn lỗi bố cục chỉ thấy khi chụp màn hình thật

Validator kiểm màu, **không kiểm bố cục**. Render ra nhìn mới thấy:

1. **Một mốc thời gian vẽ ra chấm lơ lửng** giữa khung trống → dưới 3 mốc chuyển sang cột.
2. **Bước phễu 0 khách vẫn vẽ vạch màu** → trông như có dữ liệu; nay 0 thì không vẽ.
3. **Hàng ô số cao so le** khi ô nào thiếu dòng phụ → `flex h-full`.
4. Endpoint mới **chưa được đăng ký** vì API đang chạy bản build cũ — màn hiện "Không tìm thấy
   dữ liệu" mà log sạch.

## Tiêu chí các CRM lớn có mà ta chưa làm

| Tiêu chí | Cần thêm gì |
|---|---|
| Tốc độ chốt đơn (ngày từ tạo khách → mua) | **Đã đủ dữ liệu** — làm được ngay |
| Dự báo doanh thu (pipeline × tỷ lệ chốt) | Cần cột giá trị dự kiến trên khách đang tư vấn |
| Tỷ lệ giữ chân / mua lại | Cần vài tháng dữ liệu thật mới có nghĩa |

## Liên quan

- [CRM](./crm.md) — FR-17 → FR-21
- [Design token](../frontend/design-tokens.md)
