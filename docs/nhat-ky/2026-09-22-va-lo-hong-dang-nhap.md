# 22/09/2026 — Vá 7/8 lỗ hổng luồng đăng nhập

Chủ sản phẩm xem bản rà soát rồi chốt: *"lần lượt toàn bộ"*. Hỏi lại đúng một điều — **mục 7**
(chuyển refresh token sang cookie `httpOnly`) là thay đổi kiến trúc, cần ADR và khi triển khai
sẽ **đá mọi người đang đăng nhập ra**. Chốt: làm 1→6 và 8 trước, mục 7 bàn riêng.

Đợt này xong **1, 2, 3, 4, 5, 6, 8** — tức tất cả trừ mục 7.

---

## Mục 1 + 8 — đăng xuất thật (bắt buộc đi cùng nhau)

`POST /auth/dang-xuat` **không tồn tại**. Nút "Đăng xuất" chỉ xoá `localStorage`, nên bấm xong
thì access token vẫn sống 60 phút và refresh token 30 ngày.

### Cái bẫy suýt dẫm phải

Bản rà soát đề xuất: *"thu hồi refresh token + `PhienHienTai = null` + xoá cache"*. Làm đúng
theo đó thì **endpoint mới không chặn được gì** — vì `null` đã mang nghĩa *"chưa từng đăng nhập
kể từ 20/09"* và middleware **cố ý cho qua** giá trị đó.

Chú thích trong `TaiKhoan.cs` viết sẵn *"`null` = chưa từng đăng nhập, **hoặc đã đăng xuất**"* —
tức người viết hôm 20/09 đã hình dung luồng đăng xuất, nhưng hai nghĩa ấy **không thể dùng chung
một giá trị**: một cái phải cho qua, một cái phải chặn.

Thay bằng `TaiKhoan.DaDangXuat` — một `Guid` hằng. Không `jti` nào trùng được (jti sinh ngẫu
nhiên), nên mọi token của tài khoản đều lệch ⇒ đều bị chặn. Không thêm cột, không migration,
không đụng token đang lưu hành.

### Và vì sao mục 8 phải làm cùng

`PhienDuyNhatMiddleware` cho qua token thiếu `jti` — nhượng bộ tương thích từ 20/09. Access
token chỉ sống 60 phút nên token kiểu đó đã chết từ lâu; nhánh ấy giờ chỉ còn là **đường vòng
fail-open**. Nếu giữ lại, ai gửi được token không `jti` sẽ bỏ qua cả cơ chế một-phiên **lẫn
đăng xuất**.

### Một mutation SỐNG

Gỡ bản vá fail-open mà **cả bộ test vẫn xanh** — tức lúc đó không có gì canh nó. Phải viết thêm
`Token_KHONG_co_jti_bi_chan_chu_khong_cho_qua`: dựng token thiếu `jti` nhưng **ký bằng đúng khoá
thật** của môi trường test. Ký sai khoá thì 401 đến từ tầng xác thực và test xanh giả.

Đây đúng là loại lỗ hổng mà mutation test sinh ra để tìm: mã đã sửa đúng, test đã xanh, nhưng
không có mối liên hệ nào giữa hai thứ đó.

### Một test tôi đoán sai

Tôi viết `Dang_xuat_hai_lan_van_tra_204` với phỏng đoán lần hai sẽ **401**. Thực tế **204** —
vì `/auth/*` cố ý nằm ngoài middleware phiên. Hành vi thật **tốt hơn** phỏng đoán (đăng xuất
lặp lại được), nên sửa test theo sản phẩm và giữ lại ghi chú, vì người đọc sau có thể cũng đoán
nhầm như vậy.

---

## Mục 2 — mật khẩu admin ngẫu nhiên

`TenantSeeder` băm hằng `"123456"`, còn `DangKyTrungTamController` **tự viết** `matKhau =
"123456"` vào response. Hai chỗ không liên quan nhau về mã, chỉ **tình cờ cùng giá trị**.

Nên nếu chỉ sửa seeder sinh ngẫu nhiên, controller vẫn trả `"123456"` và **không ai đăng nhập
được vào trung tâm vừa tạo** — vá bảo mật xong lại hỏng đăng ký.

Chữa bằng kiểu dữ liệu, không bằng sự cẩn thận: đổi `ITenantSeeder` trả `TenantMoi(Tenant,
MatKhauAdmin)`. Trình biên dịch lập tức chỉ ra **4 chỗ gọi** phải sửa, trong đó có đúng chỗ
controller. Không thể quên.

Bộ ký tự bỏ `0/O` và `1/l/I`: mật khẩu này người ta đọc từ màn hình rồi gõ lại, có khi đọc qua
điện thoại cho nhau — nhầm một ký tự là mất quyền vào trung tâm vừa tạo.

---

## Mục 3 — kênh thời gian

Chú thích cũ ghi *"Sai mã trung tâm, sai username, sai mật khẩu → CÙNG một mã lỗi"*. Đúng về mã
lỗi, nhưng nhánh không tìm thấy `throw` ngay mà **không chạy PBKDF2** (~100k vòng). Chênh lệch
hàng chục mili-giây đo được qua mạng ⇒ biện pháp chống dò bị vô hiệu trên thực tế.

