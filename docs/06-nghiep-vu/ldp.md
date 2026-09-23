# LDP — Trang đích công khai (FR-30)

> **Hệ thống con thứ tư**, cạnh HRM · CRM · LMS. Quản nội dung hiển thị trên **trang giới
> thiệu công khai** của mỗi trung tâm.
>
> Liên quan: [ADR-0008](../02-kien-truc/adr/0008-nhan-dien-tenant-qua-domain.md) (giải tenant
> từ domain) · [ADR-0009](../02-kien-truc/adr/0009-tai-khoan-cap-he-thong.md) (gắn domain).

## FR-30 — Trang đích của trung tâm

### Vấn đề

Trung tâm cần một trang giới thiệu cho **khách chưa biết gì về họ**: chương trình học, đội ngũ,
cảm nhận học viên, và một chỗ để lại số điện thoại. Hiện toàn bộ hệ thống nằm sau đăng nhập —
khách vãng lai không thấy được gì.

### Về trang tham chiếu

Chủ sản phẩm đưa edulife.com.vn làm mẫu. Ta lấy **khuôn bố cục** — thứ vốn là quy ước chung của
landing giáo dục, không của riêng ai: header dính có nút gọi hành động, hero căn giữa, lưới thẻ
khoá học, băng chuyền ngang cho cảm nhận và giáo viên, dải logo đối tác, các bước đánh số, khối
cơ sở, form liên hệ cuối trang, nút gọi nổi trên di động.

**Không lấy**: chữ nghĩa, hình ảnh, logo, bảng màu hay bất kỳ phần nhận diện nào của họ. Màu
lấy từ design token của chính hệ thống này; nội dung do từng trung tâm tự nhập. Trang dựng ra
phải mang thương hiệu của trung tâm dùng nó, không phải bản sao của một trung tâm khác.

### Điều làm LDP khác mọi module đã có

Đây là lần đầu hệ thống phục vụ **người chưa đăng nhập** với dữ liệu của tenant. Ba hệ quả:

1. **Không có JWT để giải tenant** — dùng domain (ADR-0008) hoặc `/t/{mã}`.
2. **Global Query Filter có nhánh `TenantIdHienTai == null` tắt filter hoàn toàn.** Request
   không giải ra tenant sẽ **trả dữ liệu mọi trung tâm**, không phải trả rỗng. Thất bại theo
   hướng **mở**, và **im lặng**.
3. **Dữ liệu nghiệp vụ không được đi thẳng ra ngoài** — nó chứa học phí, CCCD, hồ sơ nhân sự.

### Quyết định: nội dung nhập RIÊNG, không lấy từ nghiệp vụ

Landing có bảng nội dung của chính nó. **Không** đọc `KHOA_HOC`, `HO_SO_GIAO_VIEN`, `LOP_HOC`.

Vì sao, dù nhập hai lần phiền hơn:

- Nối thẳng dữ liệu nghiệp vụ ra trang công khai là mở một đường rò rỉ **vĩnh viễn**: mỗi lần
  ai đó thêm cột vào `KHOA_HOC`, cột đó có nguy cơ xuất hiện trên Internet mà không ai rà.
- Bài học 07/09/2026 (rò rỉ học phí): trường nhạy cảm đi nhờ DTO của module khác thì **lọt qua
  mọi tầng phân quyền**, vì các tầng đó lọc **hàng**, không lọc **cột**.
- Nội dung marketing vốn khác nội dung vận hành: tên khoá trên landing là "IELTS cấp tốc 3
  tháng — cam kết 6.5", còn trong CRM là "IELTS 6.5 K1".

Admin **chủ động chọn** đưa gì ra ngoài. Đó là điểm chính.

### Cấu trúc: khối cố định, nội dung sửa được

Bố cục do mã quyết định; admin sửa **nội dung**, **bật/tắt**, và **sắp thứ tự**. Mười khối,
sinh sẵn khi trung tâm mở màn soạn lần đầu.

