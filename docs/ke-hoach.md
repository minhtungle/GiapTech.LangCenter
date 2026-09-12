# Kế hoạch & tiến độ

> Cập nhật cuối: **2026-09-12**. Nhật ký chi tiết theo ngày: [nhat-ky/](./nhat-ky/README.md).

## Tiến độ tổng

```
Nghiệp vụ  ███████████████░  23/24 FR chạy đầu-cuối (còn FR-15 Thống kê)
Ba hệ thống ████████████████  Cả ba hệ thống đủ nghiệp vụ
Hạ tầng    ████████████░░░░  CI/CD sẵn sàng, chờ VPS thật
Còn lại    █████░░░░░░░░░░░  Dashboard + Bài kiểm tra + 23 nợ kỹ thuật
```

## Trạng thái mã FR

| Mã | Chức năng | Backend | Frontend | Ghi chú |
|---|---|:---:|:---:|---|
| FR-01 | Đăng nhập | ✅ | ✅ | Bộ ba {mã trung tâm, username, mật khẩu}; refresh token xoay vòng |
| FR-02 | Quên mật khẩu | ✅ | ✅ | Chưa cấu hình SMTP thật — email ghi log |
| FR-03 | Người dùng (hồ sơ con người) | ✅ | ✅ | Tách khỏi tài khoản (07/09). Ba bảng hồ sơ theo vai trò. Từ 08/09 chia hai màn: **Nhân sự** (HRM) và **Học viên** (LMS) |
| FR-04 | Tài khoản đăng nhập | ✅ | ✅ | Màn riêng ở cụm Quản trị dùng chung; gán được cho cả bốn vai trò |
| FR-05 | Phân quyền truy cập | ✅ | ✅ | Ma trận **24 chức năng** × 4 thao tác, 4 nhóm dựng sẵn. Từ 08/09 có **tab theo hệ thống** HRM/CRM/LMS |
| FR-06 | Thiết lập chung | ✅ | ✅ | Gồm múi giờ và ngưỡng cảnh báo nợ học phí |
| FR-07 | Lớp học | ✅ | ✅ | Vòng đời nháp → sắp khai giảng → đang học → kết thúc (bước **kết thúc chưa có endpoint** — nợ N26); tên nháp không chiếm chỗ. Từ 12/09 gán được **tối đa 3 khoá học** |
| FR-08 | Học viên trong lớp | ✅ | ✅ | Học phí riêng từng người (snapshot lúc ghi danh) |
| FR-09 | Buổi học & sinh lịch | ✅ | ✅ | Sinh theo thứ trong tuần, tối đa 500 buổi / 10 năm. Có **lịch dạng calendar** (FullCalendar) và **view chi tiết buổi 5 tab** |
| FR-10 | Điểm danh | ✅ | ✅ | Hai nguồn: học viên tự khai + giáo viên chốt. Kèm **nhận xét hai chiều**: GV nhận xét từng học viên, học viên nhận xét buổi học |
| FR-11 | Bài tập | ✅ | ✅ | Đính kèm nhiều tệp |
| FR-12 | Bài nộp | ✅ | ✅ | Nộp nhiều lần, giữ lịch sử; chấm điểm qua endpoint riêng |
| FR-13 | Tài liệu | ✅ | ✅ | Gán lớp, hoặc để trống = chung toàn trung tâm |
| FR-14 | Học phí & công nợ | ✅ | ✅ | Sổ thu + bảng công nợ tính động. Phạm vi tách riêng khỏi phạm vi lớp |
| FR-15 | Thống kê / Dashboard | ⬜ | ⬜ | 3 dashboard: Admin / Giáo viên / Học viên |
| FR-16 | Nhật ký hệ thống | ✅ | ✅ | Ghi tự động ở pipeline MediatR + interceptor chụp trường đổi |
| FR-17 | Khách hàng (CRM) | ✅ | ✅ | Bảng riêng, nối `nguoi_dung_id` khi khách vào học. **View 3 tab**: thông tin · lịch sử chăm sóc (kèm phễu bán hàng) · lịch sử mua hàng (gộp cả sổ tiền đã đóng, 09/09) |
| FR-18 | Doanh thu (CRM) | ✅ | ✅ | Đa tiền tệ VND/USD/EUR/CAD, **tỷ giá chụp lúc đăng ký**; giá gốc snapshot, % tính động. Đăng ký = **cam kết**, sổ thu nhiều đợt riêng |
| FR-19 | Khoá học (CRM) | ✅ | ✅ | Danh mục khoá bán ra. Khác `LOP_HOC` (lần mở cụ thể). Đã bán thì ngừng bán, không xoá |
| FR-20 | Sản phẩm khác (CRM) | ✅ | ✅ | Sách, học cụ; có **số lượng**. Đơn hàng dùng **2 FK nullable loại trừ** + `CHECK`. Mua hàng từ tab chăm sóc ghi **cả đơn + lịch sử** trong một transaction |
| FR-21 | Yêu cầu xếp lớp (CRM → LMS) | ✅ | ✅ | Bán khoá → gửi yêu cầu (kèm **ghi chú** + người gửi) → duyệt vào lớp **2 cách**, hoặc **từ chối kèm lý do bắt buộc**. Một đơn gửi **nhiều lần**, lịch sử mua hàng hiện số lần + trạng thái từng lần. **Học phí lấy từ đơn CRM**; hồ sơ học viên tự tạo từ dữ liệu khách. Từ 12/09: đơn **đã xếp vẫn gửi lại được**, trạng thái tham gia lớp **suy động** từ bảng ghi danh, **cảnh báo lệch khoá** khi duyệt (không chặn) |
| FR-22 | Cơ cấu tổ chức (HRM) | ✅ | ✅ | Cây `PHONG_BAN` tự tham chiếu + người quản lý, màn `/hrm/co-cau` (thư viện `@headless-tree`). **Mọi vai trò nhân sự** xếp được vào phòng (giáo viên cũng là nhân viên). Hai cách xếp; chống chu trình ở handler |
| FR-23 | Hồ sơ nhân sự mở rộng (HRM) | ✅ | ✅ | CCCD (không unique), số tài khoản, **liên kết MXH nhiều dòng** (bảng riêng), tệp hồ sơ dùng `TEP_DINH_KEM` cột FK thứ sáu. Áp cho mọi vai trò nhân sự |
| FR-24 | Danh mục chức vụ (HRM) | ✅ | ✅ | Bảng `CHUC_VU` do admin quản, seeder dựng sẵn 5 chức vụ gồm **Ban quản lý**. Áp cho **mọi vai trò nhân sự**; ngừng dùng thay vì xoá. Migration sinh danh mục từ 40 hàng dữ liệu cũ |

