# ERD — Mô hình dữ liệu

**32 bảng**, PostgreSQL. Nội dung dưới đây khớp với schema thật (kiểm bằng
`information_schema` sau khi áp toàn bộ migration), không phải bản thiết kế trên giấy.

## Nguyên tắc bắt buộc

> Mọi bảng nghiệp vụ có cột `tenant_id` (FK → `TENANT`) và **bắt buộc** áp dụng EF Core Global
> Query Filter theo `tenant_id` đang đăng nhập. Không được quên ở bất kỳ entity mới nào.
> Cách triển khai: [../backend/multi-tenant.md](../backend/multi-tenant.md).

Áp dụng cho **cả bảng chi tiết** — xem
[Denormalize tenant_id xuống bảng con](#denormalize-tenant_id-xuống-bảng-con).

## Quy ước đặt tên

| Đối tượng | Quy ước | Ví dụ |
|---|---|---|
| Bảng | `UPPER_SNAKE_CASE`, số ít | `LOP_HOC`, `KHOAN_THU_HOC_PHI` |
| Cột | `lower_snake_case` | `giao_vien_chinh_id`, `hoc_phi_ap_dung` |
| Khoá chính | `id` (`uuid`) | |
| Khoá ngoại | `<bảng>_id` | `lop_hoc_id` |
| Index | `ix_<bảng>_<cột>` | `ix_buoi_hoc_lop_hoc_id_thu_tu` |

Sinh tự động bởi `UseSnakeCaseNamingConvention` — xem
[quy-uoc-migration.md](./quy-uoc-migration.md).

## Sơ đồ tổng quan

```mermaid
erDiagram
    TENANT ||--o{ NGUOI_DUNG : "có"
    TENANT ||--o{ TAI_KHOAN : "có"
    TENANT ||--o{ QUYEN : "có"
    TENANT ||--o{ LOP_HOC : "có"
    TENANT ||--o{ TAI_LIEU : "có"

    NGUOI_DUNG ||--o| TAI_KHOAN : "đăng nhập 0..1"
    NGUOI_DUNG ||--o| HO_SO_GIAO_VIEN : "hồ sơ 0..1"
    NGUOI_DUNG ||--o| HO_SO_HOC_VIEN : "hồ sơ 0..1"
    NGUOI_DUNG ||--o| HO_SO_NHAN_VIEN : "hồ sơ 0..1"

    TAI_KHOAN }o--o{ QUYEN : "NGUOIDUNG_QUYEN"
    QUYEN ||--o{ QUYEN_CHUC_NANG : "chi tiết quyền"
    TAI_KHOAN ||--o{ REFRESH_TOKEN : "phiên"
    TAI_KHOAN ||--o{ TOKEN_DATLAI_MATKHAU : "đặt lại mật khẩu"

    LOP_HOC ||--o{ LOP_HOC_HOC_VIEN : "ghi danh"
    LOP_HOC ||--o{ LOP_HOC_TRO_GIANG : "phân công"
    LOP_HOC ||--o{ BUOI_HOC : "lịch học"
    LOP_HOC ||--o{ BAI_KIEM_TRA : "bài kiểm tra"
    LOP_HOC ||--o{ TAI_LIEU_LOP_HOC : "gán tài liệu"
    LOP_HOC ||--o{ KHOAN_THU_HOC_PHI : "sổ thu"
    NGUOI_DUNG ||--o{ LOP_HOC : "dạy chính"

    BUOI_HOC ||--o{ DIEM_DANH : "điểm danh"
    BUOI_HOC ||--o{ NHAN_XET_BUOI_HOC : "học viên nhận xét"
    BUOI_HOC ||--o{ BAI_TAP : "giao bài"
    BAI_TAP ||--o{ BAI_NOP : "nộp nhiều lần"
    BAI_KIEM_TRA ||--o{ BAI_LAM : "bài làm"
    TAI_LIEU ||--o{ TAI_LIEU_LOP_HOC : "gán lớp"

    BAI_TAP ||--o{ TEP_DINH_KEM : "đính kèm"
    BAI_NOP ||--o{ TEP_DINH_KEM : "đính kèm"
    BAI_KIEM_TRA ||--o{ TEP_DINH_KEM : "đính kèm"
    BAI_LAM ||--o{ TEP_DINH_KEM : "đính kèm"
    TAI_LIEU ||--o{ TEP_DINH_KEM : "đính kèm"

    NGUOI_DUNG ||--o{ LOP_HOC_HOC_VIEN : "là học viên"
    NGUOI_DUNG ||--o{ DIEM_DANH : "được điểm danh"
    NGUOI_DUNG ||--o{ NHAN_XET_BUOI_HOC : "viết nhận xét"
    NGUOI_DUNG ||--o{ KHOAN_THU_HOC_PHI : "nộp học phí"

    KHACH_HANG ||--o{ DANG_KY_KHOA_HOC : "đăng ký"
    KHACH_HANG ||--o{ LICH_SU_CHAM_SOC : "chăm sóc"
    DANG_KY_KHOA_HOC ||--o{ THU_TIEN_DANG_KY : "thu nhiều đợt"
    KHOA_HOC ||--o{ DANG_KY_KHOA_HOC : "được đăng ký"
    SAN_PHAM ||--o{ DANG_KY_KHOA_HOC : "được mua"
    NGUOI_DUNG |o--o| KHACH_HANG : "cùng một người (0..1)"
```

## Chi tiết bảng

### Nhóm nền tảng (12 bảng)

**Người và tài khoản là hai bảng riêng** (tách 07/09/2026). `NGUOI_DUNG` là bảng "con người",
`TAI_KHOAN` là cách họ đăng nhập — xem [FR-03](../nghiep-vu/quan-tri-he-thong.md#fr-03--người-dùng-hồ-sơ-con-người).

| Bảng | Vai trò | Ghi chú |
|---|---|---|
| `TENANT` | Trung tâm | `ma_trung_tam` 7 ký tự, duy nhất **toàn hệ thống**. `mui_gio` (mặc định `Asia/Ho_Chi_Minh`), `so_ngay_canh_bao_no_hoc_phi` (mặc định 14) |
| `NGUOI_DUNG` | **Con người** | `ho_ten` bắt buộc, `loai_nguoi_dung` **chỉ để lọc và chọn hồ sơ**, không dùng phân quyền. `trang_thai_nhan_su` = còn thuộc trung tâm không. **12 khoá ngoại nghiệp vụ trỏ vào đây** |
| `TAI_KHOAN` | **Đăng nhập** | `username`, `password_hash`, `phai_doi_mat_khau`, `trang_thai` = còn đăng nhập được không. `nguoi_dung_id` nullable (tài khoản kỹ thuật) |
| `HO_SO_GIAO_VIEN` | Hồ sơ người dạy | Bằng cấp, chuyên môn, ngày vào làm. **Trợ giảng dùng chung** |
| `HO_SO_HOC_VIEN` | Hồ sơ người học | Trường/lớp, tên và SĐT phụ huynh — trung tâm dạy trẻ em cần gọi được cho phụ huynh |
| `HO_SO_NHAN_VIEN` | Hồ sơ vận hành | Chức vụ, phòng ban |
| `QUYEN` | Nhóm quyền | Seeder tạo sẵn 4 nhóm: Quản trị viên / Giáo viên / Trợ giảng / Học viên |
| `QUYEN_CHUC_NANG` | Chi tiết quyền | `(quyen_id, ten_chuc_nang, hanh_dong)` — nguồn của phân quyền động |
| `NGUOIDUNG_QUYEN` | Gán nhóm quyền | Nhiều–nhiều, gán cho **TÀI KHOẢN** (`tai_khoan_id`) chứ không cho người |
| `REFRESH_TOKEN` | Phiên đăng nhập | Xoay vòng, phát hiện tái sử dụng |
| `TOKEN_DATLAI_MATKHAU` | Quên mật khẩu | Hash, hạn 30 phút, dùng một lần |
| `NHAT_KY_HE_THONG` | **Nhật ký thao tác** (FR-16) | Một bản ghi cho mỗi LỆNH, không phải mỗi dòng dữ liệu. Chỉ ghi thêm — không sửa, không xoá. `username`/`ho_ten` lưu **bản chụp** để đọc được cả khi tài khoản đã xoá |

**Hai cột trạng thái, đừng nhầm:**

| Cột | Câu hỏi | Ảnh hưởng |
|---|---|---|
| `NGUOI_DUNG.trang_thai_nhan_su` | Còn làm ở trung tâm không? | Chặn phân công vào lớp **mới** |
| `TAI_KHOAN.trang_thai` | Còn đăng nhập được không? | Chặn đăng nhập, **không đụng dữ liệu** |

Trước 07/09/2026 một cột gánh cả hai, nên vô hiệu hoá tài khoản một giáo viên đã nghỉ thì
không phân công được họ vào lớp cũ nữa.

### Nhóm lớp học (3 bảng)

| Bảng | Cột đáng chú ý |
|---|---|
| `LOP_HOC` | `giao_vien_chinh_id` NOT NULL (đúng 1 người), `hoc_phi` nullable (`null` = chưa nhập ≠ `0` = miễn phí), `suc_chua_toi_da` nullable = không giới hạn, `nhan_ban_tu_lop_id` (dấu vết nguồn gốc), `trang_thai` |
| `LOP_HOC_HOC_VIEN` | `ngay_vao_lop`, `ngay_roi_lop`, `trang_thai`, **`hoc_phi_ap_dung`** — snapshot lúc ghi danh, cho phép miễn giảm từng người |
| `LOP_HOC_TRO_GIANG` | Bảng **riêng**, không gộp với học viên bằng cột `vai_tro`: gộp thì nửa số cột luôn NULL và mọi query học viên phải nhớ `WHERE vai_tro = 1` |

### Nhóm buổi học & điểm danh (3 bảng)

| Bảng | Cột đáng chú ý |
|---|---|
| `BUOI_HOC` | `bat_dau`, `ket_thuc` là `timestamptz` — **không có cột `ngay_hoc`**: cột ngày tách rời sẽ lệch khi trung tâm đổi múi giờ. `giao_vien_id` nullable = dùng giáo viên của lớp. `la_hoc_bu` |
| `DIEM_DANH` | **Hai cột trạng thái**: `trang_thai_tu_khai` (nullable — học viên tự khai; null ≠ Vắng) và `trang_thai_chinh_thuc` (NOT NULL — **nguồn sự thật duy nhất cho mọi báo cáo**). Gộp một cột là mất vĩnh viễn thông tin học viên đã khai gì trước khi giáo viên ghi đè. **`nhan_xet`** — nhận xét của giáo viên về học viên NÀY trong buổi NÀY (khác `ly_do_vang`: lý do nói vì sao không có mặt, nhận xét nói về việc học) |
| `NHAN_XET_BUOI_HOC` | Học viên nhận xét về **buổi** (chiều ngược của `DIEM_DANH.nhan_xet`). `muc_hai_long` 1–5 **nullable** — không ép cho điểm mới gửi được góp ý. Bảng riêng chứ không thêm cột vào `DIEM_DANH` vì **quyền khác nhau** (học viên ghi ở đây nhưng không được đụng `DIEM_DANH`) và **vòng đời khác nhau** (học viên vắng vẫn nhận xét được) |

### Nhóm học liệu (6 bảng)

| Bảng | Cột đáng chú ý |
|---|---|
| `BAI_TAP` | Gắn vào `buoi_hoc_id` |
| `BAI_NOP` | **`lan_nop`** — nộp nhiều lần, giữ lịch sử; bài mới nhất là `MAX(lan_nop)` |
| `BAI_KIEM_TRA` | Gắn vào `lop_hoc_id`. `loai` giữ sẵn `TracNghiemOnline` cho tương lai, hiện chỉ nộp file |
| `BAI_LAM` | `han_nop_rieng` nullable = gia hạn riêng; hạn hiệu lực = `han_nop_rieng ?? bai_kiem_tra.dong_luc` |
| `TAI_LIEU` + `TAI_LIEU_LOP_HOC` | **Không có hàng nào** trong bảng gán = tài liệu chung toàn trung tâm |
| `TEP_DINH_KEM` | Một bảng dùng chung, **5 cột FK nullable loại trừ nhau**, ép bằng `CHECK` |

### Nhóm CRM (6 bảng)

| Bảng | Cột đáng chú ý |
|---|---|
| `KHACH_HANG` | Người **quan tâm**, chưa chắc thành học viên. `link_facebook` (kênh liên hệ chính), `nguoi_dung_id` **nullable** = nối tới hồ sơ học viên khi họ thật sự vào học. Bảng riêng chứ không dùng `NGUOI_DUNG`: nhồi vào đó thì danh sách học viên bên LMS lẫn người chưa học |
| `KHOA_HOC` | **Sản phẩm** bán ra: tên, giá + `don_vi_tien`, `so_buoi` niêm yết, `dang_ban`. Khác `LOP_HOC` (một **lần mở** có giáo viên và lịch) — gộp thì không bán được trước khi mở lớp |
| `DANG_KY_KHOA_HOC` | **Đơn hàng** — tên bảng giữ nguyên dù nay chứa cả sản phẩm (đổi tên bảng có dữ liệu là việc rủi ro). **Hai FK nullable loại trừ nhau** `khoa_hoc_id`/`san_pham_id` + `CHECK` đúng một cột khác NULL. `so_luong` (khoá luôn 1). **Ba cột tiền**: `gia_goc` (snapshot giá niêm yết lúc đăng ký), `so_tien` (**CAM KẾT** khách trả), `ty_gia_ve_vnd` (chụp lúc đăng ký). % trên giá gốc và quy đổi VND **tính động, không lưu cột** |
| `SAN_PHAM` | Vật phẩm bán kèm: sách, học cụ. `don_vi_tinh` ("quyển", "bộ") chỉ để đọc. Bảng RIÊNG chứ không gộp `KHOA_HOC` kèm cột `loai`: gộp thì `so_buoi` luôn NULL cho sách và mọi query khoá phải nhớ `WHERE loai` |
| `LICH_SU_CHAM_SOC` | Từng lần liên hệ: `thoi_diem`, `hinh_thuc`, `noi_dung`, `nguoi_phu_trach_id` (từ token), **`trang_thai_sau`**. Trạng thái phễu hiện tại = `trang_thai_sau` của dòng **mới nhất** — không lưu cột trên `KHACH_HANG` để hai chỗ không lệch nhau |
| `THU_TIEN_DANG_KY` | Tiền **thật đã nhận** cho một đăng ký, khách đóng nhiều đợt. Cùng đơn vị tiền với đăng ký. Còn thiếu = cam kết − tổng thu, **tính động** |

### Nhóm học phí (1 bảng)

| Bảng | Cột đáng chú ý |
|---|---|
| `KHOAN_THU_HOC_PHI` | `so_tien numeric(18,2)` + `CHECK (so_tien > 0)`, `phuong_thuc`, `so_phieu` (đối chiếu phiếu giấy), `nguoi_thu_id`. **Không có cột `da_thu` ở đâu cả** — công nợ tính động bằng `SUM` |

## Ràng buộc nghiệp vụ quan trọng

| Ràng buộc | Bảng | Lý do |
|---|---|---|
| `UNIQUE(ma_trung_tam)` | `TENANT` | Mã 7 ký tự sinh tự động, duy nhất **toàn hệ thống** — xem [FR-01](../nghiep-vu/dang-nhap.md#mã-đội) |
| `UNIQUE(tenant_id, username)` | `TAI_KHOAN` | Username duy nhất **trong phạm vi trung tâm**; hai trung tâm đều có thể có `admin` |
| `UNIQUE(nguoi_dung_id) WHERE NOT NULL` | `TAI_KHOAN` | Một người tối đa một tài khoản — hai tài khoản cùng người thì không biết quyền nào thắng |
| `UNIQUE(nguoi_dung_id)` | `HO_SO_*` | Quan hệ 1–1 với người |
| `UNIQUE(tenant_id, ten) WHERE trang_thai <> 0` | `LOP_HOC` | Tên lớp duy nhất trong trung tâm, **nhưng lớp nháp không chiếm tên** — nháp bỏ ngang không được chặn người khác ba tháng sau |
| `UNIQUE(lop_hoc_id, hoc_vien_id)` | `LOP_HOC_HOC_VIEN` | Ghi danh một lần |
| `UNIQUE(lop_hoc_id, tro_giang_id)` | `LOP_HOC_TRO_GIANG` | Phân công một lần |
| `UNIQUE(lop_hoc_id, thu_tu)` | `BUOI_HOC` | Số thứ tự buổi không trùng trong lớp |
| `UNIQUE(buoi_hoc_id, hoc_vien_id)` | `DIEM_DANH` | Mỗi học viên một dòng điểm danh/buổi — chặn ở **tầng DB**, không chỉ ở UI |
| `UNIQUE(buoi_hoc_id, hoc_vien_id)` | `NHAN_XET_BUOI_HOC` | Mỗi học viên một nhận xét/buổi. Gửi lần hai là **sửa**, không tạo bản mới |
| `UNIQUE(tenant_id, so_dien_thoai)` **partial** | `KHACH_HANG` | Chặn hai người bán nhập cùng một khách. Lọc `IS NOT NULL AND <> ''` — khách chỉ để lại Facebook thì không có số, UNIQUE thường sẽ chặn oan người thứ hai |
| `UNIQUE(tenant_id, ten)` | `KHOA_HOC` | Hai khoá cùng tên thì người bán chọn sai |
| `UNIQUE(tenant_id, ten)` | `SAN_PHAM` | Cùng lý do |
| `CHECK` đúng một mặt hàng | `DANG_KY_KHOA_HOC` | `(khoa_hoc_id NOT NULL AND san_pham_id NULL) OR (ngược lại)` — đơn không có mặt hàng, hoặc có cả hai, là dữ liệu mà mọi báo cáo phải tự đoán cách xử lý |
| `CHECK(so_tien > 0)` | `THU_TIEN_DANG_KY` | Thu 0 đồng là dòng rác; thu âm thì dùng chức năng hoàn tiền (chưa có) |
| `UNIQUE(bai_tap_id, hoc_vien_id, lan_nop)` | `BAI_NOP` | Nộp nhiều lần nhưng không trùng số lần |
| `UNIQUE(bai_kiem_tra_id, hoc_vien_id)` | `BAI_LAM` | Bài kiểm tra làm một lần (khác bài tập) |
| `UNIQUE(tai_lieu_id, lop_hoc_id)` | `TAI_LIEU_LOP_HOC` | Gán một lần |
| `CHECK` đúng một FK khác null | `TEP_DINH_KEM` | `ck_tep_dinh_kem_dung_mot_chu` — không dựa vào validate ở handler |
| `CHECK (so_tien > 0)` | `KHOAN_THU_HOC_PHI` | `ck_khoan_thu_so_tien_duong`. Số âm làm mọi báo cáo tổng thu sai mà không ai nhìn ra |

Các UNIQUE trên bảng con **không kèm `tenant_id`**: cột đầu đã là FK về bảng đã mang tenant.

## Hành vi xoá — Restrict ở đâu và vì sao

| Quan hệ | Delete | Lý do |
|---|---|---|
| `LOP_HOC → NGUOI_DUNG` (giáo viên chính) | Restrict | Xoá giáo viên đang dạy phải bị chặn, buộc bàn giao lớp trước |
| `BUOI_HOC → LOP_HOC` | Restrict | Cascade sẽ cuốn sạch lịch sử chuyên cần khi xoá lớp |
| `DIEM_DANH → BUOI_HOC` | Restrict | Điểm danh là bằng chứng |
| `NHAN_XET_BUOI_HOC → BUOI_HOC` | Restrict | Nhận xét là ý kiến đã phát biểu — xoá buổi không được cuốn nó đi. Buổi có nhận xét thì **huỷ**, không xoá |
| `NHAN_XET_BUOI_HOC → NGUOI_DUNG` | Restrict | Cùng lý do; đồng thời chặn xoá học viên còn để lại phản hồi |
| `KHOAN_THU_HOC_PHI → NGUOI_DUNG` / `→ LOP_HOC` | Restrict | Dữ liệu tiền không được biến mất theo tài khoản hay theo lớp |
| `DANG_KY_KHOA_HOC → SAN_PHAM` | Restrict | Đơn cũ phải giữ được tên sản phẩm đã bán; không dùng nữa thì **ngừng bán** |
| `DANG_KY_KHOA_HOC → KHACH_HANG` / `→ KHOA_HOC` | Restrict | Cùng lý do — và đơn hàng cũ phải giữ được tên khoá đã bán. Khoá không dùng nữa thì **ngừng bán**, không xoá |
| `LICH_SU_CHAM_SOC → KHACH_HANG` | **Cascade** | Lịch sử chăm sóc thuộc HẲN về khách, không có nghĩa độc lập. Khác đăng ký (Restrict — dữ liệu tiền), nên xoá khách vẫn bị chặn nếu họ đã mua |
| `THU_TIEN_DANG_KY → DANG_KY_KHOA_HOC` | Restrict | Dữ liệu tiền: muốn xoá đăng ký thì phải xoá các lần thu trước, một cách có ý thức |
| `KHACH_HANG → NGUOI_DUNG` | SetNull | Chỉ là mối nối "cùng một người", không phải phụ thuộc: xoá hồ sơ học viên không được cuốn theo dữ liệu khách hàng và đơn hàng |
| `DIEM_DANH → NGUOI_DUNG` (người xác nhận) | SetNull | Chỉ là dấu vết; Restrict sẽ khoá cứng mọi tài khoản giáo viên vĩnh viễn |
| `KHOAN_THU_HOC_PHI → NGUOI_DUNG` (người thu) | SetNull | Cùng lý do |
| `LOP_HOC → LOP_HOC` (nhân bản từ) | SetNull | Chỉ là dấu vết nguồn gốc; bản sao là lớp thật đang chạy |
| `TEP_DINH_KEM → *` | Cascade | Tệp đi theo nội dung chứa nó |
| `NHAT_KY_HE_THONG → NGUOI_DUNG` | SetNull | Nhật ký sống lâu hơn người dùng; Restrict sẽ khoá cứng mọi tài khoản vĩnh viễn vì ai cũng có vết |
| `TAI_KHOAN → NGUOI_DUNG` | SetNull | Xoá người để lại tài khoản mồ côi chứ không xoá kèm — còn dấu vết ai từng đăng nhập |
| `HO_SO_* → NGUOI_DUNG` | Cascade | Hồ sơ là một phần của người, vô nghĩa khi đứng riêng |
| `NGUOIDUNG_QUYEN`, `REFRESH_TOKEN`, `TOKEN_DATLAI_MATKHAU → TAI_KHOAN` | Cascade | Quyền và phiên chết cùng tài khoản |

Vì Restrict chồng Restrict, UI đưa **"Huỷ lớp"** làm hành động mặc định; xoá cứng chỉ cho lớp
nháp chưa có buổi học.

## Denormalize tenant_id xuống bảng con

Mọi bảng chi tiết (`QUYEN_CHUC_NANG`, `NGUOIDUNG_QUYEN`, `HO_SO_GIAO_VIEN`,
`HO_SO_HOC_VIEN`, `HO_SO_NHAN_VIEN`, `LOP_HOC_HOC_VIEN`,
`LOP_HOC_TRO_GIANG`, `BUOI_HOC`, `DIEM_DANH`, `NHAN_XET_BUOI_HOC`, `BAI_TAP`, `BAI_NOP`, `BAI_LAM`,
`KHACH_HANG`, `KHOA_HOC`, `SAN_PHAM`, `DANG_KY_KHOA_HOC`, `LICH_SU_CHAM_SOC`,
`THU_TIEN_DANG_KY`,
`TAI_LIEU_LOP_HOC`, `TEP_DINH_KEM`, `KHOAN_THU_HOC_PHI`) **mang cột `tenant_id` riêng** thay vì
chỉ kế thừa phạm vi qua bảng cha.

**Vì sao:** Global Query Filter chỉ áp cho entity có `TenantId`. Nếu bảng con không có, mọi truy
vấn trực tiếp — đếm buổi vắng, cộng dồn học phí, kiểm tra quyền — đều phải nhớ join lên bảng cha.
Quên một lần là rò rỉ dữ liệu chéo trung tâm, đúng lỗi nghiêm trọng nhất hệ thống này có thể mắc.

**Đánh đổi đã chấp nhận:** thừa một cột `uuid` mỗi bảng, và cần giữ `tenant_id` của bản ghi con
nhất quán với cha. Việc gán đã tự động hoá trong `AppDbContext.SaveChanges` nên tầng Application
không phải nhớ; `CachLyTenantTests` canh mọi bảng đều có cột và có filter.

## Chỗ ERD không bảo vệ được

**Kho tệp MinIO không có Query Filter.** Cách ly tenant ở đó dựa hoàn toàn vào quy ước khoá
`{tenantId}/{loai}/{guid}{ext}` và việc `TaiVe`/`Xoa` kiểm lại tiền tố. Xem
[multi-tenant.md](../backend/multi-tenant.md).

## Tham chiếu

- Nghiệp vụ dùng các bảng này: [../nghiep-vu/](../nghiep-vu/README.md)
- Cách áp Global Query Filter: [../backend/multi-tenant.md](../backend/multi-tenant.md)
- Quy ước migration: [quy-uoc-migration.md](./quy-uoc-migration.md)
