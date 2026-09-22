# Rà soát bảo mật luồng đăng nhập — 22/09/2026

> Phạm vi: **luồng xác thực** (đăng nhập, làm mới token, quên/đổi mật khẩu, một-phiên-mỗi-tài-khoản,
> endpoint ẩn danh ở màn đăng nhập). Không rà soát phân quyền nghiệp vụ hay tầng hạ tầng.
>
> Đối chiếu theo các hạng mục quen thuộc của OWASP (A01 Broken Access Control, A02 Cryptographic
> Failures, A07 Identification & Authentication Failures) và OWASP ASVS chương 2 (Authentication).

## Kết luận ngắn

Phần **mật mã học và quản lý token làm rất chắc** — PBKDF2, refresh token xoay vòng có phát hiện
tái sử dụng, hash token trong DB, `ClockSkew = 0`, fail-fast `JWT_SECRET`. 27 hạng mục đạt, liệt
kê ở cuối.

Khoảng trống thật nằm ở **vòng đời phiên** (không có đăng xuất phía server) và **chống dò/chống
thử** (không khoá tài khoản, kênh thời gian lộ username). Không có lỗ hổng nào cho phép **vượt
qua xác thực** hay **đọc dữ liệu trung tâm khác** — cách ly tenant đã kiểm và đúng.

---

## Trạng thái khắc phục

Chủ sản phẩm chốt ngày 22/09/2026: **làm lần lượt toàn bộ**. **8/8 mục đã vá.**

⚠️ Mục 7 khi triển khai sẽ **đá mọi người đang đăng nhập ra một lần** — chọn giờ thấp điểm và
báo trước cho trung tâm.

| Mục | Trạng thái |
|---|---|
| 1. Đăng xuất phía server | ✅ **XONG** 22/09 |
| 2. Mật khẩu admin mặc định | ✅ **XONG** 22/09 |
| 3. Kênh thời gian | ✅ **XONG** 22/09 |
| 4. Khoá tài khoản sau N lần sai | ✅ **XONG** 22/09 |
| 5. Security header ở tầng ứng dụng | ✅ **XONG** 22/09 |
| 6. Độ dài mật khẩu tối thiểu | ✅ **XONG** 22/09 (blocklist còn nợ) |
| 7. Token trong `localStorage` | ✅ **XONG** 22/09 — [ADR-0007](./kien-truc/adr/0007-refresh-token-cookie-httponly.md) |
| 8. Fail-open của `PhienDuyNhatMiddleware` | ✅ **XONG** 22/09 (làm cùng mục 1) |

---

## Cần sửa, theo thứ tự ưu tiên

### 1. ✅ ĐÃ SỬA — Không có đăng xuất phía server

`POST /auth/dang-xuat` **không tồn tại**. `dangXuat()` ở frontend chỉ xoá `localStorage`.

Hệ quả: bấm "Đăng xuất" xong, access token vẫn sống **tới 60 phút** và refresh token **tới 30
ngày**. `TaiKhoan.PhienHienTai` không được xoá nên `PhienDuyNhatMiddleware` vẫn cho token cũ đi
qua. Ai đọc được `localStorage` sau đó (máy dùng chung, extension, backup profile) dùng lại được.

Chú thích ở `TaiKhoan.cs:52` đã ghi *"`null` = chưa từng đăng nhập, **hoặc đã đăng xuất**"* —
tức ý định đã có, phần cài đặt thì chưa. **Đã kiểm chứng: không dòng nào set `PhienHienTai = null`.**

**Đã sửa 22/09/2026** — `POST /auth/dang-xuat` (`DangXuatCommand`) làm **ba việc cùng lúc**:
thu hồi refresh token, ghi `TaiKhoan.DaDangXuat` vào `PhienHienTai`, xoá cache phiên.

