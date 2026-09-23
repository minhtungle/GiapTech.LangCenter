# Module CRM (FR-17 → FR-20)

Bán khoá học: gom dữ liệu khách hàng, bán khoá, tổng hợp doanh thu.

> **Hệ thống không xử lý tiền.** Cùng giới hạn với [Học phí](hoc-phi.md): không cổng thanh
> toán, không đối chiếu sao kê. Số ở đây là **lời khai của trung tâm**.

## Ba màn, một dòng chảy

```
                                              ┌──▶ Khoá học   FR-19
Khách hàng  ──(mua hàng)──▶  Doanh thu  ──────┤
 FR-17                        FR-18           └──▶ Sản phẩm   FR-20
 TẤT CẢ khách               khách ĐÃ trả tiền      (sách, học cụ)
```

**Hai hình thức mua hàng** (chốt 08/09/2026): tham gia khoá học, hoặc mua sản phẩm khác.

- **Màn Khách hàng hiện TẤT CẢ** — kể cả người chưa mua gì. Đây là nơi gom dữ liệu khách.
- **Màn Doanh thu chỉ hiện khách đã trả tiền.** Không phải một nút "chuyển": khách có đăng ký
  thì tự xuất hiện, không có thì không.

## FR-17 — Khách hàng

Bảng `KHACH_HANG`: họ tên, email, số điện thoại, **link Facebook**, ghi chú, **hình thức thanh
toán** (mặc định của khách này).

### Vì sao là bảng RIÊNG, không dùng `NGUOI_DUNG`

Khách hàng là **người quan tâm**, chưa chắc thành học viên. Nhồi họ vào `NGUOI_DUNG` thì:

- Danh sách học viên bên LMS lẫn người chưa học — giáo viên xếp lớp phải tự lọc bằng mắt.
- `NGUOI_DUNG` có `trang_thai_nhan_su`, `loai_nguoi_dung`, ba bảng hồ sơ vai trò — mọi thứ đó
  vô nghĩa với một người mới để lại số điện thoại trên Facebook.

Ngược lại, tách bảng thì phải trả giá: **một người có thể có hai bản ghi** (một ở
`KHACH_HANG`, một ở `NGUOI_DUNG` sau khi vào học). Chấp nhận, và xử lý bằng cột
`nguoi_dung_id` nullable — xem dưới.

### Nối sang học viên khi họ thật sự vào học

`KHACH_HANG.nguoi_dung_id` (nullable) trỏ tới `NGUOI_DUNG` khi khách đã thành học viên:

- `null` = chưa vào học, chỉ là khách.
- Có giá trị = **cùng một con người**, hồ sơ học tập nằm ở `NGUOI_DUNG`.

Không **copy** họ tên/email sang `NGUOI_DUNG` rồi để hai bên trôi khỏi nhau — đó đúng là lỗi
hai nguồn sự thật đã gặp với tài khoản/người dùng (07/09/2026). Sửa tên ở màn Khách hàng
**không** đổi tên học viên, và ngược lại; UI phải nói rõ điều đó khi đã nối.

### View chi tiết khách hàng — 4 tab

Bấm một khách mở `/crm/khach-hang/:id`:

| Tab | Nội dung |
|---|---|
| **Thông tin chung** | Hồ sơ + sửa tại chỗ; mối nối tới hồ sơ học viên |
| **Lịch sử chăm sóc** | Từng lần liên hệ — xem `LICH_SU_CHAM_SOC` dưới |
| **Lịch sử mua hàng** | Mọi thứ khách đã mua, **nhóm theo loại** (khoá học · sản phẩm), mỗi nhóm có tổng tiền riêng + tổng chung |
| **Số tiền đã đóng** | Sổ thu theo từng đơn + **còn thiếu bao nhiêu**. **Không** nhóm theo loại: ở đây người dùng đi theo từng đơn để ghi thu, thứ tự thời gian đúng hơn |

### Mua hàng ngay từ tab chăm sóc

Tab Lịch sử chăm sóc có **hai nút**:

| Nút | Ghi gì |
|---|---|
| **Ghi lần chăm sóc** | Một dòng `LICH_SU_CHAM_SOC` |
| **Ghi đơn** | Một dòng `DANG_KY_KHOA_HOC` (đơn hàng) **VÀ** một dòng `LICH_SU_CHAM_SOC` |

Hai nút cạnh nhau chứ không phải hộp thoại "bạn muốn làm gì?": người bán biết trước mình đang
ghi cuộc gọi hay ghi đơn hàng.

**Một lệnh `POST /khach-hang/{id}/mua-hang`, một `SaveChanges`** cho cả đơn hàng, lần thu (nếu
đã nhận đủ tiền) và dòng chăm sóc. Không để frontend gọi hai API: API thứ hai lỗi (mạng đứt,
429) sẽ để lại **đơn hàng không có dấu vết chăm sóc** — người bán sau không biết ai chốt đơn này
và bằng cách nào.

