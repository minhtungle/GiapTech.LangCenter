# FR-25 → FR-27 — Học tập trực tuyến (E-learning)

> **Trạng thái: ĐẶC TẢ, chưa code.** Viết 13/09/2026 theo yêu cầu chủ sản phẩm, chờ duyệt trước
> khi làm (quy ước: mô tả FR trước khi viết dòng code nào — CLAUDE.md mục 5).
>
> Ba câu hỏi mở đã được chốt cùng ngày — xem [mục cuối](#ba-câu-đã-chốt-13092026).

## Luồng chủ sản phẩm mô tả

Ba câu, mỗi câu chốt một ranh giới:

| # | Phát biểu | Ranh giới nó chốt |
|---|---|---|
| 1 | Dữ liệu và doanh thu khách hàng đều lưu tại **CRM** | Tiền và hồ sơ khách ở CRM, không chảy sang LMS |
| 2 | **Quản trị** khởi tạo tài khoản và gán với hồ sơ (người dùng **hoặc khách hàng**) | Tài khoản do quản trị cấp, và gán được cho **hai** loại hồ sơ |
| 3 | **LMS** quản lý kênh học tập cho **tất cả** học viên (khoá học, elearning) | Nội dung học ở LMS, không phân biệt học viên đến từ đâu |

Câu 3 là câu quan trọng nhất và **sửa một hiểu nhầm**: elearning không phải một hệ thống thứ tư,
cũng không thuộc CRM. Nó là **một kênh học tập nữa của LMS**, song song với lớp offline.

```
CRM                              LMS
├── KHACH_HANG  (hồ sơ khách)    ├── LOP_HOC     — kênh 1: học theo lớp, có lịch, có giáo viên
├── DANG_KY_KHOA_HOC (đơn)       └── KHOA_ONLINE — kênh 2: tự học, không lịch, không giáo viên
├── KHOA_HOC   (bảng giá lớp)              ↑
└── SAN_PHAM   (bảng giá món) ──────────────┘
         └── đơn thanh toán → mở quyền học
```

**CRM bán, LMS dạy.** Danh mục bán hàng ở CRM chỉ là *bảng giá* — không chứa bài học nào. Nội
dung học nằm ở LMS.

**Khoá online bán như một SẢN PHẨM** (chốt 13/09/2026), **không** dùng chung `KHOA_HOC`:
`KHOA_HOC` là khoá dạy theo lớp, có `so_buoi`, và `LOP_HOC` gắn tối đa 3 khoá đó để đối chiếu
khi xếp lớp. Khoá online không có buổi nào và không xếp lớp — nhét chung sẽ làm cảnh báo lệch
khoá (FR-21) so nhầm.

---

## Vì sao KHÔNG tách hệ thống thứ tư

Chủ sản phẩm có hỏi. Câu trả lời là **không**, cùng lý lẽ đã chốt ở
[ADR-0005](../kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md):

- Dữ liệu dùng chung (`NGUOI_DUNG`, `TAI_KHOAN`, `TENANT`, nhóm quyền, `TEP_DINH_KEM`) sẽ phải
  **đồng bộ giữa hai database** nếu tách.
- Một người hoàn toàn có thể **vừa học lớp offline vừa mua khoá online**. Tách ra là họ có hai
  hồ sơ, hai tài khoản, và không màn nào trả lời được "người này đang học những gì".
- Câu 3 nói rõ *"tất cả học viên"* — tức là một kênh trong LMS, không phải một hệ thống riêng.

Elearning vào LMS dưới dạng **nhóm chức năng mới trong `ChucNang`**, hiện trong sidebar LMS như
Lớp học và Bài tập hiện nay.

---

## FR-25 — Tài khoản gán cho khách hàng

### Vấn đề hiện tại

`TAI_KHOAN.nguoi_dung_id` chỉ trỏ `NGUOI_DUNG`. Nhưng câu 2 nói tài khoản gán được cho
**"người dùng hoặc khách hàng"**, và khách mua khoá online thì **chưa phải học viên** —
không có buổi học nào, không thuộc lớp nào.

Hiện FR-21 xử lý bằng cách **tạo `NGUOI_DUNG` khi duyệt vào lớp**
(`YeuCauXepLopDtos.cs:188`). Cách đó đúng cho lớp offline, nhưng khoá online không đi qua bước
duyệt xếp lớp nào cả.

### Ba phương án, chọn phương án 3

| | Cách làm | Vấn đề |
|---|---|---|
| 1 | Thêm `TAI_KHOAN.khach_hang_id` nullable, loại trừ với `nguoi_dung_id` | Mọi truy vấn "tài khoản này là ai" phải rẽ hai nhánh. Phân quyền, nhật ký, `ICurrentUser.UserId` đều phải sửa |
| 2 | Bỏ `KHACH_HANG`, dồn hết vào `NGUOI_DUNG` | Phá ranh giới CRM/LMS, trái câu 1 |
| 3 | **Khách mua hàng → tự tạo `NGUOI_DUNG` tối thiểu, nối bằng `KHACH_HANG.nguoi_dung_id` đã có** | Không đổi schema tài khoản |

**Chọn 3.** Lý do: `KHACH_HANG.nguoi_dung_id` **đã tồn tại và đã dùng** — FR-21 đang làm đúng
việc này khi duyệt vào lớp. Ta chỉ mở rộng thời điểm gọi nó: thêm một thời điểm nữa là *khi đơn
khoá online được thanh toán*.

`NGUOI_DUNG` ở đây là **danh tính để đăng nhập và học**, không phải "nhân sự". Đó cũng đúng với
thiết kế đã chốt 07/09: *người ≠ tài khoản*, và `NGUOI_DUNG` sống lâu hơn cả hai.

> **Không copy họ tên/email từ `KHACH_HANG` sang `NGUOI_DUNG`** — nối bằng khoá ngoại. Copy thì
> hai bên trôi khỏi nhau và không biết bên nào đúng (lỗi đã gặp 07/09/2026).

### Luồng

```
1. CRM: nhân viên tạo đơn mua SẢN PHẨM là khoá online
        (hoặc khách tự đăng ký — KHACH_HANG.nguon = TuDangKy)
2. CRM: đơn được đánh dấu đã thanh toán
3. → tự tạo NGUOI_DUNG (LoaiNguoiDung = HocVien) + HO_SO_HOC_VIEN
        nếu KHACH_HANG.nguoi_dung_id còn null
4. → mở quyền học: ghi GHI_DANH_KHOA_ONLINE
5. Quản trị: cấp tài khoản đăng nhập, gán vào NGUOI_DUNG vừa tạo
6. Học viên đăng nhập → thấy khoá đã mua ở LMS
```

**Bước 5 do quản trị làm, không tự động** — đúng câu 2. Không tự sinh mật khẩu gửi email ở bản
này: `IEmailSender` chưa nối SMTP thật (nợ N10), và tự tạo tài khoản từ endpoint công khai là
đúng bề mặt tấn công mà nợ N3 đang cảnh báo.

### Cái bẫy: `LoaiNguoiDung.HocVien` không phải quyền

`LoaiNguoiDung` chỉ dùng để **lọc danh sách**, không bao giờ để phân quyền (quy tắc #9). Người
mua khoá online nhận nhóm quyền "Học viên" như mọi học viên khác; việc họ thấy gì do
`GHI_DANH_KHOA_ONLINE` quyết định, không do loại người dùng.

---

## FR-26 — Khoá học trực tuyến

### Schema

```
KHOA_ONLINE                      BAI_HOC_ONLINE
├── id                           ├── id
├── ten                          ├── khoa_online_id  → KHOA_ONLINE (Cascade)
├── mo_ta                        ├── tieu_de
├── san_pham_id  → SAN_PHAM ?    ├── noi_dung        (markdown)
├── trang_thai                   ├── thu_tu
└── (4 cột audit)                ├── cong_khai       bool
                                 └── (4 cột audit)

GHI_DANH_KHOA_ONLINE             TIEN_DO_BAI_HOC
├── id                           ├── id
├── khoa_online_id               ├── bai_hoc_online_id
├── hoc_vien_id  → NGUOI_DUNG    ├── hoc_vien_id  → NGUOI_DUNG
├── dang_ky_id   → DANG_KY ?     ├── hoan_thanh_luc
├── ngay_bat_dau                 └── (4 cột audit)
├── ngay_het_han ?               UNIQUE(bai_hoc_online_id, hoc_vien_id)
└── UNIQUE(khoa_online_id, hoc_vien_id)
```

Năm điểm thiết kế:

- **`san_pham_id` nullable** — nối sang bảng giá để biết "mua món này thì mở khoá nào". Nullable
  vì khoá nội bộ không bán (học bù, học thử, tài liệu miễn phí cho học viên đang học lớp) là ca
  hợp lệ, không phải dữ liệu thiếu. `RESTRICT` khi xoá: xoá món đang bán không được âm thầm cắt
  đường mở quyền học.
- **`SAN_PHAM` nay có hai loại hàng.** Cần thêm `SAN_PHAM.loai` (`HangHoa` / `KhoaOnline`) chứ
  không suy từ `san_pham_id` có bản ghi `KHOA_ONLINE` trỏ về hay không — suy ngược như vậy thì
  một khoá online chưa soạn xong sẽ bị tính là hàng vật lý. Hai hệ quả bắt buộc:

  | | Hàng hoá | Khoá online |
  |---|---|---|
  | `SoLuong` | mua 3 quyển = 1 dòng `SoLuong=3` | **luôn 1** — ép ở handler như `KHOA_HOC` đang làm, không tin client |
  | Tồn kho (nợ N17) | có giới hạn | **không áp dụng** — bán bao nhiêu suất cũng được |

- **`dang_ky_id` nullable**: ghi danh thường sinh từ đơn CRM, nhưng quản trị cấp tay được (học
  thử, đền bù, học viên lớp offline được tặng khoá ôn). Null = cấp tay — đó là **thông tin**,
  không phải thiếu dữ liệu.
- **`ngay_het_han` nullable**: null = học vĩnh viễn.
- **UNIQUE ở tầng DB**, không phải `if` trong handler (quy tắc #8) — thêm `InlineData` vào
  `DongThoiTests`.

### Hết hạn: chặn, trừ bài công khai

Chốt 13/09/2026: **hết hạn thì không xem được, trừ bài đánh dấu `cong_khai`.**

- Hết hạn **không xoá** `TIEN_DO_BAI_HOC` — gia hạn lại thì học tiếp từ chỗ cũ, và tiến độ là
  dấu vết học tập của người ta, không phải thứ để dọn.
- `cong_khai = true` → **ai đăng nhập cũng xem được**, không cần ghi danh khoá đó. Dùng cho bài
  giới thiệu, bài mẫu.
- **Không có endpoint ẩn danh** cho bài công khai ở bản này: "công khai" ở đây nghĩa là *trong
  trung tâm*, vẫn nằm trong ranh giới tenant. Mở ra Internet là bề mặt tấn công mới và phải bàn
  riêng — xem nợ N3.

> Cái bẫy: `cong_khai` nằm ở **bài học**, không ở khoá. Nên truy vấn "học viên này đọc được bài
> nào" là **hợp của hai tập** — bài thuộc khoá còn hạn, và bài công khai của mọi khoá. Viết
> thành hai truy vấn rồi `UNION` thì dễ đúng hơn một `WHERE` lồng nhau.

### Quyền

Ba chức năng mới trong `ChucNang`, đều thuộc `HeThong.Lms`:

| Chức năng | Ai cần |
|---|---|
| `KhoaOnline` | Quản trị/giáo vụ soạn khoá và bài học |
| `GhiDanhKhoaOnline` | Quản trị cấp/thu quyền học |
| `HocOnline` | Học viên đọc bài, đánh dấu đã học — quyền của chính người học |

### Phạm vi — tầng bảo vệ thứ tư

Ba tầng hiện có (`RequirePermission` · Query Filter · `IPhamViLopHoc`) **không đủ**:
`IPhamViLopHoc` lọc theo `LOP_HOC`, mà khoá online không có lớp nào.

Cần `IPhamViKhoaOnline` với **ba nhánh**, mỗi nhánh một test riêng:

| Nhánh | Điều kiện | Rủi ro nếu sai |
|---|---|---|
| Ghi danh còn hạn | `GHI_DANH` có và `ngay_het_han` null hoặc chưa qua | Mua một khoá đọc được tất cả |
| Bài công khai | `BAI_HOC_ONLINE.cong_khai` | Đánh dấu nhầm → lộ toàn bộ nội dung trả phí |
| Người soạn | có `KhoaOnline` + `Sua` | Giáo vụ không sửa được khoá mình soạn |

**Bắt buộc test cả ba kèm kiểm chiều ngược**, theo đúng bài học từ
`Hoc_vien_chi_thay_lop_minh_dang_hoc` và `Lop_nhap_chi_nguoi_tao_thay` (13/09): nhánh phạm vi
không test nào canh thì xoá đi vẫn xanh cả bộ.

Nhánh **hết hạn** đặc biệt dễ xanh giả: test dựng ghi danh mặc định `ngay_het_han = null` thì
nhánh kiểm hạn không bao giờ chạy. Phải có ca ghi danh **đã hết hạn** và khẳng định đọc bài
thường → 404, bài công khai → 200.

---

## FR-27 — Bài tập cho khoá online

### Ràng buộc phải gỡ trước

`BAI_TAP.buoi_hoc_id` hiện **NOT NULL** — bài tập gắn buổi học. Khoá online không có buổi nào.

Đổi thành **hai FK nullable loại trừ nhau + `CHECK`** — đúng khuôn `DANG_KY_KHOA_HOC` đang dùng
cho khoá học/sản phẩm (FR-20):

```sql
buoi_hoc_id        uuid NULL  → BUOI_HOC
bai_hoc_online_id  uuid NULL  → BAI_HOC_ONLINE
CHECK (num_nonnulls(buoi_hoc_id, bai_hoc_online_id) = 1)
```

> ⚠️ **Đây là thay đổi rủi ro nhất của cả ba FR.** Nó đụng FR-11/FR-12 đang chạy thật. Quy tắc
> #1: làm hẹp/đổi cột phải hỏi trước. Migration **chỉ nới lỏng** (NOT NULL → NULL) nên dữ liệu
> hiện có an toàn, nhưng **mọi truy vấn đi qua `n.BaiTap.BuoiHoc.LopHocId` sẽ gặp null** —
> trong đó có `LayTongQuanHandler` (FR-15, viết hôm qua) và `IPhamViLopHoc`.
>
> Phải rà **từng** chỗ đọc `BaiTap.BuoiHoc` trước khi đổi, không sau.

### Chấm bài

Giữ nguyên cơ chế FR-12 (`BAI_NOP`, nộp nhiều lần, chấm qua endpoint riêng). Khác một điểm:
khoá online không có giáo viên phụ trách, nên **ai chấm** phải nói rõ — mặc định là người có
`BaiNopBaiTap` + `Sua` trong trung tâm, không suy từ "giáo viên của lớp".

---

## Thứ tự làm — 5 bước, commit riêng

| Bước | Nội dung | Rủi ro |
|---|---|---|
| 0 | `SAN_PHAM.loai` (`HangHoa`/`KhoaOnline`) + ép `SoLuong=1` cho khoá online | Thấp — cột mới, mặc định `HangHoa` giữ nguyên nghĩa 3 hàng đang có |
| 1 | FR-25: tự tạo `NGUOI_DUNG` khi đơn thanh toán + màn gán tài khoản | Thấp — dùng cột đã có |
| 2 | FR-26 schema + soạn khoá/bài học + `IPhamViKhoaOnline` | Trung bình — tầng phạm vi mới |
| 3 | FR-26 tiến độ học + màn học viên | Thấp |
| 4 | FR-27 nới `BAI_TAP` + chấm bài | **Cao — đụng FR-11/12 đang chạy** |

Bước 4 tách riêng và làm cuối **có chủ ý**: ba bước đầu không đụng gì đang chạy, nên nếu bước 4
phải hoãn thì elearning vẫn dùng được ở mức "học và theo dõi tiến độ".

## Ba câu đã chốt (13/09/2026)

| Câu hỏi | Trả lời | Ảnh hưởng |
|---|---|---|
| Khoá có **video** không? | **Tạm thời chưa cần** | Bài học là markdown + tệp đính kèm. Không đụng bài toán streaming/băng thông VPS. Nếu sau này cần thì bàn riêng, đừng gộp |
| **Hết hạn** có xem lại được? | **Không, trừ bài công khai** | `cong_khai` nằm ở bài học; truy vấn đọc được là **hợp của hai tập** |
| Gắn **nhiều khoá CRM**? | **Không liên quan khoá CRM** — bán như một **sản phẩm** | Thêm `SAN_PHAM.loai`; `KHOA_ONLINE.san_pham_id` thay cho `khoa_hoc_id` |

## Liên quan

- [CRM](./crm.md) — FR-17 → FR-21, nơi bán và giữ tiền
- [Lớp học](./lop-hoc.md) — kênh học tập thứ nhất
- [Học liệu](./hoc-lieu.md) — FR-11/FR-12 mà bước 4 sẽ đụng vào
- [ADR-0005](../kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md) — vì sao không tách service