| Khối | Nội dung | Kiểu |
|---|---|---|
| `Hero` | Tiêu đề lớn, mô tả, ảnh nền, nút gọi hành động | Một bản ghi |
| `GioiThieu` | Đoạn văn + ảnh | Một bản ghi |
| `KhoaHoc` | Tên · mô tả · ảnh · giá niêm yết (tuỳ chọn) | Nhiều mục |
| `GiaoVien` | Tên · chức danh · ảnh · giới thiệu ngắn | Nhiều mục |
| `CamNhan` | Tên người học · nội dung · ảnh | Nhiều mục |
| `TinTuc` | Tiêu đề · tóm tắt · ảnh · ngày | Nhiều mục |
| `QuyTrinh` | Các bước đăng ký, đánh số theo thứ tự | Nhiều mục |
| `DoiTac` | Logo đơn vị đối tác / công nhận | Nhiều mục (chỉ ảnh) |
| `CoSo` | Cơ sở: ảnh · tên · địa chỉ · giờ mở cửa | Nhiều mục |
| `LienHe` | Địa chỉ · hotline · email · form đăng ký tư vấn | Một bản ghi |

Không làm **page builder tự do** (kéo thả khối bất kỳ): khối lượng lớn gấp nhiều lần, và kết
quả thường là trang xấu — chủ sản phẩm đã cân nhắc và bỏ phương án đó.

### Form đăng ký tư vấn → CRM

Khách điền form ⇒ lưu vào `LIEN_HE_LANDING`, rồi người phụ trách bấm **"Chuyển sang CRM"** để
sinh `KHACH_HANG` với `Nguon = TuLanding`.

> **Vì sao có bước trung gian** (điều chỉnh so với chốt ban đầu "ghi thẳng vào CRM"): form là
> endpoint ẩn danh nên nó sẽ nhận cả bot và rác. Ghi thẳng thì danh sách khách hàng thật —
> thứ đội kinh doanh làm việc hàng ngày — bị loãng bởi rác, và không có cách nào lọc ngược.
>
> Bước chuyển chỉ là **một nút bấm**, và bản ghi gốc giữ lại làm dấu vết "khách này đến từ
> đâu". Nếu sau một thời gian thấy rác không đáng kể, bỏ bước này chỉ là xoá một nút.

Đây là **endpoint ẩn danh GHI dữ liệu** — loại nguy hiểm nhất, và trước LDP cả hệ thống mới có
đúng một cái (`/dang-ky-trung-tam`, nay đã đóng). Bảo vệ:

| Lớp | Việc |
|---|---|
| Rate limit | Theo IP, tầng ứng dụng **và** tầng nginx |
| Honeypot | Trường ẩn — bot điền thì bỏ qua **im lặng**, không báo lỗi |
| Giới hạn độ dài | Mọi trường, chặn ở validator |
| Không trả dữ liệu | Phản hồi chỉ "đã nhận", không xác nhận số điện thoại đã tồn tại hay chưa |

`TuLanding` tách khỏi `TuDangKy` sẵn có để CRM **đo được kênh landing** — cùng nghĩa "khách tự
tìm đến, không tính doanh số cá nhân", nhưng khác nguồn.

### Hai đường vào trang công khai

| Đường | Khi nào |
|---|---|
| `vietgeneducation.edu.vn` | Trung tâm đã trỏ DNS riêng (ADR-0008) |
| `<mặc định>/t/{maTrungTam}` | Chưa trỏ domain — xem thử, và dùng ở local |

Đường thứ hai gắn `noindex`: một nội dung ở hai địa chỉ thì Google phạt trùng lặp, và ta muốn
nó lưu domain chính thức.

## Quyền

| Chức năng | Thao tác | Ai dùng |
|---|---|---|
| `TrangDich` | `Xem` · `Sua` · `XuatBan` | Người quản lý nội dung của trung tâm |
| `LienHeLanding` | `Xem` · `ChuyenCrm` · `Xoa` | Người chăm sóc khách hàng |

`ChuyenCrm` là **cầu nối LDP → CRM** — phải khai vào `RanhGioiHeThongConTests` kèm lý do, giống
cầu nối FR-21 (CRM → LMS).

`XuatBan` tách khỏi `Sua`: soạn nội dung và **quyết định cho nó lên Internet** là hai việc khác
nhau về hậu quả. Trang chưa xuất bản thì đường công khai trả 404.

## Chưa làm trong phạm vi này

- Trang chi tiết cho từng khoá học / bài viết (hiện chỉ một trang cuộn dài)
- Tự tối ưu ảnh (dựa vào ảnh admin tải lên)
- Đa ngôn ngữ cho **nội dung landing** — giao diện quản trị vẫn đủ 5 thứ tiếng, nhưng nội dung
  do trung tâm nhập thì chỉ một bản, đúng nguyên tắc i18n hiện có (dịch giao diện, không dịch
  dữ liệu người dùng nhập)
