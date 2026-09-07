# 07/09/2026 — View lịch dạng calendar

Yêu cầu: thêm view calendar chuẩn cho lịch học, **dùng thư viện mã nguồn mở thay vì tự viết**.

## Chọn thư viện

Khảo sát hai ứng viên chính:

| | FullCalendar 6.1.21 | react-big-calendar 1.20 |
|---|---|---|
| Giấy phép | MIT | MIT |
| React 19 | ✅ | ✅ |
| Hỗ trợ `timeZone` | **Có sẵn** | Qua localizer, phải tự cấu hình |
| Phụ thuộc | `preact` (~1 gói) | moment + luxon + lodash + globalize |

Chọn FullCalendar vì `timeZone` là **yêu cầu bắt buộc** ở đây, và vì nó không kéo theo bốn thư
viện ngày/tháng trùng lặp nhau.

**Dùng 6.1.21 chứ không 7.1.0**: v7 chỉ có `@fullcalendar/react` ổn định, còn các gói view
(`daygrid`, `timegrid`, `list`) mới ở mức beta/rc. Lấy v7 sẽ phải trộn bản beta vào production.

## Điều quan trọng hơn cả view: múi giờ

Frontend trước nay hiển thị giờ bằng `toLocaleString('vi-VN')` — tức dùng múi giờ **của máy
người xem**. Với bảng thì chỉ lệch giờ; với **lịch** thì lệch giờ làm buổi **nhảy sang ô ngày
khác**, sai rõ hơn nhiều.

Buổi 18:00 giờ Việt Nam lưu là 11:00 UTC. Mở trên máy đặt UTC+9 sẽ vẽ vào 20:00 — vẫn cùng
ngày. Nhưng buổi 6:00 sáng thì rơi sang ô ngày hôm trước.

Thêm `GET /toi/cau-hinh` trả `TENANT.mui_gio`. **Không lấy từ `/thiet-lap`** vì endpoint đó gác
bằng `ThietLapChung.Xem`: giáo viên và học viên không gọi được, mà họ chính là người xem lịch
nhiều nhất. Đặt vào `ToiController` — đúng ngữ nghĩa "thông tin cho phiên hiện tại", và cùng
chỗ với `/toi/quyen` làm hôm nay.

## Ba chi tiết cố ý

**Map `--fc-*` sang design token** trong `lich-buoi-hoc.css` (14 chỗ), không ghi màu cứng. Nếu
không, đổi bảng màu hoặc bật chế độ tối sẽ để lại một khối lịch màu lạ giữa trang.

**Màu trên lịch khớp badge ở bảng** — đã lên lịch (màu chủ đạo), đã hoàn thành (xanh), đã huỷ
(mờ + gạch ngang). Hai chỗ hiển thị cùng dữ liệu thì không được nói khác nhau. Buổi bù dùng
**viền nhấn** thay vì đổi màu nền, để vẫn đọc được trạng thái.

**Trục giờ 6h–22h**: trung tâm ngoại ngữ không dạy đêm; để trục 24 tiếng thì buổi tối bị nén
thành một dải mỏng không đọc được.

## Tách chunk

Build đầu tiên ra 865 kB (255 kB gzip) — FullCalendar chiếm ~230 kB. Lịch chỉ dùng khi người
dùng chủ động bật chế độ Lịch, nên chuyển sang `lazy` + `Suspense`:

```
trước:  index.js  865 kB
sau:    index.js  631 kB  +  LichBuoiHoc.js  234 kB (tải khi cần)
```

## Kiểm chứng

- **253 test xanh**, build 0 warning, frontend `build` + `oxlint` sạch.
- 2 test mới trong `QuyenCuaToiTests`: mọi vai trò đọc được múi giờ; chưa đăng nhập nhận 401.
- **Chạy thật**: `/toi/cau-hinh` trả `Asia/Ho_Chi_Minh` cho cả ba vai trò; đối chiếu dữ liệu
  buổi học cho thấy 11:00 UTC ↔ 18:00 VN đúng như mong đợi; chunk lịch nạp được qua dev server.

## Còn nợ

- Lịch chỉ đọc: chưa kéo-thả để đổi giờ buổi. FullCalendar hỗ trợ sẵn (`editable` +
  `eventDrop`), nhưng cần nghĩ kỹ về buổi đã khoá và về việc xác nhận trước khi lưu.
- Chưa có lịch **toàn trung tâm** (mọi lớp trên một lịch) — component đã nhận `hienTenLop` để
  dùng cho việc đó, chỉ chưa có màn gọi tới.
- Hàm `gioVN` trong `LichVaDiemDanh.tsx` vẫn dùng múi giờ máy. Bảng thì chấp nhận được nhưng
  nên chuyển sang `useMuiGio()` cho nhất quán.
