# Module Quản trị hệ thống (FR-03 → FR-06)

## FR-03 — Người dùng (hồ sơ con người)

**`NGUOI_DUNG` là bảng "con người", không phải bảng đăng nhập.** Đây là phân biệt quan trọng
nhất của module này: một người tồn tại trong hệ thống độc lập với việc họ có đăng nhập được
hay không.

**Trường chung mọi vai trò:** họ tên, ngày sinh, email, số điện thoại, địa chỉ, ảnh đại diện,
vai trò (`LoaiNguoiDung`), **trạng thái nhân sự**.

### Vì sao tách khỏi tài khoản

Trước 07/09/2026, `NGUOI_DUNG` gánh cả hai việc và một cột `TrangThai` mang hai nghĩa: "còn
đăng nhập được" **và** "còn làm ở trung tâm". Hệ quả cụ thể: vô hiệu hoá tài khoản một giáo
viên đã nghỉ thì **không phân công được họ vào lớp cũ nữa** — `KiemNhanSu` đòi
`TrangThai == HoatDong`. Hai khái niệm khác nhau bị nhốt chung một cột.

Nay tách đôi:

| Khái niệm | Nằm ở | Ý nghĩa |
|---|---|---|
| `NGUOI_DUNG.trang_thai_nhan_su` | Người | `DangLamViec` / `DaNghi` — còn thuộc trung tâm không |
| `TAI_KHOAN.trang_thai` | Tài khoản | `HoatDong` / `VoHieuHoa` — còn đăng nhập được không |

Người **đã nghỉ** vẫn giữ nguyên mọi dữ liệu lịch sử: tên trong bảng điểm danh, sổ học phí, bài
đã chấm. Tài khoản **vô hiệu hoá** chỉ chặn đăng nhập.

### Hồ sơ riêng theo vai trò

Mỗi vai trò có một bảng hồ sơ riêng, quan hệ **1–1** với `NGUOI_DUNG`:

| Bảng | Trường |
|---|---|
| `HO_SO_GIAO_VIEN` | bằng cấp, chuyên môn, ngày vào làm |
| `HO_SO_HOC_VIEN` | trường/lớp đang học, tên phụ huynh, SĐT phụ huynh |
| `HO_SO_NHAN_VIEN` | chức vụ, phòng ban |

**Mỗi người một vai trò** — `loai_nguoi_dung` quyết định hồ sơ nào áp dụng. Trợ giảng dùng
chung `HO_SO_GIAO_VIEN` (cùng loại thông tin: bằng cấp, chuyên môn).