Hệ quả kèm theo: trạng thái phễu tự thành **Đã mua**, người bán không phải nhớ chọn. Nội dung
chăm sóc để trống thì hệ thống tự ghi `Mua <tên mặt hàng> × <số lượng>`.

**Không gộp dòng doanh thu của cùng một khách** — mỗi lần mua là một sự kiện riêng, có ngày và
mức giá riêng.

### Lịch sử chăm sóc (`LICH_SU_CHAM_SOC`)

Mỗi lần liên hệ một dòng: `thoi_diem`, `hinh_thuc` (Gọi điện · Zalo/Facebook · Email · Gặp trực
tiếp · Khác), `noi_dung`, `nguoi_phu_trach_id`, và **`trang_thai_sau`**.

`trang_thai_sau` (Mới · Đang tư vấn · Đã mua · Từ chối) làm nên **phễu bán hàng**:

> **Trạng thái hiện tại của khách = `trang_thai_sau` của lần chăm sóc MỚI NHẤT.**
>
> Suy từ lịch sử chứ **không** thêm cột `trang_thai` vào `KHACH_HANG`: hai chỗ lưu cùng một
> thông tin thì chúng lệch nhau ngay lần đầu ai đó sửa lịch sử mà quên cột kia. Cùng nguyên tắc
> với công nợ học phí tính động (FR-14).

Khách chưa có lần chăm sóc nào → coi là **Mới**.

`nguoi_phu_trach_id` lấy từ **token**, không nhận từ client — không có tham số để ghi hộ người khác.

### Quy tắc

- `UNIQUE(tenant_id, so_dien_thoai)` khi số điện thoại **không rỗng** — chặn hai người bán
  cùng nhập một khách. Số điện thoại rỗng thì không chặn (khách chỉ để lại Facebook).
- Xoá khách **đã có đăng ký** bị chặn — đăng ký là dữ liệu tiền.

### Bộ lọc (15/09/2026)

Cả hai màn **Khách hàng** và **Doanh thu** lọc được theo sáu chiều. Bộ lọc nâng cao ẩn sau nút
**Lọc thêm** kèm badge số bộ lọc đang bật.

