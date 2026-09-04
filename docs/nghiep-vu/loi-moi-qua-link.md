# FR-18 — Lời mời thách đấu qua link/QR

> ⚠️ **Tài liệu của DỰ ÁN CŨ** (quản lý CLB đá bóng phong trào). Từ 05/09/2026 repo này là
> base cho hệ thống quản lý trung tâm ngoại ngữ — **phần nghiệp vụ dưới đây không còn trong
> code**. Giữ lại để tham khảo cách viết đặc tả. Xem [`CLAUDE.md`](../../CLAUDE.md) mục 1.

> Bổ sung 20/08/2026. Liên quan: [FR-17 Cộng đồng](./lich-thi-dau.md#fr-17--cộng-đồng-bổ-sung-18082026)
> · [multi-tenant.md](../backend/multi-tenant.md) · [ADR-0005](../kien-truc/adr/0005-loi-moi-qua-link.md)

## Vấn đề đang giải

Hôm nay có hai loại đối thủ, và chúng không nói chuyện được với nhau:

| Loại | Cách tạo | Hạn chế |
|---|---|---|
| **Tên gõ tay** | Gõ "FC Sông Hàn" vào sổ đối thủ | Chỉ là một chuỗi. Không có thành tích, không đối đầu được, không hiện trên Cộng đồng |
| **CLB có ID** | Tra mã 7 ký tự, hoặc chọn trên Cộng đồng | Chỉ dùng được nếu họ ĐÃ đăng ký hệ thống |

Đội phong trào gần như luôn ở tình huống thứ nhất: bạn hẹn đá với một đội qua Zalo, họ chưa dùng
app. Bạn gõ tên họ vào, đá xong, và dữ liệu đối đầu không bao giờ khớp giữa hai bên.

**FR-18 bắc cầu giữa hai loại đó:** bạn gửi một link/QR, họ bấm vào, đăng nhập hoặc tạo đội mới,
chấp nhận — và đối thủ "chỉ là cái tên" **nâng cấp thành CLB có ID thật**, cùng với trận đấu.

## Luồng chính

```
Bạn                                    Họ
───                                    ──
1. Tạo trận với "FC Sông Hàn"
   (đối thủ tên gõ tay)
2. Bấm "Mời qua link"
   → sinh token + link + QR
   → gửi thư vào hòm thư (nếu họ đã có tenant)
3. Copy link gửi Zalo ─────────────────►
                                       4. Mở link (KHÔNG cần đăng nhập)
                                          → xem: ai mời, trận nào, khi nào, ở đâu
                                       5a. Đã có tài khoản → đăng nhập
                                       5b. Chưa có → tạo đội mới
                                       6. Xác nhận danh tính:
                                          "Bạn chấp nhận với tư cách FC X (mã ...)?"
                                       7. Chấp nhận / Từ chối
◄──────────────────────────────────────
8. `DoiThu.MaDoiHeThong` ← mã đội của họ
   Trận bên họ được tạo
   Hai bên thấy nhau trên Cộng đồng
```

## Vì sao trang xem lời mời phải CÔNG KHAI

Người nhận **chưa có tài khoản** — bắt đăng nhập trước khi xem là yêu cầu họ tạo đội cho một lời
mời họ chưa biết nội dung. Nên tách hai quyền:

| Hành động | Cần đăng nhập? | Lộ gì |
|---|---|---|
| **Xem** lời mời (qua token) | Không | Tên CLB mời · thời gian · địa điểm · lời nhắn. **Không** lộ cầu thủ, quỹ, thành tích |
| **Chấp nhận / Từ chối** | Có | — |

Token là thứ duy nhất bảo vệ trang xem, nên nó phải dài và không đoán được — dùng chung cơ chế với
token đặt lại mật khẩu: sinh ngẫu nhiên, **lưu hash** trong DB, không lưu token thô.

## Mười ba trường hợp và cách xử lý

### Bốn ca chính

| # | Tình huống | Xử lý |
|---|---|---|
| **1** | Họ **đã có** tenant, bạn mời qua link (không đích danh) | Bấm link → đăng nhập → chấp nhận → gán `MaDoiHeThong`, tạo trận bên họ. Kết quả **giống hệt** như bạn đã mời theo ID từ đầu |
| **2** | Họ **chưa có** tenant | Bấm link → tạo đội mới ngay tại đó → chấp nhận → xử lý như ca 1 |
| **3** | Họ **từ chối** | Trận của bạn **giữ nguyên**, `MaDoiHeThong` vẫn null. Không xoá trận: bạn vẫn đá với họ ngoài hệ thống. Lưu lý do từ chối |
| **4** | Bạn mời **đích danh** theo ID/Cộng đồng | Dùng luồng FR-17 hiện có, **không** sinh link. Ẩn nút "Mời qua link" cho đối thủ đã có `MaDoiHeThong` — hai đường làm cùng một việc là nguồn của lỗi |

### Tám ca biên

| # | Tình huống | Xử lý | Vì sao |
|---|---|---|---|
| **5** | Link bị **chuyển tiếp cho người lạ** | Bắt **xác nhận danh tính** trước khi chấp nhận ("với tư cách FC X, mã ..."). Ghi lại tenant đã chấp nhận. Cho người mời **huỷ liên kết** nếu sai người | Link chia sẻ được là bản chất, không chống tuyệt đối. Nhưng phải **phát hiện và hoàn tác được** |
| **6** | Link **hết hạn** | Hạn = ngày trận + 1, hoặc 30 ngày nếu chưa hẹn giờ. Trang nói rõ "Lời mời đã hết hiệu lực" + cách liên hệ lại | 404 im lặng làm người nhận tưởng link sai và bỏ luôn |
| **7** | Bạn **thu hồi** lời mời trước khi họ bấm | Token vô hiệu ngay. Trang hiện "Lời mời đã được thu hồi" | |
| **8** | Link **dùng lần thứ hai** | Token dùng-một-lần cho hành động **chấp nhận**; lần sau chỉ *xem* kết quả. Không tạo trận thứ hai | Cùng cơ chế token đặt lại mật khẩu |
| **9** | Họ có nhiều CLB, đăng nhập **sai đội** | Cho chấp nhận với CLB **đang đăng nhập**, nhưng hỏi rõ trước (ca 5). Không ép đúng một CLB | Ép sẽ chặn oan người quản nhiều đội |
| **10** | Hai bên **mời chéo** cùng lúc | Khi liên kết xong, kiểm có trận cùng ngày cùng đối thủ chưa. Có thì **gộp**, báo cả hai bên | Hai trận trùng làm thống kê đếm đôi |
| **11** | Trận **đã đá xong** rồi mới liên kết | Cho liên kết để lịch sử đối đầu đếm được, nhưng **không tạo trận mới bên họ** | Trận đã qua, bên họ không có dữ liệu đánh giá gì. Tạo ra chỉ thành trận rỗng trong lịch họ |
| **12** | Tên bạn gõ **khác** tên CLB thật của họ | **Giữ nguyên** tên trong sổ của bạn, hiện thêm tên thật + mã đội bên dưới | Đổi tên trong sổ của bạn là sửa dữ liệu bạn không yêu cầu (quy tắc #1) |
| **13** | Bạn **đã có** một đối thủ khác trỏ về cùng CLB đó | **Gộp**: chuyển mọi trận (và lời mời cũ) sang bản ghi vừa được mời, xoá bản trùng | Hai bản ghi cùng `ma_doi_he_thong` làm thành tích đối đầu đếm sai — mỗi bản chỉ thấy phần trận của nó. **Phát hiện khi xem màn Đối thủ sau khi chạy luồng thật**, không test nào bắt được lúc đó |

## Hai quyết định phụ thuộc

### Đăng ký CLB phải MỞ ở production

Luồng ca 2 không chạy được nếu `/dang-ky-clb` còn bị chặn — đây chính là
[nợ N4](../ke-hoach.md). **Quyết định:** mở tự do (chủ sản phẩm: *"người dùng có thể tạo đội bóng
qua trang đăng nhập"*), kèm:

- **Rate limit ở tầng Caddy** — không thì một script tạo được vô hạn CLB rác, và chúng hiện hết
  lên Cộng đồng của mọi người. Gộp cùng [nợ N3](../ke-hoach.md).
- Trang đăng nhập hiện lại link "Tạo câu lạc bộ" khi cờ `dangKyClb` bật.

### Thông báo: trong hệ thống + copy link

"Gửi thư tới hòm thư đội đối thủ" chỉ làm được khi họ **đã có** tenant. Chưa có thì không có hòm
thư nào để gửi.

**Quyết định:** thư trong hệ thống (khi có tenant) + nút **copy link/QR** để người mời tự gửi qua
Zalo. Email/SMS để sau khi có hạ tầng — và đó cũng là cách CLB phong trào làm thật, giống nút
"sao chép danh sách nợ" ở FR-16.

## Điều gì KHÔNG thay đổi

- `DoiThu.MaDoiHeThong` vẫn là **chuỗi, không FK** — lý do ở [FR-10](./lich-thi-dau.md#tra-cứu-clb-khác-trong-hệ-thống).
- Trận vẫn **hai bản độc lập**, mỗi bên một trận trong lịch của mình. Trận dùng chung sẽ buộc một
  CLB sửa dữ liệu nằm trong tenant của CLB kia.
- Cộng đồng vẫn **không lộ** cầu thủ, quỹ, số tài khoản.


## Đã kiểm

- **20 test tích hợp** (`MoiQuaLinkTests`) phủ cả 13 ca, canh **9 phản chứng**.
- Chạy đầu-cuối trên PostgreSQL thật với hai CLB: link → xem khi chưa đăng nhập → đăng nhập →
  xác nhận danh tính → chấp nhận → trận vào lịch cả hai bên → đối thủ nâng cấp có mã đội.

### Hai phản chứng lọt ở lần đầu

| Phá gì | Vì sao lọt |
|---|---|
| Thêm `IgnoreQueryFilters()` vào truy vấn **thu hồi** | Test cách ly chỉ canh chiều ĐỌC (danh sách), không canh chiều GHI qua id trực tiếp. Một CLB biết id là thu hồi được link của người khác |
| Không có test cho ca 13 | Ca này chỉ lộ khi **xem màn Đối thủ** sau khi chạy luồng thật — thấy hai dòng cùng mã `GACKQD2` |
