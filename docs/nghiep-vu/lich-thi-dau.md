# Module Lịch thi đấu (FR-07 → FR-11)

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

Thời gian, đối thủ (ID đội), trạng thái, link video, nhận xét chung, ghi chú.

**Bàn thắng đội nhà chỉ HIỂN THỊ, không nhập** — xem [Một nguồn sự thật cho tỷ số](#một-nguồn-sự-thật-cho-tỷ-số).
Bàn thua nhập tay.

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