**Đổi vai trò không xoá hồ sơ cũ.** Giáo viên chuyển sang làm nhân viên văn phòng thì hàng
`HO_SO_GIAO_VIEN` giữ lại — bằng cấp và ngày vào làm vẫn là sự thật lịch sử, và họ có thể quay
lại dạy (quy tắc #1).

### Quy tắc

- **Tạo người dùng không bắt buộc tạo tài khoản.** Học viên nhỏ tuổi không cần đăng nhập; giáo
  viên thỉnh giảng có thể chỉ cần có tên trong lịch dạy.
- Hồ sơ vai trò **tự sinh khi cần**: đặt `loai_nguoi_dung = GiaoVien` thì hàng
  `HO_SO_GIAO_VIEN` được tạo (rỗng) nếu chưa có. Không bắt người dùng điền ngay.
- **Người đã nghỉ vẫn phân công được vào lớp cũ** — chỉ cảnh báo, không chặn. Chặn cứng sẽ làm
  không sửa nổi dữ liệu lịch sử.
- **Không xoá cứng người dùng đang có dữ liệu**: 12 khoá ngoại nghiệp vụ trỏ tới `NGUOI_DUNG`,
  7 trong số đó là `Restrict`. Dùng `trang_thai_nhan_su = DaNghi`.

## FR-04 — Tài khoản đăng nhập

`TAI_KHOAN` — chỉ thông tin cần để vào hệ thống: username, mật khẩu, cờ buộc đổi mật khẩu,
trạng thái, và **`nguoi_dung_id` (nullable)** trỏ tới người sở hữu.

### Quy tắc

- **Username duy nhất trong phạm vi trung tâm**, không phải toàn hệ thống —
  `UNIQUE(tenant_id, username)`. Hai trung tâm đều có thể có `admin`.
- **Một người tối đa một tài khoản** — `UNIQUE(nguoi_dung_id)`. Hai tài khoản cùng một người thì
  không biết quyền nào thắng.
- `nguoi_dung_id` **nullable**: tài khoản kỹ thuật (tích hợp, seed) không gắn con người nào.
- Chỉ **Admin** đổi được mật khẩu cho tài khoản khác. Người dùng thường chỉ đổi của chính mình.
- **Vô hiệu hoá tài khoản không đụng tới người dùng.** Đây chính là mục tiêu của việc tách bảng.
- Nhóm quyền gán cho **tài khoản**, không phải người — quyền là chuyện đăng nhập.

### Xoá dữ liệu

| Quan hệ | Delete | Vì sao |
|---|---|---|
| `TAI_KHOAN → NGUOI_DUNG` | SetNull | Xoá người thì tài khoản thành mồ côi chứ không biến mất — còn dấu vết ai từng đăng nhập |
| `NGUOIDUNG_QUYEN → TAI_KHOAN` | Cascade | Quyền vô nghĩa khi không còn tài khoản |
| `REFRESH_TOKEN`, `TOKEN_DATLAI_MATKHAU → TAI_KHOAN` | Cascade | Phiên và token đặt lại chết cùng tài khoản |
| `HO_SO_* → NGUOI_DUNG` | Cascade | Hồ sơ là một phần của người, không có nghĩa khi đứng riêng |

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

Thông tin trung tâm, khớp đúng các cột của bảng `TENANT`:

| Nhóm | Trường |
|---|---|
| Nhận diện | `ten_trung_tam`, `ten_viet_tat`, `mo_ta`, `logo_url`, `anh_bia_url` |
| Liên hệ | `dia_chi`, `lien_he` |
| Chuyển khoản | `so_tai_khoan`, `ten_ngan_hang`, `chu_tai_khoan`, `anh_qr_url` |
| Vận hành | `mui_gio` (mặc định `Asia/Ho_Chi_Minh`), `so_ngay_canh_bao_no_hoc_phi` (mặc định 14) |

`ma_trung_tam` **không sửa được** — nó là thứ người dùng gõ khi đăng nhập; đổi mã là làm mọi
người trong trung tâm không vào được hệ thống.

### Hai trường vận hành, dễ bị coi nhẹ

- **`mui_gio`** quyết định buổi học rơi vào ô ngày nào trên lịch. Frontend lấy qua
  `GET /toi/cau-hinh` (endpoint **không** gác `ThietLapChung.Xem`, vì giáo viên và học viên là
  người xem lịch nhiều nhất mà họ không có quyền đó). Xem
  [buổi học & điểm danh](./buoi-hoc-diem-danh.md).
- **`so_ngay_canh_bao_no_hoc_phi`** là ngưỡng để bảng công nợ đánh dấu "quá hạn". Admin tự cấu
  hình chứ không hard-code, vì chính sách nhắc nợ mỗi trung tâm mỗi khác.

### Quy tắc

- Tenant mới chưa cấu hình → dùng **giá trị mặc định**, không chặn người dùng vào hệ thống.
- Logo, ảnh bìa và ảnh QR lưu trên MinIO, **API làm proxy** (`GET /api/v1/anh/{khoa}`) chứ không
  dùng presigned URL: MinIO không expose ra Internet (quy tắc #6), và đi qua API thì mỗi lần đọc
  đều kiểm được tenant. Xem [upload ảnh](#upload-ảnh).
- **`null` = không gửi → giữ nguyên** (quy tắc #1). Bảy trường thêm sau (`dia_chi`, `lien_he`,
  bốn trường chuyển khoản) mặc định `null` để client cũ chưa biết chúng vẫn cập nhật được tên
  trung tâm mà **không xoá mất** các trường đó. Đây đúng là lỗi 16/08 mặc áo mới — canh bởi
  `CapNhatKhongMatDuLieuTests`.

## Upload ảnh

Ảnh đại diện người dùng (FR-03), logo · ảnh bìa · ảnh QR chuyển khoản của trung tâm (FR-06),
và tệp đính kèm học liệu (FR-11 → FR-13) lưu trên MinIO (ADR-0004).

**DB lưu KHOÁ, không lưu URL đầy đủ**: đổi domain hay chuyển kho lưu trữ thì mọi hàng vẫn dùng
được, không phải migration sửa hàng loạt chuỗi.

### Cách ly tenant

Khoá có dạng `{tenantId}/{loai}/{guid}{ext}` — tenant nằm ngay đầu đường dẫn. Kho lưu trữ
**không có Global Query Filter** như EF Core, nên cách ly phải tự cài đặt: tầng lưu trữ kiểm
tiền tố tenant trước khi đọc/xoá. Không có bước này thì đoán được khoá là đọc được ảnh trung tâm
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

**Cập nhật tài khoản (PUT)** sửa được: người dùng được gán, nhóm quyền, trạng thái hoạt động.
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
cả trung tâm mất quyền quản trị.
| FR-04 | `/api/v1/tai-khoan` (GET/POST/PUT/DELETE) | Chặn xoá người quản trị cuối cùng |
| FR-05 | `/api/v1/quyen` (GET/POST/PUT/DELETE), `GET /danh-muc` | Chặn xóa nhóm đang được gán; tự xóa cache quyền khi sửa |
| FR-06 | `/api/v1/thiet-lap` (GET/PUT) | `ma_trung_tam` không cho sửa — người dùng gõ nó khi đăng nhập |

Đăng nhập lần đầu: tài khoản do seeder tạo mang cờ `PhaiDoiMatKhau`, bị `BuocDoiMatKhauMiddleware`
chặn khỏi **mọi** endpoint nghiệp vụ cho tới khi gọi `POST /api/v1/auth/doi-mat-khau`. Chặn ở tầng API
chứ không chỉ ở frontend, vì token vẫn hợp lệ và gọi thẳng API sẽ qua được.

## Tham chiếu

- Bảng `NGUOI_DUNG`, `CAU_THU`, `QUYEN`, `QUYEN_CHUC_NANG`, `NGUOIDUNG_QUYEN`, `TENANT` — xem
  [ERD](../database/erd.md).
