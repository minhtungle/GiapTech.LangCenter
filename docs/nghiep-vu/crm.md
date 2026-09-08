# Module CRM (FR-17 → FR-19)

Bán khoá học: gom dữ liệu khách hàng, bán khoá, tổng hợp doanh thu.

> **Hệ thống không xử lý tiền.** Cùng giới hạn với [Học phí](./hoc-phi.md): không cổng thanh
> toán, không đối chiếu sao kê. Số ở đây là **lời khai của trung tâm**.

## Ba màn, một dòng chảy

```
Khách hàng  ──(mua hàng)──▶  Doanh thu  ──(chọn từ)──▶  Khoá học
 FR-17                        FR-18                     FR-19
 người quan tâm               đăng ký + tiền            danh mục sản phẩm
```

**Khách hàng chưa mua thì chưa có doanh thu.** Ai mua mới xuất hiện ở màn Doanh thu — đó là
định nghĩa của "chuyển qua doanh thu", không phải một nút bấm.

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

### Quy tắc

- Khách chưa có đăng ký nào thì **không** xuất hiện ở màn Doanh thu.
- Sửa/xoá đăng ký ghi vào [nhật ký](./nhat-ky-he-thong.md) như mọi thao tác khác (FR-16).
- **Chưa nối với sổ thu học phí LMS** (FR-14) — hai sổ độc lập, chưa đối chiếu. Nợ kỹ thuật.

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

## Chưa làm

- **Nối đăng ký CRM với sổ thu học phí LMS** — hiện hai sổ độc lập.
- **Phễu bán hàng** (trạng thái khách: mới → đang tư vấn → đã mua → mất). Nay chỉ suy từ "có
  đăng ký hay không".
- **Nhân viên kinh doanh phụ trách khách** — cần khi làm hoa hồng (module HRM).
- Tự tra tỷ giá từ API ngoài.
