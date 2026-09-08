# Module CRM (FR-17 → FR-20)

Bán khoá học: gom dữ liệu khách hàng, bán khoá, tổng hợp doanh thu.

> **Hệ thống không xử lý tiền.** Cùng giới hạn với [Học phí](./hoc-phi.md): không cổng thanh
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
| **Ghi mua hàng** | Một dòng `DANG_KY_KHOA_HOC` (đơn hàng) **VÀ** một dòng `LICH_SU_CHAM_SOC` |

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
- Sửa/xoá đăng ký ghi vào [nhật ký](./nhat-ky-he-thong.md) như mọi thao tác khác (FR-16).
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

## Chưa làm

- **Nối đăng ký CRM với sổ thu học phí LMS** — hiện hai sổ độc lập.
- **Nhân viên kinh doanh phụ trách khách** (cột cố định trên `KHACH_HANG`) — cần khi làm hoa
  hồng ở HRM. Nay chỉ biết *ai đã chăm sóc* qua `LICH_SU_CHAM_SOC.nguoi_phu_trach_id`.
- Tự tra tỷ giá từ API ngoài; ngưỡng cảnh báo số tiền vô lý theo từng đơn vị.
- **Tồn kho sản phẩm** — hiện bán không giới hạn số lượng.
- Đổi tên bảng `DANG_KY_KHOA_HOC` → `DON_HANG` (nay chứa cả sản phẩm).
- Nhắc lịch chăm sóc (hẹn gọi lại) — cần lịch/thông báo.
