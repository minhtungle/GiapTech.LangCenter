# ADR-0005: Một source cho cả ba hệ thống con, đổi tên về `GiapTech.LangCenter`

- **Ngày:** 09/09/2026
- **Trạng thái:** Đã chốt
- **Bối cảnh liên quan:** [ADR-0001](./0001-lua-chon-cong-nghe.md) (Clean Architecture + CQRS),
  [ADR-0004](./0004-ha-tang-tu-host-vps.md) (1 VPS)

## Bối cảnh

Dự án khởi đầu là LMS (đào tạo), nay có thêm CRM (FR-17 → FR-21) và HRM (FR-22 → FR-24). Nảy ra
hai câu hỏi cùng lúc:

1. Có nên tách thành **ba source riêng** theo HRM · CRM · LMS?
2. Tên `GiapTech.LangCenter.LMS` còn đúng khi hệ thống không chỉ là LMS?

## Quyết định

**Một source duy nhất**, và **đổi tên về `GiapTech.LangCenter`** (bỏ hậu tố `.LMS`).

Ba hệ thống con vẫn là **cách nhóm chức năng phân quyền** để lọc sidebar — `ChucNang.HeThongCua()`
là nguồn chân lý — **không phải ba ứng dụng**.

## Vì sao KHÔNG tách ba source

Đo trên chính codebase ngày 09/09/2026:

### 1. FR-21 là giao dịch cắt ngang CRM ↔ LMS

`DuyetVaoLopHandler` ghi **cả hai phía trong một `SaveChanges`**: thêm `LOP_HOC_HOC_VIEN` (LMS) và
đóng `YEU_CAU_XEP_LOP` (CRM). Tách ra là mất ACID — phải xây saga/outbox, và trạng thái trung gian
(học viên đã vào lớp nhưng yêu cầu chưa đóng) làm danh sách chờ và danh sách lớp nói hai chuyện.

### 2. `NGUOI_DUNG` bị 17 bảng của cả ba hệ thống trỏ vào

| Hệ thống | Bảng trỏ vào `NGUOI_DUNG` |
|---|---|
| LMS | `LOP_HOC`, `LOP_HOC_HOC_VIEN`, `BUOI_HOC`, `DIEM_DANH`, `BAI_TAP`, `BAI_NOP`, `BAI_LAM`, `BAI_KIEM_TRA`, `NHAN_XET_BUOI_HOC`, `KHOAN_THU_HOC_PHI` |
| CRM | `KHACH_HANG`, `LICH_SU_CHAM_SOC`, `THU_TIEN_DANG_KY`, `YEU_CAU_XEP_LOP` |
| HRM | `PHONG_BAN`, `HO_SO_GIAO_VIEN`, `HO_SO_HOC_VIEN`, `HO_SO_NHAN_VIEN` |

Một giáo viên là **cùng một hàng** mà HRM dùng xếp phòng ban và LMS dùng phân công lớp. Ba database
nghĩa là ba bản `NGUOI_DUNG` phải đồng bộ — đúng lỗi `ChucNang.cs` đã cảnh báo: *"hai nguồn sự thật
cho cùng một người thì đổi tên một bên là bên kia sai"*. Chốt 09/09 *"giáo viên cũng là nhân viên"*
**dựa trên** việc hai hệ thống nhìn cùng một bảng.

### 3. Hạ tầng dùng chung nhiều hơn nghiệp vụ riêng

| Phần | Dòng code |
|---|---|
| **Dùng chung** (multi-tenant, phân quyền, xác thực, middleware, phân trang) | **3.031** |
| LMS riêng (`Application/DaoTao`) | 2.611 |
| CRM riêng (`Application/Crm`) | 1.593 |
| HRM riêng (`Application/NhanSu`) | 265 |

