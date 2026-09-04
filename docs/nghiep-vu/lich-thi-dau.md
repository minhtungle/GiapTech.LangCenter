# Module Lịch thi đấu (FR-07 → FR-11)

> ⚠️ **Tài liệu của DỰ ÁN CŨ** (quản lý CLB đá bóng phong trào). Từ 05/09/2026 repo này là
> base cho hệ thống quản lý trung tâm ngoại ngữ — **phần nghiệp vụ dưới đây không còn trong
> code**. Giữ lại để tham khảo cách viết đặc tả. Xem [`CLAUDE.md`](../../CLAUDE.md) mục 1.

## FR-07 — Lọc thông tin

Bộ lọc dùng chung với module [Thống kê](./thong-ke.md) (FR-12) — cùng một component, cùng một tập tham số:

| Tiêu chí | Kiểu |
|---|---|
| Thời gian | Khoảng từ ngày → đến ngày |
| Kết quả | Thắng / Hòa / Thua (chọn nhiều) |
| Số bàn thắng | Khoảng giá trị |
| Số bàn thua | Khoảng giá trị |

## FR-08 — Danh sách trận đấu

Hai chế độ hiển thị, **chuyển đổi qua lại** được, giữ nguyên bộ lọc đang áp dụng:

- **Calendar** — trận đấu theo lịch tháng/tuần, màu theo kết quả (xanh=thắng, vàng=hòa, đỏ=thua).
- **Datatable** — bảng mật độ dòng gọn, lọc và **sắp xếp ngay trên tiêu đề cột**.

### Sắp xếp

Sắp được theo: thời gian · đối thủ · tỷ số · kết quả · trạng thái. Bấm lần đầu ra **giảm dần**
(mới nhất / lớn nhất lên trước — cái người dùng thường tìm), bấm lại đảo chiều.

Bốn quyết định đáng ghi:

- **Cột là enum đóng** (`CotSapXep`), không nhận tên cột tự do từ client: ghép chuỗi vào
  `ORDER BY` là đường dẫn tới SQL injection. Canh bởi `Cot_sap_xep_la_khong_hop_le_bi_tu_choi`.
- **Sắp xếp ở server, không ở client**: bảng có phân trang, sắp trong trang hiện tại chỉ xáo
  20 dòng đang thấy chứ không ra thứ tự của cả bộ lọc.
- **Luôn có khoá phụ `ThoiGian` giảm dần**. Không có thì các hàng bằng nhau (cùng "Thắng",
  cùng trạng thái) xếp theo thứ tự PostgreSQL trả về — thứ tự đó *không ổn định giữa các
  trang*, nên một trận có thể xuất hiện ở cả trang 1 lẫn trang 2, hoặc biến mất khỏi cả hai.
- **Cột tỷ số sắp theo hiệu số**, không theo bàn thắng đội nhà: 5-6 mà đứng trên 2-0 thì đọc
  ra kết quả ngược. Canh bởi `Sap_theo_ty_so_dung_hieu_so`.
- Trận **chưa gán đối thủ** luôn xếp cuối thay vì lẫn lên đầu theo chuỗi rỗng.

Đổi cột sắp xếp thì **về trang 1** — trang 3 của thứ tự cũ không có nghĩa gì ở thứ tự mới.

## FR-09 — Hòm thư

Màn **Hòm thư** (route `/hom-thu`, trước đây là `/loi-moi`) gom mọi thứ cần người dùng phản
hồi về một chỗ. Tách hai màn thì cầu thủ phải nhớ vào hai nơi.

**Nội dung khác nhau theo người đăng nhập**: cầu thủ thường chỉ thấy lời mời đăng ký dành cho
mình; trưởng nhóm thấy thêm lời mời giao hữu, bảng tổng hợp ai đã trả lời, và nút gửi lời mời.

### Trưởng nhóm

