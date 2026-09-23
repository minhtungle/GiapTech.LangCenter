# FR-25 → FR-27 — Học tập trực tuyến (E-learning)

> **Trạng thái: bước 1-3 XONG (elearning dùng được). Bước 4 (bài tập được chấm) còn là đặc tả.** Viết 13/09/2026 theo yêu cầu chủ sản phẩm, chờ duyệt trước
> khi làm (quy ước: mô tả FR trước khi viết dòng code nào — CLAUDE.md mục 5).
>
> Ba câu hỏi mở đã được chốt cùng ngày — xem [mục cuối](#ba-câu-đã-chốt-13092026).

## Luồng chủ sản phẩm mô tả

Ba bước, chốt lại 13/09/2026 — *"rất đơn giản, tách biệt không chồng chéo"*:

```
1. LMS:  tạo các khoá học elearning
2. CRM:  học viên mua hàng → ghi nhận DOANH THU (như mọi đơn hàng khác)
3. LMS:  quản trị tạo tài khoản học viên, vào khoá elearning, thêm quyền truy cập
```

### Điểm cốt lõi: KHÔNG có liên kết tự động giữa hai bước

Bước 2 và bước 3 **không nối với nhau bằng code**. CRM ghi tiền, LMS cấp quyền, người điều phối
là **con người** — quản trị viên nhìn đơn rồi vào LMS cấp quyền.

Nghe như thiếu sót, nhưng đó chính là chỗ *"tách biệt không chồng chéo"*:

| | Nếu nối tự động | Luồng đã chốt |
|---|---|---|
| CRM phải biết | có những khoá online nào, món nào mở khoá nào | **không biết gì** về elearning |
| Bảng giá | `SAN_PHAM` phải phân loại hàng hoá / khoá online | giữ nguyên, khoá online là một món như mọi món |
| Khi sai | đơn ghi nhầm món → học viên vào nhầm khoá, phải sửa hai bên | quản trị cấp nhầm → sửa một chỗ ở LMS |
| Ca không mua mà vẫn học | phải thêm nhánh ngoại lệ (học thử, tặng, học bù) | **không phải ca đặc biệt** — mọi ghi danh đều do người cấp |

Cái giá phải trả: bán nhiều thì quản trị cấp quyền bằng tay nhiều. Khi nào thành gánh nặng thật
thì mới tự động hoá — và lúc đó đã có dữ liệu thật để biết nối theo tiêu chí nào. Tự động hoá
trước khi biết là đoán.

```
CRM                              LMS
├── KHACH_HANG  (hồ sơ khách)    ├── LOP_HOC     — kênh 1: học theo lớp, có lịch, có giáo viên
├── DANG_KY_KHOA_HOC (đơn+tiền)  └── KHOA_ONLINE — kênh 2: tự học, không lịch, không giáo viên
├── KHOA_HOC   (bảng giá lớp)             ↑
└── SAN_PHAM   (bảng giá món)             │
                                  quản trị cấp quyền bằng tay
                                  (không có FK nào nối sang CRM)
```

**CRM bán và giữ tiền, LMS dạy.** Không bảng nào của LMS trỏ sang CRM, và ngược lại — trừ
`KHACH_HANG.nguoi_dung_id` đã có sẵn từ FR-21.

## Vì sao KHÔNG tách hệ thống thứ tư

Chủ sản phẩm có hỏi. Câu trả lời là **không**, cùng lý lẽ đã chốt ở
[ADR-0005](../02-kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md):

- Dữ liệu dùng chung (`NGUOI_DUNG`, `TAI_KHOAN`, `TENANT`, nhóm quyền, `TEP_DINH_KEM`) sẽ phải
  **đồng bộ giữa hai database** nếu tách.
- Một người hoàn toàn có thể **vừa học lớp offline vừa mua khoá online**. Tách ra là họ có hai
  hồ sơ, hai tài khoản, và không màn nào trả lời được "người này đang học những gì".
- Câu 3 nói rõ *"tất cả học viên"* — tức là một kênh trong LMS, không phải một hệ thống riêng.

Elearning vào LMS dưới dạng **nhóm chức năng mới trong `ChucNang`**, hiện trong sidebar LMS như
Lớp học và Bài tập hiện nay.

> Lưu ý: *"tách biệt"* trong yêu cầu của chủ sản phẩm nói về **luồng nghiệp vụ** (CRM ghi tiền,
> LMS cấp quyền, không tự động nối), **không** phải tách database hay service. Hai thứ khác nhau
> — và cách làm ở đây đạt được cái thứ nhất mà không phải trả giá cho cái thứ hai.

---

## FR-25 — Tài khoản học viên cho người mua khoá online

### Vấn đề

`TAI_KHOAN.nguoi_dung_id` chỉ trỏ `NGUOI_DUNG`. Người mua khoá online là `KHACH_HANG` ở CRM và
**chưa phải học viên** — không thuộc lớp nào, không có buổi học nào.

Hiện FR-21 tạo `NGUOI_DUNG` khi **duyệt vào lớp** (`YeuCauXepLopDtos.cs:188`). Khoá online không
đi qua bước duyệt xếp lớp nào.

### Cách làm: quản trị tạo, không tự động

Đúng bước 3 của luồng — **quản trị viên LMS tạo tài khoản học viên**. Không đổi schema
`TAI_KHOAN`, không thêm nhánh tự động nào.

**Cập nhật 13/09/2026 — làm ở CRM, không ở LMS.** Bản đầu thêm ô chọn khách hàng vào màn
`/lms/hoc-vien`; sau đó chủ sản phẩm chốt *"học viên giờ quản lý tập trung tại CRM"* nên màn đó
**đã bỏ hẳn**.

Nay cấp tài khoản từ **menu thao tác ở màn Khách hàng (CRM)**. Hướng nối đảo ngược: CRM tạo hồ
sơ và nối luôn, thay vì LMS đi tìm khách để nối. Gọn hơn và đúng một chỗ duy nhất.

```
Quản trị vào /lms/hoc-vien → Thêm học viên
  ├── nhập họ tên, liên hệ
  ├── [tuỳ chọn] chọn khách hàng CRM tương ứng → nối KHACH_HANG.nguoi_dung_id
  └── cấp tài khoản đăng nhập (username + mật khẩu tạm)
Rồi vào khoá elearning → Thêm học viên vào khoá  (FR-26)
```

**Ô chọn khách hàng là TUỲ CHỌN**, không bắt buộc: học viên học thử hay được tặng khoá thì không
có đơn nào ở CRM cả. Bắt buộc nối sẽ biến ca hợp lệ thành ca không nhập được.

> **Không copy họ tên/email từ `KHACH_HANG` sang `NGUOI_DUNG`** — nối bằng khoá ngoại. Copy thì
> hai bên trôi khỏi nhau và không biết bên nào đúng (lỗi đã gặp 07/09/2026).

### Ranh giới hệ thống con

Màn này ở LMS nhưng đọc `KHACH_HANG` của CRM → là **cầu nối chéo thứ hai** sau FR-21. Phải khai
vào `CauNoiDuocPhep` của `RanhGioiHeThongConTests`, và khai cả `db.KhachHangs` vào
`DbSetCuaHeThong` — lỗ hổng đã vá 12/09 chính là kiểu này lọt qua.

Chỉ đọc `ho_ten` + `so_dien_thoai` để người dùng chọn đúng người. **Không đọc số tiền** — quy
tắc đã chốt 12/09: chỉ CRM nắm tiền.

### Cái bẫy: `LoaiNguoiDung.HocVien` không phải quyền

`LoaiNguoiDung` chỉ dùng để **lọc danh sách**, không bao giờ để phân quyền (quy tắc #9). Người
mua khoá online nhận nhóm quyền "Học viên" như mọi học viên khác; việc họ thấy gì do
`GHI_DANH_KHOA_ONLINE` quyết định, không do loại người dùng.

## FR-26 — Khoá học trực tuyến

### Schema

```
KHOA_ONLINE                      BAI_HOC_ONLINE
├── id                           ├── id
├── ten                          ├── khoa_online_id  → KHOA_ONLINE (Cascade)
├── mo_ta                        ├── tieu_de
├── trang_thai                   ├── noi_dung        (markdown)
└── (4 cột audit)                ├── thu_tu
                                 ├── cong_khai       bool
                                 └── (4 cột audit)

GHI_DANH_KHOA_ONLINE             TIEN_DO_BAI_HOC
├── id                           ├── id
├── khoa_online_id               ├── bai_hoc_online_id
├── hoc_vien_id  → NGUOI_DUNG    ├── hoc_vien_id  → NGUOI_DUNG
├── ngay_bat_dau                 ├── hoan_thanh_luc
├── ngay_het_han ?               └── (4 cột audit)
├── ghi_chu ?                    UNIQUE(bai_hoc_online_id, hoc_vien_id)
└── UNIQUE(khoa_online_id, hoc_vien_id)
```

**Bốn bảng, không bảng nào trỏ sang CRM.** Đó là điều đáng chú ý nhất: không có `khoa_hoc_id`,
không có `san_pham_id`, không có `dang_ky_id`. Quản trị cấp quyền bằng tay nên LMS không cần
biết đơn hàng nào tồn tại.

Bốn điểm thiết kế:

- **`ghi_chu` thay cho `dang_ky_id`**: ai cấp và vì sao — *"mua đơn #123"*, *"học thử"*,
  *"tặng kèm lớp IELTS"*. Chữ tự do chứ không khoá ngoại: nối FK sang `DANG_KY_KHOA_HOC` là dựng
  lại đúng cái chồng chéo vừa bỏ. Ai cấp thì **`created_by_id`** đã trả lời (cột audit, 12/09).
- **`ngay_het_han` nullable**: null = học vĩnh viễn.
- **UNIQUE ở tầng DB**, không phải `if` trong handler (quy tắc #8) — thêm `InlineData` vào
  `DongThoiTests`. Cấp quyền hai lần cho cùng người là thao tác tay dễ xảy ra.
- **Không cột "đã thanh toán"** ở `GHI_DANH`: tiền là việc của CRM. Có bản ghi ghi danh nghĩa là
  quản trị đã quyết định cho học — căn cứ nằm ở `ghi_chu`.

### Việc KHÔNG phải làm nữa

Bản đặc tả trước (cùng ngày) có hai việc nay **bỏ hẳn**, vì chúng chỉ tồn tại để phục vụ liên
kết tự động:

| Đã bỏ | Vì sao |
|---|---|
| `SAN_PHAM.loai` (`HangHoa`/`KhoaOnline`) | CRM không cần biết món nào là khoá online. Khoá online bán như mọi món khác, hoặc không bán qua CRM cũng được |
| `KHOA_ONLINE.san_pham_id` | Không có bước "mua món này thì mở khoá kia" |

Kéo theo: **`SoLuong` và tồn kho (nợ N17) không phải xử lý gì thêm** — vì `SAN_PHAM` không đổi.

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

## Thứ tự làm — 4 bước, commit riêng

| Bước | Nội dung | Rủi ro |
|---|---|---|
| 1 | ~~FR-25: ô chọn khách hàng CRM khi tạo học viên~~ **✅ XONG 13/09/2026** | Thấp — dùng cột đã có |
| 2 | ~~FR-26 schema + soạn khoá/bài học + cấp quyền + `IPhamViKhoaOnline`~~ **✅ XONG 13/09/2026** (backend) | Trung bình — tầng phạm vi mới |
| 3 | ~~FR-26 **giao diện**: màn soạn khoá, màn học của học viên~~ **✅ XONG 13/09/2026** | Thấp — backend đã xong |
| 4 | FR-27 nới `BAI_TAP` + chấm bài | **Cao — đụng FR-11/12 đang chạy** |

Bước 4 tách riêng và làm cuối **có chủ ý**: ba bước đầu không đụng gì đang chạy, nên nếu bước 4
phải hoãn thì elearning vẫn dùng được ở mức "học và theo dõi tiến độ".

## Ba câu đã chốt (13/09/2026)

| Câu hỏi | Trả lời | Ảnh hưởng |
|---|---|---|
| Khoá có **video** không? | **Tạm thời chưa cần** | Bài học là markdown + tệp đính kèm. Không đụng bài toán streaming/băng thông VPS. Nếu sau này cần thì bàn riêng, đừng gộp |
| **Hết hạn** có xem lại được? | **Không, trừ bài công khai** | `cong_khai` nằm ở bài học; truy vấn đọc được là **hợp của hai tập** |
| Gắn **nhiều khoá CRM**? | **Không liên quan khoá CRM** | LMS **không trỏ sang CRM** chút nào. Quản trị cấp quyền bằng tay, nên không cần `khoa_hoc_id` lẫn `san_pham_id`. `SAN_PHAM` giữ nguyên |

## Liên quan

- [CRM](crm.md) — FR-17 → FR-21, nơi bán và giữ tiền
- [Lớp học](lop-hoc.md) — kênh học tập thứ nhất
- [Học liệu](hoc-lieu.md) — FR-11/FR-12 mà bước 4 sẽ đụng vào
- [ADR-0005](../02-kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md) — vì sao không tách service