| Chiều | Khách hàng | Doanh thu | Ghi chú |
|---|---|---|---|
| Tìm kiếm (tên · sđt · email) | ✅ | ✅ | |
| Tình trạng đã/chưa mua | ✅ | — | |
| **Đội nhóm** | ✅ | ✅ | Phòng ban của người mang khách về — **chỉ phòng mang tag `KinhDoanh`** ([FR-22](hrm.md#tag-vai-trò-16092026)) |
| **Nhân viên** | ✅ | ✅ | Người mang khách về |
| **Nguồn khách** | ✅ | — | `NhanVienTao` / `TuDangKy` |
| **Khoảng ngày** | ✅ ngày **tạo hồ sơ** | ✅ ngày **đăng ký** | Hai mốc khác nhau, nhãn nói rõ |
| Khoá học | — | ✅ | |
| **Sản phẩm** | — | ✅ | Đơn hàng chứa cả khoá và sản phẩm (FR-20) |
| **Hình thức thanh toán** | — | ✅ | Đối chiếu tiền mặt với sao kê |

#### Mốc quy doanh số: NGƯỜI MANG KHÁCH VỀ

Lọc theo đội nhóm / nhân viên quy đơn cho **`KHACH_HANG.created_by_id`** — người tạo hồ sơ
khách, không phải người nhập đơn (`DANG_KY_KHOA_HOC.created_by_id`).

Đây là **cùng một mốc** với màn [Thống kê CRM](thong-ke-crm.md). Chọn khác đi thì cùng một đội
ra hai con số ở hai màn và không ai biết số nào đúng. Hệ quả kèm theo: khách **tự đăng ký**
không có ai phụ trách nên tự rơi ra khỏi mọi bộ lọc đội/nhân viên — đúng ý, đơn của họ không
tính vào doanh số cá nhân của ai.

> Hai màn vẫn ra số khác nhau khi **không lọc ngày**, và đó là đúng: Thống kê mặc định
> **12 tháng gần nhất** (nó là báo cáo theo kỳ), còn danh sách tra cứu **toàn bộ**. Đã kiểm trên
> W686AE9: 85 đơn tổng / 79 đơn trong 12 tháng.

#### Chỉ phòng tag Kinh doanh (16/09/2026)

Ô chọn "Đội nhóm" ở cả ba màn CRM lấy từ `GET /phong-ban/nhom-theo-tag?tag=KinhDoanh` — **phòng
không tag không xuất hiện**, dù nó vẫn nằm trong cây cơ cấu và vẫn xếp được nhân sự. Chi tiết
và lý do: [FR-22 · Tag vai trò](hrm.md#tag-vai-trò-16092026).

Biểu đồ "doanh thu theo đội nhóm" ở màn Thống kê **gom** đơn của phòng không tag vào một mục
`KHAC` thay vì ẩn — tổng biểu đồ luôn bằng ô "tổng doanh thu" ngay trên nó. Chưa phòng nào có
tag thì toàn bộ doanh thu nằm trong `KHAC`, và ô chọn đội nhóm hiện chỉ dẫn "Đánh tag ở Cơ cấu
tổ chức →" thay vì rỗng im lặng.

#### Ô số tổng phải KHỚP danh sách

Màn Doanh thu có ba ô KPI ở đầu trang, gọi endpoint `/doanh-thu/tong-hop` **riêng** với danh
sách (cộng trên trang đang xem là số vô nghĩa — 20 dòng đầu của 500 đơn). Nên thêm một bộ lọc
mà quên truyền xuống endpoint tổng hợp là người dùng thấy "85 đơn / 671 triệu" trong khi bảng
có 4 dòng — và họ tin ba ô số to, không tin bảng. Không có lỗi nào hiện ra.

Canh bởi `LocCrmTests.Tong_hop_luon_khop_danh_sach` (`[Theory]` cho từng bộ lọc: thêm bộ lọc mới
thì thêm một `InlineData`) và E2E `loc-crm.spec.ts` (kiểm ô KPI **đổi** khi lọc, không giữ số cũ).

#### Khoảng ngày: cắt theo múi giờ TRUNG TÂM

Client gửi ngày thuần (`2026-09-14`) → .NET hiểu `00:00+00:00`. Bản đầu làm
`denNgay.Date.AddDays(1)`, mà `.Date` bỏ mất offset nên mốc thành `00:00` giờ **máy chủ**
(UTC+7) = `17:00 UTC` cùng ngày — cắt mất 7 giờ cuối ngày, khách tạo lúc 17:16 UTC biến mất khỏi
kết quả "hôm nay". Nay quy mốc từ múi giờ trung tâm rồi mới so với `CreatedAt`. Cùng bài học
với FR-15 và Thống kê CRM.

### Hai màn ghi đơn phải ĐỒNG NHẤT (17/09/2026)

Đơn hàng ghi được từ **hai chỗ**: tab *Lịch sử đơn hàng* ở chi tiết khách, và màn *Doanh thu*.
Chủ sản phẩm báo hai chỗ đó *"chưa đồng nhất về cả tên và thao tác"*. Đúng, và là hai lỗi khác
nhau:

**1. Tên** — cùng một việc mà gọi hai kiểu: *"Ghi mua hàng"* vs *"Thêm đăng ký"*. Nay thống nhất
một bộ từ vựng ở khối `donHang` của `i18n.ts`:

| Trước | Nay |
|---|---|
| "Ghi mua hàng" / "Thêm đăng ký" | **Ghi đơn** |
| "Lịch sử mua hàng" | **Lịch sử đơn hàng** |
| "Sửa đăng ký" | **Sửa đơn hàng** |

Chọn *"đơn hàng"* vì nó đúng cho **cả** khoá học lẫn sản phẩm — *"đăng ký"* nghe như chỉ dành cho
khoá, *"mua hàng"* nghe như chỉ dành cho sản phẩm. Dùng chung một khối i18n chứ không chép nhãn
sang hai nơi: chép là hai chỗ sẽ trôi khỏi nhau lần nữa.

**2. Thao tác** — màn Doanh thu chỉ gửi `khoaHocId`, nên:

- **không ghi được đơn sản phẩm** (dù danh sách vẫn *lọc* được theo sản phẩm);
- **sửa đơn sản phẩm thì 400** `PHAI_CHON_DUNG_MOT_MAT_HANG` — nó gửi `khoaHocId = null` và không
  có `sanPhamId`. Trên dữ liệu thật W686AE9 có **10/85 đơn** như vậy: kế toán không sửa nổi một
  lỗi gõ trong đó.

Gốc rễ nằm ở `DangKyDto`: nó chỉ trả `TenMatHang` (chuỗi), **không trả id**, nên form sửa không
điền lại được mặt hàng. Nay DTO trả `KhoaHocId` + `SanPhamId` (đúng một cái khác null) và màn
Doanh thu có đủ ô chọn loại + số lượng, giống hệt form ở chi tiết khách.

> Số lượng **chỉ hiện với sản phẩm** — khoá học không ai mua 2 suất trong một đơn, và backend
> luôn ép `SoLuong = 1` cho khoá. Đổi loại thì **bỏ mặt hàng đang chọn**: id khoá học không có
> nghĩa trong danh mục sản phẩm.

Canh bởi `CrmTests` (DTO trả id · sửa đơn sản phẩm · đúng một mặt hàng, kiểm **cả hai chiều sai**)
và `e2e/ghi-don-dong-nhat.spec.ts` (hai màn cùng tên nút, cùng bộ ô nhập).

## FR-18 — Doanh thu (đăng ký khoá học)

Bảng `DANG_KY_KHOA_HOC`: một khách × một khoá × một mức giá. **Một khách đăng ký nhiều khoá** →
nhiều dòng.

| Cột | Ý nghĩa |
|---|---|
| `khach_hang_id` | Ai mua |
| `khoa_hoc_id` | Mua khoá nào |
| `gia_goc` | Giá niêm yết của khoá **lúc đăng ký** — snapshot |
| `so_tien` | Giá thực thu (người bán sửa được) |
| `don_vi_tien` | VND · USD · EUR · CAD |
| `ty_gia_ve_vnd` | Tỷ giá **lúc đăng ký** |
| `ngay_dang_ky`, `phuong_thuc`, `ghi_chu` | |

### Ba con số tiền, không phải một

- **`gia_goc`** snapshot từ `KHOA_HOC.gia_tien` lúc đăng ký. Không đọc động: trung tâm tăng giá
  khoá thì đơn hàng tháng trước **không được đổi** — cùng lý do `LOP_HOC_HOC_VIEN.hoc_phi_ap_dung`
  là snapshot (FR-08).
- **`so_tien`** giá thực thu.
- **`% trên giá gốc`** = `so_tien / gia_goc`, **tính động, không lưu cột**. Lưu là mở cửa cho
  lệch khi ai đó sửa một trong hai số — cùng nguyên tắc với công nợ ở FR-14.

`gia_goc = 0` → không tính %; UI hiện `—` chứ không chia cho 0.

### Quy đổi tiền: lưu tỷ giá TẠI THỜI ĐIỂM

`ty_gia_ve_vnd` chụp lúc đăng ký, không tra động. Đây là quyết định quan trọng nhất của module:

> Doanh thu tháng 9 xem hôm nay và xem tuần sau phải **ra cùng một số**. Quy đổi động thì mỗi
> lần tỷ giá biến động là mọi báo cáo quá khứ đổi theo — kế toán không dùng được.

Doanh thu tổng hợp = `SUM(so_tien × ty_gia_ve_vnd)`. Đơn vị VND thì tỷ giá = 1.

Hệ thống **không tự tra tỷ giá** (không gọi API ngoài): người bán nhập, có gợi ý giá trị dùng
lần trước cho cùng đơn vị.

### Đăng ký là CAM KẾT, không phải đã thu

`so_tien` của đăng ký = số khách **cam kết trả**. Tiền thực nhận nằm ở bảng riêng
`THU_TIEN_DANG_KY` (chốt 08/09/2026) vì khách thường đóng nhiều đợt:

| | Ý nghĩa |
|---|---|
| `DANG_KY_KHOA_HOC.so_tien` | Cam kết — cơ sở tính doanh thu |
| `SUM(THU_TIEN_DANG_KY.so_tien)` | **Đã thu thật** |
| Còn thiếu | Hiệu hai số trên, **tính động, không lưu cột** |

Thu tiền ghi cùng **đơn vị tiền của đăng ký** — không cho đóng EUR cho một đăng ký khai VND;
lẫn đơn vị trong cùng một đăng ký thì phép trừ "còn thiếu" thành vô nghĩa.

**Doanh thu vẫn tính trên CAM KẾT**, không trên tiền đã thu. Hai con số trả lời hai câu khác
nhau: bán được bao nhiêu, và đã cầm về bao nhiêu. Màn Doanh thu hiện cả hai.

### Quy tắc

- Khách chưa có đăng ký nào thì **không** xuất hiện ở màn Doanh thu.
- Sửa/xoá đăng ký ghi vào [nhật ký](nhat-ky-he-thong.md) như mọi thao tác khác (FR-16).
- **Chưa nối với sổ thu học phí LMS** (FR-14) — hai sổ độc lập, chưa đối chiếu. Nợ kỹ thuật.

## FR-20 — Sản phẩm khác

Bảng `SAN_PHAM`: tên, ghi chú, giá + đơn vị tiền, **đơn vị tính** ("quyển", "bộ", "cái").

### Vì sao là bảng RIÊNG, không gộp vào `KHOA_HOC` kèm cột `loai`

Gộp thì `so_buoi` **luôn NULL** cho sách, `don_vi_tinh` luôn NULL cho khoá, và mọi query khoá
học phải nhớ `WHERE loai = ...` — quên một lần là sách lọt vào danh sách khoá học. Cùng lý do
`LOP_HOC_HOC_VIEN` và `LOP_HOC_TRO_GIANG` là hai bảng chứ không một bảng có cột `vai_tro`.

### Đơn hàng: hai khoá ngoại nullable loại trừ nhau

`DANG_KY_KHOA_HOC` (tên bảng giữ nguyên — xem dưới) có `khoa_hoc_id` **và** `san_pham_id`, đều
nullable, ràng buộc **`CHECK` ở tầng DB**: đúng một cột khác NULL. Cùng khuôn `TEP_DINH_KEM` đã
dùng cho 5 loại đính kèm.

Không dùng hai bảng đơn hàng riêng: doanh thu sẽ phải `UNION` hai bảng ở mọi báo cáo, và tổng
hợp đa tiền tệ phải viết hai lần.

> **Tên bảng `DANG_KY_KHOA_HOC` nay chứa cả sản phẩm.** Đổi tên bảng đang có dữ liệu là việc
> rủi ro (EF dễ sinh drop-and-recreate) mà lợi ích chỉ là cái tên đẹp hơn. Đọc nó theo nghĩa
> "đơn hàng"; ghi nợ để đổi khi có dịp migration lớn.

### `so_luong`

Khoá học **luôn 1** (ép ở handler, không tin client) — không ai mua 2 suất cùng khoá trong một
đơn. Sản phẩm thì mua 3 quyển sách là 1 dòng `so_luong = 3`.

`gia_goc` và `so_tien` là **tổng của cả dòng**, không phải đơn giá: nhờ vậy mọi phép cộng doanh
thu không phải nhân thêm, và người bán sửa được tổng khi giảm giá theo lô.

### Quy tắc

- `UNIQUE(tenant_id, ten)` — hai sản phẩm cùng tên thì người bán chọn sai.
- **Xoá sản phẩm đã bán bị chặn** (`SAN_PHAM_DA_CO_DON_HANG`) — FK Restrict, và đơn cũ phải giữ
  được tên sản phẩm đã bán. Không dùng nữa thì **ngừng bán**.

## FR-19 — Khoá học

Bảng `KHOA_HOC`: tên khoá, ghi chú, giá tiền + đơn vị, số buổi.

### Khoá học ≠ Lớp học

| | `KHOA_HOC` (CRM) | `LOP_HOC` (LMS) |
|---|---|---|
| Là gì | **Sản phẩm** trong danh mục | **Một lần mở** cụ thể |
| Bán đi bán lại | Có | Không |
| Có giáo viên, lịch | Không | Có |
| Dùng để | Đăng ký, báo giá | Xếp lớp, điểm danh |

Một khoá `IELTS 6.5 · 40 buổi · 12tr` mở thành nhiều lớp (Tối T2-4-6 cô Lan, Sáng T3-5 thầy A).
Gộp hai thứ này thì **không bán được trước khi mở lớp**, và mỗi lần mở lại phải khai giá lại.

Chưa nối `LOP_HOC.khoa_hoc_id` trong đợt này — làm khi cần báo cáo "khoá này đã mở mấy lớp".

### Quy tắc

- `UNIQUE(tenant_id, ten)` — hai khoá cùng tên thì người bán chọn sai.
- **Xoá khoá đã có đăng ký bị chặn** (`KHOA_HOC_DA_CO_DANG_KY`) — FK Restrict, và đơn hàng cũ
  phải giữ được tên khoá đã bán. Không dùng được nữa thì đánh dấu ngừng bán.
- `so_buoi` là thông tin **niêm yết**, không ràng buộc số buổi lớp thật sinh ra.

## FR-21 — Yêu cầu xếp lớp (CRM → LMS)

Bán xong một khoá thì học viên chưa có lớp. FR-21 là **cầu nối** giữa hai hệ thống con: người
bán gửi yêu cầu, bên đào tạo xếp lớp.

### Luồng

1. **CRM** — tab *Lịch sử mua hàng* của khách, ở dòng đơn **khoá học** có nút
   *Gửi yêu cầu tạo lớp* (`POST /doanh-thu/{dangKyId}/yeu-cau-xep-lop`, gác bằng
   `DoanhThu.Sua`). Đơn sản phẩm không có nút này.

   Form gửi có ô **ghi chú cho bên đào tạo** (trình độ, nguyện vọng giờ học…) — người xếp lớp
   dựa vào đó chọn lớp. **Người gửi** lấy từ token, không do client gửi lên.
2. **LMS** — yêu cầu vào *danh sách chờ xếp lớp*, xếp theo **cũ nhất trước**. Danh sách hiện
   ghi chú của người gửi và đánh dấu *lần gửi thứ n* khi n > 1.

   **Phân trang ở server** (20 dòng/trang) + **tìm theo tên hoặc số điện thoại** (12/09/2026):
   hàng chờ của trung tâm đông lên vài trăm dòng, kéo hết về rồi cắt ở trình duyệt sẽ chậm dần
   mà không ai để ý. `tongSoDong` đếm **sau khi lọc** — đếm trước thì thanh phân trang báo sai
   số trang. Canh bởi `XepLopTests.Hang_cho_phan_trang_va_giu_thu_tu_cu_nhat_truoc` và
   `Tim_kiem_hang_cho_theo_ten_va_dem_sau_khi_loc`.
3. Bên đào tạo **duyệt** hoặc **từ chối** (`POST /lop-hoc/cho-xep-lop/{id}/tu-choi`, **lý do bắt
   buộc**). Bị từ chối thì người bán bổ sung thông tin và **gửi lại** — lần gửi mới, giữ nguyên
   lần cũ. Form gửi lại nhắc lại lý do bị từ chối lần trước.
4. Đưa vào lớp bằng **một trong hai cách**, cùng gọi
   `POST /lop-hoc/{lopId}/duyet-cho-xep-lop`:

   | | Vào từ đâu | Dùng khi |
   |---|---|---|
   | **Cách 1** | **Tab *Chờ xếp lớp* của màn Lớp học** → bấm duyệt → chọn lớp | Người điều phối nhìn cả hàng chờ để cân lớp |
   | **Cách 2** | Tab *Học viên* của một lớp → chọn người đang chờ | Người phụ trách một lớp muốn lấp cho đủ chỗ |

   **Chờ xếp lớp KHÔNG phải module riêng** (10/09/2026): nó là một tab của `/lms/lop-hoc`, hiện
   cho ai có quyền. Hàng chờ là việc của người xếp lớp, không phải một khu vực nghiệp vụ tách
   biệt — và người điều phối cần nhìn hàng chờ cạnh danh sách lớp để cân chỗ.

   > **Lỗi đã sửa cùng lúc**: mục menu cũ gác bằng `LopHoc.Xem`, còn cả ba endpoint chờ xếp lớp
   > đòi `LopHoc.**Sua**`. `Xem` là quyền **giáo viên và học viên cũng có**, nên họ thấy menu
   > "Chờ xếp lớp" rồi bấm vào và nhận **403** — đã kiểm bằng tay: giáo viên có đúng `['Xem']`
   > trên `LopHoc`. Nay tab gác bằng đúng `LopHoc.Sua`, và gõ thẳng `?tab=cho-xep-lop` khi không
   > có quyền thì rơi về danh sách chứ không phải tab trắng.
   > Canh bởi `e2e/cho-xep-lop-tab.spec.ts` (có test chiều giáo viên **không** thấy tab).

### Quy tắc

- **Hồ sơ học viên tự tạo từ dữ liệu khách** khi `KHACH_HANG.nguoi_dung_id` còn null: sinh
  `NGUOI_DUNG` (`LoaiNguoiDung.HocVien`) + `HO_SO_HOC_VIEN` rỗng, rồi **nối lại vào khách**.
  Không bắt người bán gõ lại họ tên — gõ lại là mở đường cho hai bản ghi lệch nhau. Mua khoá
  thứ hai dùng đúng hồ sơ đó, không tạo trùng.
- **Học phí áp dụng lấy từ đơn CRM**, không lấy `LOP_HOC.hoc_phi`. Đơn đã gồm miễn giảm đã chốt
  với khách; lấy giá lớp thì sổ học phí LMS đòi khách thêm phần đã được giảm. Đơn ngoại tệ quy
  về VND bằng **tỷ giá đã chụp** (`so_tien × ty_gia_ve_vnd`) vì sổ học phí chỉ có một đơn vị.
  Canh bởi `XepLopTests.Duyet_vao_lop_lay_hoc_phi_TU_DON_CRM_khong_lay_gia_lop` và
  `Don_ngoai_te_quy_ve_vnd_theo_ty_gia_da_chup`.
- **Một bảng `YEU_CAU_XEP_LOP` riêng**, không phải một cột trạng thái trên đơn: giữ được *ai
  gửi · ai duyệt · lúc nào · vào lớp nào*.
- **Một đơn gửi được NHIỀU lần** (đổi 09/09/2026 — trước đó `UNIQUE(dang_ky_id)` chỉ cho một
  lần). Lịch sử mua hàng hiện *đã gửi n lần* kèm trạng thái, người gửi, người xử lý, ghi chú và
  lý do từ chối của **từng lần** — đó là chỗ người bán trả lời câu hỏi "sao em chưa có lớp".
- Hai ràng buộc ở tầng DB (quy tắc #8):
  `UNIQUE(dang_ky_id, lan_gui)` — mỗi lần gửi một số thứ tự, chặn hai request song song cùng
  đọc `MAX(lan_gui)` rồi cùng ghi "lần 2";
  `ux_yeu_cau_xep_lop_dang_ky_dang_cho` — *partial* unique index `WHERE trang_thai = 0`, chỉ
  **một** lần được đang chờ trên mỗi đơn. Không có filter thì đơn bị từ chối không gửi lại được;
  không có index thì danh sách chờ có hai dòng cùng học viên. Canh bởi
  `DongThoiTests.Moi_don_chi_mot_yeu_cau_xep_lop_DANG_CHO`.
- `lan_gui` là **cột**, không đếm động: đó là số thứ tự của chính dòng đó, xoá dòng giữa thì các
  lần sau không được đánh số lại.
- **Đã xếp lớp rồi VẪN gửi lại được** (đổi 12/09/2026 — bỏ chốt `DON_DA_DUOC_XEP_LOP`). Ba ca
  nghiệp vụ thật cần điều đó: học viên **bị gỡ khỏi lớp** cần xếp lại; lớp **kết thúc/bị huỷ** mà
  còn buổi chưa học; học thêm lớp / học lại / đổi ca. Trước đây cả ba đều bế tắc — đơn đã `DaXep`
  thì người bán không gửi được yêu cầu nào nữa.

  Chốt **duy nhất** còn lại: `DA_GUI_YEU_CAU_XEP_LOP` — mỗi khoá chỉ gửi tiếp khi yêu cầu hiện
  tại **đã duyệt hoặc đã từ chối**. Hai yêu cầu cùng chờ trên một đơn làm người điều phối thấy
  hai dòng trùng mà không biết duyệt cái nào.
- **Trạng thái "đang học lớp nào" suy động từ `LOP_HOC_HOC_VIEN`**, không suy từ trạng thái yêu
  cầu (chốt 12/09/2026).

  Yêu cầu `DaXep` là **sự kiện quá khứ** ("đã từng được duyệt vào lớp X"); "đang học lớp nào" là
  **trạng thái hiện tại**. Trước đây `TenLopDaXep` suy từ yêu cầu, nên gỡ học viên khỏi lớp thì
  CRM vẫn báo "Đã vào lớp X" **mãi mãi** — yêu cầu không đổi.

  `DangKyKemThuDto` nay trả `CacLopDangHoc` · `CacLopDaHoc` · `DangThamGiaLop` · `TenLopDangHoc`.
  Hệ quả: **gỡ khỏi lớp và huỷ lớp tự động đúng**, không cần viết đồng bộ ngược — cùng nguyên tắc
  "công nợ tính động, không lưu cột" của FR-14.

  *"Đang tham gia"* = lớp **không** `DaKetThuc`/`DaHuy` **và** học viên `DangHoc`/`BaoLuu`. Lớp
  đã đóng hoặc người đã nghỉ rơi sang `CacLopDaHoc` — vẫn trả lời được "đã từng học lớp nào".

  Canh bởi `XepLopTests.Go_hoc_vien_khoi_lop_thi_CRM_khong_con_bao_dang_hoc` và
  `Lop_bi_huy_thi_khong_con_dang_hoc_nhung_con_lich_su`.

  > **Giới hạn**: một lớp gán được tối đa 3 khoá, nhưng bảng ghi danh nối theo `hoc_vien_id`
  > (con người) chứ không theo `dang_ky_id` (đơn) — nên mọi đơn khoá học của một khách cùng thấy
  > danh sách lớp của người đó. Đủ để trả lời "đang học lớp nào", chưa đủ để nói "đơn này ứng
  > với lớp này".
- **Cảnh báo lệch khoá học khi duyệt** (`KHOA_HOC_KHONG_KHOP_LOP`, 12/09/2026) — **cảnh báo,
  KHÔNG chặn**.

  Lớp gán được tối đa 3 khoá (`LOP_HOC_KHOA_HOC`, đóng nợ N19). Khoá trong đơn CRM không nằm
  trong số đó thì gần như chắc người duyệt chọn sai lớp — nhưng vẫn có ca hợp lệ: học bù, lớp
  ghép, khoá tương đương chưa kịp gán. Chủ sản phẩm chốt *"thông báo để người duyệt lưu ý, vẫn
  cho phép nếu đồng ý"*.

  Cơ chế: lần gọi đầu (`BoQuaCanhBaoKhoaHoc = false`) trả mã lỗi kèm `DuLieu` gồm **khoá của
  đơn · khoá của lớp · tên lớp** để UI dựng câu hỏi lại; người duyệt đồng ý thì client gọi lại
  với cờ `true`. Cờ mặc định false nên client cũ vẫn nhận được cảnh báo thay vì bỏ qua âm thầm.

  **Lớp chưa gán khoá nào thì KHÔNG cảnh báo**: mọi lớp tạo trước 12/09/2026 đều rỗng, cảnh báo
  hết sẽ thành tiếng ồn và người duyệt học cách bấm qua mà không đọc — đúng thứ làm cảnh báo mất
  tác dụng khi cần nhất.

  Canh bởi `XepLopTests.Duyet_lech_khoa_hoc_thi_canh_bao_nhung_van_cho_phep_khi_dong_y`,
  `Duyet_dung_khoa_thi_khong_canh_bao` và `Lop_chua_gan_khoa_thi_khong_canh_bao`.
- **`TuChoi` ≠ `DaHuy`**: từ chối là bên **đào tạo** không nhận (kèm lý do bắt buộc), huỷ là bên
  **bán** thu lại yêu cầu. Gộp một trạng thái thì không trả lời được ai quyết định.
- **Duyệt hai lần bị chặn** (`YEU_CAU_DA_XU_LY`): bấm lại, hoặc hai người cùng duyệt, sẽ tạo hai
  dòng ghi danh và học viên bị tính học phí hai lần.
- Vượt sức chứa **chặn cả lô**, không xếp một phần rồi báo lỗi.
- **Huỷ chứ không xoá** — giữ vết đã từng có yêu cầu.
- Ghi danh và đóng yêu cầu trong **một `SaveChanges`**: nếu không, danh sách chờ và danh sách
  lớp sẽ nói hai chuyện khác nhau.

### Chưa nối

`LOP_HOC` **chưa có khoá ngoại về `KHOA_HOC`** (CRM đứng riêng, chốt 08/09/2026), nên hộp thoại
chọn lớp **không** ưu tiên được "lớp cùng khoá" — tên khoá của đơn hiện ở cả bảng và hộp thoại
để người điều phối tự đối chiếu. Khi nối hai bảng thì đưa lớp cùng khoá lên đầu.

Số **đã thu ở CRM cũng chưa chảy sang** sổ học phí LMS: khách đóng 4 triệu ở CRM thì sổ LMS vẫn
ghi `daThu = 0`. Hai sổ độc lập — xem *Chưa làm* bên dưới.

## Mã lỗi

| Mã | Khi nào |
|---|---|
| `KHACH_HANG_TRUNG_SO_DIEN_THOAI` | Số điện thoại đã có khách khác dùng |
| `KHACH_HANG_DA_CO_DANG_KY` | Xoá khách còn đăng ký |
| `KHOA_HOC_TRUNG_TEN` | Tên khoá đã tồn tại |
| `KHOA_HOC_DA_CO_DANG_KY` | Xoá khoá còn đăng ký |
| `TY_GIA_KHONG_HOP_LE` | Tỷ giá ≤ 0 |
| `SO_TIEN_KHONG_HOP_LE` | Số tiền < 0 |
| `THU_VUOT_CAM_KET` | Tổng thu vượt số cam kết của đăng ký |
| `PHAI_CHON_DUNG_MOT_MAT_HANG` | Đơn hàng không có mặt hàng, hoặc có cả khoá học lẫn sản phẩm |
| `SAN_PHAM_TRUNG_TEN` | Tên sản phẩm đã tồn tại |
| `SAN_PHAM_DA_CO_DON_HANG` | Xoá sản phẩm còn đơn hàng |
| `CHI_KHOA_HOC_MOI_XEP_LOP` | Gửi yêu cầu xếp lớp cho đơn mua sản phẩm |
| `DA_GUI_YEU_CAU_XEP_LOP` | Đơn đã gửi yêu cầu trước đó |
| `YEU_CAU_KHONG_HOP_LE` | Yêu cầu không tồn tại (hoặc thuộc tenant khác) |
| `YEU_CAU_DA_XU_LY` | Duyệt/từ chối/huỷ một yêu cầu đã được xử lý |
| `DON_DA_DUOC_XEP_LOP` | Gửi lại yêu cầu cho đơn đã vào lớp |
| `CHUA_NHAP_LY_DO_TU_CHOI` | Từ chối mà không nhập lý do |

## Chưa làm

- **Nối đăng ký CRM với sổ thu học phí LMS** — hiện hai sổ độc lập: FR-21 chuyển *số cam kết*
  sang thành học phí áp dụng, nhưng *số đã thu* thì không, nên cùng một khoản tiền phải ghi hai
  lần nếu muốn cả hai sổ đúng.
- **Nối `LOP_HOC` với `KHOA_HOC`** — cần cho việc ưu tiên lớp cùng khoá ở FR-21.
- **Nhân viên kinh doanh phụ trách khách** (cột cố định trên `KHACH_HANG`) — cần khi làm hoa
  hồng ở HRM. Nay chỉ biết *ai đã chăm sóc* qua `LICH_SU_CHAM_SOC.nguoi_phu_trach_id`.
- Tự tra tỷ giá từ API ngoài; ngưỡng cảnh báo số tiền vô lý theo từng đơn vị.
- **Tồn kho sản phẩm** — hiện bán không giới hạn số lượng.
- Đổi tên bảng `DANG_KY_KHOA_HOC` → `DON_HANG` (nay chứa cả sản phẩm).
- Nhắc lịch chăm sóc (hẹn gọi lại) — cần lịch/thông báo.
