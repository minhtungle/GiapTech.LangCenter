# 02 — Prompt: rút danh sách chức năng cần thiết khi lấy dự án này làm base

Bài này **không** mô tả dự án. Nó cho bạn một **prompt dán được ngay** vào một phiên trợ lý AI
đang mở trong bản sao của repo, để phiên đó tự đọc mã nguồn và trả về *danh sách chức năng nào
giữ, nào bỏ, nào sửa* cho dự án mới của bạn.

Vì sao cần prompt thay vì một bảng liệt kê sẵn: danh sách chức năng **đổi theo dự án đích**.
Một hệ thống quản lý phòng khám giữ lại gần hết tầng nền và bỏ sạch LMS; một hệ thống quản lý
kho giữ ít hơn nhiều. Bảng tĩnh viết hôm nay sai ngay với dự án thứ hai.

---

## Hiểu cấu trúc trước khi dùng prompt

Dự án này chia chức năng làm **hai tầng**, và ranh giới đó chính là thứ quyết định bạn giữ gì:

| Tầng | Ở đâu trong mã | Số chức năng | Khi làm dự án khác |
|---|---|---|---|
| **Nền dùng chung** | `ChucNang.DungChung` | 10 | **Giữ gần như nguyên** |
| **Nghiệp vụ theo hệ thống con** | `ChucNang.TheoHeThong` | 32 | **Thay bằng nghiệp vụ của bạn** |

Tầng nền (`TaiKhoan`, `HoSoNguoiDung`, `PhanQuyen`, `ThietLapChung`, `Anh`,
`DoiMatKhauNguoiKhac`, `NhatKyHeThong`, `ThietLapEmail`, `MauEmail`, `PhoiTaiLieu`) không biết
gì về trung tâm ngoại ngữ. Đó là phần đáng lấy làm base.

32 chức năng còn lại chia theo bốn hệ thống con HRM · CRM · LMS · LDP. **HRM và CRM khá phổ
quát** (nhân sự, khách hàng, doanh thu) nên nhiều dự án giữ được; **LMS là nghiệp vụ riêng** của
đào tạo.

> Con số 10/32 là **tình trạng hôm nay**. Prompt dưới bắt trợ lý đọc lại `ChucNang.cs` chứ
> không tin vào con số trong tài liệu này — vì tài liệu trôi khỏi mã, đó là lý do
> `scripts/check-tai-lieu-khop-ma.py` tồn tại.

---

## Prompt

Dán toàn bộ khối dưới vào phiên trợ lý, sau khi sửa phần `## Dự án đích`.

````markdown
Bạn đang ở trong bản sao của `GiapTech.LangCenter` — hệ thống quản lý trung tâm ngoại ngữ,
multi-tenant, .NET 8 Clean Architecture + React. Tôi muốn dùng nó làm **base** cho một dự án
khác. Việc của bạn là trả về **danh sách chức năng cần thiết** cho dự án mới đó.

## Dự án đích

<!-- ⬇️ SỬA PHẦN NÀY, càng cụ thể càng ít phải hỏi lại ⬇️ -->
- **Hệ thống**: <ví dụ: quản lý chuỗi phòng khám nha khoa>
- **Nhiều khách hàng (multi-tenant) hay một tổ chức duy nhất**: <nhiều / một>
- **Vai trò người dùng**: <ví dụ: quản trị, lễ tân, bác sĩ, bệnh nhân>
- **Nghiệp vụ lõi, 3–7 gạch đầu dòng**: <ví dụ: đặt lịch khám · hồ sơ bệnh án · kê đơn · thu phí>
- **Có bán hàng / theo dõi doanh thu không**: <có / không>
- **Có quản lý nhân sự nội bộ không**: <có / không>
- **Có trang công khai cho khách xem không**: <có / không>
<!-- ⬆️ SỬA PHẦN NÀY ⬆️ -->

## Cách làm — đọc mã, đừng đoán

Đây là yêu cầu quan trọng nhất. **Mọi kết luận phải dẫn được về một dòng mã cụ thể.** Tài liệu
trong `docs/` có thể đã lạc hậu so với mã; khi hai bên mâu thuẫn thì **mã thắng**, và bạn ghi
chú chỗ lệch đó ra.

Đọc theo thứ tự:

1. `src/GiapTech.LangCenter.Domain/Common/ChucNang.cs` — **danh mục chức năng đóng**, nguồn
   chân lý về "hệ thống này có những gì". Lấy ra:
   - `ChucNang.DungChung` → tầng nền
   - `ChucNang.TheoHeThong` → nghiệp vụ, kèm hệ thống con của từng cái
   - bảng `ThaoTacTheoChucNang` (private, đọc thẳng trong file) → **thao tác thật** của mỗi
     chức năng (`Duyet`, `ThuTien`, `Chot`…), không phải bốn ô CRUD mặc định. Chức năng không
     có trong bảng này thì mặc định là CRUD. Đây là chỗ cho thấy nghiệp vụ thực sự làm gì.
   - Đọc cả **chú thích XML** của từng hằng: nhiều hằng giải thích *vì sao tách riêng*, và lý
     do đó thường áp dụng được cho dự án khác.
2. `docs/06-nghiep-vu/` — mô tả FR theo module. Dùng để hiểu *nghiệp vụ*, không dùng để đếm.
3. `src/GiapTech.LangCenter.Infrastructure/Persistence/AppDbContext.cs` — các `DbSet`, để biết
   mỗi chức năng kéo theo bảng nào.
4. `CLAUDE.md` mục 2 (**11 quy tắc**) và mục 6 (**nền tảng đã có sẵn**).
5. `docs/09-cam-nang/01-multi-tenant.md` — nếu dự án đích **không** multi-tenant, bài này nói
   rõ cái gì gỡ được và cái gì gỡ ra là hỏng.

