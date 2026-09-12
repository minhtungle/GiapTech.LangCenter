# FR-25 → FR-27 — Học tập trực tuyến (E-learning)

> **Trạng thái: ĐẶC TẢ, chưa code.** Viết 13/09/2026 theo yêu cầu chủ sản phẩm, chờ duyệt trước
> khi làm (quy ước: mô tả FR trước khi viết dòng code nào — CLAUDE.md mục 5).

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
CRM                          LMS
├── KHACH_HANG (hồ sơ)       ├── LOP_HOC      — kênh 1: học theo lớp, có lịch, có giáo viên
├── DANG_KY_KHOA_HOC (đơn)   └── KHOA_ONLINE  — kênh 2: tự học, không lịch, không giáo viên
└── KHOA_HOC (bảng giá)               ↑
         │                            │
         └────── đơn đã thanh toán mở quyền học ──┘
```

**CRM bán, LMS dạy.** `KHOA_HOC` của CRM là *dòng trong bảng giá* (tên, giá, số buổi) — nó
không chứa bài học nào. Nội dung học nằm ở LMS.

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
1. CRM: nhân viên tạo đơn (hoặc khách tự đăng ký — KHACH_HANG.nguon = TuDangKy)
2. CRM: đơn được đánh dấu đã thanh toán
3. → tự tạo NGUOI_DUNG (LoaiNguoiDung = HocVien) + HO_SO_HOC_VIEN nếu KHACH_HANG.nguoi_dung_id null
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
├── khoa_hoc_id  → KHOA_HOC ?    ├── noi_dung        (text/markdown)
├── trang_thai                   ├── thu_tu
└── (4 cột audit)                └── (4 cột audit)

GHI_DANH_KHOA_ONLINE             TIEN_DO_BAI_HOC
├── id                           ├── id
├── khoa_online_id               ├── bai_hoc_online_id
├── hoc_vien_id  → NGUOI_DUNG    ├── hoc_vien_id  → NGUOI_DUNG
├── dang_ky_id   → DANG_KY ?     ├── hoan_thanh_luc
├── ngay_bat_dau                 └── (4 cột audit)
├── ngay_het_han ?               UNIQUE(bai_hoc_online_id, hoc_vien_id)
└── UNIQUE(khoa_online_id, hoc_vien_id)
```

Bốn điểm thiết kế:

- **`khoa_hoc_id` nullable**: khoá online thường tương ứng một khoá bán ra của CRM, nhưng không
  bắt buộc — trung tâm có thể làm khoá nội bộ miễn phí. `RESTRICT` khi xoá, cùng lẽ với
  `LOP_HOC_KHOA_HOC` (12/09): xoá khoá đang bán không được âm thầm phá căn cứ đối chiếu.
- **`dang_ky_id` nullable**: ghi danh thường sinh từ đơn CRM, nhưng quản trị cấp tay được (học
  thử, đền bù). Null = cấp tay, và đó là thông tin chứ không phải thiếu dữ liệu.
- **`ngay_het_han` nullable**: null = học vĩnh viễn. Có hạn thì hết hạn **chặn đọc bài mới**,
  không xoá tiến độ đã có.
- **UNIQUE ở tầng DB**, không phải `if` trong handler (quy tắc #8) — thêm `InlineData` vào
  `DongThoiTests`.

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

Cần `IPhamViKhoaOnline`: học viên chỉ đọc được khoá **mình đã ghi danh và chưa hết hạn**. Đây là
chỗ rò rỉ nặng nhất nếu sai — mua một khoá đọc được tất cả. **Bắt buộc có test kiểu
`Hoc_vien_chi_doc_khoa_minh_ghi_danh` kèm kiểm chiều ngược**, theo đúng bài học từ
`Hoc_vien_chi_thay_lop_minh_dang_hoc`: nhánh phạm vi không test nào canh thì xoá đi vẫn xanh.

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

## Thứ tự làm — 4 bước, commit riêng

| Bước | Nội dung | Rủi ro |
|---|---|---|
| 1 | FR-25: tự tạo `NGUOI_DUNG` khi đơn thanh toán + màn gán tài khoản | Thấp — dùng cột đã có |
| 2 | FR-26 schema + soạn khoá/bài học + `IPhamViKhoaOnline` | Trung bình — tầng phạm vi mới |
| 3 | FR-26 tiến độ học + màn học viên | Thấp |
| 4 | FR-27 nới `BAI_TAP` + chấm bài | **Cao — đụng FR-11/12 đang chạy** |

Bước 4 tách riêng và làm cuối **có chủ ý**: ba bước đầu không đụng gì đang chạy, nên nếu bước 4
phải hoãn thì elearning vẫn dùng được ở mức "học và theo dõi tiến độ".

## Câu chưa chốt, cần chủ sản phẩm trả lời trước bước 2

1. **Khoá online có video không?** MinIO đang dùng cho ảnh và tệp hồ sơ. Video là bài toán khác
   (dung lượng, streaming, băng thông VPS) — nếu có thì nên bàn riêng, đừng gộp vào bước 2.
2. **Hết hạn khoá**: hết hạn rồi có xem lại bài đã học được không, hay chặn hoàn toàn?
3. **Một khoá online có gắn nhiều khoá CRM không?** (`LOP_HOC` đã cho gắn tối đa 3 khoá từ
   12/09 — có cần tương tự?)

## Liên quan

- [CRM](./crm.md) — FR-17 → FR-21, nơi bán và giữ tiền
- [Lớp học](./lop-hoc.md) — kênh học tập thứ nhất
- [Học liệu](./hoc-lieu.md) — FR-11/FR-12 mà bước 4 sẽ đụng vào
- [ADR-0005](../kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md) — vì sao không tách service
