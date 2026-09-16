# 16/09/2026 — HRM gộp một trang ba tab, bấm sĩ số ra danh sách người

## Yêu cầu

Chủ sản phẩm nêu hai việc:

> - cơ cấu tổ chức đang chưa xem được chi tiết danh sách nhân sự
> - hồ sơ nhân viên, cơ cấu, chức vụ đang bị tách biệt, cần tìm cách bố trí hợp lý để thuận tiện
>   thao tác

Hai việc này là **một gốc**: ba màn nói về cùng một tập người (`NGUOI_DUNG` mang cả
`phong_ban_id` lẫn `chuc_vu_id`) nhưng giao diện tách rời, nên cây cơ cấu hiện sĩ số mà không có
đường nào xem *ai*, và muốn đổi chức vụ một người thì phải đi qua sidebar hai ba lần.

## Đã làm

**Backend** — `/nhan-su` thêm ba tham số lọc: `phongBanId`, `gomPhongBanCon`, `chucVuId`.

`gomPhongBanCon` đi xuống **mọi cấp**, dựng tập id trong bộ nhớ (BFS có chặn số vòng) chứ không
recursive CTE. Lý do không dùng CTE: EF Core không sinh được nếu không viết SQL thô, mà SQL thô sẽ
**mất Global Query Filter** của multi-tenant (quy tắc #2). Cây phòng ban một trung tâm cỡ vài chục
dòng nên một truy vấn lấy hết rồi lan trong bộ nhớ vừa rẻ vừa dịch ra `WHERE ... IN (...)`.

Tham số truyền theo **tên** vào `LayDanhSachNguoiDungQuery`: query nay có bốn tham số `Guid?` liền
nhau, truyền theo vị trí thì chèn thêm một bộ lọc vào giữa là lệch im lặng — "lọc theo phòng" hoá
ra "lọc theo chức vụ", không có lỗi biên dịch.

**Frontend** — sidebar 3 mục còn 1 (`/hrm`), ba màn thành ba tab lưu ở `?tab=`. Đường cũ chuyển
hướng sang tab tương ứng. Sĩ số trên cây thành link sang tab Nhân sự đã lọc sẵn.

Ba màn con **giữ nguyên là component riêng**, không nhồi vào một file. `key={tab}` để đổi tab là
dựng lại màn con — cần vì `CoCauToChuc` giữ state cây trong thư viện ngoài React.

## Lỗi tự gây, và vì sao chỉ E2E bắt được

Cây **chỉ đếm người `DangLamViec`** (`PhongBanDtos.Handle`), còn `/nhan-su` mặc định trả cả người
đã nghỉ. Nên bấm vào sĩ số `1` lại ra `2` dòng.

Đứt ở **hai** mắt, mà mỗi mắt tự nó trông vẫn đúng:

1. Link trên cây không mang `trangThaiNhanSu=DangLamViec`.
2. Màn Nhân sự khởi tạo `locNhanSu` bằng `null`, **không đọc** tham số đó từ URL — nên kể cả khi
   link mang theo, nó vẫn bị bỏ qua.

**Không test backend nào đỏ** vì backend hoàn toàn đúng: API lọc chính xác theo tham số nó nhận
được. Chuỗi hỏng nằm ở chỗ bắc qua ba lớp — link → router → state khởi tạo → tham số API → số dòng
— và chỉ E2E đi hết chuỗi đó mới thấy.

Đáng ghi lại: lần đầu sửa mắt (1) **bị mất** do hai lần ghi file đè lên nhau; test E2E chạy lại vẫn
đỏ đúng chỗ cũ nên phát hiện ngay. Nếu tin vào "đã sửa rồi" mà không chạy lại thì lỗi đã lọt.

## Một bẫy test suýt mắc

Bản đầu có test *"lọc phòng ban không làm lọt học viên vào màn nhân sự"*. Nhưng học viên **không
bao giờ** có `phong_ban_id`: cả `KiemTraPhongBan` (lúc tạo/sửa) lẫn `XepNhanSuVaoPhongBan` (lúc xếp
từ cây) đều ném `HOC_VIEN_KHONG_VAO_CO_CAU`. Tập "học viên có phòng ban" luôn rỗng ⇒ assert đó
**xanh kể cả khi gỡ sạch bộ lọc** — test rỗng.

Đã thay bằng test kiểm **chính chốt chặn**, cả hai đường vào cơ cấu.

## Kiểm bằng đột biến

| Đột biến | Kết quả |
|---|---|
| Gom con chỉ đi một cấp (bỏ enqueue) | ĐỎ — `Gom_phong_ban_con_di_xuong_du_moi_cap` |
| Bỏ bộ lọc chức vụ | ĐỎ — `Loc_theo_chuc_vu` |
| `GomPhongBanCon` mặc định `true` | ĐỎ — 2 test |
| Gỡ `trangThaiNhanSu` khỏi link trên cây | ĐỎ — E2E |
| Màn Nhân sự không đọc tham số từ URL | ĐỎ — E2E |

Cây **ba tầng** (Khối → Phòng → Tổ) là bắt buộc cho đột biến thứ nhất: cây hai tầng không phân biệt
được "một cấp con" với "mọi cấp con".

## Một test cũ phải sửa

`sidebar.spec.ts` khẳng định sau khi chuyển sang HRM thì đường dẫn khớp `/^\/hrm\//`. Nay trang đích
của HRM là `/hrm` (không có đoạn sau) nên đỏ. Đây là **assert lạc hậu, không phải lỗi sản phẩm**:
ý định của test là "URL đi theo bộ chuyển hệ thống", mà `Layout` suy hệ thống con bằng
`startsWith('/hrm')` nên `/hrm` nhận đúng. Đã nới thành `/^\/hrm(\/|$)/` kèm lý do.

## Việc thứ hai trong ngày: đặt tên tệp hồ sơ + hạn mức 10 tệp

Yêu cầu: *"khi tải tệp hồ sơ nhân sự lên, cho phép đặt tên file để dễ theo dõi, giới hạn tổng số
lượng file là 10"*.

Hỏi chủ sản phẩm hai điểm trước khi code — đặt tên ở những lúc nào (chốt: cả lúc tải lên **và**
đổi tên sau), và hạn mức tính theo phạm vi nào (chốt: **từng hồ sơ**, không phải toàn trung tâm).
Cả hai đều là thứ đoán sai thì phải làm lại, nên hỏi rẻ hơn.

### Chỗ đáng cẩn thận nhất: đuôi tệp

Người dùng gõ "Hợp đồng lao động 2026", không ai gõ `.pdf`. Nếu lấy đuôi từ tên họ gõ thì:

- tên không có đuôi ⇒ tải về ra tệp Windows không biết mở bằng gì;
- tên có đuôi lạ (`Hợp đồng.exe`) ⇒ một PDF tải về mang tên `.exe`, đi trong
  `Content-Disposition` — đường lừa người dùng từ chính hệ thống nội bộ.

Nên đuôi **luôn** lấy từ tệp thật. Đột biến đổi một dòng (`Path.GetExtension(tenGoc)` thành
`(ten)`) làm đỏ 4 test, trong đó có hai ca `.exe`/`.html`.

### Hai thứ sai được mà không ai thấy ngay

- **Off-by-one hạn mức**: chặn ở 9 thay vì 10. Test kiểm **cả hai chiều** — tệp thứ 10 phải vào
  được, tệp thứ 11 bị từ chối kèm mã lỗi.
- **Đếm sai cái gì**: đếm *số lần đã tải* thay vì *tệp hiện có* thì xoá tệp không mở lại chỗ, và
  hồ sơ dùng lâu sẽ khoá cứng dù đang trống. Có test riêng.

Hạn mức **không** nâng lên ràng buộc DB: quy tắc #8 nói về "chỉ một" (UNIQUE giải quyết được),
còn "nhiều nhất N" thì Postgres cần trigger. Hai request song song đều thấy 9 thì thành 11 tệp —
chấp nhận, vì hậu quả là thừa một tệp, không mất dữ liệu.

### Lỗi tự gây, lần thứ hai cùng một kiểu trong một ngày

Hộp thoại đặt tên dùng chung `maLoi` với trang. `<Modal>` dựng bằng `<dialog>` nên **luôn nằm
trong DOM**, chỉ đóng lại — nên điều kiện bao ngoài không ngăn được khối lỗi bên trong, và thông
báo hiện ở **cả hai** chỗ.

Đây đúng là lỗi tôi đã sửa ở `CoCauToChuc.tsx` sáng cùng ngày (tách `maLoi` / `maLoiTrang`). Biết
rồi vẫn mắc lại ở file khác. Lần này E2E bắt —
`strict mode violation: resolved to 2 elements` — thay vì người dùng.

Cùng tính chất của `<dialog>` còn làm test của tôi sai một lần nữa: helper dùng
`toHaveCount(0)` để chờ hộp thoại đóng, mà nó không bao giờ về 0. Phải là `toBeHidden`.

### Một giới hạn của framework, đã đo chứ không đoán

MVC đổi chuỗi rỗng **và chuỗi toàn dấu cách** của `[FromForm] string?` thành `null`. Nên lệnh tải
lên không phân biệt được "gửi một tên vô dụng" với "không gửi trường này" (client cũ).

Điều kiện gác đầu tiên tôi viết còn tự phủ định: `!IsNullOrWhiteSpace(ten) && tenDat is null` —
mà `"   "` chính là chuỗi cần báo lỗi, và nó làm điều kiện sai. Sau khi probe thật, chốt: tải lên
quay về tên gốc (nhẹ, sửa lại được ngay bằng nút Sửa), còn **đổi tên** đi qua JSON nên vẫn báo
lỗi tử tế.

Ba test cũ của `chi-tiet-nhan-su.spec.ts` phải sửa: chúng chọn tệp rồi chờ tải xong, nay có hộp
thoại ở giữa. Đây là **thay đổi luồng có chủ ý**, không phải hồi quy.

## Việc thứ ba: vai trò "Nhân viên kinh doanh"

Yêu cầu nguyên văn: *"vai trò nhân viên => nhân viên kinh doanh. tránh nhầm lẫn"*.

Đọc thì tưởng là đổi một dòng nhãn. **Rà dữ liệu thật trước khi sửa** thì thấy không phải:

```
NhanVien (6 người):
  Chị Mai (nhân sự)   | chức vụ: Trưởng phòng nhân sự
  Quản trị viên       | chức vụ: Quản trị hệ thống
  Sale Hà Nội / Sài Gòn / Online A / Online B
```

Chỉ 4/6 là sale. Và quan trọng hơn: `NhanVien` là **giá trị mặc định** của `NguoiDung`, đồng thời
là vai trò mà `TenantSeeder` gán cho **tài khoản quản trị**. Đổi nhãn nó thành "Nhân viên kinh
doanh" sẽ gọi chính người quản trị hệ thống là sale — ở mọi trung tâm mới, mãi mãi. Tức là **tạo
ra một nhầm lẫn mới thay vì bỏ nhầm lẫn cũ**, ngược đúng mục đích của yêu cầu.

Nên hỏi lại chủ sản phẩm kèm ba phương án, và chốt: **thêm vai trò riêng** `NhanVienKinhDoanh = 4`.

Hỏi thêm một câu nữa về nhãn của `NhanVien`: để trần "Nhân viên" cạnh "Nhân viên kinh doanh" thì
hai tùy chọn đọc như lồng nhau, người dùng vẫn phải đoán chọn cái nào cho sale. Chốt "Nhân viên
**khác**".

### Giá trị 4, không chen vào giữa

DB lưu `int`. Đặt `NhanVienKinhDoanh = 1` cho "gọn" sẽ làm mọi hàng `GiaoVien` cũ đọc thành vai trò
khác, im lặng. Có test chốt cả 5 giá trị số.

### Chỗ dễ sai nhất, và là lý do viết file test riêng

Vai trò mới phải khai vào `NhanSuController.VaiTroNhanSu` — phạm vi cố định của màn HRM. Thiếu chỗ
đó thì **tạo người vẫn trả 200** nhưng danh sách không hiện ra và `/nhan-su/{id}` trả 404. Không
ngoại lệ nào ném, không test cũ nào đỏ. Đột biến bỏ vai trò khỏi mảng này làm đỏ **5** test.

### Một đột biến sống, và vì sao nó đúng là sống

Đột biến "bảng `HO_SO_NHAN_VIEN` chỉ tạo cho `NhanVien`" **không** làm đỏ test nào lúc đầu. Không
phải test yếu: bảng đó nay chỉ còn FK + `tenant_id` (cột `chuc_vu` đã sang `CHUC_VU` ở FR-24) nên
**không xuất hiện trong DTO nào** — không API nào quan sát được sự khác biệt.

Đã thêm test đọc **thẳng DB** thay vì bỏ qua: hàng thiếu chỉ lộ ra vào đúng lúc FR-23 ghi thêm
trường vào bảng đó, khi dữ liệu thật đã tích lũy và phải backfill.

### Chuyển dữ liệu

Không làm trong EF migration: "ai là nhân viên kinh doanh" là quyết định nghiệp vụ **của từng trung
tâm**, migration đoán hộ sẽ gán sai cho mọi tenant khác — mà đó là dữ liệu thật.

Nhận diện theo tên (`Sale%`) chứ không theo phòng ban có tag Kinh doanh: "Sale Online B" đang nằm ở
phòng **"Đào tạo"**, nên phòng ban của người này mới là thứ đặt sai. Chủ sản phẩm chốt chuyển cả 4.

Kiểm trước khi chạy: `Sale%` chỉ khớp W686AE9 (192 tenant E2E trong DB dev dùng tên "Trần Sale",
không khớp). Backup 6.9 MB → dry-run trên DB bản sao → chạy thật: `UPDATE 4`. Admin và Chị Mai giữ
nguyên `NhanVien`. Ảnh chụp tenant thật xác nhận không ai bị gắn nhãn sai.

## Việc thứ tư: thống kê lọc từ ngày → đến ngày

Yêu cầu: *"phần khoảng thời gian lọc hãy đổi thành từ ngày tới ngày giống khách hàng và doanh
thu"*. Rõ ràng, và phần UI đúng là đổi một khối JSX. Hai thứ ngầm bên dưới mới đáng kể.

### Lệch một ngày, không có gì báo

Đọc handler trước khi sửa. Hai màn so ngày **khác nhau**:

| Màn | So sánh | Frontend gửi gì |
|---|---|---|
| Doanh thu | `NgayDangKy <= denNgay` | gắn `T23:59:59Z` |
| **Thống kê** | `NgayDangKy < den` | phải gửi **ngày hôm sau** |

Nếu tôi copy y nguyên cách của màn Doanh thu — hoặc gửi thẳng ngày người dùng chọn — thì **mất
trọn ngày cuối kỳ**. Chọn "đến 30/09" mà đơn ngày 30/09 không được tính, và không có ngoại lệ nào
ném ra: chỉ là con số nhỏ hơn thực tế. Loại lỗi mà kế toán phát hiện hộ sau vài tuần.

Cũng không gắn `Z`: handler cắt kỳ theo **múi giờ trung tâm** (bài học FR-15 đã ghi sẵn trong
`ThongKeCrmDtos`), gắn `Z` là cắt theo UTC và đẩy đơn sáng sớm sang kỳ trước.

### Lỗi tự gây: bộ lọc biến mất

Màn này có `if (!tk) return <TrangTrong/>`. Hợp lý **hồi bộ lọc là ô chọn sẵn** — luôn có giá trị
hợp lệ nên `tk` hầu như không rỗng. Nhưng khi cho nhập tay, một kỳ rỗng làm cả trang **kể cả bộ
lọc** thành "Không tìm thấy dữ liệu": người dùng không còn ô nào để sửa lại và phải F5.

Thấy được nhờ chụp ảnh lúc kiểm chứng, không nhờ tsc hay lint. Đây là lần thứ ba trong ngày ảnh
chụp bắt lỗi mà validator không bắt được.

### Đột biến sống, và vì sao

Đột biến "khôi phục `return` sớm" lúc đầu **không làm đỏ** test nào — dù nó chính là lỗi tôi vừa
sửa. Lý do: ca tôi viết là "khoảng đảo đầu", mà lúc đó query bị `enabled: false` và **TanStack
Query giữ lại dữ liệu của lần gọi trước**, nên `tk` vẫn có và `return` không bao giờ chạy.

Đã kiểm bằng probe riêng (đặt khoảng sai rồi reload) chứ không suy đoán: hai ô ngày là state của
component, không nằm trong URL, nên khoảng sai không sống qua F5. Thêm ca **kỳ rỗng** (ngày hợp lệ
nhưng không có đơn — tái hiện chắc chắn) thì đột biến đỏ ngay.

Bài học: một đột biến sống không mặc nhiên là "test yếu"; phải hiểu **vì sao** nó sống rồi mới biết
nên thêm test hay chấp nhận.

## Kết quả

536 test backend · 28 frontend · 35 E2E — xanh hết.