Thêm `IPasswordHasher.BamGia()` — chạy đúng một lần băm rồi bỏ kết quả. **Không** `Thread.Sleep`
một hằng số: thời gian băm phụ thuộc tải máy và số vòng cấu hình, hằng số ngủ sẽ lệch, và lệch
theo hướng ngược lại cũng lộ.

Quên mật khẩu còn rò to hơn — nhánh "email có thật" gửi **SMTP đồng bộ**, chênh hàng trăm ms tới
vài giây trong khi cả hai nhánh đều trả 204. Đưa việc gửi ra khỏi đường trả lời.

### Test đo thời gian mà không thất thường

Đo giờ trong test rất dễ đỏ oan. Cách làm: **làm nóng trước** (bỏ lần gọi đầu), lấy **trung vị**
chứ không trung bình, và **ngưỡng rộng** (hệ số 5, không phải mili-giây tuyệt đối).

Lý lẽ: test này không đo chính xác vài ms, nó bắt loại lỗi *"một nhánh bỏ hẳn phép băm"* —
chênh khi đó là hàng chục lần. Thà bỏ lọt chênh lệch nhỏ còn hơn có test đỏ ngẫu nhiên rồi bị
ai đó tắt đi; lúc ấy thì không còn gì canh cả.

### Một chỗ trống CỐ Ý

**Không** viết test đo thời gian cho quên mật khẩu. `ApiFactory` thay `IEmailSender` bằng
`TestEmailSender` chạy trong bộ nhớ, nên nhánh "có thật" vốn đã nhanh — test sẽ xanh **kể cả
khi gỡ bản vá**. Một test xanh-bất-kể-đúng-sai tệ hơn không có test, vì nó làm người đọc tin
rằng chỗ đó đang được canh. Ghi lý do ngay trong `KenhThoiGianTests`.

---

## Mục 4 — khoá tạm sau nhiều lần sai

Rate limit cũ 10 req/phút **mỗi IP**. Chặn được một máy thử hàng nghìn mật khẩu, nhưng không
chặn được botnet: mỗi IP thử 10 lần/phút vào **cùng một tài khoản**, IP nào cũng dưới hạn mức
nên không bao giờ bị chặn, còn tổng số lần thử thì vô hạn.

Đếm theo `{mã trung tâm, username}`, khoá **15 phút** sau **10 lần** sai.

Bốn quyết định, mỗi cái tránh một cách hỏng:

| Quyết định | Nếu làm ngược lại |
|---|---|
| Khoá **tạm**, tự mở | Ai cũng khoá được tài khoản người khác bằng cách gõ sai vài lần |
| Hết hạn **tuyệt đối**, không trượt | Kẻ tấn công thử thêm là gia hạn khoá ⇒ giữ nạn nhân đóng vĩnh viễn |
| Đếm theo **{mã, username}**, không theo id tài khoản | Không đếm được username không tồn tại ⇒ hai nhánh khác nhau ⇒ tạo lại đúng kênh dò mục 3 vừa bịt |
| **10 lần**, không phải 3 | Người gõ nhầm thật bị khoá liên tục, tổng đài nhận việc nhiều hơn là chặn được tấn công |

### Lỗi im lặng nhất của mục này

Bộ đếm đăng ký DI **phải là Singleton**. Nhầm sang `Scoped` thì mỗi request có bộ đếm mới, luôn
bằng 0, cơ chế thành vô dụng — **không có lỗi nào báo, mọi test khác vẫn xanh**. Nên có riêng
một test kiểm thẳng đăng ký DI (`Assert.Same`), và mutation đổi sang `Scoped` → chết.

---

## Mục 5 — header bảo mật ở tầng ứng dụng

Toàn bộ header chỉ có trong `deploy/nginx/langcenter.conf` — tệp **phải copy tay lên VPS**, mà
certbot còn tự chèn thêm block 443. Cài sai, quên reload, hay dựng môi trường mới mà bỏ sót thì
production chạy trần trụi.

Không phải rủi ro lý thuyết: chú thích trong chính `langcenter.conf` và `Caddyfile.dev` xác nhận
**đã từng xảy ra** và kéo dài.

`HeaderBaoMatMiddleware` là **lớp đáy**, không thay Nginx. Ba chi tiết:

- **`TryAdd`, không gán** — Nginx đặt rồi thì giữ của Nginx. Ghi đè thì hai nơi cấu hình âm thầm
  đá nhau, và người sửa Nginx sẽ không hiểu vì sao thay đổi của mình vô tác dụng.
- **Qua `OnStarting`** — endpoint trả stream (ảnh MinIO) bắt đầu gửi rất sớm; thêm header sau đó
  ném `InvalidOperationException`.