Ngoài bảng: **Bài kiểm tra** (`BAI_KIEM_TRA`, `BAI_LAM`) đã có schema và cách ly tenant, nhưng
**chưa có API và UI** — xem nợ N1.

## Lộ trình

### Giai đoạn 0 — Vá base + danh mục quyền · ✅ xong

`HoTen` cho `NguoiDung`, `ILuuTruTep` cho tệp không phải ảnh, 5 → 16 chức năng, 4 nhóm quyền
dựng sẵn, bổ khuyết quyền idempotent cho trung tâm đã tồn tại. Nhật ký:
[06/09](./nhat-ky/2026-09-06.md).

### Giai đoạn 1 — Lớp học (FR-07, FR-08) · ✅ xong

`LOP_HOC` + 2 bảng trung gian, `IPhamViLopHoc` cho phạm vi "lớp mình phụ trách".

### Giai đoạn 2 — Buổi học & điểm danh (FR-09, FR-10) · ✅ xong

Sinh lịch thuần hàm (19 unit test), điểm danh hai cột trạng thái, tự điểm danh giới hạn khung
giờ `[bắt đầu − 15 phút, kết thúc]`.

### Giai đoạn 3 — Học liệu (FR-11 → FR-13) · ✅ xong

`TEP_DINH_KEM` một bảng dùng chung với 5 FK loại trừ nhau. Vá một lỗ hổng: `Anh.Xoa` chỉ nói
"được xoá tệp", không nói "tệp nào" — thêm kiểm chủ sở hữu.

### Giai đoạn 4 — Học phí (FR-14) · ✅ xong

Sổ thu + công nợ tính động. `IPhamViHocPhi` **tách riêng** khỏi `IPhamViLopHoc`: giáo viên thấy
lớp mình dạy nhưng học phí là quan hệ giữa học viên và trung tâm.

### Giai đoạn 5 — Thống kê / Dashboard (FR-15) · ⬜ chưa làm

Không migration. 3 dashboard. Báo cáo điểm danh **luôn dùng `trang_thai_chinh_thuc`**. Cảnh báo
nợ dùng `TENANT.so_ngay_canh_bao_no_hoc_phi`. Nếu chậm: tối ưu index trước, materialized view
sau — không denormalize sớm.

