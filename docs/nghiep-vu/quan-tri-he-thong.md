# Module Quản trị hệ thống (FR-03 → FR-06)

## FR-03 — Tài khoản người dùng

CRUD tài khoản đăng nhập.

**Trường dữ liệu:** username, password, cờ "bắt buộc đổi mật khẩu", email, số điện thoại, địa chỉ,
danh sách quyền (nhiều nhóm quyền / tài khoản), hồ sơ cầu thủ liên kết (0..1).

### Quy trình chuẩn tạo tài khoản (wizard tuần tự)

1. **Tạo hồ sơ cầu thủ** nếu chưa có (FR-04) — hoặc chọn hồ sơ đã tồn tại, hoặc bỏ qua nếu tài khoản
   không gắn cầu thủ nào.
2. **Tạo/chọn nhóm quyền** nếu chưa có (FR-05).
3. **Tạo tài khoản**, gán quyền + hồ sơ cầu thủ.

### Quy tắc

- Chỉ **Admin** được đổi mật khẩu cho tài khoản khác. Người dùng thường chỉ đổi mật khẩu của chính mình.
- Một tài khoản liên kết **tối đa 1** hồ sơ cầu thủ (`NGUOI_DUNG.cau_thu_id` nullable).
- Username duy nhất trong phạm vi tenant, không phải toàn hệ thống.
- Xóa tài khoản không xóa hồ sơ cầu thủ liên kết — hai thực thể độc lập.

## FR-04 — Hồ sơ cầu thủ

CRUD hồ sơ: ảnh đại diện, họ tên, **số áo**, **vị trí sở trường**, ngày sinh, ngày tham gia, ghi chú.

Số áo và vị trí sở trường là **nguồn mặc định cho bảng chiến thuật** (FR-10 tab b) — không có
chúng thì phải gõ lại số áo cho từng người ở từng trận. Sơ đồ của một trận vẫn ghi đè được
(mượn áo, trùng số).

**Số áo KHÔNG đặt UNIQUE**: CLB phong trào hay trùng số, ràng buộc cứng sẽ chặn cả việc nhập
liệu bình thường.

### Quy tắc

- **Độc lập hoàn toàn với tài khoản đăng nhập** — một cầu thủ có thể chưa có tài khoản (ví dụ cầu thủ
  mới, chỉ cần có mặt trong đội hình và danh sách đóng quỹ).
- Ảnh đại diện upload lên MinIO qua presigned URL, DB chỉ lưu đường dẫn.
- Trước khi xóa hồ sơ cầu thủ: cảnh báo nếu cầu thủ đang có dữ liệu liên quan (đội hình trận, đánh giá,
  đóng góp quỹ, vote MVP).

## FR-05 — Phân quyền truy cập

CRUD **nhóm quyền**. Mỗi nhóm cấu hình chi tiết theo **chức năng + thao tác**.

- Thao tác: `xem` / `thêm` / `sửa` / `xóa`.
- Một tài khoản có thể gán **nhiều nhóm quyền**; quyền hiệu lực = **hợp (union)** của tất cả các nhóm.
- Đây **không phải** role cố định kiểu `[Authorize(Roles=...)]` — quyền đọc động từ bảng
  `QUYEN_CHUC_NANG` tại runtime. Chi tiết triển khai: [phân quyền động](../backend/phan-quyen-dong.md).

### Giao diện

Ma trận chức năng × thao tác (checkbox), ưu tiên desktop vì thao tác phức tạp — xem
[nguyên tắc UI/UX](../frontend/ui-ux-nguyen-tac.md).

## FR-06 — Thiết lập chung

Thông tin CLB: tên đội, tên viết tắt, ngày thành lập, logo, ảnh bìa, mô tả, **bộ áo đấu**.

### Bộ áo đấu

CLB chọn **nhiều màu** từ bảng 8 màu cố định (trắng · đỏ · xanh dương · vàng · cam · tím ·
đen · hồng) — thường 2–3 bộ: sân nhà, sân khách, áo thủ môn. Lưu JSON vào `TENANT.mau_ao_json`.

**Bảng chiến thuật (FR-10 tab b) chỉ cho chọn trong bộ này.** Không ràng buộc thì mỗi trận lại
vẽ một màu khác, xem lại lịch sử không nhận ra đội mình mặc gì.

Ba trường hợp biên:

- **Chưa khai** (null / mảng rỗng) → sơ đồ mở **toàn bộ** bảng màu. Khoá người dùng khỏi tính
  năng chỉ vì họ chưa vào màn thiết lập là chặn nhầm chỗ.
- **Màu đang dùng trên sơ đồ luôn có mặt** kể cả khi CLB vừa bỏ nó khỏi bộ áo — nếu không,
  bảng chọn không có ô nào sáng và người dùng tưởng hỏng.
- **Mã lạ bị chặn tại cổng** (`MAU_AO_KHONG_HOP_LE`), không lọc âm thầm: lọc im lặng thì người
  dùng tưởng đã lưu được.

Bảng màu tồn tại ở hai nơi — `Domain/Common/MauAo.cs` (validate) và `BANG_MAU_AO` ở frontend
(vẽ, có mã hex + màu chữ). Không gộp được vì Domain không nên biết mã màu CSS. Hai danh sách
**mã** phải khớp, canh bởi `MauAoDongBoTests`.

### Quy tắc

