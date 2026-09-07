# 08/09/2026 — Chia thành ba hệ thống con HRM · CRM · LMS

Yêu cầu: hệ thống lớn là ba hệ thống nhỏ chia theo nhóm quyền; màn phân quyền hiện quyền theo
từng hệ thống; tài khoản có đủ ba quyền thì có bộ chuyển đặt gần Đăng xuất, và **sidebar chỉ
hiện quyền của hệ thống đang chọn, không gộp tất cả**.

## Quyết định đầu tiên: KHÔNG tách service

Tôi đã hỏi lại vì "ba hệ thống" đọc được theo hai nghĩa rất khác nhau. Chốt: **một ứng dụng,
chia theo nhóm quyền**. Vẫn một API, một database, một lần đăng nhập.

Lý do tách service chưa đáng làm bây giờ: mỗi hệ thống hiện có 1–2 module, mà dữ liệu dùng
chung (`NGUOI_DUNG`, `TENANT`, nhóm quyền) sẽ phải đồng bộ giữa ba database — đúng cái bẫy hai
nguồn sự thật vừa tránh khi tách người dùng khỏi tài khoản hôm qua. Nếu sau này thật cần tách,
`ChucNang.HeThongCua()` đã là đường biên sẵn có để cắt theo.

## Chỗ khó nhất: nhóm chức năng dùng chung

`TaiKhoan`, `PhanQuyen`, `ThietLapChung`, `Anh`, `DoiMatKhauNguoiKhac`, `NhatKyHeThong` **không
thuộc hệ thống nào**. Ép vào một hệ thống sai theo cả hai hướng:

- Cho vào LMS → trưởng phòng nhân sự phải sang LMS mới sửa được tài khoản.
- Nhân bản mỗi hệ thống một bản → ba quyền cho cùng một việc, admin phải tích ba lần.

Nên có nhóm thứ tư: `DungChung`, hiện ở sidebar và ở **mọi tab** phân quyền.

Kéo theo một khẳng định dễ làm sai, và tôi đã viết test riêng cho nó: **quyền dùng chung không
mở lối vào hệ thống nào.** Nếu `/toi/he-thong` tính cả nhóm này thì người chỉ quản trị tài
khoản sẽ "vào được" cả ba hệ thống — mà cả ba đều chỉ hiện đúng cụm Quản trị, ba lối vào giống
hệt nhau, bộ chuyển thành vô nghĩa. Đã thử phá code (thêm `.Concat(DungChung)`) để chắc test đỏ.

## Nhóm ở backend, không ở frontend

`/quyen/danh-muc` trả sẵn `heThongs`. Cách rẻ hơn là để frontend khai một object
`{ Hrm: [...], Lms: [...] }` — nhưng khi đó có **hai bản đồ ở hai nơi**, và thêm module mới thì
phải sửa cả hai mới thấy nó xuất hiện. Sửa một chỗ, quên chỗ kia, không có gì đỏ.

Cũng vì vậy **không lưu vào DB**: hệ thống của một chức năng là thuộc tính của mã nguồn
(`LopHoc` thuộc LMS là bất biến), không phải dữ liệu tenant sửa được. Lưu xuống DB thì mỗi
trung tâm nhóm một kiểu và sidebar hết xác định.

## Hai cái bẫy mất dữ liệu đã tránh

**Tab phân quyền không được có state riêng.** `oDaChon` là **một tập duy nhất** cho cả ba hệ
thống, tab chỉ lọc *dòng hiển thị*. Nếu mỗi tab giữ state riêng thì chuyển tab rồi bấm Lưu sẽ
âm thầm xoá quyền của hệ thống khác — đúng loại lỗi quy tắc #1 cấm, và là lỗi 16/08 (form thiếu
ô địa chỉ) mặc áo mới.

**Lựa chọn hệ thống đã lưu chỉ dùng khi CÒN quyền.** Admin thu quyền HRM của một người thì lần
vào sau họ phải rơi về hệ thống khác. Đọc thẳng `localStorage` ở nơi dùng thì họ kẹt ở sidebar
trống mà không hiểu vì sao.

## Test canh việc khai đủ

`NhomHeThongTests` buộc mọi chức năng **hoặc** thuộc một hệ thống **hoặc** nằm trong
`DungChung` — cùng tinh thần với `CachLyTenantTests` hỏi chiều ngược về entity không bị lọc.
`HeThongCua` trả `null` (thành "dùng chung") thay vì ném lỗi cho hằng chưa khai: hiện quá nhiều
thì dễ thấy, còn ném lỗi sẽ làm sập màn phân quyền của mọi tenant. Đã thử thêm một hằng quên
khai để chắc test đỏ và **gọi tên đúng hằng đó**.

## Tenant đã tồn tại

Ba chức năng mới không tự về nhóm "Quản trị viên" của trung tâm cũ — seeder chỉ chạy lúc tạo
tenant. Nhưng `BoKhuyetQuyenQuanTri` đã có sẵn từ giai đoạn 0 và chạy lúc khởi động, nên chỉ
cần khởi động lại API. Kiểm thật: admin của tenant `W686AE9` (tạo hôm qua) vào được cả ba hệ
thống mà không phải làm gì bằng tay.

## Kiểm chứng

- `dotnet build` 0 warning · **297 test xanh** (61 unit + 236 integration), 18 test mới.
- `check-i18n-keys.py` xanh (554 khoá) — script viết chiều qua, lần này dùng ngay.
- Kiểm tay: `admin` → `[Hrm, Crm, Lms]`; `co.lan`/`tg.hoa`/`hv1` → `[Lms]` (không có bộ chuyển);
  tạo `ns.mai` chỉ HRM+CRM → `[Hrm, Crm]`, thấy cụm Quản trị nhưng **không** thấy module LMS.
- Danh mục: HRM 2 · CRM 1 · LMS 11 · Dùng chung 6 = 20 chức năng, khớp tổng.

## Còn lại

Ba module mới là **khung trống** — phân quyền, bộ chuyển, sidebar chạy thật, nghiệp vụ chưa có.
Khi làm nghiệp vụ cần chốt trước: doanh thu tính từ đâu (`KHOAN_THU_HOC_PHI` hay có nguồn khác),
nhân viên kinh doanh theo dõi gì (chỉ tiêu, hoa hồng, khách phụ trách), và giáo viên ở HRM cần
thêm trường gì so với `HO_SO_GIAO_VIEN` đang có (hợp đồng, lương, chấm công).

Đã chốt: giáo viên ở HRM và ở LMS là **cùng một con người** — vẫn `NGUOI_DUNG` +
`HO_SO_GIAO_VIEN`, chỉ khác quyền và khác màn hình. Không tạo bảng nhân sự thứ hai.
