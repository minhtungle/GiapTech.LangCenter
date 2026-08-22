# Module Tài chính — Quỹ đội (FR-15, FR-16)

> Đây là dữ liệu nhạy cảm. Mọi PR đụng tới module này **bắt buộc có security review** — xem
> [CONTRIBUTING.md](../../CONTRIBUTING.md) mục 4.

## FR-15 — Danh sách quỹ

Lịch sử đóng quỹ, hiển thị:

| Cột | Mô tả |
|---|---|
| Thời gian | Thời điểm tạo đợt quỹ / thời hạn |
| Tên quỹ | Ví dụ "Quỹ tháng 3", "Quỹ thuê sân giải X" |
| Tiến độ đóng | Đã đóng / Tổng — hiển thị dạng thanh tiến độ |

Màu trạng thái theo quy ước chung: **xanh** = đã đóng đủ, **đỏ** = quá hạn, **vàng** = đang chờ.

## FR-16 — Thêm / Cập nhật quỹ

**Trường dữ liệu:** tên quỹ, chọn thành viên + số tiền mỗi người, ghi chú, thời hạn.

### Thông tin chuyển khoản *(bổ sung 19/08/2026)*

**Hệ thống KHÔNG xử lý tiền.** Nó chỉ hiển thị số tài khoản và mã QR để thành viên biết chuyển
vào đâu; tiền đi trực tiếp giữa hai người, thủ quỹ vào nhập tay số đã nhận. Không cổng thanh
toán, không webhook, không đối chiếu sao kê — tự động ghi nhận đòi quyền đọc sao kê ngân hàng
của CLB, và cổng thanh toán đòi tài khoản merchant có phí.

Ba nơi:

| Nơi | Việc |
|---|---|
| **Thiết lập chung** | Khai `so_tai_khoan` · `ten_ngan_hang` · `chu_tai_khoan` · `anh_qr_url` |
| **Form đợt quỹ** | Checkbox *Hiện thông tin chuyển khoản cho đợt quỹ này* |
| **Màn thu tiền** | Khối chuyển khoản hiện trên đầu, kèm nút sao chép số tài khoản |

**Cờ theo TỪNG ĐỢT, không phải bật/tắt toàn cục.** Có đợt thu tiền mặt tại sân (đóng ngay sau
trận), có đợt thu chuyển khoản. Hiện QR cho đợt thu tiền mặt chỉ làm người ta chuyển khoản trong
khi thủ quỹ đang đứng chờ nhận tiền tươi. Mặc định `false`: đợt quỹ cũ không tự nhiên hiện số
tài khoản lên.

**Lưu ảnh QR do CLB tự tải lên**, không tự sinh mã VietQR: sinh mã cần đúng BIN ngân hàng và
tuân thủ chuẩn EMVCo — sai một ký tự là app ngân hàng từ chối quét mà người dùng không hiểu vì
sao. Ảnh họ chụp từ app ngân hàng thì chắc chắn quét được.

#### Đây là dữ liệu NỘI BỘ

Khác `lien_he_cong_khai` (hiện cho CLB đã chấp nhận lời mời thách đấu), bốn trường chuyển khoản
**không lên Cộng đồng** — cả danh sách lẫn trang chi tiết CLB. Số tài khoản quỹ lộ ra ngoài là
cho người lạ biết tài khoản nào đang gom tiền của đội nào. Canh bởi
`So_tai_khoan_KHONG_lo_ra_Cong_dong` và `Anh_QR_khong_lo_ra_Cong_dong`.

Ảnh QR dùng thư mục riêng `qr-chuyen-khoan/` trong MinIO, tách khỏi `logo/` và `anh-bia/` (hai
loại đó **có** lên Cộng đồng).

#### Lỗi đã tránh và lỗi đã xảy ra

- **Tránh:** cả hai handler ảnh dùng `default:` cho ảnh bìa. Thêm `LoaiAnh.AnhQr` mà không đổi
  thành `case` tường minh sẽ khiến QR âm thầm ghi lên `anh_bia_url` — vừa mất ảnh bìa, vừa làm
  QR hiện lên Cộng đồng. Giờ mọi nhánh liệt kê đủ, nhánh mặc định ném `LOAI_ANH_KHONG_HO_TRO`.
- **Đã xảy ra:** lệnh lưu đợt quỹ chỉ làm mới cache `quy`, không làm mới `quy-chi-tiet`. Tắt
  hiển thị chuyển khoản xong mở lại màn thu tiền vẫn thấy số tài khoản. Phát hiện khi xem màn
  hình, không test nào bắt được lúc đó. Canh bởi E2E *thông tin chuyển khoản: bật/tắt theo đợt*.

### Nhắc nhở

**Hiện tại:** nút **Sao chép danh sách nợ** — chép tên + số tiền còn thiếu ra clipboard để dán
vào Zalo/Messenger. Đó là cách CLB phong trào nhắc nợ thật, và dùng được ngay mà không cần
tài khoản dịch vụ nào.

**Chưa làm:** gửi tự động qua SMS / Email. Hạ tầng gửi chưa cấu hình.

- Email: SMTP (SendGrid / Gmail API).
- SMS: SMS Gateway nội địa (eSMS / Speedsms).
- Cấu hình: xem [biến môi trường](../ha-tang/bien-moi-truong.md).