- Tenant mới chưa cấu hình → dùng **giá trị mặc định**, không chặn người dùng vào hệ thống.
- Logo và ảnh bìa lưu trên MinIO, **API làm proxy** (`GET /api/v1/anh/{khoa}`) chứ không dùng
  presigned URL: MinIO không expose ra Internet (quy tắc #6), và đi qua API thì mỗi lần đọc đều
  kiểm được tenant. Xem [upload ảnh](#upload-ảnh).
- Lệnh cập nhật gửi `mauAo = null` (client cũ) thì **giữ nguyên** bộ áo; chỉ mảng rỗng mới là
  "người dùng chủ động bỏ hết" (quy tắc #1). Canh bởi `Sua_ten_doi_khong_lam_mat_bo_ao`.

## Upload ảnh

Ảnh đại diện cầu thủ (FR-04), logo và ảnh bìa CLB (FR-06) lưu trên MinIO (ADR-0004).

**DB lưu KHOÁ, không lưu URL đầy đủ**: đổi domain hay chuyển kho lưu trữ thì mọi hàng vẫn dùng
được, không phải migration sửa hàng loạt chuỗi.

### Cách ly tenant

Khoá có dạng `{tenantId}/{loai}/{guid}{ext}` — tenant nằm ngay đầu đường dẫn. Kho lưu trữ
**không có Global Query Filter** như EF Core, nên cách ly phải tự cài đặt: tầng lưu trữ kiểm
tiền tố tenant trước khi đọc/xoá. Không có bước này thì đoán được khoá là đọc được ảnh CLB
khác. Canh bởi `Khong_doc_duoc_anh_cua_clb_khac`, kiểm chứng bằng phản chứng.

`ILuuTruAnh` đăng ký **Scoped**, không Singleton: nó phụ thuộc `ICurrentTenant` (theo request).
Singleton sẽ giữ tenant của request đầu tiên cho mọi request sau.

### Ba quyết định

- **SVG bị từ chối.** SVG là XML, chứa được `<script>` và chạy khi trình duyệt mở trực tiếp —
  nhận nó là mở đường cho XSS lưu trữ. Chỉ nhận JPG/PNG/WebP/GIF, tối đa 5 MB.
- **Frontend tải ảnh qua axios rồi tạo blob URL**, không dùng `<img src="/api/v1/anh/...">`:
  trình duyệt không gắn header `Authorization` cho request của thẻ img nên endpoint trả 401 và
  ảnh hiện thành icon hỏng (đã gặp đúng lỗi này). Hai lựa chọn khác đều tệ hơn — token trong
  querystring bị lộ vào log server, hoặc expose MinIO ra Internet (trái quy tắc #6).
- **Đổi ảnh dọn ảnh cũ.** Không dọn thì mỗi lần đổi avatar để lại một tệp mồ côi vĩnh viễn.
  Ghi khoá mới vào DB TRƯỚC khi xoá tệp cũ: xoá trước mà ghi DB lỗi thì mất cả hai.
  Canh bởi `Doi_anh_thi_xoa_anh_cu`.

Bucket tạo lúc tải lên đầu tiên, không lúc khởi động: API phải lên được kể cả khi MinIO tạm chết.

## Trạng thái triển khai

| Mã | Endpoint | Ghi chú |
|---|---|---|
| FR-03 | `/api/v1/tai-khoan` (GET/POST/PUT/DELETE), `POST {id}/dat-lai-mat-khau` | Đặt lại mật khẩu dùng chức năng riêng `DoiMatKhauNguoiKhac` |

**Cập nhật tài khoản (PUT)** sửa được: email, SĐT, hồ sơ cầu thủ liên kết, nhóm quyền, trạng thái.
Cố tình **không** cho sửa:

| Trường | Vì sao |
|---|---|
| `username` | Là định danh đăng nhập — đổi sẽ khoá người dùng ra ngoài mà họ không biết |
| `password` | Có luồng riêng (`dat-lai-mat-khau`) để luôn bật cờ buộc đổi, admin không giữ mật khẩu đang dùng của người khác |

> ⚠️ **Quy tắc chống mất dữ liệu:** mọi trường mà `CapNhatTaiKhoanCommand` ghi đè đều phải có
> mặt trong `TaiKhoanDto` **và** trong form sửa. Thiếu một trường thì form không điền lại được,
> và khi lưu sẽ gửi `null` lên — xóa mất dữ liệu người dùng chưa từng đụng tới. Lỗi này đã xảy
> ra với `diaChi`; `CapNhatKhongMatDuLieuTests` canh không cho tái diễn.

Chặn **tự vô hiệu hóa chính mình**: đăng xuất xong không vào lại được, và nếu là admin duy nhất thì
cả CLB mất quyền quản trị.
| FR-04 | `/api/v1/cau-thu` (GET/POST/PUT/DELETE) | Chặn xóa khi còn dữ liệu đóng quỹ |
| FR-05 | `/api/v1/quyen` (GET/POST/PUT/DELETE), `GET /danh-muc` | Chặn xóa nhóm đang được gán; tự xóa cache quyền khi sửa |
| FR-06 | `/api/v1/thiet-lap` (GET/PUT) | `MaDoi` không cho sửa — người dùng gõ nó khi đăng nhập |

Đăng nhập lần đầu: tài khoản do seeder tạo mang cờ `PhaiDoiMatKhau`, bị `BuocDoiMatKhauMiddleware`
chặn khỏi **mọi** endpoint nghiệp vụ cho tới khi gọi `POST /api/v1/auth/doi-mat-khau`. Chặn ở tầng API
chứ không chỉ ở frontend, vì token vẫn hợp lệ và gọi thẳng API sẽ qua được.

## Tham chiếu

- Bảng `NGUOI_DUNG`, `CAU_THU`, `QUYEN`, `QUYEN_CHUC_NANG`, `NGUOIDUNG_QUYEN`, `TENANT` — xem
  [ERD](../database/erd.md).