- **HSTS chỉ khi thật sự HTTPS** — gửi qua `http://localhost` ở dev sẽ **khoá luôn localhost
  sang HTTPS** trong trình duyệt lập trình viên. Lỗi đó nằm trong cache trình duyệt chứ không
  trong mã, nên rất khó chẩn đoán.

**Không** đặt CSP ở đây: nó phụ thuộc thứ **trang** tải (`blob:` cho ảnh qua axios, `data:` cho
favicon), không phải thứ **API** trả. Đoán mò một CSP ở tầng API thì hoặc quá chặt (ảnh biến mất
không lỗi nào hiện) hoặc quá lỏng (vô nghĩa).

Test đáng nói nhất: *"header có cả trên phản hồi LỖI"* — nửa hay bị quên, và là nửa quan trọng
hơn, vì trang lỗi mới là thứ kẻ tấn công muốn nhúng iframe. Nginx phải dùng `always` cũng vì lý
do đó.

---

## Mục 6 — mật khẩu tối thiểu 12 ký tự

Từ 6 lên 12. Cộng với việc **trước đây không khoá tài khoản** sau nhiều lần sai (mục 4, vá cùng
đợt), 6 ký tự là ngắn một cách nguy hiểm.

**Không** thêm luật "phải có hoa/thường/số/ký tự đặc biệt". Theo NIST SP 800-63B luật đó phản
tác dụng: người dùng đáp ứng bằng `Matkhau@123` — dễ đoán hơn hẳn một cụm từ dài — rồi dán lên
màn hình vì không nhớ nổi.

### Năm bản sao của một quy tắc

`MinimumLength(6)` nằm rải ở **5 nơi** backend, cộng **5 ô** `minLength={6}` ở frontend. Mười
chỗ chép tay cùng một con số. Sửa từng chỗ thì lần sau vẫn thế, nên gom vào `ChinhSachMatKhau`
(backend) và `DO_DAI_MAT_KHAU_TOI_THIEU` (frontend).

Frontend cũng phải sửa, không chỉ backend: để `minLength={6}` trong khi server đòi 12 thì trình
duyệt cho bấm Lưu rồi server mới từ chối — người dùng không hiểu vì sao form "hợp lệ" lại lỗi.
Bản dịch `MAT_KHAU_QUA_NGAN` cũng ghi số 6, sửa nốt kẻo nó nói dối.

### 157 test đỏ

Đổi ngưỡng làm **157 test đỏ** cùng lúc — chúng tạo tài khoản với `matkhau123` (10 ký tự),
`manager123` (10), `player123` (9). Không phải lỗi: đó là fixture, không phải khẳng định về
chính sách. Kéo dài các giá trị đó là xong.

### Còn nợ

**Blocklist mật khẩu phổ biến** chưa làm. Blocklist tử tế cần đóng gói một danh sách lớn, còn
HIBP là gọi ra dịch vụ ngoài — cả hai là quyết định riêng, không nên lặng lẽ gộp vào một thay
đổi về độ dài. Ghi ở `docs/ra-soat-bao-mat-dang-nhap.md`.

---

## Kết quả

**619 test backend xanh** (từ 585, +34) · **40 frontend** · **48 E2E xanh**.

Verify trên PostgreSQL thật với nick `co.lan` của tenant W686AE9: đăng nhập 200 → đăng xuất 204
→ access token cũ **401** → làm mới token **400** → đăng nhập lại **200**. Tạo hai trung tâm
thật, nhận hai mật khẩu ngẫu nhiên khác nhau. `curl` xác nhận header bảo mật có trên cả phản hồi
200 lẫn 401, và **không** có HSTS trên HTTP thuần.

**Còn lại mục 7** (refresh token sang cookie `httpOnly`) — để riêng theo quyết định của chủ sản
phẩm, cần ADR.

## Một test E2E đỏ — và lần này KHÔNG phải flaky

`mot-phien-moi-tai-khoan` đỏ. Lần trước cũng nó, và lần trước đúng là flaky (chạy riêng thì
xanh). Lần này chạy riêng **đỏ đều** ⇒ là hồi quy thật.

Nguyên nhân: test bọc `catch` cho `goto` (interceptor tự điều hướng cắt ngang, đã biết từ trước)
nhưng **không bọc cho `waitForURL`**. Thêm `POST /auth/dang-xuat` làm mỗi lần thoát phiên có
thêm một request, cửa sổ đua rộng ra, và cú cắt ngang bắt đầu rơi vào đúng lúc `waitForURL` đang
chờ.

Sản phẩm đúng (ảnh chụp lúc lỗi cho thấy trang **đã** ở màn đăng nhập; kiểm bằng `curl` cũng
thấy 401 kèm `PHIEN_DA_BI_DAY_RA`). Sửa test: bọc `catch` rồi khẳng định bằng `expect(...)
.toHaveURL` — `expect` tự thử lại nên không quan tâm điều hướng bị cắt mấy lần, chỉ quan tâm
**đích đến**.

Nới lỏng test thì phải kiểm lại nó còn bắt lỗi không: mutation gỡ hẳn đoạn chặn phiên trong
middleware → test **vẫn đỏ**. Còn canh.