### Giai đoạn 5b — Ba hệ thống con HRM · CRM · LMS · ✅ xong

Chia chức năng phân quyền thành ba hệ thống + nhóm dùng chung (08/09). **Không tách service** —
một API, một DB, một lần đăng nhập; dữ liệu dùng chung (`NGUOI_DUNG`, `TENANT`, nhóm quyền) sẽ
phải đồng bộ giữa ba DB nếu tách. Quyết định giữ một source: [ADR-0005](./kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md).

Đã chạy: bộ chuyển hệ thống, sidebar lọc theo hệ thống, tab phân quyền, hồ sơ con người tách
đôi (Nhân sự → HRM · Học viên → LMS), **URL theo tiền tố** `/hrm` `/crm` `/lms` (10/09).

Nghiệp vụ ba hệ thống nay **đủ**: HRM có FR-22 → FR-24, CRM có FR-17 → FR-21, LMS có FR-07 →
FR-14.

**Hai màn từng dự kiến đã BỎ** (10/09, theo yêu cầu chủ sản phẩm): "Nhân viên kinh doanh" và
"Giáo viên — góc nhìn nhân sự". Lý do: hồ sơ của **cả ba vai trò nhân sự** đã nằm chung ở màn Hồ
sơ nhân sự, và "nhân viên kinh doanh" vốn là **quyền** (`ChucNang.NhanVienKinhDoanh`) chứ không
phải một loại người — tách màn riêng là nhân đôi cùng một dữ liệu.

Đã chốt: giáo viên ở HRM và ở LMS là **cùng một con người** (`NGUOI_DUNG` + `HO_SO_GIAO_VIEN`),
khác quyền và khác màn hình. Không tạo bảng nhân sự thứ hai.

### Giai đoạn 6 — Triển khai

VPS → domain + HTTPS → backup. Xem
[trien-khai-pull-code.md](./ha-tang/trien-khai-pull-code.md).

---

## Nợ kỹ thuật