Tách là **nhân ba 3.031 dòng hạ tầng**, trong đó có Global Query Filter và phân quyền động — sai
ở một bản là rò rỉ dữ liệu chéo trung tâm (quy tắc #2, lỗi nghiêm trọng nhất hệ thống có thể mắc).

### 4. Hạ tầng là 1 VPS

ADR-0004 chốt 1 VPS + Docker Compose. Tách 3 service trên cùng một máy chỉ thêm 3 pipeline CI và
3 lần deploy, **không** được lợi ích thật của microservice (scale độc lập, cách ly sự cố) — tất cả
vẫn chết cùng nhau khi VPS chết.

## Danh sách cầu nối chéo đã khai (cập nhật khi thêm)

Mỗi cầu nối là **một sợi dây phải cắt** nếu sau này tách source, nên phải đếm được. Canh bởi
`RanhGioiHeThongConTests` — thêm cầu nối mà không khai vào `CauNoiDuocPhep` là test đỏ.

| Cầu nối | Chiều | Vì sao cần |
|---|---|---|
| FR-21 xếp lớp | CRM → LMS | Bán khoá xong xếp học viên vào lớp; phải gọi `BaoDamThayLop` của LMS để tôn trọng `IPhamViLopHoc` |
| FR-25 nối hồ sơ | QuanTri → CRM | Tạo hồ sơ học viên cho người đã mua khoá online, nối `KHACH_HANG.nguoi_dung_id` |
| Tên NVKD trong lớp | LMS → CRM | Danh sách học viên hiện tên nhân viên kinh doanh đã tạo hồ sơ khách |
| **FR-29 thống kê nhân sự** | **HRM → CRM + LMS** | Xếp hạng nhân viên kinh doanh theo doanh thu/số học viên (chỉ CRM có), và giáo viên/trợ giảng theo số lớp/số buổi/điểm giảng dạy (chỉ LMS có) |

FR-29 (16/09/2026) là cầu nối **rộng nhất** tới nay vì nó đọc dữ liệu của cả hai hệ thống kia.
Chấp nhận được vì bản chất yêu cầu là *"đánh giá con người bằng kết quả công việc của họ"*, mà
công việc nằm ở CRM (bán hàng) và LMS (giảng dạy) — HRM chỉ giữ hồ sơ con người. Giới hạn tự đặt:
**chỉ ĐỌC qua `IAppDbContext`, không gọi handler của hệ thống khác**, và HRM không đọc cột tiền nào
của LMS (chốt 12/09: chỉ CRM nắm tiền).

Nếu sau này tách source, FR-29 sẽ phải đổi thành gọi API đọc báo cáo của CRM/LMS — không phải
viết lại phép tính.

## Khi nào xét lại

Ba dấu hiệu thật, không phải cảm giác:

1. Một hệ thống cần **scale khác hẳn** (LMS 10.000 học viên xem lịch cùng lúc vs HRM 5 người dùng).
2. **Hai đội** deploy theo nhịp khác nhau và đang chờ nhau.
3. **Bán riêng từng module** cho khách khác nhau — lý do nghiệp vụ, mạnh nhất.

## Đổi tên: phạm vi và rủi ro

Đổi **toàn bộ**, kể cả định danh hạ tầng:

| Hạng mục | Cũ | Mới | Rủi ro đã xử lý |
|---|---|---|---|
| Namespace, project, solution | `GiapTech.LangCenter.LMS.*` | `GiapTech.LangCenter.*` | Không — thuần mã nguồn (214 file) |
| Database | `langcenter_lms` | `langcenter` | `ALTER DATABASE ... RENAME` giữ nguyên dữ liệu; đã đối chiếu 6 con số trước/sau |
| Role PostgreSQL | `langcenter_lms` | `langcenter` | Không rename được role đang kết nối và không có superuser `postgres` → **tạo role mới**, role cũ giữ lại |
| Bucket MinIO | `langcenter-lms-anh` | `langcenter-anh` | Bucket **không rename được** → `mc cp --recursive` rồi đối chiếu từng khoá; bucket cũ giữ lại làm dự phòng |
| `JWT_ISSUER` | `langcenter-lms-api` | `langcenter-api` | `ValidateIssuer = true` nên **mọi token đang lưu hành bị từ chối** → người dùng phải đăng nhập lại |
| Image Docker, CI | `langcenter-lms-api` | `langcenter-api` | Cần sửa secret/workflow GitHub; lần deploy đầu tạo container mới |

### Ba chỗ `JWT_ISSUER` phải đổi CÙNG NHAU

`Program.cs` (validate) · `TokenService.cs` (phát hành) · `ApiFactory.cs` (test). Lệch một chỗ là
mọi request 401 mà không có lỗi biên dịch nào.

### Việc phải làm khi triển khai production

Đổi tên **không tự áp lên VPS**. Thứ tự an toàn:

1. `pg_dump` trước khi làm gì.
2. `ALTER DATABASE langcenter_lms RENAME TO langcenter;` (không dump/restore — nhanh và nguyên vẹn).
3. `mc cp --recursive` bucket cũ → mới, **đối chiếu số object**, giữ bucket cũ tới khi chắc chắn.
4. Cập nhật `.env` trên VPS: `POSTGRES_DB`, `MINIO_BUCKET`, `JWT_ISSUER`.
5. Thông báo trước: **mọi người phải đăng nhập lại** (đổi `JWT_ISSUER`).

## Hệ quả

- (+) Tên khớp phạm vi thật; không còn "LMS" gây nhầm là chỉ có đào tạo.
- (+) Giữ được ACID cho FR-21 và một `NGUOI_DUNG` duy nhất.
- (−) Mọi token đang lưu hành bị vô hiệu — chấp nhận, đổi lấy sự nhất quán.
- (−) Bucket và role cũ còn nằm đó, phải dọn tay sau khi chắc chắn.
- (−) Repo GitHub và thư mục local **chưa đổi tên** — việc ngoài repo, chủ dự án tự làm.