Cờ `NGUOI_DUNG.la_truong_nhom`, **không suy từ nhóm quyền**: "được sửa lịch thi đấu" và "là
người triệu tập đội" là hai chuyện khác nhau — thư ký CLB có thể sửa lịch mà không phải trưởng
nhóm. Một đội có nhiều trưởng nhóm được (CLB lớn chia đội hình A/B).

Admin của mỗi CLB **bật sẵn** cờ này; migration cũng bật cho admin của các CLB đã tồn tại,
nếu không thì tính năng nằm chết cho tới khi có người tự vào bật mà họ chưa biết cờ đó có.

Kiểm quyền ở tầng Application (`XacThucTruongNhom`) chứ không dùng `[RequirePermission]` —
thêm chức năng mới vào `QUYEN_CHUC_NANG` sẽ bắt mọi CLB đang chạy phải cấp lại quyền.

### Lời mời đăng ký thi đấu

Trưởng nhóm bấm **Mời đăng ký** tại một trận → mọi cầu thủ nhận lời mời, trả lời
**Tham gia / Chưa chắc / Không**.

- Gửi lời mời tạo sẵn hàng *"chưa trả lời"* cho **mọi cầu thủ**: trưởng nhóm cần thấy ai chưa
  trả lời, không chỉ ai đã đồng ý.
- **Chưa chắc** là trạng thái thật của bóng đá phong trào ("để xem hôm đó có tăng ca không").
  Ép chọn có/không sẽ khiến người ta chọn bừa rồi bỏ trận.
- Tách khỏi `DOIHINH_TRANDAU`: đăng ký là **ý định** của cầu thủ, đội hình là **quyết định**
  của trưởng nhóm. Mười lăm người đăng ký nhưng chỉ mười một người được xếp — gộp một bảng
  thì mất thông tin ai đã sẵn sàng mà không được chọn.
- Trưởng nhóm **đóng** lời mời khi đã chốt đội hình; đóng rồi không nhận trả lời mới
  (`LOI_MOI_DA_DONG`), mở lại được.
- Tài khoản quản lý thuần tuý (không gắn hồ sơ cầu thủ) không đăng ký được — không có gì để
  đăng ký (`TAI_KHOAN_CHUA_GAN_CAU_THU`).
- Xoá trận thì xoá luôn lời mời và phản hồi (Cascade).

Bảng liên quan: `LOI_MOI_THAM_GIA`, `PHAN_HOI_THAM_GIA`.

### Lời mời giao hữu từ đội bạn

1. Xem danh sách lời mời giao hữu từ đối thủ (kèm thời gian đề xuất).
2. **Chấp nhận** → tự sinh trận đấu mới ở trạng thái *"đã lên lịch"*, lời mời chuyển sang *đã chấp nhận*.
3. **Từ chối** → đóng lời mời, không sinh trận.

Bảng liên quan: `LOI_MOI_DOI_THU`, `DOI_THU`, `TRAN_DAU`.

## FR-10 — Thêm / Cập nhật trận đấu

Mở **view riêng theo ID trận**, tổ chức thành **4 tab**, cho phép **lưu nháp giữa chừng**.

Đặc tả gốc ghi 3 tab; thực tế tách thành 4 vì hai lý do — đội hình và sơ đồ **gộp** làm một
(cùng một mạch việc), còn video **tách** khỏi đánh giá (xem tab (c1) bên dưới).

### Tab (a) — Thông tin chung

Thời gian, đối thủ, trạng thái, link video, nhận xét chung, ghi chú.

**Ba đường chọn đối thủ**, làm được ngay trong form thêm trận, không phải rời sang màn Đối thủ
rồi quay lại:

1. **Chọn từ sổ đối thủ** — đội đã lưu trước đó.
2. **Gõ tên lạ → tạo ngay** trong dropdown. Dòng *Tạo đội "…"* hiện khi từ khoá **không khớp
   chính xác** mục nào đang có — không phải khi danh sách rỗng: gõ "FC Hải" mà đã có "FC Hải
   Châu" thì vẫn cho tạo, vì đó là hai đội khác nhau.