⚠️ **Không ghi `null`** như đề xuất ban đầu của bản rà soát này: `null` mang nghĩa *"chưa từng
đăng nhập"* và middleware **cố ý cho qua**, nên ghi `null` thì token vừa đăng xuất vẫn đi lọt —
tức thêm endpoint mà không chặn được gì. Dùng một `Guid` hằng làm giá trị đánh dấu.

Canh bởi `DangXuatTests` (7 test). Mutation "ghi `null` thay vì `DaDangXuat`" → chết.

### 2. ✅ ĐÃ SỬA — Mọi trung tâm mới có `admin` / `123456`

`TenantSeeder.cs:20` — `string matKhauAdmin = "123456"`, và `DangKyTrungTamController` gọi **không
truyền** tham số này.

Giảm nhẹ đã có: tự đăng ký **đang tắt mặc định** (22/09/2026), và middleware buộc đổi mật khẩu.
Nhưng `/auth/dang-nhap` **vẫn cho đăng nhập thành công** với cặp này, và `/auth/doi-mat-khau` nằm
trong allowlist của middleware — nên kẻ vào được sẽ tự đặt mật khẩu của mình và chiếm trung tâm.

**Đã sửa 22/09/2026** — mật khẩu admin **sinh ngẫu nhiên** (CSPRNG, 16 ký tự, ~92 bit), trả về
đúng một lần trong response đăng ký; DB chỉ giữ bản băm.

Đổi luôn kiểu trả về của `ITenantSeeder` thành `TenantMoi(Tenant, MatKhauAdmin)` để **trình biên
dịch bắt** mọi chỗ gọi phải xử lý — nếu chỉ sửa bên trong seeder thì controller vẫn trả
`"123456"` và cặp đó không đăng nhập được, tức vá bảo mật xong lại hỏng đăng ký.

Canh bởi `MatKhauAdminNgauNhienTests` (4 test), trong đó có test *"mật khẩu trả về PHẢI đăng
nhập được"* chốt đúng ca hỏng trên.

### 3. ✅ ĐÃ SỬA — Dò được username qua thời gian phản hồi

`DangNhapCommand.cs` — username **không tồn tại** thì `throw` ngay, **không chạy PBKDF2**;
username có thật thì chạy ~100k vòng băm. Chênh lệch hàng chục ms, đo được.

Chú thích ở đó nói rõ ý định *"cùng một mã lỗi"* — đúng về mã lỗi, nhưng **kênh thời gian thì
chưa xử lý**, nên biện pháp chống dò bị vô hiệu trên thực tế.

Cùng lỗi ở `QuenMatKhauCommand`, và **nặng hơn**: nhánh có email thật còn gửi SMTP **đồng bộ**
(chênh hàng trăm ms tới vài giây), trong khi cả hai nhánh đều trả 204 giống nhau.

**Sửa:** băm một hash giả khi không tìm thấy tài khoản; đẩy gửi email sang chạy nền.

### 4. ✅ ĐÃ SỬA — Không khoá tài khoản sau nhiều lần sai

Rate limit là **10 lần/phút theo IP**. Không có cột đếm lần sai, không có khoá tạm.

Hệ quả: credential stuffing phân tán (botnet, proxy pool) thử mật khẩu vào **cùng một tài khoản**
mà không bao giờ bị chặn — mỗi IP vẫn dưới hạn mức. Nginx lớp ngoài cũng theo IP nên cùng điểm mù.

Cộng với **chính sách mật khẩu tối thiểu 6 ký tự** (mục 6), đây là rủi ro thực tế nhất trong danh
sách này.

**Sửa:** thêm phân vùng rate limit theo `{maTrungTam}:{username}` song song với theo IP.

### 5. 🟠 Header bảo mật chỉ có ở nginx, không có lớp đáy trong ứng dụng

Không có `UseHsts()`, không CORS, không `X-Frame-Options`/`nosniff` ở tầng ứng dụng.
`UseHttpsRedirection()` bị tắt khi `SAU_REVERSE_PROXY=true` (đúng cấu hình production).

