# 08/09/2026 — Chia thành ba hệ thống con HRM · CRM · LMS

Yêu cầu: hệ thống lớn là ba hệ thống nhỏ chia theo nhóm quyền; màn phân quyền hiện quyền theo
từng hệ thống; tài khoản có đủ ba quyền thì có bộ chuyển đặt gần Đăng xuất, và **sidebar chỉ
hiện quyền của hệ thống đang chọn, không gộp tất cả**.

## Quyết định đầu tiên: KHÔNG tách service

Tôi đã hỏi lại vì "ba hệ thống" đọc được theo hai nghĩa rất khác nhau. Chốt: **một ứng dụng,
chia theo nhóm quyền**. Vẫn một API, một database, một lần đăng nhập.

Lý do tách service chưa đáng làm bây giờ: mỗi hệ thống hiện có 1–2 module, mà dữ liệu dùng
chung (`NGUOI_DUNG`, `TENANT`, nhóm quyền) sẽ phải đồng bộ giữa ba database — đúng cái bẫy hai
nguồn sự thật vừa tránh khi tách người dùng khỏi tài khoản hôm qua. Nếu sau này thật cần tách,
`ChucNang.HeThongCua()` đã là đường biên sẵn có để cắt theo.

## Chỗ khó nhất: nhóm chức năng dùng chung

`TaiKhoan`, `PhanQuyen`, `ThietLapChung`, `Anh`, `DoiMatKhauNguoiKhac`, `NhatKyHeThong` **không
thuộc hệ thống nào**. Ép vào một hệ thống sai theo cả hai hướng:

- Cho vào LMS → trưởng phòng nhân sự phải sang LMS mới sửa được tài khoản.
- Nhân bản mỗi hệ thống một bản → ba quyền cho cùng một việc, admin phải tích ba lần.

Nên có nhóm thứ tư: `DungChung`, hiện ở sidebar và ở **mọi tab** phân quyền.

Kéo theo một khẳng định dễ làm sai, và tôi đã viết test riêng cho nó: **quyền dùng chung không
mở lối vào hệ thống nào.** Nếu `/toi/he-thong` tính cả nhóm này thì người chỉ quản trị tài
khoản sẽ "vào được" cả ba hệ thống — mà cả ba đều chỉ hiện đúng cụm Quản trị, ba lối vào giống
hệt nhau, bộ chuyển thành vô nghĩa. Đã thử phá code (thêm `.Concat(DungChung)`) để chắc test đỏ.

## Nhóm ở backend, không ở frontend

`/quyen/danh-muc` trả sẵn `heThongs`. Cách rẻ hơn là để frontend khai một object
`{ Hrm: [...], Lms: [...] }` — nhưng khi đó có **hai bản đồ ở hai nơi**, và thêm module mới thì
phải sửa cả hai mới thấy nó xuất hiện. Sửa một chỗ, quên chỗ kia, không có gì đỏ.

Cũng vì vậy **không lưu vào DB**: hệ thống của một chức năng là thuộc tính của mã nguồn
(`LopHoc` thuộc LMS là bất biến), không phải dữ liệu tenant sửa được. Lưu xuống DB thì mỗi
trung tâm nhóm một kiểu và sidebar hết xác định.

## Hai cái bẫy mất dữ liệu đã tránh

**Tab phân quyền không được có state riêng.** `oDaChon` là **một tập duy nhất** cho cả ba hệ
thống, tab chỉ lọc *dòng hiển thị*. Nếu mỗi tab giữ state riêng thì chuyển tab rồi bấm Lưu sẽ
âm thầm xoá quyền của hệ thống khác — đúng loại lỗi quy tắc #1 cấm, và là lỗi 16/08 (form thiếu
ô địa chỉ) mặc áo mới.

**Lựa chọn hệ thống đã lưu chỉ dùng khi CÒN quyền.** Admin thu quyền HRM của một người thì lần
vào sau họ phải rơi về hệ thống khác. Đọc thẳng `localStorage` ở nơi dùng thì họ kẹt ở sidebar
trống mà không hiểu vì sao.

## Test canh việc khai đủ

