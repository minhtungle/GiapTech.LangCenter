# 2026-09-13 — Học trực tuyến, và ba lỗ hổng trong lưới canh của chính mình

Ngày này dựng elearning từ đặc tả tới chạy được (bước 1→3 của 4). Nhưng thứ đáng ghi lại hơn cả
tính năng là **ba lần lưới canh của dự án tỏ ra có lỗ** — hai lần nó bắt được tôi, một lần chính
nó hỏng mà không ai biết.

## Đặc tả viết hai lần, bản sau đơn giản hơn hẳn

Bản đầu tôi tự động hoá luồng: *"đơn thanh toán → tự tạo `NGUOI_DUNG` → tự ghi `GHI_DANH`"*.
Chủ sản phẩm đọc rồi chốt lại bằng ba câu:

> Dữ liệu và doanh thu khách hàng đều lưu tại CRM · Quản trị khởi tạo tài khoản và gán với hồ sơ
> · LMS quản lý kênh học tập cho tất cả học viên.
>
> *"Rất đơn giản, tách biệt không chồng chéo."*

Bỏ tự động hoá thì **ba thứ biến mất khỏi thiết kế**:

| Bỏ | Vì sao nó từng tồn tại |
|---|---|
| `SAN_PHAM.loai` (HangHoa/KhoaOnline) | để CRM biết món nào mở khoá nào |
| `KHOA_ONLINE.san_pham_id` | để nối "mua món này → mở khoá kia" |
| `GHI_DANH.dang_ky_id` | để truy ngược đơn hàng |

Cả ba chỉ tồn tại **để phục vụ liên kết tự động**. Bỏ liên kết thì chúng thành thừa.

Lợi ích không lường trước: ca *"học thử / được tặng / học bù"* trong thiết kế cũ là **nhánh ngoại
lệ** phải viết riêng. Giờ nó không còn đặc biệt — mọi ghi danh đều do người cấp, khác nhau chỉ ở
dòng `ghi_chu` chữ tự do.

**Bài học**: khi một thiết kế cần nhiều cột chỉ để nối hai thứ lại, hỏi xem có cần nối không đã.

## Lỗ hổng 1 — lưới ranh giới không phủ hết thư mục

Thêm `db.KhachHangs` vào `QuanTri/NguoiDung/NguoiDungDtos.cs` rồi chạy
`RanhGioiHeThongConTests` để xem nó có bắt không. **Test xanh.**

Lưới chỉ quét ba thư mục `Crm/`, `DaoTao/`, `NhanSu/` — nên `QuanTri/`, `DangNhap/`, `Common/`
nằm ngoài hoàn toàn.

Hôm qua vá lỗ hổng *"namespace không thấy `db.X`"*; hôm nay là *"lưới không phủ hết thư mục"*.
Cùng một bài học: **danh sách những-chỗ-được-kiểm phải là danh sách đóng.** Nay mọi thư mục phải
rơi vào một trong ba nhóm khai tường minh — hệ thống con · dùng chung (vẫn quét) · miễn trừ.

### Test chiều ngược tôi viết để vá nó cũng hỏng

Viết `Moi_thu_muc_phai_nam_trong_luoi_hoac_duoc_khai_mien_tru`, chạy xanh. Tiêm đột biến — tạo
thư mục mới chưa khai — **vẫn xanh**.

Lý do: nó hỏi *"thư mục này có được quét không"*, mà `ThuMucCanQuet` = mọi thư mục trừ miễn trừ,
nên câu trả lời **luôn** là có. Test không thể đỏ.

Viết lại thành *"đã phân loại chưa"*. Nay ba đột biến đều đỏ đúng.

## Lỗ hổng 2 — nhánh phạm vi không ai canh

Gộp hai cột `nguoi_tao_id` / `created_by_id`, tôi phải sửa `PhamViLopHoc`:

```csharp
(l.TrangThai != TrangThaiLopHoc.Nhap || l.CreatedById == uid)
```

Grep thử: **không test nào canh lớp nháp**. Sửa sai thì 444 test vẫn xanh trong khi lớp nháp của
người này lộ cho giáo viên được phân công dạy nó.

Viết test, tiêm đột biến (bỏ hẳn nhánh lọc) — đỏ đúng.

## Cột audit không có khoá ngoại

Gộp cột trùng nghĩa thì phát hiện thứ nặng hơn: `created_by_id` trên **37 bảng** không có FK
nào. Chú thích viết *"trỏ `PERSON.id`"* nhưng không gì ép — cột `uuid` trần chứa được GUID rác
hoặc trỏ người đã xoá. Trong khi `nguoi_tao_id` — cột cũ nó thay thế — **có** FK `SET NULL`.

Gộp mà không thêm FK là **bước lùi**. Nay áp 74 FK.

> Dấu vết audit sai còn tệ hơn không có dấu vết: người đọc tin vào nó.

### Hai bẫy khi khai FK toàn cục, cả hai `dotnet build` đều xanh

| Bẫy | Triệu chứng |
|---|---|
| `HasOne(typeof(NguoiDung))` | EF không biết quan hệ đi qua property nào → tạo **cột bóng** `CreatedById1`, **108 cột rác** trong snapshot |
| `NGUOI_DUNG` tự tham chiếu | Hai navigation cùng trỏ `NguoiDung` → EF tưởng là quan hệ 1-1, ném *"dependent side could not be determined"* |

Cả hai chỉ lộ khi **sinh migration**, không phải lúc build.

## Lỗ hổng 3 — lỗi chỉ E2E thấy được

`Modal` **giữ children khi đóng** (nó chỉ đóng thẻ `<dialog>`), nên `defaultValue` chỉ áp dụng
đúng lần mount đầu. Hậu quả: mở sửa Bài 1 sau khi vừa soạn Bài 2 → ô nội dung hiện nội dung
**Bài 2**. Bấm Lưu là ghi đè Bài 1.

`tsc` xanh, 456 test tích hợp xanh — vì lỗi nằm ở **vòng đời component**, không ở handler.

Test đầu tôi viết cho chỗ này cũng không bắt được: nó chỉ soạn **một bài**, nên không phân biệt
nổi *"nạp đúng nội dung"* với *"giữ lại nội dung lần trước"*. Phải hai bài khác nội dung.

## Ba lần viết tên API không tồn tại

`muiGio.KhoangNgayHomNay()` (12/09) · `IThoiGian` · `PhanTrangAsync` — cả ba đều do tôi viết tên
theo cái mình **muốn có** thay vì cái đang có. Build đỏ ngay nên vô hại, nhưng đáng cảnh giác:
nếu tên đó *tình cờ* tồn tại với nghĩa khác thì build vẫn xanh.

## Kết quả

| | Đầu ngày | Cuối ngày |
|---|---|---|
| Test backend | 445 | **456** |
| Bảng DB | 38 | **42** |
| Tầng phạm vi | 3 | **4** (thêm `IPhamViKhoaOnline`) |

Elearning dùng được đầu-cuối: soạn khoá · cấp quyền học · học viên đọc bài và đánh dấu tiến độ.
Bài tập chấm điểm (FR-27) tách riêng làm cuối vì nó đụng FR-11/FR-12 đang chạy.
