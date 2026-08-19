# Bộ dữ liệu mẫu để test tay

> Dựng bằng một lệnh, chỉ chạy ở **Development**. Chặn ở production giống `/dang-ky-clb` — và ở
> đây còn nặng hơn: endpoint này có tuỳ chọn **xoá sạch mọi dữ liệu**.

## Chạy

```bash
# Thêm bộ mẫu, GIỮ dữ liệu đang có
curl -X POST http://localhost:8080/api/v1/du-lieu-mau/seed

# Xoá sạch rồi dựng lại từ đầu (~8 giây)
curl -X POST "http://localhost:8080/api/v1/du-lieu-mau/seed?xoaDuLieuCu=true"
```

Kết quả trả về **mã đội + mật khẩu** của cả 7 CLB để đăng nhập ngay.

`xoaDuLieuCu` mặc định `false`: xoá dữ liệu phải là lựa chọn tường minh, không phải mặc định của
một lệnh tiện ích (quy tắc #1).

## Có gì

| CLB | Vai | Dữ liệu |
|---|---|---|
| **FC Hoà Xuân** | Đầy đủ #1 | 18 cầu thủ · 20 trận · 4 đợt quỹ · 7 khoản chi · 3 mẫu đội hình |
| **Hải Châu FC** | Đầy đủ #2 | 16 cầu thủ · 20 trận · 4 đợt quỹ · 7 khoản chi · 3 mẫu đội hình |
| 5 CLB khác | Phụ | Thông tin công khai + 1–8 trận có kết quả |

**Hai CLB đầy đủ ngang nhau** để đăng nhập lần lượt cả hai mà thấy cách ly dữ liệu, và test lời
mời thách đấu theo cả hai chiều.

Mọi tài khoản: mật khẩu `matkhau123`, **không** bị buộc đổi lần đầu.

### Ba vai mỗi CLB đầy đủ

| Username | Quyền | Dùng để test |
|---|---|---|
| `admin` | Đủ quyền + **trưởng nhóm** | Gửi lời mời đăng ký, mọi chức năng |
| `manager` | Đủ quyền, KHÔNG trưởng nhóm | Kiểm cờ trưởng nhóm thật sự chặn |
| `player` | **Chỉ Xem** | Kiểm phân quyền: sửa tiền quỹ phải bị 403 |

### Phạm vi thời gian

**6 tháng quá khứ + 1 tháng tương lai**, tính từ lúc chạy seed.

- 15 trận đã đá: đủ kết quả, đội hình, sơ đồ 2 hiệp, đánh giá 6 chỉ số, vote MVP, video ở 1/4 trận.
- 4 trận sắp tới: một đã xếp đội hình · một có lời mời đăng ký đang chờ · một trống · một đã huỷ.

### Bốn đợt quỹ, đủ mọi màu trạng thái

Đã thu đủ (đã đóng) · **quá hạn còn nợ** (đỏ) · đang thu có bật chuyển khoản · mới lập chưa ai đóng.

### Bốn lời mời thách đấu, đủ mọi trạng thái

Ta gửi đang chờ · ta nhận đang chờ · đã chấp nhận (kèm trận ở lịch **cả hai** bên) · đã từ chối.

## Nguyên tắc: dữ liệu đi qua đúng đường nghiệp vụ

Tỷ số nhà **không gán trực tiếp** — cộng từ bàn thắng trong đánh giá cầu thủ rồi gọi
`TranDau.DongBoTySoNha()`, đúng đường mà ứng dụng thật đi.

Gán tay sẽ tạo ra trận có tỷ số 3-1 mà tổng bàn cầu thủ là 0 — thứ hệ thống **không sinh nổi**.
Test trên dữ liệu đó là test một hệ thống khác. Canh bởi
`Ty_so_moi_tran_KHOP_tong_ban_thang_cau_thu`.

## Bốn lỗi thật đã gặp khi làm bộ này

Cả bốn đều lộ ra khi **xem con số trên màn hình**, không test nào bắt được lúc đó:

| Lỗi | Nguyên nhân |
|---|---|
| 18 người "đóng" 1₫, 2₫… 17₫, tổng quỹ **153₫** | `_ => canDong` trong switch **lồng** bị C# hiểu là *pattern gán biến* — `canDong` thành tên biến mới bắt giá trị `i % 5`. Đọc code không thấy |
| Trận hiện **22:00** và **02:00** | Ghi `TimeSpan.Zero` (coi 15h là 15h UTC) nên frontend +7 thành 22h. Đội phong trào không đá lúc 22h đêm |
| Bảng xếp hạng thiếu 4 người, **cột "Cứu thua" trống hoàn toàn** | Đội hình xoay `i % 4` chỉ dịch 0–3 nên 4 người cuối không bao giờ ra sân; thủ môn dự bị không đá → không ai có cứu thua |
| Test "số tiền" vẫn xanh khi tổng quỹ là 153₫ | Test chỉ kiểm "có đợt = 0" và "có đợt = tổng cần"; 153 khác cả hai nên lọt. Phải kiểm **từng khoản đóng** |

Mỗi lỗi giờ có một test canh, và cả bốn đã kiểm bằng phản chứng.

## Còn trống: màn Tổng quan

`/` chỉ hiện "Xin chào, admin" — **màn này chưa được làm**, trống từ trước khi có bộ mẫu. Nó không
thuộc FR nào nên không ai để ý. Xem [nợ kỹ thuật](./ke-hoach.md).
