# FR-19 — Đăng ký đá trận nhanh qua link/QR (không cần đăng nhập)

> Bổ sung 21/08/2026. Liên quan: [FR-13 Đăng ký tham gia](./lich-thi-dau.md) ·
> [FR-18 Lời mời qua link](./loi-moi-qua-link.md) (dùng chung cơ chế token)

## Vấn đề

Đăng ký đá trận hiện tại (FR-13) yêu cầu **tài khoản có quyền**. Thực tế đội bóng phong trào:
18 thành viên, nhưng chỉ trưởng nhóm và 1–2 người nữa có tài khoản. Mười lăm người còn lại
không đăng ký được — nên trưởng nhóm vẫn phải hỏi từng người qua chat rồi **tự bấm hộ**, đúng
việc mà FR-13 sinh ra để bỏ.

Tạo tài khoản cho cả 18 người là câu trả lời sai: họ dùng ứng dụng này 2 phút mỗi tuần, không
ai muốn nhớ thêm một mật khẩu cho việc đó.

## Giải pháp

Trưởng nhóm sinh **một link + QR** cho lời mời đăng ký của trận. Dán vào nhóm chat (hoặc chìa QR
ngoài sân). Người vào **không đăng nhập**, chọn tên mình từ danh sách, bấm Tham gia / Chưa chắc /
Không. Cập nhật thẳng vào `PHAN_HOI_THAM_GIA` — trưởng nhóm thấy ngay trên màn chi tiết trận.

## Nơi đặt tính năng: TRONG chi tiết trận, không phải Hòm thư

Yêu cầu của chủ sản phẩm 21/08: *"phần mời vote phải nằm ngay trong xem chi tiết lịch thi đấu"*.

Đây là **sửa chỗ đặt sai từ đầu**, không chỉ là thêm mới. Lời mời đăng ký thuộc về **một trận cụ
thể**, nhưng nút gửi nó lại nằm ở Hòm thư — nên trưởng nhóm phải rời trận đang xem, sang Hòm thư,
tìm đúng lời mời của trận đó. Cùng loại lỗi với "muốn thêm đối thủ phải rời form thêm trận" đã sửa
hôm 17/08.

→ Thêm **tab thứ 5** vào chi tiết trận: `Đăng ký`. Hòm thư vẫn hiển thị lời mời (cầu thủ vào đó để
trả lời), nhưng **chỗ tạo và quản lý** chuyển về trận.

## Chọn ai được mời — thứ hiện chưa có

Hiện tại `GuiLoiMoiDangKyHandler` mời **tất cả** cầu thủ:

```csharp
var cauThuIds = await db.CauThus.Select(c => c.Id).ToListAsync(ct);
```

Không có cách bỏ ai. Thực tế thì có người nghỉ dài, người chấn thương, người đã báo trước là
không đá — mời họ chỉ làm bảng phản hồi đầy "chưa trả lời" giả.

→ Danh sách toàn bộ thành viên, **mặc định tick hết** (giữ hành vi cũ làm mặc định), trưởng nhóm
bỏ tick ai không mời. `GuiLoiMoiDangKyCommand` nhận thêm `CauThuIds`; **null = mời tất cả** để
không phá vỡ các lời mời đã tạo và code đang gọi.

### Bỏ tick sau khi đã gửi — quy tắc #1

Sửa danh sách sau khi gửi có thể **xoá câu trả lời đã có**. Quyết định của chủ sản phẩm: **cảnh
báo rồi mới cho bỏ**.

| Người bị bỏ tick | Xử lý |
|---|---|
| Chưa trả lời | Bỏ im lặng, không hỏi — không có gì để mất |
| Đã trả lời (Tham gia / Chưa chắc / Không) | Hiện rõ *"`<tên>` đã trả lời «Tham gia». Bỏ khỏi danh sách sẽ xoá câu trả lời đó."* rồi mới cho xác nhận |

Không chặn hẳn: trưởng nhóm có thể mời nhầm người đã nghỉ đội, chặn thì họ phải xoá cả lời mời và
tạo lại — mất hết câu trả lời của 17 người kia.

## Ba quyết định của chủ sản phẩm (21/08/2026)

### 1. CHỈ cho đăng ký đá trận, KHÔNG cho vote MVP

Yêu cầu ban đầu dùng chữ "vote" cho cả hai. Nhưng chúng khác nhau về bản chất rủi ro:

| | Đăng ký đá trận | Vote MVP |
|---|---|---|
| Bản chất | Khai báo "tôi đá được" | **Bầu chọn** — cần bí mật |
| Chọn tên từ select | Hợp lý, ai cũng biết ai đá | **Phá vỡ tính bí mật** |
| Sai thì sao | Trưởng nhóm gọi lại xác nhận | Kết quả bị lũng đoạn, **không phát hiện được** |