`NhomHeThongTests` buộc mọi chức năng **hoặc** thuộc một hệ thống **hoặc** nằm trong
`DungChung` — cùng tinh thần với `CachLyTenantTests` hỏi chiều ngược về entity không bị lọc.
`HeThongCua` trả `null` (thành "dùng chung") thay vì ném lỗi cho hằng chưa khai: hiện quá nhiều
thì dễ thấy, còn ném lỗi sẽ làm sập màn phân quyền của mọi tenant. Đã thử thêm một hằng quên
khai để chắc test đỏ và **gọi tên đúng hằng đó**.

## Tenant đã tồn tại

Ba chức năng mới không tự về nhóm "Quản trị viên" của trung tâm cũ — seeder chỉ chạy lúc tạo
tenant. Nhưng `BoKhuyetQuyenQuanTri` đã có sẵn từ giai đoạn 0 và chạy lúc khởi động, nên chỉ
cần khởi động lại API. Kiểm thật: admin của tenant `W686AE9` (tạo hôm qua) vào được cả ba hệ
thống mà không phải làm gì bằng tay.

## Tách màn hồ sơ con người, và một lỗ hổng tôi tự tạo ra

Yêu cầu tiếp: đưa người dùng (nhân viên, giáo viên, trợ giảng) sang module riêng bên HRM.

Tôi hỏi lại một câu vì danh sách chỉ có ba vai trò, mà màn cũ quản **bốn** — học viên đi đâu?
Chốt: học viên ở lại LMS thành màn riêng. Đúng nghiệp vụ: học viên là **khách**, không phải
nhân sự; người phụ trách tuyển sinh cần thêm học viên nhưng không nên thấy hợp đồng, lương của
giáo viên. Màn **Tài khoản** ở lại cụm Quản trị — đó là quyền ĐĂNG NHẬP, và nó gán cho cả bốn
vai trò.

Một component `NguoiDung` dùng cho hai màn qua prop `phamVi` (endpoint, vai trò, quyền gác).
Chép thành hai file là chép ~600 dòng form ba loại hồ sơ, rồi sửa lỗi một bên quên bên kia.

### Lỗ hổng: học viên đọc được danh sách mọi học viên

Tôi gác `/hoc-vien` bằng `ChucNang.LopHoc` với lý luận "ai quản lớp thì quản danh sách học
viên". Sai: **`LopHoc.Xem` là quyền học viên cũng có** — họ cần nó để xem lớp mình học. Nên học
viên đọc được họ tên, số điện thoại, địa chỉ, tên và số điện thoại phụ huynh của **mọi** học
viên khác.

Lộ ra khi tôi chạy bảng kiểm quyền bằng tài khoản thật và thấy `hv1` nhận **200** ở
`/hoc-vien`. Bảy test tôi vừa viết cho hai endpoint này đều xanh — vì **chúng chỉ dùng admin**.
Bài học lặp lại lần thứ ba trong tuần: endpoint mới phải thử bằng tài khoản **ít quyền nhất**,
không phải bằng admin.

Sửa lần đầu tôi dùng `LopHocToanTrungTam` (đúng thứ đã dùng chặn rò rỉ học phí) — nhưng đọc lại
`NhomQuyenMacDinh` thì thấy **giáo viên cố ý KHÔNG có** chức năng đó: nó chính là thứ giới hạn
họ trong lớp được phân công. Gác bằng nó sẽ chặn oan người cần dùng màn này nhất.

Đáp án đúng là `TaiKhoan`: nhóm Giáo viên **có** `TaiKhoan.Xem` sẵn, kèm chú thích trong mã
nguồn "xem học viên lớp mình" — tức người viết seeder đã lường đúng nhu cầu này từ trước. Nhóm
Học viên không có chức năng `TaiKhoan` nào. Nay có test cả **hai chiều** (học viên bị chặn,
giáo viên vào được), và tôi đã thử đặt lại gate cũ để chắc chúng đỏ.

### Một test tự nó không kiểm được gì

`Bo_loc_tren_url_khong_pha_duoc_pham_vi_man_hinh` ban đầu chỉ khẳng định "không chứa học viên"
— đúng cả khi tham số bị **bỏ qua hoàn toàn** và trả về toàn bộ nhân sự. Thử phá code thì test
vẫn xanh, nên tôi viết lại: thêm `Assert.NotEmpty` để phân biệt "bộ lọc bị bỏ qua" (hành vi
đúng, thân thiện hơn bảng trắng) với "giao rỗng". Lần này phá code là đỏ.