## Kết quả cần trả về

Một file Markdown, `docs/chuc-nang-du-an-moi.md`, gồm đúng bốn phần:

### 1. Giữ nguyên — tầng nền

Bảng: `| Chức năng | Việc nó lo | File/thư mục chính | Sửa gì khi mang đi |`

Mỗi dòng một chức năng trong `DungChung`. Cột cuối nói rõ cái gì phải đổi (ví dụ: nhãn tiếng
Việt trong `i18n.ts`, hoặc mẫu email mang tên trung tâm).

### 2. Giữ có sửa — nghiệp vụ gần giống

Chức năng của HRM/CRM/LMS/LDP mà dự án đích **có thứ tương đương**. Bảng:
`| Chức năng gốc | Tương ứng ở dự án mới | Sửa gì | Độ lớn (nhỏ/vừa/lớn) |`

Ví dụ kiểu suy luận mong đợi: `KhachHang` của CRM ↔ "bệnh nhân" của phòng khám — **cùng hình
dạng** (danh sách + hồ sơ + lịch sử chăm sóc), chỉ khác tên và vài trường.

### 3. Bỏ — nghiệp vụ riêng của trung tâm ngoại ngữ

Bảng: `| Chức năng | Vì sao không dùng được | Gỡ kéo theo gì |`

Cột cuối quan trọng: gỡ một chức năng thường kéo theo **bảng DB, mã lỗi, khoá i18n, ô phân
quyền, và test**. Nêu rõ, vì bỏ sót ô phân quyền sẽ làm
`MaTranQuyenPhaiKhopThucTeTests` đỏ.

### 4. Thiếu — dự án mới cần mà base không có

Bảng: `| Chức năng cần thêm | Gần nhất trong base | Làm theo khuôn nào |`

Cột cuối chỉ ra module hiện có **giống về hình dạng** để bắt chước, kèm đường dẫn file.

## Ràng buộc khi viết

- **Dẫn nguồn.** Mỗi khẳng định về base kèm `đường/dẫn/file.cs:dòng`. Không dẫn được thì ghi
  "chưa kiểm chứng" thay vì viết chắc nịch.
- **Nói cả cái không chắc.** Chỗ nào phụ thuộc thông tin tôi chưa cung cấp thì liệt kê thành
  mục "Cần bạn xác nhận" ở cuối, tối đa 5 câu, gộp một lần — đừng hỏi lắt nhắt từng cái.
- **Đừng sửa mã.** Phiên này chỉ đọc và viết đúng một file kết quả.
- **Ước lượng công sức** bằng nhỏ/vừa/lớn, không bằng số giờ.
- Viết **tiếng Việt**, giọng như `docs/09-cam-nang/01-multi-tenant.md`: nói thẳng, nêu bẫy đã
  vấp thật, không trang trí.

## Bốn cái bẫy khi cắt bớt — kiểm riêng từng cái

Dự án này có **bảy test canh kiến trúc** (xem `CLAUDE.md` mục 7). Gỡ chức năng sai cách sẽ làm
chúng đỏ. Trong phần 3, với mỗi chức năng bỏ đi, nói rõ:

1. **Ô phân quyền chết** — quyền khai trong `ChucNang` mà không endpoint nào dùng →
   `MaTranQuyenPhaiKhopThucTeTests` đỏ. Bỏ chức năng thì bỏ cả hằng.
2. **Entity mất Query Filter** — `CachLyTenantTests` hỏi **chiều ngược**: entity không bị lọc
   phải nằm trong danh sách ngoại lệ *có khai lý do*. Thêm entity mới mà quên là đỏ.
3. **Mã lỗi và khoá i18n mồ côi** — `MaLoi.cs` và `i18n.ts` phải đi cùng nhau; thiếu bản dịch
   **không** làm build đỏ, người dùng thấy thẳng chuỗi mã lỗi trên màn hình.
4. **Cầu nối giữa hệ thống con** — `RanhGioiHeThongConTests` chốt rằng HRM/CRM/LMS không gọi
   chéo nhau ngoài **đúng một** cầu nối đã khai (FR-21 xếp lớp). Bỏ một đầu của cầu nối mà
   quên đầu kia là đỏ.
````

---

## Sau khi có kết quả

Đọc phần 3 trước, không phải phần 1. Phần 1 (giữ nguyên) gần như luôn đúng; **phần 3 là chỗ
trợ lý dễ sai nhất** — nó hay đề nghị bỏ thứ mà tầng nền đang dựa vào.

Hai câu kiểm nhanh cho mỗi dòng ở phần 3:

- Chức năng này có nằm trong `ChucNang.DungChung` không? Nếu có mà bị xếp vào "bỏ" thì sai.
- Gỡ nó đi thì `python3 scripts/check-nhan-phan-quyen.py` và
  `python3 scripts/check-tai-lieu-khop-ma.py` còn xanh không? Hai script này chạy mất chưa tới
  nửa giây, chạy thử rẻ hơn tranh luận.

## Vì sao prompt bắt đọc `ChucNang.cs` đầu tiên

Vì nó là **danh mục đóng**: người dùng chọn quyền từ danh sách này, không tự gõ chuỗi. Hệ quả
là mọi chức năng thật của hệ thống **bắt buộc** phải có một hằng ở đó — không có đường vòng.
Đếm màn hình, đếm controller hay đếm bảng DB đều cho con số lệch; đếm hằng trong `ChucNang.cs`
thì không.

Đó cũng là một ý tưởng đáng mang đi: **một danh mục đóng, đặt ở `Domain`, là nơi duy nhất trả
lời câu "hệ thống này có những chức năng gì"**.