Với vote MVP, cơ chế "chọn tên mình" cho phép người mở link đầu tiên vote thay 18 người. Quy tắc
#8 (`UNIQUE(tran_dau_id, nguoi_vote_id)`) ép một-vote-một-người ở tầng DB, nhưng nó **không biết
ai thực sự bấm**. Đó là lỗ hổng *thiết kế*, không phải lỗ hổng cài đặt — thêm ràng buộc DB không
sửa được.

→ Vote MVP giữ nguyên: phải đăng nhập.

### 2. Cho SỬA LẠI, không khoá cứng

Yêu cầu ban đầu: "người nào chọn xong thì khoá người đó không cho chọn lại".

Vấn đề: người **đầu tiên** mở link thấy cả 18 tên. Họ chọn "Đỗ Quang Huy" → Huy bị khoá và không
đăng ký được nữa, mà không ai biết ai đã làm việc đó. Cơ chế khoá tự nó thành **công cụ phá
hoại**, và nó nhằm sai mục tiêu: cái cần chống là *mạo danh*, còn khoá chỉ chống *bấm hai lần*.

→ Cho sửa lại. Ghi `ThoiGianTraLoi` mỗi lần sửa và đếm `SoLanSua` để trưởng nhóm thấy bất thường
("người này sửa 6 lần" là dấu hiệu, còn "bị khoá oan" thì không sửa được).

Đánh đổi được chấp nhận: mất tính chống-trùng, đổi lấy việc không ai bị khoá oan. Với bóng phong
trào, trưởng nhóm nhìn danh sách 18 người là biết ngay ai bất thường.

### 3. Trưởng nhóm tự chọn hạn — nhưng chỉ 4 mốc, chặn ở validator

Yêu cầu ban đầu là 5 phút. 5 phút quá ngắn cho luồng thật: dán link vào nhóm chat, người đọc lúc
đang lái xe hoặc đang họp, mở sau 20 phút thì hết hạn, phải nhắn xin lại link — một việc thành ba.

Nhưng cho chọn **tự do** thì sẽ có người đặt 30 ngày, và một link *ghi dữ liệu* ẩn danh sống 30
ngày là rủi ro thật.

→ Bốn mốc cố định: **5 phút · 1 giờ · tới giờ đá · 7 ngày**. Chặn ở **validator** chứ không chỉ ở
dropdown — dropdown chỉ là gợi ý, ai gọi API trực tiếp vẫn gửi được 365 ngày.

Cộng nút **thu hồi ngay** cho trưởng nhóm (như FR-18). Thu hồi được thì không cần hạn ngắn để an
toàn.

## Nội dung trang vote nhanh

Chủ sản phẩm yêu cầu *"trong vote sẽ có thêm thông tin về đối thủ và trận đấu"*, và chọn thêm
**lịch sử đối đầu**.

| Hiện | Vì sao |
|---|---|
| Giờ đá · sân · tên đối thủ · lời nhắn của trưởng nhóm | Thông tin nền tối thiểu — không có giờ thì không ai trả lời được là có đá được hay không |
| **Lịch sử đối đầu** với đối thủ này: số lần đã đá + thắng/hoà/thua | Chủ sản phẩm chọn. Giúp ước lượng trận sắp tới |

**Không** hiện: thành tích chung của đối thủ, số người đã nhận đá, danh sách ai đã nhận. Ba thứ đó
đều rò rỉ thêm mà không giúp người trả lời quyết định.

Lịch sử đối đầu tính từ các trận **đã có kết quả** với cùng `DoiThuId` — dữ liệu của chính CLB
mình, không đọc sang tenant khác.

## Bảy trường hợp và cách xử lý

| # | Trường hợp | Xử lý |
|---|---|---|
| **1** | Link còn hạn, người dùng chọn tên chưa ai trả lời | Ghi phản hồi, hiện "Đã ghi nhận: `<tên>` — Tham gia" |
| **2** | Chọn tên **đã** trả lời trước đó | Cho sửa, hiện câu trả lời cũ để họ biết mình đang đổi gì. Tăng `SoLanSua` |
| **3** | Link **hết hạn** | Nói rõ "Link đã hết hiệu lực, liên hệ trưởng nhóm" — **không** trả 404. 404 làm người dùng tưởng link sai và bỏ luôn |
| **4** | Link **bị thu hồi** | Thông báo riêng "Trưởng nhóm đã đóng đăng ký" — phân biệt với hết hạn để họ biết là chủ ý |
| **5** | Lời mời **đã đóng** (`DaDong`) — trưởng nhóm chốt đội hình rồi | Chặn ở **handler**, không chỉ ẩn nút. Hiện "Đội hình đã chốt" |
| **6** | Trận **bị xoá / bị huỷ** sau khi link đã gửi | Hiện "Trận không còn" thay vì lỗi hệ thống |
| **7** | Token sai / bịa | Cùng một thông báo với hết hạn — **không** phân biệt, để không xác nhận token nào tồn tại |