### Quy tắc

- Số tiền mỗi người có thể khác nhau trong cùng một đợt quỹ (`DONGGOP_QUY.so_tien_can_dong` theo từng
  cầu thủ).
- `so_tien_da_dong` cho phép đóng **từng phần** — tiến độ tính theo `so_tien_da_dong / so_tien_can_dong`.
- Đối tượng đóng quỹ là **hồ sơ cầu thủ** (`CAU_THU`), không phải tài khoản đăng nhập — cầu thủ chưa có
  tài khoản vẫn nằm trong danh sách đóng quỹ.
- Player chỉ **xem** tiến độ quỹ, không được sửa số tiền: endpoint đọc dùng quyền `Xem`,
  mọi endpoint đổi tiền dùng `Sua`/`Them`/`Xoa`.

### Bảo toàn tiền (quy tắc #1)

Ba ràng buộc, cả ba đã kiểm chứng bằng phản chứng:

- **Sửa đợt quỹ không đụng `so_tien_da_dong`.** Lệnh lưu quỹ chỉ gán `so_tien_can_dong`; gán
  cả số đã đóng sẽ xoá trắng tiền thật đã vào túi mỗi lần thủ quỹ sửa tên đợt.
  Canh bởi `Sua_quy_khong_lam_mat_tien_da_thu`.
- **Không gỡ được người đã đóng tiền** khỏi đợt quỹ (`KHONG_XOA_NGUOI_DA_DONG_TIEN`) — xoá là
  mất vết một khoản tiền có thật. Muốn gỡ thì hoàn số tiền về 0 trước.
- **Đợt quỹ đã thu tiền không xoá được** (`QUY_DA_THU_TIEN_KHONG_XOA_DUOC`). Muốn ẩn khỏi
  danh sách thì đóng đợt quỹ.

Hoàn tiền về 0 sẽ **xoá luôn `ngay_dong`** — không thì báo cáo thấy "đóng ngày X, số tiền 0".

### Hoàn tác khoản đã thu *(bổ sung 20/08/2026)*

Backend vốn đã cho: `GhiNhanThuCommand` nhận số tiền bất kỳ ≥ 0, gửi 0 là hoàn tác. Vướng nằm ở
**UI** — trước đây muốn về 0 phải tự xoá ô rồi gõ `0`, không ai đoán ra, nên bấm nhầm nút ✓ coi
như xong.

| Sửa gì | Vì sao |
|---|---|
| Nút **↺ Hoàn tác** hiện khi `so_tien_da_dong > 0` | Cú bấm rõ ràng thay vì mẹo gõ tay |
| **Hộp xác nhận** nói tên + số tiền + hậu quả | Thao tác trên tiền: nó xoá vết một khoản đã ghi nhận |
| Nút ✓ và ↺ có **chỗ riêng cố định** | Dùng chung một chỗ thì sau khi bấm ✓, nút ↺ nhảy vào đúng toạ độ đó — cú bấm tiếp theo theo quán tính sẽ xoá mất khoản vừa ghi |
| `key` trên ô nhập | `defaultValue` chỉ có tác dụng ở render đầu, nên bấm ✓ xong ô vẫn hiện 0 trong khi cột "Còn thiếu" báo đã đủ |

Hoàn tác **không khoá vĩnh viễn**: thu lại được ngay, và ràng buộc `KHONG_XOA_NGUOI_DA_DONG_TIEN`
cũng mở lại (gỡ được người khỏi đợt quỹ sau khi hoàn tác về 0).

Canh bởi `Hoan_tac_dua_tien_do_va_trang_thai_ve_dung` · `Hoan_tac_roi_thu_lai_duoc` ·
`Hoan_tac_xong_thi_go_duoc_nguoi_khoi_dot_quy` + E2E *hoàn tác được khi bấm nhầm đã đóng tiền*.

## Khoản chi và số dư *(ngoài phạm vi FR-15/16)*

Bảng `KHOAN_CHI`. Thu tiền vào mà không ghi được tiền ra thì con số "đã thu" không nói lên quỹ
còn bao nhiêu.

- `quy_id` **nullable**: chi có thể thuộc một đợt quỹ hoặc là chi chung của CLB. Bắt buộc gắn
  đợt sẽ khiến thủ quỹ tạo đợt quỹ giả chỉ để ghi một khoản chi.
- Xoá đợt quỹ **không xoá** khoản chi (`SetNull`, không Cascade): tiền đã tiêu là sự thật kế
  toán, giữ lại dưới dạng chi chung. Kiểm bằng tay trên PostgreSQL thật — in-memory không
  thực thi ràng buộc FK.
- Khoản chi **xoá được** (khác đợt quỹ đã thu tiền): thủ quỹ gõ nhầm một dòng chi là chuyện
  thường, không xoá được thì họ phải sửa nó thành "0 đồng" — bẩn hơn.
- **Số dư = đã thu − đã chi**, dùng tiền *thực nhận* chứ không phải tiền phải thu: quỹ chỉ
  tiêu được số đã vào túi. Số dư âm hiện màu đỏ.

## Tham chiếu

- Bảng `QUY`, `DONGGOP_QUY`, `CAU_THU` — xem [ERD](../database/erd.md).