Toàn bộ header nằm ở `deploy/nginx/langcenter.conf` — tệp **phải copy tay lên VPS**. Cài sai hoặc
quên reload thì production chạy không HSTS, không nosniff, không X-Frame-Options. Chính chú thích
trong hai tệp cấu hình xác nhận **việc này đã từng xảy ra**.

**Đã sửa 22/09/2026** — `HeaderBaoMatMiddleware` đặt `X-Content-Type-Options`,
`X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` trên **mọi** phản hồi của API, và
HSTS khi request thật sự là HTTPS.

Ba điểm đáng lưu ý:

- **`TryAdd`, không ghi đè** — Nginx đã đặt thì giữ của Nginx. Ghi đè sẽ làm hai nơi cấu hình
  âm thầm đá nhau và người sửa Nginx không hiểu vì sao thay đổi của mình vô tác dụng.
- **Đặt qua `OnStarting`** — endpoint trả stream (ảnh qua MinIO) bắt đầu gửi rất sớm; thêm
  header sau đó sẽ ném `InvalidOperationException`.
- **HSTS chỉ khi thật sự HTTPS** — gửi qua HTTP thuần là vô nghĩa, mà ở dev chạy
  `http://localhost` thì nó **khoá luôn localhost sang HTTPS** trong trình duyệt lập trình
  viên: lỗi rất khó chẩn đoán vì nằm trong cache trình duyệt, không nằm trong mã.

**Không đặt CSP ở đây**: CSP phụ thuộc thứ frontend thật sự tải (`blob:`, `data:`…), nên thuộc
về nơi phục vụ **trang**, không phải nơi phục vụ **API**. Nginx giữ CSP.

Canh bởi `HeaderBaoMatTests` (5 test), gồm test *"header có cả trên phản hồi LỖI"* — nửa hay bị
quên, và là nửa quan trọng hơn vì trang lỗi chính là thứ kẻ tấn công muốn nhúng iframe.
Verify bằng `curl` trên API thật.

### 6. 🟡 Mật khẩu tối thiểu 6 ký tự, không có blocklist

5 chỗ dùng `MinimumLength(6)`. Không kiểm độ phức tạp, không blocklist mật khẩu phổ biến.
`123457` là hợp lệ. Khuyến nghị hiện hành (NIST SP 800-63B) là **tối thiểu 8–12** + blocklist,
và bỏ yêu cầu ký tự đặc biệt.

### 7. ✅ ĐÃ SỬA — Token trong `localStorage`

**Đã sửa 22/09/2026** theo [ADR-0007](./kien-truc/adr/0007-refresh-token-cookie-httponly.md):
refresh token vào cookie `httpOnly; Secure; SameSite=Lax; Path=/api/v1/auth`, access token vào
RAM, thêm double-submit chống CSRF.

Việc này **lôi ra ba lỗi có sẵn từ trước** mà `localStorage` che đi — đọc chi tiết trong ADR:
đổi mật khẩu tự giết phiên của chính mình; `React.StrictMode` tự kích hoạt cơ chế chống trộm;
và phiên bị đẩy ra kéo theo phiên của người vừa đăng nhập (phải thêm cột `REFRESH_TOKEN.ly_do`
mới sửa đúng gốc).

### 8. ✅ ĐÃ SỬA — `PhienDuyNhatMiddleware` fail-open không còn lý do tồn tại

Token thiếu `jti`/`TaiKhoanId`, hoặc `PhienHienTai == null` → **cho qua**. Đây là nhượng bộ tương
thích cho token phát trước 20/09/2026; access token chỉ sống 60 phút nên **những token đó đã chết
từ lâu**.

**Đã sửa 22/09/2026, cùng lúc với mục 1** — token thiếu `jti`/`TaiKhoanId` nay **bị chặn** (401
`PHIEN_DA_BI_DAY_RA`) thay vì cho qua.