| # | Việc | Mức |
|---|---|---|
| N1 | **Bài kiểm tra**: schema xong, chưa có API và UI | Cao |
| N3 | `/dang-ky-trung-tam` **mở ở mọi môi trường** — ai cũng tự tạo trung tâm. Đã có hạn mức 10 req/phút mỗi IP ở tầng ứng dụng (08/09), nhưng **rate limit ở reverse proxy vẫn bắt buộc** trước khi mở ra Internet; chưa có captcha / xác thực email | Cao |
| N4 | Kiểm trùng lịch giáo viên có API nhưng **chưa nối vào UI** | Trung bình |
| N26 | **Chưa có đường kết thúc lớp** — `TrangThaiLopHoc.DaKetThuc` có trong enum và mọi logic đã xử lý đúng (trạng thái tham gia lớp, danh sách lớp chọn để xếp), nhưng **không endpoint nào set được** nó: chỉ có `/huy` → `DaHuy`. Vòng đời lớp trong tài liệu ghi "nháp → sắp khai giảng → đang học → kết thúc" nhưng bước cuối chưa chạy được (phát hiện 12/09/2026) | Trung bình |
| N5 | Job dọn tệp mồ côi trong MinIO (Cascade xoá hàng DB nhưng không xoá object) | Trung bình |
| N6 | Danh mục ngày nghỉ hệ thống (sinh lịch hiện không né ngày lễ) | Trung bình |
| N7 | Import Excel danh sách học viên | Trung bình |
| N9 | Lịch sử chỉnh sửa khoản thu (ai sửa gì lúc nào) | Thấp |
| N10 | Nhắc nợ học phí / thông báo lịch học qua email (`IEmailSender` đã có, chưa nối) | Thấp |
| N11 | Endpoint dọn tenant test + `globalTeardown` cho E2E. **Đã dọn tay 12/09/2026**: DB dev từ 212 → **1 tenant** (`W686AE9`, trung tâm chủ sản phẩm đang dùng), xoá kèm 33.960 dòng nhật ký; DB còn 17 MB. Nhưng **nguyên nhân chưa chữa** — mỗi lần chạy cả bộ E2E vẫn sinh ~19 tenant mới. Chốt 12/09: Claude kiểm chứng thay đổi **trực tiếp trên `W686AE9`**, không tạo tenant mới | Trung bình |
| N13 | **Chưa dọn nhật ký cũ** — bảng `NHAT_KY_HE_THONG` tăng vô hạn, cần chính sách lưu giữ trước khi chạy production lâu dài | Trung bình |
| N17 | Đổi tên bảng `DANG_KY_KHOA_HOC` → `DON_HANG` (nay chứa cả sản phẩm) và **tồn kho sản phẩm** — hiện bán không giới hạn | Thấp |
| N18 | **Số đã thu ở CRM không chảy sang sổ học phí LMS** — FR-21 chuyển *số cam kết* thành học phí áp dụng, nhưng khách đóng 4tr ở CRM thì sổ LMS vẫn ghi `daThu = 0`. Cùng một khoản tiền phải ghi hai lần nếu muốn cả hai sổ đúng | **Cao** |
| ~~N19~~ | ~~`LOP_HOC` chưa có FK về `KHOA_HOC`~~ — **XONG 12/09/2026**: bảng `LOP_HOC_KHOA_HOC` (tối đa 3 khoá/lớp) + cảnh báo lệch khoá khi duyệt. Còn lại: hộp thoại chọn lớp chưa **ưu tiên sắp xếp** lớp cùng khoá lên đầu (đã có dữ liệu để làm) | Thấp |
| N20 | Badge `%` trên giá gốc hiện cả ở đơn **sản phẩm** (luôn `100.0%`) — sản phẩm không có khái niệm giảm giá so với niêm yết nên con số vô nghĩa | Thấp |
| N21 | **Chưa canh: mỗi `Command` phải có `Validator`** — quên validator thì dữ liệu rác vào DB mà không lỗi nào. Thêm một test canh theo khuôn `MoiEndpointPhaiDuocGacTests` | Trung bình |
| N22 | `RanhGioiHeThongConTests` chỉ quét `Application/`; tầng `API/Controllers` vẫn gọi chéo hệ thống tự do (đúng vì controller là chỗ ghép, nhưng nếu muốn siết thì cần danh sách khai tương tự) | Thấp |
| N23 | **Role PostgreSQL và bucket MinIO cũ còn nằm đó** sau khi đổi tên 09/09 (`langcenter_lms`, `langcenter-lms-anh`) — giữ làm dự phòng, dọn tay sau khi chắc chắn | Thấp |
| N24 | **Kéo-thả đổi cha trong cây cơ cấu** chưa làm (`@headless-tree` có `dragAndDropFeature`, chưa bật) — nay đổi cha bằng cách sửa phòng ban | Thấp |
| N16 | **4 test E2E lạc hậu** (`dang-nhap-tra-ma.spec.ts` ×2, `quan-tri.spec.ts` ×2): `quan-tri` tìm `button[title="Sửa"]` nhưng nút thao tác đã vào `MenuThaoTac` (07/09), còn `dang-nhap-tra-ma` chờ chữ **"đội"** thời dự án bóng đá trong khi app trả "Không tìm thấy **trung tâm** tương ứng". **App đúng, test lạc hậu mới sai** — đỏ liên tục từ trước 10/09. Sửa kỳ vọng của test. *(Từng ghi trùng thành N25, đã gộp 11/09.)* | Trung bình |
| N15 | **E2E phải tắt rate limit mới chạy được** (`GIOI_HAN_TAN_SUAT=false`) vì mỗi test tự tạo tenant qua endpoint có hạn mức 10 req/phút. Cách đúng hơn: fixture dùng CHUNG một tenant, hoặc endpoint tạo tenant riêng cho test | Trung bình |
| N14 | Màn Học viên (LMS) và Nhân sự (HRM) cho đọc danh sách **toàn trung tâm**, chưa giới hạn "học viên lớp mình" — cần mở rộng `IPhamViLopHoc` cho hồ sơ con người | Trung bình |
| N12 | `Token_bi_sua_chu_ky_thi_bi_tu_choi` **chớp nháy** — đỏ một lần khi chạy toàn bộ (12 giây), xanh khi chạy riêng | Trung bình |

## Kiểm chứng hiện tại

- **442 test backend xanh** (66 unit + 376 integration), build 0 warning — đo `dotnet test` 12/09/2026.
- **20 test E2E / 8 spec**; 16 xanh, **4 đỏ là nợ N16** (test lạc hậu thời dự án bóng đá, không
  phải lỗi nghiệp vụ). Chạy cả bộ phải `GIOI_HAN_TAN_SUAT=false` — xem nợ N15.
- Frontend `tsc -b` + `vite build` sạch, `oxlint` không lỗi.
- **PostgreSQL + MinIO thật**: 37 bảng, 22 migration áp sạch, luồng đầu-cuối chạy tay đủ từ tạo
  trung tâm tới thu học phí.