## Kiểm chứng

- `dotnet build` 0 warning · **306 test xanh** (61 unit + 245 integration), 27 test mới.
- `check-i18n-keys.py` xanh (554 khoá) — script viết chiều qua, lần này dùng ngay.
- Kiểm tay: `admin` → `[Hrm, Crm, Lms]`; `co.lan`/`tg.hoa`/`hv1` → `[Lms]` (không có bộ chuyển);
  tạo `ns.mai` chỉ HRM+CRM → `[Hrm, Crm]`, thấy cụm Quản trị nhưng **không** thấy module LMS.
- Danh mục: HRM 2 · CRM 1 · LMS 11 · Dùng chung 6 = 20 chức năng, khớp tổng.
- Bảng kiểm quyền hai endpoint hồ sơ trên tài khoản thật: `hv1` **403** ở `/hoc-vien` (đã sửa,
  trước là 200) nhưng vẫn 200 ở `/lop-hoc`; `co.lan`/`tg.hoa` 200 ở `/hoc-vien`, 403 ở
  `/nhan-su`; `ns.mai` ngược lại.

## Còn lại

Ba module mới là **khung trống** — phân quyền, bộ chuyển, sidebar chạy thật, nghiệp vụ chưa có.
Khi làm nghiệp vụ cần chốt trước: doanh thu tính từ đâu (`KHOAN_THU_HOC_PHI` hay có nguồn khác),
nhân viên kinh doanh theo dõi gì (chỉ tiêu, hoa hồng, khách phụ trách), và giáo viên ở HRM cần
thêm trường gì so với `HO_SO_GIAO_VIEN` đang có (hợp đồng, lương, chấm công).

Đã chốt: giáo viên ở HRM và ở LMS là **cùng một con người** — vẫn `NGUOI_DUNG` +
`HO_SO_GIAO_VIEN`, chỉ khác quyền và khác màn hình. Không tạo bảng nhân sự thứ hai.

---

## Soát lại tài liệu: tìm ra một lỗ hổng bảo mật

Yêu cầu "cập nhật tài liệu". Tôi soát các file **lâu nhất chưa sửa** thay vì đọc lại những file
vừa viết — drift nằm ở chỗ không ai chạm tới.

Việc đáng kể nhất không phải là dọn chữ: **`/dang-ky-trung-tam` là endpoint ẩn danh GHI dữ liệu
mà không có `[EnableRateLimiting]`**. `AuthController` đã gắn hạn mức từ 21/08; endpoint đăng ký
bị bỏ sót.

Đáng ghi lại là **cách nó trốn được**:

1. Nợ N3 ghi *"đang chặn bằng `IsDevelopment()`"* — nhưng controller nói rõ **MỞ Ở MỌI MÔI
   TRƯỜNG**, và `IsDevelopment()` trong `Program.cs` chỉ dùng cho Swagger.
2. Dự án **đã có** test quét mọi endpoint `[AllowAnonymous]` thiếu rate limit — test tốt, đúng
   loại tôi vẫn khen. Nó xanh vì `DangKy` nằm trong danh sách miễn trừ, **kèm lý do chép lại
   đúng tiền đề sai của N3**: "chỉ bật ở Development, đã có chặn riêng".

Tức là: tài liệu sai → người viết test tin tài liệu → miễn trừ có lý do nghe hợp lý → lỗ hổng
được canh giữ bởi chính cái test lẽ ra phải bắt nó. Danh sách miễn trừ là chỗ nguy hiểm nhất
trong một test kiểu quét-toàn-bộ, và **lý do miễn trừ cần được kiểm chứng như code**.

Đã gắn hạn mức, gỡ khỏi miễn trừ, viết lại N3 theo sự thật, và thử bỏ lại thuộc tính để chắc
test đỏ đúng chỗ.

## Di sản bóng đá còn sót ở 10 file

Phần lớn vô hại (từ ngữ), nhưng ba loại thật sự gây hại:

| Loại | Ví dụ | Hại gì |
|---|---|---|
| Endpoint/luồng không tồn tại | `/dang-ky-clb`, "luồng lời mời qua link (FR-18) cần nó" | Người triển khai gọi endpoint 404, hoặc tin vào một tính năng không có |
| Tên định danh sai | claim `ten_doi`, hàm `capNhatTenDoi()` | Đọc tài liệu rồi grep không ra gì |
| Nguyên tắc đã bị thay | "Không hỏi khi không mất gì" | Trái quyết định 07/09 (hỏi mọi thao tác ghi) — người mới làm theo tài liệu sẽ làm ngược |

Lúc này tôi **chưa sửa ADR** (viện quy tắc #7: bản ghi lịch sử) và **chưa đổi tên token**
`--status-win/lose/draw` (viện "là code, không phải tài liệu"). Chủ sản phẩm bác cả hai — xem
hai mục cuối file.

## Số liệu lệch

16 → 20 chức năng · 7 → 25/26 bảng có `tenant_id` · 8 → 13 nợ · cập nhật cuối 05/09 → 08/09.
Mỗi con số đều **đếm lại từ code hoặc DB thật**, không chép từ trí nhớ. Ba hệ thống con trước đó
chỉ có trong `phan-quyen-dong.md` — nay có ở tổng quan kiến trúc, README nghiệp vụ, tổng thuật
và kế hoạch.

---

## Chủ sản phẩm bác lập luận "giữ ADR làm bản ghi lịch sử"

Lượt trước tôi giữ nguyên ADR-0001/0004/0005 với lý do quy tắc #7: ADR ghi quyết định trong bối
cảnh thật, sửa đè là làm sai lệch hồ sơ. Chủ sản phẩm **bác lại**: tài liệu phải bám sát dự án
này, cái nào của dự án cũ thì cập nhật hoặc bỏ hẳn.

Ông đúng, và lý do tôi sai đáng ghi lại: tôi đã cân nhắc **tính toàn vẹn của bản ghi** mà không
cân nhắc **cái giá của việc đọc**. ADR không phải bảo tàng — nó là thứ người mới đọc để hiểu vì
sao hệ thống như hiện tại. Một ADR nói "quy mô 1 CLB" và liệt kê "Nginx + Certbot" ở mục *phương
án đã loại bỏ* thì không phải bản ghi lịch sử trung thực; nó là **tài liệu chỉ dẫn sai đường**.

Nghiêm trọng nhất là ADR-0004: nó nói dự án dùng **Caddy** và đã *loại bỏ* Nginx. Thực tế ngược
hoàn toàn — `docker-compose.yml` ghi rõ dùng Nginx + certbot có sẵn trên VPS, tránh tranh port
80/443. Ai đọc ADR rồi đi cài Caddy sẽ làm sập reverse proxy của mọi dự án khác trên máy đó.

Cách xử lý phân biệt hai loại:

| Loại | Xử lý |
|---|---|
| ADR-0005 (lời mời thách đấu) — nghiệp vụ **không còn dòng code nào** | **Xoá hẳn.** Còn trong git history |
| ADR-0001/0002 — quyết định kỹ thuật **vẫn hiệu lực**, chỉ bối cảnh cũ | Viết lại bối cảnh, ghi rõ quyết định chốt cho dự án tiền thân và vẫn giữ hiệu lực |
| ADR-0004 — quyết định **đã đổi thật** | Ghi khối "Sửa đổi 05/09" ở đầu, sửa nội dung, chuyển Caddy sang mục phương án đã cân nhắc kèm lý do bỏ |

## `prompt-trien-khai-vps.md`: file nguy hiểm nhất

128 dòng viết cho dự án cũ, và nó là file **được dán thẳng vào một agent đang chạy trên VPS**:

- Mô tả ứng dụng là "quản lý CLB bóng đá phong trào"
- Ghi sẵn một domain không thuộc dự án này
- Hướng dẫn "kiểm DNS **trước khi khởi động Caddy**" và kiểm `docker compose ps` thấy service
  `caddy` — làm theo là tranh port với Nginx
- Luồng thử nghiệm: "thêm một cầu thủ, một đối thủ, một trận đấu", mở link mời ở cửa sổ ẩn danh
- Số liệu bịa: "399 test backend + 57 E2E" (thật: 306 + 11)

Viết lại toàn bộ. Đáng chú ý một quyết định nhỏ: **không ghi sẵn domain**, dùng `<domain>` và yêu
cầu thay trước khi dán. Ghi sẵn một giá trị sai thì người triển khai làm theo mà không nghi ngờ —
đúng lỗi bản cũ đã mắc.

## Thứ tôi cố ý KHÔNG viết lại

**Nhật ký ngày 16–21/08.** Đó là bản ghi *theo ngày* của dự án tiền thân; viết lại "CLB" thành
"trung tâm" sẽ tạo ra một quá khứ không có thật. Đã ghi chú rõ ở đầu mục lục nhật ký, kèm lý do
giữ: bài học tầng hệ thống vẫn đúng nguyên (sự cố quy tắc #1 ngày 16/08 nay được canh bởi
`CapNhatKhongMatDuLieuTests`).

Các entry CHANGELOG cũ cũng giữ — chúng ghi *đã phát hành gì vào lúc nào*. Chỉ gỡ liên kết tới
ADR-0005 đã xoá để `check-doc-links.py` không đỏ.

## Còn một chỗ nữa cần biết

Tên token `--status-win/lose/draw` **vẫn là di sản bóng đá trong CODE** (nợ N8). Tài liệu nay ghi
rõ nghĩa hiện tại kèm cảnh báo, nhưng người đọc code sẽ vẫn thấy `variant="win"` cho "học phí đã
đủ". Đổi tên là việc của code, không phải tài liệu — nói nếu ông muốn tôi làm.

---

## "Sửa nốt đi, đừng bỏ sót phần nào"

Hai thứ tôi để lại ở lượt trước, chủ sản phẩm yêu cầu làm hết.

### Đổi tên token trạng thái (nợ N8 — xong)

`win` / `lose` / `draw` → **`ok` / `loi` / `cho`**. Đổi ở 16 file: biến CSS trong `index.css`,
ánh xạ Tailwind, `Badge variant`, và mọi chỗ dùng.

Vì sao đây không phải việc trang trí: người đọc code thấy `variant="win"` cho *"học phí đã đủ"*
và `'lose'` cho *"tài khoản bị vô hiệu hoá"* — nghĩa đọc lên **trái hẳn** nghĩa thật, mà một tên
sai nghĩa thì mỗi lần dùng lại là một lần đoán. Ba tên mới nói đúng ba trạng thái: xong · hỏng ·
đang chờ.

Kiểm chứng không chỉ bằng `npm run build` (đổi tên biến CSS mà quên một chỗ thì build **vẫn
xanh**, chỉ màu biến mất): tôi grep bundle CSS thật trong `dist/` để chắc ba biến `--status-ok/
loi/cho` và sáu class `text-/bg-status-*` đều sinh ra, và **không còn** tên cũ nào.

Cũng sửa lý do màu chủ đạo: `--primary` từng ghi *"gợi sân cỏ"* → nay *"trung tính, đủ tương phản
với chữ trắng (WCAG AA)"*. Giá trị hex **không đổi** — đổi màu là redesign, không ai yêu cầu.

### Gỡ sáu nhật ký của dự án tiền thân

1.014 dòng mô tả sàn đối thủ, quỹ CLB, đăng ký đá trận qua link. Lượt trước tôi giữ với lý do
"bản ghi theo ngày". Nhưng lý do đó chỉ đúng nếu bản ghi còn được ai đọc — mà ở đây nó **cạnh
tranh chú ý** với nhật ký thật của dự án, và ba mã nợ **N3/N4/N9** trong đó **đánh số khác** bảng
nợ hiện tại, ai đối chiếu là hiểu sai.

Nên: trích những bài học **code hiện tại còn dẫn chiếu tới** vào một file duy nhất
[`2026-08-bai-hoc-du-an-tien-than.md`](./2026-08-bai-hoc-du-an-tien-than.md) — quy tắc #1 và sự
cố mất địa chỉ 16/08, hạn mức phải kiểm được bằng **con số** chứ không chỉ tên policy, chốt còn
người quản trị cuối. Rồi xoá sáu file; nội dung đầy đủ còn trong git history.

Kèm theo phát hiện nhỏ: bảng mục lục nhật ký **không đúng thứ tự** (entry 08/09 nằm giữa danh
sách, 09/06 nằm dưới 09/05). Đã sắp lại mới-nhất-trước.
