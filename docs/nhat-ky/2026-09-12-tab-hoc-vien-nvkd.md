# 2026-09-12 (FR-08) — Tab Học viên gọn lại, và một lỗ hổng trong chính test canh kiến trúc

Hai yêu cầu của chủ sản phẩm: gọn phần thêm học viên, và hiện tên nhân viên kinh doanh. Yêu cầu
thứ hai kéo theo một phát hiện không nằm trong kế hoạch.

## Phần dễ: gom vào modal

> *"Phần thêm học viên vẫn chưa hợp lý, làm gọn gàng hơn chứ không show ngay từ đầu như vậy"*

Ô "Thêm học viên" và khối "Đang chờ xếp lớp" bày sẵn **trên** bảng. Mỗi lần chỉ muốn xem danh
sách đều phải cuộn qua hai khối không dùng tới — mà **xem** là việc làm thường xuyên, **thêm**
chỉ thỉnh thoảng. Đảo lại: trang chỉ còn sĩ số + nút + bảng.

Trong modal vẫn giữ **hai đường riêng biệt**, không gộp thành một danh sách:

| Đường | Học phí lấy từ |
|---|---|
| Duyệt người đang chờ (FR-21) | **đơn CRM** — gồm miễn giảm đã chốt với khách |
| Thêm trực tiếp | **lớp** |

Gộp lại thì người dùng không biết mình đang áp mức nào. Đó là lỗi tiền bạc, không phải bất tiện.

## Phần cần quyết: "nhân viên kinh doanh" lấy từ đâu

`KHACH_HANG` **không có cột người tạo**. Có ba nguồn khả dĩ, và chúng trả lời ba câu khác nhau:

| Nguồn | Câu hỏi nó trả lời |
|---|---|
| `KHACH_HANG.nguoi_tao_id` | Ai **tạo** hồ sơ khách — cố định |
| `LICH_SU_CHAM_SOC.nguoi_phu_trach_id` | Ai **đang chăm** — đổi theo thời gian |
| `YEU_CAU_XEP_LOP.nguoi_gui_id` | Ai **đẩy** khách sang đào tạo |

Chủ sản phẩm chọn cái đầu — đúng nghĩa "đã tạo". Đánh đổi đã nêu trước khi làm: **khách cũ sẽ
trống vĩnh viễn**, không truy ngược được. Ba khách hiện có trong DB đều `null`.

Thêm luôn cả ba vào `THUAT-NGU.md`: đây là loại nhầm lẫn sẽ tái diễn.

### Gán ở đâu quan trọng hơn gán cái gì

```csharp
kh = new Domain.Entities.KhachHang { NguoiTaoId = currentUser.UserId };
```

Gán trong **nhánh tạo mới**, không ở phần ghi trường chung bên dưới. Để chung thì **sửa hồ sơ
khách sẽ biến người sửa thành "người tạo"** — và không có gì báo, vì cả hai đều là `Guid` hợp lệ.

`UserId` chứ không `TaiKhoanId`: khoá ngoại nghiệp vụ trỏ `NGUOI_DUNG`. Lẫn hai thứ này trả rỗng
im lặng, không có lỗi biên dịch — CLAUDE.md đã ghi và tôi vẫn phải dừng lại kiểm.

## Cái bẫy đã gặp một lần, suýt mắc lại

`HocVienTrongLopDto` gác bằng `LopHoc.Xem` — quyền mà **giáo viên và học viên đều có**. Thêm tên
NVKD vào đó mà không gác riêng thì họ đọc được ai bán khách nào.

Đây **đúng** cái bẫy đã làm rò rỉ học phí 07/09/2026, và dự án đã ghi nó thành interface:

> *"Bài học: **đừng để trường tiền đi nhờ DTO của module không phải học phí.**"*
> — `IPhamViHocPhi`

Nên gác riêng bằng `KhachHang.Xem`. Học viên **không** thấy kể cả dòng của chính mình — khác học
phí (họ thấy số của mình): *ai bán mình* không phải thông tin của mình.

Test canh có **cả chiều ngược** (admin PHẢI thấy). Thiếu nó thì trả `null` cho mọi người cũng
xanh, và tính năng coi như không tồn tại.

## Phát hiện ngoài dự kiến: test canh kiến trúc có lỗ

Đọc `db.KhachHangs` từ handler LMS là **cầu nối chéo hệ thống** (ADR-0005). Tôi chạy
`RanhGioiHeThongConTests` để xem nó bắt không.

**Nó xanh.**

Lý do: test quét theo *namespace* —

```csharp
$@"(using\s+GiapTech\.LangCenter\.Application\.{khac}\b)|(\b{khac}\.[A-Z])"
```

— mà `db.KhachHangs` không có `using ...Application.Crm` nào. **Mọi `DbSet` nằm chung trong
`IAppDbContext`**, nên compiler không chặn và regex cũng không thấy.

Tức là: cầu nối chéo hệ thống thêm bằng đường `DbSet` sẽ **không bao giờ bị bắt**. Test đang canh
một nửa cửa.

Thêm `Khong_doc_thang_DbSet_cua_he_thong_khac_ngoai_cau_noi_da_khai` với danh sách `DbSet` theo
hệ thống. Đột biến (bỏ khai cầu nối) → đỏ kèm tên file và `db.KhachHangs`.

### Test chiều ngược cũng phải sửa theo

Sau khi khai cầu nối mới, test `Danh_sach_cau_noi_khong_chua_muc_da_lac_hau` **đỏ ngay**: nó quét
thực tế bằng regex namespace, không thấy cầu nối kiểu `DbSet`, nên báo mục vừa khai là "lạc hậu".

Phải dạy nó nhận cả hai kiểu. Đây là điều dễ bỏ sót: thêm một *loại* cầu nối thì **cả hai chiều**
của test đều phải biết về loại đó.

## Kiểm chứng

Migration chỉ `AddColumn` nullable — không đụng dữ liệu (quy tắc #1). Áp lên DB dev: 3 khách còn
nguyên, cột mới `null`.

Đột biến trên bản vá gác quyền: đổi `ChucNang.KhachHang` thành `ChucNang.LopHoc` → **đỏ đúng
test**. Đây là lỗi dễ mắc nhất khi copy đoạn gác quyền từ chỗ khác.

- **441 test backend xanh** (66 unit + 375 integration), build 0 warning
- E2E kiểm tay: tab gọn còn 3 cột, modal thông tin hiện "Nhân viên kinh doanh: Quản trị viên"
- `tsc -b` + `oxlint` sạch; i18n 867 khoá
- Tenant tạm dùng để đo đã xoá — DB còn đúng `W686AE9`

## Bài học

**Một test canh kiến trúc chỉ canh đúng thứ nó biết cách nhìn.** `RanhGioiHeThongConTests` bảo vệ
ranh giới suốt từ 09/09, nhưng chỉ ở tầng namespace — tầng mà `IAppDbContext` vô tình mở một cửa
sau. Không phải test sai, mà là mô hình mối đe doạ của nó thiếu một nhánh.

Cách phát hiện: **chạy test canh ngay sau khi làm điều mà nó lẽ ra phải chặn.** Nếu nó xanh mà
đáng lẽ phải đỏ, thì vừa tìm được một lỗ hổng — đáng giá hơn việc chỉ chạy test cuối kỳ.