## Ràng buộc bảo mật

### Endpoint ẩn danh THỨ BA — và là cái đầu tiên GHI dữ liệu

| Endpoint | Ẩn danh | Đọc/Ghi |
|---|---|---|
| `GET /auth/ten-doi/{maDoi}` | ✅ | Đọc |
| `POST /moi-qua-link/xem` | ✅ | Đọc |
| `POST /dang-ky-nhanh/xem` · `/tra-loi` | ✅ | **GHI** ← mới |

Nợ **N3 đã làm xong trước tính năng này (21/08)** — chính vì lý do trên: hai endpoint kia chỉ
đọc, cái này ghi, nên dò token thành công là *sửa dữ liệu* chứ không chỉ là *xem*.

Hai endpoint mới của FR-19 dùng policy `GioiHanTanSuat.LoiMoiTheoToken` (10 request/phút mỗi IP),
và `GioiHanTanSuatTests.Moi_endpoint_AN_DANH_deu_PHAI_co_gioi_han` sẽ đỏ nếu quên gắn.

### Token

- Sinh 32 byte ngẫu nhiên (`RandomNumberGenerator`), lưu **SHA-256** — dùng lại `BamToken` của
  FR-02/FR-18. DB bị đọc lén thì kẻ đọc không dựng lại được link.
- Truy vấn theo token phải `IgnoreQueryFilters()` (chưa biết tenant nào). **Đây là chỗ cần canh
  chặt nhất** — cùng loại rủi ro đã ghi ở [FR-18](./loi-moi-qua-link.md).
- QR buộc phải nhúng token vào URL, nhưng trang frontend đọc rồi **POST** lên body. Caddy không
  log query string.

### Dữ liệu trả về cho người ẩn danh

Chỉ `id` + `hoTen` + `soAo` của cầu thủ, cộng thông tin trận (giờ, sân, đối thủ). **Tuyệt đối
không**: ngày sinh, SĐT, email, địa chỉ, `tenantId`, hay bất kỳ dữ liệu tài chính.

Ai có link **lấy được danh sách thành viên CLB** — đây là đánh đổi có ý thức, không phải sơ suất.
Với bóng phong trào thì tên + số áo không phải bí mật; nhưng nó là lý do danh sách phải giới hạn
đúng ba trường đó.

## Ba lỗi giao diện, không lỗi nào do test bắt

Tất cả lộ ra khi **chụp màn hình và nhìn**, trong khi 369 test backend xanh:

1. **`traLoi` là CHUỖI, frontend so với SỐ.** API serialize enum thành `"ThamGia"`, không phải `1`.
   Bảng hiện "Chưa trả lời" cho người đã trả lời, dòng thống kê đếm 0. **Không lỗi console.**
   Che mất lỗi: lệnh *ghi* nhận cả số lẫn chuỗi nên trả lời vẫn thành công — chỉ phần *đọc* sai.
2. **Danh sách tick về 16/16** dù lời mời chỉ 14 người, vì `useEffect` chạy trước khi `phanHois`
   tải xong. Bấm "Lưu danh sách" ngay sau khi mở tab sẽ **âm thầm mời lại** người vừa bỏ.
3. **Hiện `Tenant.SanNha` làm địa điểm trận.** Trận thoả thuận đá ở *Sân Tuyên Sơn* nhưng trang
   hiện *Sân Chi Lăng* — người đọc đến sai sân. Xem mục "Vì sao không có địa điểm" ở
   `TrangDangKyNhanh`.

Cả ba giờ có E2E canh (`e2e/dang-ky-nhanh.spec.ts`), và đã phản chứng: đưa từng lỗi trở lại thì
test đỏ.

## Giới hạn tần suất và bộ test

Rate limit `XacThuc` (10/phút mỗi IP) chặn chính bộ E2E: 48 test, mỗi test tạo CLB rồi đăng nhập
2 lần từ **cùng một IP**, nên từ test thứ 5 trở đi `waitForURL` timeout ở bước đăng nhập — 8 test
đỏ vì lý do không liên quan tới thứ chúng kiểm.

Cách sai là nới hạn mức trong `GioiHanTanSuat`: production mất lớp bảo vệ chỉ vì test cần chạy
nhanh. Cách đúng: `GIOI_HAN_TAN_SUAT=false` trong `docker-compose.dev.yml` — tắt ở đúng môi trường
cần tắt. Cơ chế vẫn được canh bởi `GioiHanTanSuatTests` (factory riêng tự bật cờ).

Cùng vấn đề và cùng cách sửa với 112 integration test đỏ hôm 21/08.

## Trạng thái triển khai

✅ **Xong 21/08/2026.** Backend + tab Đăng ký trong chi tiết trận + trang ẩn danh.
17 integration test · 4 E2E test · 9 phản chứng (2 lọt lần đầu, đã siết).