Hai việc bắt buộc đi cùng nhau: nếu đăng xuất ghi dấu vào `PhienHienTai` mà nhánh fail-open còn
đó thì middleware vẫn cho qua token vừa bị đăng xuất.

Canh bởi `DangXuatTests.Token_KHONG_co_jti_bi_chan_chu_khong_cho_qua` — viết **sau khi một
mutation SỐNG** cho thấy lúc đó chưa có gì canh bản vá này.

---

## Bổ sung 22/09/2026 — nhật ký hành vi đăng nhập

Chủ sản phẩm yêu cầu *"nhớ log cả hành vi đăng nhập"*. Kiểm lại thì đăng nhập **đã** được ghi
(mọi lệnh `...Command` đi qua `NhatKyBehavior`), nhưng đo trên DB thật lộ ra **hai lỗ hổng**:

| | Vấn đề | Đo được |
|---|---|---|
| 1 | **Không ghi lần THẤT BẠI** | 61 bản ghi đăng nhập, **0 thất bại** |
| 2 | **Username ghi nhầm người** | Vài dòng `username` lệch hẳn với `ThamSo` |

Nguyên nhân chung: `GhiNhatKy` lấy tenant/username từ JWT, mà lệnh đăng nhập **chưa có JWT**.
Không có tenant thì nó `return` sớm ⇒ mất trắng lần thất bại; còn `ICurrentUser` thì vẫn mang
danh tính của request **trước** trong cùng kết nối ⇒ ghi nhầm người.

Lỗ hổng 1 nghiêm trọng hơn vẻ ngoài: nhật ký chỉ kể chuyện thành công là **vô dụng đúng lúc
cần điều tra**. Dò mật khẩu không để lại vết nào.

**Đã sửa**: thêm `ILenhXacThuc` — lệnh xác thực tự khai mã trung tâm + username, behavior tra
ra `TenantId`. Làm bằng interface trên command chứ không sửa behavior, để thêm lệnh xác thực
mới không thể quên.

Nay ghi đủ: **sai mật khẩu**, **username không tồn tại** (dấu vết rõ nhất của người đang dò),
và **bị khoá tạm**. Kèm IP. Mật khẩu vẫn bị lọc thành `***`.

Còn một ca **không ghi được**: sai **mã trung tâm**. Bảng nhật ký tách theo tenant nên không
biết ghi vào đâu. Chấp nhận — ca đó cũng ít giá trị vì không rõ nhắm vào ai.

Canh bởi `NhatKyDangNhapTests` (5 test). Không test IP tự động vì máy chủ test trong bộ nhớ
không đặt `RemoteIpAddress`; đã kiểm bằng tay trên API thật (`::1`), lý do ghi trong tệp test.

---

## Xem xét rồi KẾT LUẬN LÀ KHÔNG PHẢI LỖ HỔNG

Ghi lại để lần rà soát sau không mất công điều tra lại:

| Nghi ngờ | Vì sao không phải |
|---|---|
| Đăng nhập trả `TAI_KHOAN_BI_VO_HIEU_HOA` khác `DANG_NHAP_THAT_BAI` → dò username? | Kiểm `VoHieuHoa` đặt **sau** kiểm mật khẩu, nên mã đó chỉ trả khi mật khẩu **đúng**. Thiết kế đúng. |
| `TraTenTrungTamQuery` trả tên/mô tả/địa chỉ cho người ẩn danh | Có chủ ý và có lập luận: không trả `Id`, không `LienHe`, không số tài khoản; chỉ khớp **chính xác** mã 7 ký tự (`31^7 ≈ 27 tỷ`), có rate limit. Là bề mặt cần theo dõi, không phải lỗ hổng. |
| So sánh hash token không dùng hằng thời gian | So sánh nằm trong PostgreSQL trên B-tree unique index, không phải so byte trong bộ nhớ ứng dụng. Kênh thời gian gần như không khai thác được qua mạng. |
| Cache phiên 10 giây | Đánh đổi minh bạch và hợp lý, đã ghi chú tại chỗ. |
| Không cấu hình CORS | Không cấu hình = không origin chéo nào được phép. An toàn theo mặc định; frontend cùng origin qua nginx. |