3. **Tra mã đội 7 ký tự** — thêm CLB khác cũng dùng hệ thống này. Xem
   [Tra cứu CLB khác](#tra-cứu-clb-khác-trong-hệ-thống) bên dưới.

**Bàn thắng đội nhà chỉ HIỂN THỊ, không nhập** — xem [Một nguồn sự thật cho tỷ số](#một-nguồn-sự-thật-cho-tỷ-số).
Bàn thua nhập tay.

#### Tra cứu CLB khác trong hệ thống

`GET /api/v1/doi-thu/tra-cuu-clb/{maDoi}` là **endpoint duy nhất trong hệ thống đọc dữ liệu
ngoài tenant hiện tại**, nên bị giới hạn chặt hơn mọi endpoint khác (quy tắc #2):

| Giới hạn | Lý do |
|---|---|
| Chỉ so khớp **chính xác** mã 7 ký tự | Cho `Contains` hoặc tìm theo tên là bất kỳ ai gõ một chữ cũng dò ra danh sách toàn bộ CLB |
| Trả **đúng 3 field**: `maDoi`, `tenDoi`, `daCoTrongSo` | Không trả `id`: có id là mở đường thử gọi endpoint khác với id đó |
| **404 giống hệt nhau** cho: mã sai định dạng · mã không tồn tại · mã của chính mình | Phân biệt được thì người dò biết ngay mã nào đúng định dạng, thu hẹp không gian dò rất nhiều |
| `daCoTrongSo` đọc sổ đối thủ **của tenant hiện tại** | Bỏ query filter ở đây là rò rỉ chéo: cờ bật lên chỉ vì CLB khác đã thêm đội đó vào sổ của họ |

Không gian mã là `31^7 ≈ 27 tỷ` (bộ ký tự bỏ `0/O` và `1/I/L`) nên dò ngẫu nhiên không khả thi.
**Nợ kỹ thuật:** vẫn nên thêm rate limit ở tầng Caddy trước khi lên production.

`DOI_THU.ma_doi_he_thong` lưu **mã đội**, cố ý **không phải FK** tới `TENANT`:

- FK cho phép join xuyên tenant — một truy vấn vô tình thành rò rỉ dữ liệu chéo CLB.
- CLB kia có thể xoá tài khoản; lịch sử đối đầu của ta phải giữ nguyên, không bị Cascade theo.

Canh bởi `TraCuuClbTests` (12 test, 5 phản chứng) và `e2e/chon-doi-thu.spec.ts` (5 test).

### Tab (b) — Đội hình & Sơ đồ *(gộp từ hai tab cũ)*

Chọn thành viên và xếp họ lên sân là **một mạch việc**, tách hai tab thì mỗi lần thêm người
phải nhảy tab mới thấy áo xuất hiện. Đặc tả gốc của tab (a) vốn đã ghi "danh sách thành viên
tham gia, sắp xếp đội hình" — gộp lại là quay về đúng ý ban đầu.

**Bố cục side by side**: sân bên trái, băng ghế + ô sửa áo bên phải; xuống một cột trên mobile.
Xếp dọc thì mỗi lần đổi sơ đồ phải cuộn lên xem kết quả rồi cuộn xuống bấm tiếp.

- Chọn thành viên tham gia (khối gập lại khi đã có đội hình).
- **Loại sân 5-5 / 7-7 / 9-9 / 11-11** — đổi cả **tỷ lệ khung sân** lẫn **bộ sơ đồ dựng sẵn**.
  Sân 5 người gần vuông (5/7), sân 11 người dài (2/3); vẽ cùng tỷ lệ thì sơ đồ 5-5 nhìn như
  đội hình dàn trên sân lớn, sai hẳn cảm giác khoảng cách giữa các tuyến.
  Sơ đồ theo loại sân: 5-5 có 2-2 · 1-2-1 · 3-1; 7-7 có 2-3-1 · 3-2-1 · 2-2-2 · 3-3;
  9-9 có 3-3-2 · 3-4-1 · 4-3-1; 11-11 có 4-4-2 · 4-3-3 · 4-2-3-1 · 3-5-2 · 5-3-2 · 5-4-1.
- **Hai hiệp riêng** (`hiep1` / `hiep2`), chuyển qua lại bằng nút, có nút *Chép từ hiệp 1*.
  Đội phong trào hay đổi người và đổi sơ đồ giữa giờ nghỉ — một sơ đồ cho cả trận không tả được.
- **Bảng chiến thuật kéo-thả** (tham khảo `renderfoot.com/tactical-board`):
  - Áo tròn mang **số áo + mã vị trí** (GK, CB, ST…); bấm một áo để sửa, kéo để đổi chỗ,
    nháy đúp để bỏ khỏi sân.
  - **Cả hai đội thao tác y hệt nhau**: cùng kiểu dữ liệu, cùng kéo-thả, cùng ô sửa số áo và
    mã vị trí. Đối thủ thêm ô sửa tên (không có hồ sơ để lấy).
  - Đối thủ **đá ngược hướng**: `y` hiển thị = `100 − y` lưu. Không lật thì hai thủ môn chồng
    lên nhau ở cùng một khung thành. Lật khi VẼ chứ không khi lưu, để hai đội cùng hệ quy chiếu.
  - Số áo và vị trí mặc định **lấy từ hồ sơ cầu thủ** (`CAU_THU.so_ao`, `vi_tri_so_truong`) —
    khỏi gõ lại từng trận; sơ đồ vẫn ghi đè được cho trận riêng lẻ.
  - **Chọn màu áo** cho từng đội, **chỉ trong bộ áo CLB đã khai** ở
    [FR-06 Thiết lập chung](./quan-tri-he-thong.md#bộ-áo-đấu). CLB chưa khai thì mở toàn bộ
    bảng màu. Không dùng color picker: người dùng dễ chọn xanh lá chìm vào sân, hoặc hai màu
    gần nhau cho hai đội. Màu chữ tính sẵn theo độ sáng nền — để tự động thì số áo trên áo
    vàng thành chữ trắng, không đọc nổi.
  - Dự bị nằm trên **băng ghế** cạnh sân, kéo vào sân được.
  - **Thao tác ghi đè hàng loạt phải xác nhận trước**: xoá hết, áp sơ đồ dựng sẵn, chép hiệp 1
    sang hiệp 2, áp mẫu. Hộp thoại nói rõ **mất cái gì, bao nhiêu** ("Sẽ bỏ 7 cầu thủ Đối thủ
    khỏi sân ở Hiệp 2"), nút Huỷ giữ focus mặc định.
    **Không hỏi khi không mất gì** — áp sơ đồ vào sân trống thì chạy thẳng; hỏi mọi thứ sẽ
    khiến người dùng bấm Đồng ý theo phản xạ và hộp thoại mất hết tác dụng.
    Thao tác một quân (nháy đúp bỏ một người) cũng không hỏi.
- **Chọn mẫu đội hình** đã lưu để áp nguyên đội hình + sơ đồ hai hiệp; hoặc **Lưu thành mẫu**
  từ sơ đồ đang xếp. Xem [Mẫu đội hình](#mẫu-đội-hình-dùng-lại).
- Ghi chú chiến thuật.
- Lưu JSON vào `SODO_CHIENTHUAT.so_do_json`:
  `{ loaiSan, hiep1: {ta: [{id,x,y,so,viTri,ten}], doiThu: [...]}, hiep2: {...} }`
  (quan hệ **1—1** với trận đấu). Toạ độ là **phần trăm 0–100**, không phải pixel.
  **Bản ghi cũ dạng phẳng** `{viTri, doiThu}` vẫn đọc được, coi là hiệp 1 — không có bước này
  thì mọi sơ đồ lưu trước đây biến mất khỏi màn hình (quy tắc #1).
- **Hai nút Lưu riêng** cho đội hình và sơ đồ — một nút lưu tất thì bấm lưu sơ đồ cũng ghi đè
  đội hình (quy tắc #1).
- Thao tác phức tạp → **ưu tiên desktop**.

### Mẫu đội hình dùng lại

Bảng `MAU_DOI_HINH`, màn riêng ở menu **Mẫu đội hình**. CLB phong trào đá đi đá lại gần như
cùng một đội hình; không có mẫu thì mỗi trận phải chọn lại từng người rồi kéo lại từng áo.

- Lưu **cả cầu thủ lẫn vị trí, hai hiệp, cả đối thủ** — áp mẫu là ra y hệt.
- Tạo mẫu theo hai đường: dựng mới trong màn quản lý (chọn từ **toàn bộ hồ sơ cầu thủ** của
  CLB), hoặc **Lưu thành mẫu** ngay từ trận vừa xếp (`POST /mau-doi-hinh/tu-tran/{id}`).
- Áp mẫu vào trận sẽ **bỏ qua cầu thủ không còn trong đội hình trận đó** — người đã nghỉ CLB
  không tự chui vào sân.
- **Mẫu và trận độc lập sau khi áp**: sửa hay xóa mẫu KHÔNG đụng tới sơ đồ của trận đã đá.
  Ràng buộc chúng lại sẽ khiến lịch sử trận thay đổi theo mẫu (quy tắc #1).
  Canh bởi `Xoa_mau_khong_lam_mat_so_do_cua_tran`.
- `UNIQUE(tenant_id, ten)`: hai CLB đều đặt được mẫu cùng tên, cách ly tenant canh bởi
  `Mau_cach_ly_theo_tenant` (kiểm hai chiều, cả đọc lẫn sửa/xóa xuyên tenant).

### Tab (c1) — Video sau trận *(tab riêng)*

Tách khỏi tab Đánh giá dù đặc tả gốc gộp chung: gắn link là việc làm **ngay sau trận**, thường
do người khác làm (ai quay thì người đó dán link), còn chấm điểm cầu thủ là việc của ban huấn
luyện làm sau. Nhét chung một tab thì mỗi lần dán link phải cuộn qua cả bảng chấm 6 chỉ số ×
11 người.

- **Nhiều link video** mỗi trận (`VIDEO_TRAN`), mỗi link có **tên riêng** và mô tả: một trận
  thường có video hiệp 1, hiệp 2, bản highlight và vài clip bàn thắng ở các nguồn khác nhau.
- Hệ thống **chỉ lưu link** (Youtube/Drive), không lưu file — xem ADR-0004.
- URL chỉ chấp nhận `http`/`https`: link được render thành thẻ `<a>`, để lọt `javascript:`
  là mở đường cho XSS. Canh bởi `Url_khong_phai_http_bi_tu_choi`.
- Xóa trận thì xóa luôn video (Cascade) — link mồ côi không dùng được vào việc gì.
- Xóa một link **đã lưu** phải xác nhận; dòng vừa thêm chưa có gì để mất nên xóa thẳng.

### Tab (c2) — Đánh giá sau trận

**Bảng danh sách cầu thủ, sửa từng người trong modal.** Thẻ gập được (bản trước) cho một cái
nhìn tổng quan tệ — phải mở từng cái mới thấy điểm, mà mở hết 11 thẻ thì trang dài lê thê.
Bảng cho thấy toàn đội trong một màn hình, modal lo phần nhập liệu chi tiết.

Cột: họ tên · bàn thắng · cứu thua · điểm trung bình (kèm thanh mức để đọc lướt) · ghi chú ·
MVP · nút sửa.

**Vote MVP nằm ngoài modal**, ngay trên bảng: mỗi người một phiếu (quy tắc #8) và gửi ngay khi
bấm, không chờ Lưu — để trong modal thì người dùng tưởng phải bấm Lưu mới tính.

Modal lưu **một cầu thủ mỗi lần**, an toàn vì handler backend không xoá đánh giá của người
vắng mặt trong payload, và tỷ số vẫn cộng trên toàn bộ đánh giá của trận
(`Luu_danh_gia_tung_phan_khong_lam_tut_ty_so`).

- Nội dung modal:
  - **Bàn thắng / cứu thua**.
  - **6 chỉ số kỹ năng thang 1–10**: Tấn công · Phòng ngự · Chuyền bóng · Rê dắt · Thể lực ·
    Tinh thần. Lưu JSON vào `DANHGIA_CAUTHU.chi_so_ky_nang` — thêm tiêu chí mới không cần migration.
  - **Biểu đồ radar 6 cạnh**: đỉnh đặc = đã chấm, đỉnh rỗng = suy từ trung bình. Chỉ số chưa
    chấm KHÔNG lấy 0 — chấm một mục 8 điểm mà vẽ 0 cho năm mục còn lại thì hình thành một vạch,
    đọc nhầm thành "cầu thủ kém toàn diện".
  - Ghi chú.
- **Thả tim vote MVP** — mỗi người **tối đa 1 tim / trận**.

Nhận xét chung cho cả trận nằm ở tab (a).

### Thư viện video

Màn riêng ở menu **Thư viện video**, lọc theo đối thủ và tìm theo tên/mô tả.

**Không có bảng riêng** — đọc thẳng từ `VIDEO_TRAN` của mọi trận. Giữ bản sao sẽ tạo hai nguồn
sự thật: sửa tên video ở màn trận mà thư viện không đổi theo, rồi không biết bên nào đúng.
Đọc thẳng thì đồng bộ là hệ quả tự nhiên, không phải việc phải nhớ làm.
Canh bởi `Thu_vien_dong_bo_ngay_khi_sua_o_tran`.

Sửa/xoá video thì vào trận tương ứng — mỗi video thuộc đúng một trận.

### Một nguồn sự thật cho tỷ số

**Bàn thắng đội nhà = tổng bàn thắng của cầu thủ** trong `DANHGIA_CAUTHU`, do
`TranDau.DongBoTySoNha` đặt mỗi khi lưu đánh giá. Hệ quả:

- `LuuTranDauCommand` **không có trường `TySoNha`** — để lại thì có hai đường ghi vào cùng một
  ô, đường nào chạy sau thắng, và người dùng sửa bàn thắng cầu thủ xong quay ra lưu thông tin
  chung là tỷ số bật về giá trị cũ.
- Bảng vua phá lưới (FR-14) và tỷ số trận không bao giờ lệch nhau.
- **Bàn thua vẫn nhập tay**: bàn của đối thủ thì không cầu thủ nào của ta ghi.
- Lưu đánh giá **từng phần** cộng tổng trên toàn bộ đánh giá của trận, không chỉ phần gửi lên —
  nếu không, lưu riêng cầu thủ B sẽ làm tỷ số tụt mất số bàn của A.

Canh bởi `Sua_thong_tin_chung_khong_lam_mat_ban_thang`, `Ty_so_nha_doi_theo_ban_thang_cau_thu`
và `Luu_danh_gia_tung_phan_khong_lam_tut_ty_so` — cả ba đã kiểm chứng bằng phản chứng.

> **Ràng buộc bắt buộc:** `UNIQUE(tran_dau_id, nguoi_vote_id)` ở tầng **database**, không chỉ chặn ở UI.
> Đây là quy tắc bất di bất dịch #8 trong `CLAUDE.md`.

## FR-11 — Xóa trận đấu

Xóa trận + toàn bộ dữ liệu liên quan: đội hình (`DOIHINH_TRANDAU`), sơ đồ chiến thuật
(`SODO_CHIENTHUAT`), đánh giá (`DANHGIA_CAUTHU`), vote MVP (`VOTE_MVP`).

### Khuyến nghị

- Chỉ cho **xóa cứng** trận ở trạng thái *"đã lên lịch"*.
- Trận *"đã diễn ra"* nên **archive** thay vì xóa — dữ liệu đã diễn ra là đầu vào của thống kê (FR-13,
  FR-14); xóa cứng sẽ làm sai lệch lịch sử.

## Tham chiếu

- Bảng `TRAN_DAU`, `DOIHINH_TRANDAU`, `SODO_CHIENTHUAT`, `DANHGIA_CAUTHU`, `VOTE_MVP`, `DOI_THU`,
  `LOI_MOI_DOI_THU` — xem [ERD](../database/erd.md).


---

## FR-17 — Cộng đồng (bổ sung 18/08/2026)

Danh sách các CLB đã đăng ký hệ thống, để tìm đội và **gửi lời mời thách đấu**.

### Quyết định của chủ sản phẩm

| Câu hỏi | Chọn | Hệ quả |
|---|---|---|
| CLB nào lên cộng đồng | **Tất cả, không tắt được** | CLB đăng ký để quản lý nội bộ vẫn bị đưa ra cho người lạ xem |
| Lộ những gì | **Thành tích thắng/hoà/thua** | Dữ liệu nhập cho mục đích nội bộ thành công khai |
| Mời qua đâu | **Hòm thư của CLB kia** | Cần bảng xuyên tenant `LOI_MOI_BAT_DOI` |

Tính năng này **cố ý đi ngược** thiết kế của [tra cứu CLB](#tra-cứu-clb-khác-trong-hệ-thống) —
endpoint đó dựng để *chặn* việc liệt kê danh sách CLB, sàn thì mở chính cái đó. Ghi lại để người
đọc sau không tưởng là sơ suất.

**Muốn cho CLB tự chọn ẩn/hiện:** sửa mệnh đề `Where` trong `LayDanhSachCongDongHandler` (thêm cột
`Tenant.HienTrenSan`), **không** sửa ở tầng UI.

### Trên sàn hiện gì

Có: tên · mã đội · tên viết tắt · logo · khu vực · sân nhà · mô tả · số trận đã đá · T/H/B.

**Không** có, dù đã chọn mức lộ nhiều nhất:

- **Liên hệ, kể cả `lien_he_cong_khai`.** Trả nó ở danh sách cộng đồng là mở đúng cửa spam: một lần
  gọi API thu được số điện thoại của mọi CLB. Liên hệ chỉ hiện trong hòm thư, **sau khi** bên
  kia đồng ý lời mời. *(Đây là lỗi thật đã xảy ra khi làm tính năng — DTO trả liên hệ, phát hiện
  khi gọi API và đọc kết quả.)*
- `id` tenant · danh sách cầu thủ · quỹ, khoản chi · chi tiết trận, đánh giá, vote MVP.

Sắp theo **tên**, không theo thành tích: xếp theo thành tích biến sàn thành bảng xếp hạng toàn hệ
thống, đội mới hoặc thua nhiều bị đẩy xuống cuối và không ai tìm thấy để thách đấu — ngược mục đích.

### Ba ô mới ở Thiết lập chung

`khu_vuc` · `san_nha` · `lien_he_cong_khai`. Tách thành nhóm riêng trên form và ghi rõ **"hiện
CÔNG KHAI"**: người dùng điền số điện thoại phải biết ai đọc được.

Cả ba theo quy ước quy tắc #1 giống `mau_ao`: `null` = client không gửi → **giữ nguyên**; chuỗi
rỗng = người dùng chủ động xoá → ghi `null`. Không phân biệt hai ca này thì mỗi lần lưu từ màn cũ
sẽ âm thầm xoá khu vực và liên hệ — đúng lỗi đã xảy ra 16/08 với ô địa chỉ.

### Lời mời thách đấu

`LOI_MOI_BAT_DOI` là **bảng duy nhất trong hệ thống thuộc về hai tenant cùng lúc**, nên không thể
có Global Query Filter. Chi tiết ở [multi-tenant.md](../backend/multi-tenant.md#những-chỗ-global-query-filter-không-bảo-vệ).

- `ma_doi_he_thong` kiểu **chuỗi, không phải FK** — cùng lý do với sổ đối thủ: FK cho phép join
  xuyên tenant và Cascade mất lịch sử nếu CLB kia xoá tài khoản.
- FK tới `TENANT` dùng **Restrict**, không Cascade: xoá một CLB không được xoá lời mời khỏi hòm
  thư của CLB kia — đó là dữ liệu của họ.
- **Một lời mời đang chờ cho mỗi cặp CLB.** Không chặn thì bấm nhiều lần dội hàng loạt thẻ giống
  nhau vào hòm thư bên kia. Chỉ chặn `ChoPhanHoi` — đá xong rồi mời lại lần sau là hợp lý.
- **Đồng ý tạo HAI trận độc lập**, một ở lịch mỗi bên. Trận dùng chung sẽ buộc một CLB sửa dữ liệu
  nằm trong tenant của CLB kia. Hộp xác nhận nói trước điều này.
- Chỉ **bên nhận** trả lời được, chỉ **bên gửi** huỷ được, và chỉ khi chưa ai trả lời.

### Chi tiết một CLB — `GET /cong-dong/{maDoi}`

Bấm vào một CLB trong danh sách để xem hồ sơ công khai: mô tả, khu vực, sân nhà, ngày thành lập,
bộ áo, thành tích đầy đủ (kèm bàn thắng/bàn thua), **10 trận gần nhất đã có kết quả**, và **số
trận đã đối đầu với ta**. Có nút gửi lời mời thách đấu ngay trên trang.

Tách khỏi DTO danh sách thay vì trả luôn mọi thứ: danh sách 20 CLB × 10 trận là 200 dòng cho một
lần xem, mà người dùng chỉ mở chi tiết một hai đội.

**Vẫn KHÔNG lộ**, dù đây là trang chi tiết:

| Không lộ | Vì sao |
|---|---|
| Danh sách cầu thủ, tên người, ảnh cá nhân | Dữ liệu của **cá nhân**, không phải của CLB. Một đội có thể muốn công khai thành tích nhưng không muốn công khai tên từng người |
| Ghi chú và nhận xét từng trận | Viết cho nội bộ đọc ("hàng phòng ngự yếu") |
| Quỹ, khoản chi, đánh giá, vote MVP, sơ đồ | Dữ liệu vận hành và bí mật nghiệp vụ |
| `lien_he_cong_khai` | Chỉ hiện ở hòm thư sau khi hai bên đồng ý |
| `id` tenant | Có id là mở đường thử gọi endpoint khác |

`soTranDoiDauVoiTa` đếm trong **sổ của ta** (có Query Filter), không đọc sổ của họ. Hai bên có thể
lệch số nếu một bên không lưu `ma_doi_he_thong` — chấp nhận được, mỗi CLB tự quản lịch của mình.

Route dùng ràng buộc `{maDoi:length(7)}`. Không có nó thì `{maDoi}` cũng khớp `khu-vuc` và
`loi-moi` — mà cả hai **đúng 7 ký tự**, một trùng hợp khiến ràng buộc độ dài không phân biệt được
chúng; thứ tự literal của ASP.NET Core mới là cái đang giữ. Test canh bằng chuỗi 6 và 8 ký tự.

Canh bởi `CongDongTests` (21 test, 12 phản chứng) và `e2e/cong-dong.spec.ts` (7 test).
