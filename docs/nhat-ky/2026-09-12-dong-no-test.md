# 2026-09-12 — Đóng nợ N16 và N12: bộ test đáng tin trở lại

Hai nợ nằm im từ đầu tháng, cùng một hậu quả: **không ai tin bộ test nữa**. E2E đỏ 4 test liên
tục, backend đỏ ngẫu nhiên 1 test. Khi đỏ là chuyện thường ngày thì lần đỏ thật cũng bị bỏ qua.

## N16 — bốn test E2E lạc hậu

Nợ ghi "test lạc hậu thời dự án bóng đá". Đúng một phần: hai test đầu chỉ là chữ, hai test sau
lạc hậu **sâu hơn** thế.

### `dang-nhap-tra-ma` — chỉ là chữ

Test chờ `"Không tìm thấy đội tương ứng"`, app trả `"Không tìm thấy trung tâm tương ứng"`. Di sản
từ dự án CLB đá bóng. Đổi chuỗi là xong.

### `quan-tri` — trỏ nhầm màn hình

Test "sửa tài khoản không làm mất địa chỉ" mở modal Tài khoản rồi điền `#diaChi`. Nhưng modal đó
**không còn ô địa chỉ** — nó chỉ còn username, nhóm quyền, trạng thái.

Địa chỉ và email đã sang màn khác từ 07/09 khi **tách người ≠ tài khoản**, rồi sang `/hrm/nhan-su`
khi chia ba hệ thống con (08/09). Test không theo kịp hai lần dịch chuyển đó.

Sửa: trỏ sang `/hrm/nhan-su`. Giữ nguyên ý nghĩa — nó canh **quy tắc #1**, sinh ra từ sự cố
16/08 khi form thiếu ô địa chỉ nên âm thầm xoá địa chỉ mỗi lần lưu.

> Giữ bản E2E dù `CapNhatKhongMatDuLieuTests` đã canh chặt hơn ở tầng integration: lỗi 16/08 nằm
> ở **form thiếu ô**, không ở handler. Chỉ E2E mới thấy được form.

### Cả hai màn còn thiếu bước xác nhận

Sau khi sửa đường dẫn, test vẫn đỏ ở chỗ "modal không đóng". Ảnh chụp cho thấy hộp **"Xác nhận
lưu"** đang mở — thêm 07/09/2026 cho mọi thao tác ghi, mà test chỉ bấm submit rồi chờ modal đóng.

Thêm helper `luuVaXacNhan()`. Ảnh cũng xác nhận bản vá `inert` hôm qua chạy đúng: modal dưới mờ
đi khi hộp xác nhận mở.

## N12 — KHÔNG phải "test chớp nháy"

Nợ ghi *"đỏ một lần khi chạy toàn bộ, xanh khi chạy riêng"* — nghe như tranh chấp trạng thái giữa
các test. Sai.

```csharp
// Đổi ký tự cuối của phần chữ ký.
var gia = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');
```

Chữ ký HS256 là **32 byte = 43 ký tự base64url**. 43 × 6 = 258 bit, mà chỉ cần 256 — nên **ký tự
cuối chỉ mang 2 bit có nghĩa**. Kiểm bằng số:

```
nhóm ký tự cuối cho ra CÙNG chữ ký: 16
ví dụ nhóm đầu: ['A', 'B', 'C', 'D']
```

Đổi `'a'` → `'b'` mà cả hai nằm trong một nhóm thì **chữ ký không đổi**, token vẫn hợp lệ, API
trả 200 thay vì 401 → test đỏ.

Không ngẫu nhiên chút nào: nó phụ thuộc **ký tự cuối của chữ ký sinh ra lần đó**. Chạy riêng hay
chạy cả bộ không liên quan — chỉ là xác suất ~25% mỗi lần.

Sửa: đổi ký tự **ở giữa** chữ ký, nơi mang đủ 6 bit. Chạy 5 lần đơn lẻ + 3 lần toàn bộ đều xanh.

### Vì sao đáng ghi lại

Nợ này bị **chẩn đoán sai suốt hai ngày**. "Chớp nháy" là lời giải thích dễ chấp nhận cho một
test đỏ thất thường, và nó khiến không ai tìm nguyên nhân thật. Bài học: khi gán nhãn "flaky"
cho một test, phải nói được **cơ chế** làm nó thất thường — nếu không, đó chỉ là cách hoãn việc
điều tra.

## Một lỗi phát sinh, bắt được ngay

Chạy toàn bộ E2E sau khi sửa: 19 xanh, **1 đỏ** — chính test `modal-long-nhau` tôi viết hôm qua.

Nó đọc `api.length`, nhưng endpoint hàng chờ đã đổi sang **phân trang** cùng ngày, trả
`KetQuaTrang` chứ không phải mảng. Sửa thành `api.tongSoDong`.

Đây là ví dụ tốt cho lý do phải chạy **cả bộ**, không chỉ test vừa sửa.

## Kết quả

| | Trước | Sau |
|---|---|---|
| E2E | 16 xanh / 4 đỏ | **20 xanh / 0 đỏ** |
| Backend | 442, đỏ ngẫu nhiên 1 | **442 xanh**, 3 lần liên tiếp |

Bộ test dùng để gác merge được rồi.