---

## Đã làm tốt (đã đối chiếu mã nguồn)

**Mật mã & token**
1. PBKDF2-HMAC-SHA256, salt riêng từng mật khẩu, 100k vòng (Identity mặc định)
2. Hash hỏng không thành 500 — không lộ tài khoản nào tồn tại
3. Refresh token 64 byte CSPRNG, **không** dùng `Guid`
4. Refresh token lưu **hash SHA-256**, không lưu thô
5. Refresh token **xoay vòng** — token cũ chết ngay khi cấp mới
6. **Phát hiện tái sử dụng** token đã thu hồi → thu hồi **toàn bộ** phiên + ghi log cảnh báo
7. Token đặt lại mật khẩu: 48 byte CSPRNG, hash trong DB, **dùng một lần**, hạn 30 phút, vô hiệu token cũ
8. Đổi/đặt lại mật khẩu **thu hồi mọi refresh token** đang mở

**Cấu hình JWT**
9. `alg=none` bị chặn (`ValidateIssuerSigningKey` + khoá đối xứng, HS256 tường minh)
10. Kiểm cả issuer và audience
11. `ClockSkew = 0` — không cho thêm 5 phút mặc định
12. `JWT_SECRET` fail-fast ≥32 ký tự **lúc khởi động**, không đợi tới lúc phát hành

**Chống dò & chống lộ**
13. Sai mã trung tâm / sai username / sai mật khẩu → **cùng một mã lỗi**
14. Quên mật khẩu luôn trả 204, không tiết lộ email có tồn tại (còn kênh timing — mục 3)
15. Không trả `exception.Message` ra client, chỉ mã lỗi
16. Không lộ cấu trúc nội bộ .NET qua lỗi model-binding
17. Log **không** ghi mật khẩu/token — che đệ quy cả object lồng nhau
18. Endpoint logo/ảnh bìa ẩn danh nhận **mã trung tâm**, không nhận khoá tự do

**Phiên & phân quyền**
19. Một phiên mỗi tài khoản, có hiệu lực **ngay**, không đợi 60 phút
20. Buộc đổi mật khẩu chặn ở **middleware**, không phó mặc frontend
21. Token thiếu claim tenant → 401, **không** fail-open
22. Đổi mật khẩu dùng `TaiKhoanId` **từ claim**, không nhận id từ client
23. Không có nhánh "nếu là admin thì cho qua" trong phân quyền
24. Cách ly tenant: tenant lấy từ **claim do server ký**, không từ header/body; 4 chỗ `IgnoreQueryFilters()` trong luồng đăng nhập đều lọc `TenantId` tường minh hoặc tra bằng hash duy nhất

**Frontend & hạ tầng**
25. Xoá sạch cache quyền + query khi đổi phiên
26. Không thử làm mới khi `PHIEN_DA_BI_DAY_RA`; có khử đua refresh song song
27. Rate limit dùng **cửa sổ trượt**, `QueueLimit = 0`, có `Retry-After`

---

## Ghi chú về "nhớ đăng nhập" (22/09/2026)

Chức năng thêm cùng ngày rà soát này **cố ý không lưu mật khẩu** — chỉ nhớ mã trung tâm và tên
đăng nhập. Xem `frontend/src/lib/nhoDangNhap.ts`. Nếu lưu mật khẩu, nó sẽ nằm trong
`localStorage` cùng chỗ với token, và khác token ở chỗ **không thu hồi được**.

Ô này **mặc định tắt**: trung tâm có máy dùng chung ở quầy lễ tân, điền sẵn tên đăng nhập của
người trước là nói cho người sau biết ai vừa dùng máy.
