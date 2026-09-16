# Kế hoạch & tiến độ

> Cập nhật cuối: **2026-09-12**. Nhật ký chi tiết theo ngày: [nhat-ky/](./nhat-ky/README.md).

## Tiến độ tổng

```
Nghiệp vụ  ████████████████  27/28 FR chạy đầu-cuối (FR-27 bài tập online chưa)
Ba hệ thống ████████████████  Cả ba hệ thống đủ nghiệp vụ
Hạ tầng    ████████████░░░░  CI/CD sẵn sàng, chờ VPS thật
Còn lại    ███░░░░░░░░░░░░░  Bài kiểm tra · bài tập online · 16 nợ kỹ thuật
```

## Trạng thái mã FR

| Mã | Chức năng | Backend | Frontend | Ghi chú |
|---|---|:---:|:---:|---|
| FR-01 | Đăng nhập | ✅ | ✅ | Bộ ba {mã trung tâm, username, mật khẩu}; refresh token xoay vòng |
| FR-02 | Quên mật khẩu | ✅ | ✅ | Chưa cấu hình SMTP thật — email ghi log |
| FR-03 | Người dùng (hồ sơ con người) | ✅ | ✅ | Tách khỏi tài khoản (07/09). Ba bảng hồ sơ theo vai trò. Từ 08/09 chia hai màn: **Nhân sự** (HRM) và **Học viên** (LMS). **Từ 13/09 bỏ màn Học viên** — học viên quản lý tập trung ở CRM, cấp tài khoản từ màn Khách hàng |
| FR-04 | Tài khoản đăng nhập | ✅ | ✅ | Màn riêng ở cụm Quản trị dùng chung; gán được cho cả bốn vai trò |
| FR-05 | Phân quyền truy cập | ✅ | ✅ | Ma trận **27 chức năng** × 4 thao tác, 4 nhóm dựng sẵn. Từ 08/09 có **tab theo hệ thống** HRM/CRM/LMS |
| FR-06 | Thiết lập chung | ✅ | ✅ | Gồm múi giờ và ngưỡng cảnh báo nợ học phí |
| FR-07 | Lớp học | ✅ | ✅ | Vòng đời nháp → sắp khai giảng → đang học → kết thúc (bước **kết thúc chưa có endpoint** — nợ N26); tên nháp không chiếm chỗ. Từ 12/09 gán được **tối đa 3 khoá học** |
| FR-08 | Học viên trong lớp | ✅ | ✅ | Học phí riêng từng người (snapshot lúc ghi danh) |
| FR-09 | Buổi học & sinh lịch | ✅ | ✅ | Sinh theo thứ trong tuần, tối đa 500 buổi / 10 năm. Có **lịch dạng calendar** (FullCalendar) và **view chi tiết buổi 5 tab** |
| FR-10 | Điểm danh | ✅ | ✅ | Hai nguồn: học viên tự khai + giáo viên chốt. Kèm **nhận xét hai chiều**: GV nhận xét từng học viên, học viên nhận xét buổi học |
| FR-11 | Bài tập | ✅ | ✅ | Đính kèm nhiều tệp |
| FR-12 | Bài nộp | ✅ | ✅ | Nộp nhiều lần, giữ lịch sử; chấm điểm qua endpoint riêng |
| FR-13 | Tài liệu | ✅ | ✅ | Gán lớp, hoặc để trống = chung toàn trung tâm |
| FR-14 | Học phí & công nợ | ✅ | 🔒 | Sổ thu + công nợ tính động. **Từ 12/09/2026 ẩn khỏi LMS** — chỉ CRM nắm tiền. Bảng, endpoint và màn hình giữ nguyên (route `/lms/hoc-phi` còn sống), chỉ bỏ khỏi sidebar và tab lớp; DTO của LMS trả `null` cho mọi trường tiền |
| FR-15 | Thống kê / Tổng quan | ✅ | ✅ | **Một màn cho mọi vai trò**, không phải 3 dashboard — `IPhamViLopHoc` đã lọc đúng phạm vi từng người. 5 số: 3 việc tồn đọng (chỉ hiện khi > 0) + 2 bối cảnh. **Không có số tiền nào**. `choXepLop` trả 0 với người không có `LopHoc.Sua` |
| FR-16 | Nhật ký hệ thống | ✅ | ✅ | Ghi tự động ở pipeline MediatR + interceptor chụp trường đổi |
| FR-17 | Khách hàng (CRM) | ✅ | ✅ | Bảng riêng, nối `nguoi_dung_id` khi khách vào học. **View 3 tab**: thông tin · lịch sử chăm sóc (kèm phễu bán hàng) · lịch sử mua hàng (gộp cả sổ tiền đã đóng, 09/09) |
| FR-18 | Doanh thu (CRM) | ✅ | ✅ | Đa tiền tệ VND/USD/EUR/CAD, **tỷ giá chụp lúc đăng ký**; giá gốc snapshot, % tính động. Đăng ký = **cam kết**, sổ thu nhiều đợt riêng |
| FR-19 | Khoá học (CRM) | ✅ | ✅ | Danh mục khoá bán ra. Khác `LOP_HOC` (lần mở cụ thể). Đã bán thì ngừng bán, không xoá |
| FR-20 | Sản phẩm khác (CRM) | ✅ | ✅ | Sách, học cụ; có **số lượng**. Đơn hàng dùng **2 FK nullable loại trừ** + `CHECK`. Mua hàng từ tab chăm sóc ghi **cả đơn + lịch sử** trong một transaction |
| FR-21 | Yêu cầu xếp lớp (CRM → LMS) | ✅ | ✅ | Bán khoá → gửi yêu cầu (kèm **ghi chú** + người gửi) → duyệt vào lớp **2 cách**, hoặc **từ chối kèm lý do bắt buộc**. Một đơn gửi **nhiều lần**, lịch sử mua hàng hiện số lần + trạng thái từng lần. **Học phí lấy từ đơn CRM**; hồ sơ học viên tự tạo từ dữ liệu khách. Từ 12/09: đơn **đã xếp vẫn gửi lại được**, trạng thái tham gia lớp **suy động** từ bảng ghi danh, **cảnh báo lệch khoá** khi duyệt (không chặn) |
| FR-22 | Cơ cấu tổ chức (HRM) | ✅ | ✅ | Cây `PHONG_BAN` tự tham chiếu + người quản lý, màn `/hrm/co-cau` (thư viện `@headless-tree`). **Mọi vai trò nhân sự** xếp được vào phòng (giáo viên cũng là nhân viên). Hai cách xếp; chống chu trình ở handler |
| FR-23 | Hồ sơ nhân sự mở rộng (HRM) | ✅ | ✅ | CCCD (không unique), số tài khoản, **liên kết MXH nhiều dòng** (bảng riêng), tệp hồ sơ dùng `TEP_DINH_KEM` cột FK thứ sáu. Áp cho mọi vai trò nhân sự |
| FR-24 | Danh mục chức vụ (HRM) | ✅ | ✅ | Bảng `CHUC_VU` do admin quản, seeder dựng sẵn 5 chức vụ gồm **Ban quản lý**. Áp cho **mọi vai trò nhân sự**; ngừng dùng thay vì xoá. Migration sinh danh mục từ 40 hàng dữ liệu cũ |
| FR-25 | Tài khoản học viên cho khách CRM | ✅ | ✅ | Cấp tài khoản **từ màn Khách hàng (CRM)**; hồ sơ tự nối `KHACH_HANG.nguoi_dung_id` nên một người không thành hai hồ sơ. Màn `/lms/hoc-vien` đã **bỏ** 13/09 — học viên quản lý tập trung ở CRM |
| FR-26 | Khoá học trực tuyến | ✅ | ✅ | 4 bảng, **không bảng nào trỏ sang CRM**. Quản trị cấp quyền học bằng tay. `IPhamViKhoaOnline` — tầng phạm vi **thứ tư**, ba nhánh: ghi danh còn hạn · bài công khai · người soạn |
| FR-27 | Bài tập cho khoá online | ⬜ | ⬜ | Bước 4/4 của elearning. Phải nới `BAI_TAP.buoi_hoc_id` thành 2 FK loại trừ — **đụng FR-11/FR-12 đang chạy**, nên tách riêng làm cuối |
| FR-28 | Thống kê CRM | ✅ | ✅ | 4 loại (khoá học · sản phẩm · elearning · đội nhóm), lọc theo danh sách, biểu đồ tăng trưởng. **Doanh số tính cho người tạo hồ sơ khách**. Elearning **chỉ đo số lượng** — không nối đơn hàng nên không có tiền để chia |

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

### Giai đoạn 5 — Thống kê / Tổng quan (FR-15) · ✅ xong (12/09/2026)

Không migration. **Hai điểm làm khác kế hoạch cũ**, lý do đầy đủ trong
[đặc tả FR-15](./nghiep-vu/thong-ke.md):

- Kế hoạch ghi *"3 dashboard Admin / Giáo viên / Học viên"* → làm **một màn**. `IPhamViLopHoc`
  đã lọc đúng phạm vi từng người nên cùng một truy vấn cho ra con số đúng với từng người. Ba bản
  sao là ba chỗ phải sửa khi đổi.
- Kế hoạch ghi *"cảnh báo nợ dùng `TENANT.so_ngay_canh_bao_no_hoc_phi`"* → **bỏ**. LMS không
  hiển thị tiền học từ 12/09 (N18); ngưỡng đó vẫn còn trong thiết lập cho CRM dùng sau.

`GET /api/v1/toi/tong-quan` — không `[RequirePermission]` (mọi người đăng nhập đều thấy màn
chủ), khai lý do trong `MoiEndpointPhaiDuocGacTests`. Chưa cần tối ưu: 5 `COUNT` trên tập lớp
đã lọc; nếu chậm thì index trước, materialized view sau — không denormalize sớm.

### Giai đoạn 5b — Ba hệ thống con HRM · CRM · LMS · ✅ xong

Chia chức năng phân quyền thành ba hệ thống + nhóm dùng chung (08/09). **Không tách service** —
một API, một DB, một lần đăng nhập; dữ liệu dùng chung (`NGUOI_DUNG`, `TENANT`, nhóm quyền) sẽ
phải đồng bộ giữa ba DB nếu tách. Quyết định giữ một source: [ADR-0005](./kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md).

Đã chạy: bộ chuyển hệ thống, sidebar lọc theo hệ thống, tab phân quyền, hồ sơ con người tách
đôi (Nhân sự → HRM · Học viên → LMS), **URL theo tiền tố** `/hrm` `/crm` `/lms` (10/09).

Nghiệp vụ ba hệ thống nay **đủ**: HRM có FR-22 → FR-24, CRM có FR-17 → FR-21 + FR-28, LMS có FR-07 →
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

> **Rà lại toàn bộ 12/09/2026.** Đọc kế hoạch cũ phải hỏi *"nay còn đúng không"*, không chỉ
> *"đã làm chưa"* — FR-15 hôm nay mô tả một cách làm viết trước khi `IPhamViLopHoc` tồn tại,
> và N16 · N12 trước đó đều **mô tả sai bản chất** vấn đề. Nên mỗi nợ dưới đây đã được đối
> chiếu với mã nguồn hiện tại, kèm file:dòng làm bằng chứng.
>
> Kết quả: **1 nợ đã tự hết** (N9), **1 nợ mô tả sai** (N4 — tệ hơn ghi chép), **2 nợ rẻ hơn**
> ghi chép (N19, N20), **1 nợ đáng lo hơn** và đã nâng mức (N14).

| # | Việc | Mức |
|---|---|---|
| N1 | **Bài kiểm tra**: schema xong, chưa có API và UI. Từ 14/09/2026 `BaiKiemTra` và `BaiLamKiemTra` khai **rỗng** trong `ChucNang.ThaoTacTheoChucNang` nên không hiện trên màn phân quyền và không nhóm nào được cấp — khi làm API thì **khai thao tác trước**, rồi mới cấp lại cho Giáo viên | Cao |
| N3 | `/dang-ky-trung-tam` **mở ở mọi môi trường** — ai cũng tự tạo trung tâm. Đã có hạn mức 10 req/phút mỗi IP ở tầng ứng dụng (08/09), **Rate limit tầng proxy đã có 15/09/2026** — `deploy/nginx/langcenter.conf` (6r/phút cho đăng ký, 30r/phút cho xác thực, đã kiểm thật: bắn 8 request thì 4 cái sau nhận 429). Còn thiếu: captcha / xác thực email, và nên tắt endpoint bằng cờ `dangKyTrungTam` sau khi tạo xong trung tâm thật | Cao |
| N4 | ~~Kiểm trùng lịch giáo viên có API nhưng chưa nối vào UI~~ — **MÔ TẢ SAI, rà lại 12/09/2026**: không có endpoint nào cả. `KiemTrungLichQuery` + `KiemTrungLichHandler` (`Application/DaoTao/BuoiHoc/BuoiHocDtos.cs:516-566`) là **mã chết**: 0 tham chiếu ngoài chính file định nghĩa, không controller, không UI, **không test nào chạm tới**. Handler viết chín (loại trừ lớp đang sửa, dùng `<` nghiêm ngặt để không cảnh báo giả) nhưng chưa chạy lần nào. Nối vào API còn vướng: `List<(DateTimeOffset, DateTimeOffset)>` không bind được từ JSON body, phải thêm DTO trung gian | Trung bình |
| N26 | **Chưa có đường kết thúc lớp** — `TrangThaiLopHoc.DaKetThuc` có trong enum và mọi logic đã xử lý đúng (trạng thái tham gia lớp, danh sách lớp chọn để xếp), nhưng **không endpoint nào set được** nó: chỉ có `/huy` → `DaHuy`. **Đừng nhầm với `HanhDong.HoanTat`** (thêm 14/09/2026): đó là hoàn tất *wizard tạo lớp* (nháp → sắp khai giảng), không phải kết thúc lớp. Vòng đời lớp trong tài liệu ghi "nháp → sắp khai giảng → đang học → kết thúc" nhưng bước cuối chưa chạy được (phát hiện 12/09/2026) | Trung bình |
| N5 | Job dọn tệp mồ côi trong MinIO (Cascade xoá hàng DB nhưng không xoá object). **Đã dọn tay 16/09/2026**: 84 object của tenant test đã xoá, DB không tham chiếu ảnh nào. Endpoint `DELETE /anh/trung-tam/*` thì dọn object đúng — nợ chỉ còn ở đường Cascade (xoá tenant/nhân sự không kéo theo tệp). Job tự động vẫn chưa có | Trung bình |
| N6 | Danh mục ngày nghỉ hệ thống (sinh lịch hiện không né ngày lễ) | Trung bình |
| N7 | Import Excel danh sách học viên | Trung bình |
| ~~N9~~ | ~~Lịch sử chỉnh sửa khoản thu (ai sửa gì lúc nào)~~ — **ĐÃ TỰ HẾT, xác minh 12/09/2026**. Không ai làm riêng cho học phí: `ChanBatThayDoi` quét `ChangeTracker.Entries<BaseEntity>()` nên áp cho **mọi** entity (`KhoanThuHocPhi : TenantEntity : BaseEntity`), `NhatKyBehavior` trong pipeline MediatR bắt mọi `Command`, và 4 cột audit (12/09) cho vế "ai". Khoá lại bằng `Sua_khoan_thu_de_lai_vet_ai_sua_gi_luc_nao` — kiểm **đủ ba vế** *ai* (`username`) · *lúc nào* (`createdAt`) · *gì* (`truoc=500000`, `sau=750000`). Hai đột biến đã kiểm đỏ: thêm `"tien"` vào `TruongNhayCam`, và bỏ chụp trước `SaveChanges` | ✅ |
| N10 | Nhắc nợ học phí / thông báo lịch học qua email (`IEmailSender` đã có, chưa nối) | Thấp |
| N11 | Endpoint dọn tenant test + `globalTeardown` cho E2E. **Đã dọn tay 12/09/2026**: DB dev từ 212 → **1 tenant** (`W686AE9`, trung tâm chủ sản phẩm đang dùng), xoá kèm 33.960 dòng nhật ký; DB còn 17 MB. Nhưng **nguyên nhân chưa chữa** — mỗi lần chạy cả bộ E2E vẫn sinh ~19 tenant mới. Chốt 12/09: Claude kiểm chứng thay đổi **trực tiếp trên `W686AE9`**, không tạo tenant mới | Trung bình |
| N13 | **Chưa dọn nhật ký cũ** — bảng `NHAT_KY_HE_THONG` tăng vô hạn, cần chính sách lưu giữ trước khi chạy production lâu dài | Trung bình |
| N17 | Đổi tên bảng `DANG_KY_KHOA_HOC` → `DON_HANG` (nay chứa cả sản phẩm) và **tồn kho sản phẩm** — hiện bán không giới hạn | Thấp |
| ~~N18~~ | ~~Số đã thu ở CRM không chảy sang sổ học phí LMS~~ — **GIẢI QUYẾT KHÁC 12/09/2026**: không đồng bộ hai sổ mà **bỏ hẳn tiền khỏi LMS**. Chốt với chủ sản phẩm: *"LMS không quản lý tiền học nữa, cũng không hiển thị tiền. Bảo mật thông tin — chỉ CRM mới nắm được số tiền."* Bảng và cột GIỮ NGUYÊN (CRM + báo cáo đọc), chỉ ẩn khỏi API và UI của LMS | ✅ |
| ~~N19~~ | ~~`LOP_HOC` chưa có FK về `KHOA_HOC`~~ — **XONG 12/09/2026**: bảng `LOP_HOC_KHOA_HOC` (tối đa 3 khoá/lớp) + cảnh báo lệch khoá khi duyệt. Còn lại: hộp thoại chọn lớp chưa **ưu tiên sắp xếp** lớp cùng khoá lên đầu. **Rẻ hơn ghi chép**: dữ liệu đã có sẵn ở client (`LopHocDto.khoaHocs` + `YeuCauXepLopDto.tenKhoaHoc`), chỉ cần một `.sort()` ở `ChoXepLop.tsx:145`, không phải đổi API. ⚠️ Comment ở `ChoXepLop.tsx:141` vẫn ghi *"`LOP_HOC` hiện chưa có khoá ngoại về `KHOA_HOC`"* — **lỗi thời từ chính ngày 12/09**, cần xoá khi làm | Thấp |
| N27 | **Form khoá trực tuyến thiếu hộp xác nhận lưu** — mọi form ghi khác trong dự án đều có từ 07/09/2026, riêng `KhoaOnline.tsx` và `ChiTietKhoaOnline.tsx` (làm 13/09) thì không. Phát hiện 14/09 khi viết E2E: bước bấm "Đồng ý" không có nút để bấm. Không mất dữ liệu, nhưng lệch thói quen người dùng và làm E2E phải xử lý hai kiểu | Thấp |
| N20 | Badge `%` trên giá gốc hiện cả ở đơn **sản phẩm** (mặc định `100.0%`) — sản phẩm không có khái niệm giảm giá so với niêm yết nên con số vô nghĩa. **Rà 12/09/2026**: 4 chỗ render (`DoanhThu.tsx:349,523` · `ChiTietKhachHang.tsx:1313,784`), không chỗ nào kiểm `d.loai` — mà trường đó **đã có sẵn cùng scope** (`DoanhThu.tsx:334` đang dùng nó). Hệ quả phụ: `mauPhanTram` tô xanh "ok" mọi `p >= 100` nên đơn sản phẩm nào cũng xanh, che mất đơn khoá học thật sự giảm giá | Thấp |
| N21 | **Chưa canh: mỗi `Command` phải có `Validator`** — quên validator thì dữ liệu rác vào DB mà không lỗi nào. **Đo 12/09/2026: 70 Command / 39 Validator → 31 thiếu**, và `grep "Validator" tests/` trả về **0**. Nhưng 26/31 là lệnh Xoá/Huỷ chỉ nhận `Guid Id` — không cần validator thật. **5 chỗ đáng lo**: `ChamBaiNopCommand` (không ai canh khoảng điểm hợp lệ), `NopBaiCommand`, `TaiAnhLenCommand`, `TaiTepCommand`, `TaiTepHoSoCommand`. Test canh phải có danh sách ngoại lệ khai lý do, nếu không 26 lệnh nhóm Xoá làm nó đỏ vô ích | Trung bình |
| N22 | `RanhGioiHeThongConTests` chỉ quét `Application/`; tầng `API/Controllers` vẫn gọi chéo hệ thống tự do. **Rà 12/09/2026 — nhẹ hơn mô tả**: quét 14 controller, chỉ **1 file** dùng từ 2 hệ thống trở lên (`LopHocController.cs` — chính cầu nối FR-21, đã khai hợp lệ ở tầng Application), và `grep IAppDbContext Controllers/` trả về **0** nên biến thể lỗ hổng `db.X` không tồn tại ở tầng này. Lỗ hổng `db.KhachHangs` phát hiện cùng ngày **đã vá** (`DbSetCuaHeThong`). Cách siết rẻ: cho `GocApplication()` nhận tham số thư mục rồi thêm `[Theory]` chạy cả `API/Controllers` | Thấp |
| N23 | **Role PostgreSQL và bucket MinIO cũ còn nằm đó** sau khi đổi tên 09/09 (`langcenter_lms`, `langcenter-lms-anh`) — giữ làm dự phòng, dọn tay sau khi chắc chắn. **Bucket MinIO cũ `langcenter-lms-anh` đã xoá 16/09/2026** (1 object mồ côi); role PostgreSQL cũ vẫn còn | Thấp |
| N24 | **Kéo-thả đổi cha trong cây cơ cấu** chưa làm (`@headless-tree` có `dragAndDropFeature`, chưa bật) — nay đổi cha bằng cách sửa phòng ban | Thấp |
| ~~N16~~ | ~~4 test E2E lạc hậu~~ — **XONG 12/09/2026**. Ba test `dang-nhap-tra-ma` chờ chữ "đội" thời dự án bóng đá; hai test `quan-tri` trỏ **màn Tài khoản** trong khi địa chỉ/email đã sang `/hrm/nhan-su` (tách người ≠ tài khoản 07/09) và thiếu bước **xác nhận lưu** (thêm 07/09). App đúng, test lạc hậu | ✅ |
| N15 | **E2E phải tắt rate limit mới chạy được** (`GIOI_HAN_TAN_SUAT=false`) vì mỗi test tự tạo tenant qua endpoint có hạn mức 10 req/phút. Cách đúng hơn: fixture dùng CHUNG một tenant, hoặc endpoint tạo tenant riêng cho test | Trung bình |
| ~~N14~~ | ~~Màn Học viên cho đọc danh sách toàn trung tâm~~ — **XONG 14/09/2026**. `IPhamViLopHoc.LocHocVienTheoPhamVi` lọc về lớp mình phụ trách; người có `LopHocToanTrungTam` vẫn thấy tất cả. Chỉ áp khi tập vai trò đúng bằng `{HocVien}` — màn lớp học gọi cùng query để chọn người thêm vào lớp, lọc ở đó sẽ chặn oan (quy tắc #1). Canh bởi `PhamViHocVienTests`, **hai đột biến kiểm đỏ**: bỏ lọc (rò rỉ) và lọc quá tay (chặn oan). Màn Nhân sự HRM không đụng tới: nó gác bằng `ChucNang.NhanSu` mà giáo viên không có | ✅ |
| ~~N12~~ | ~~`Token_bi_sua_chu_ky_thi_bi_tu_choi` chớp nháy~~ — **XONG 12/09/2026**. KHÔNG phải chớp nháy: test đổi **ký tự cuối** chữ ký base64url, mà ký tự cuối chỉ mang 2 bit có nghĩa nên **16 nhóm ký tự cho ra cùng chữ ký** — rơi trúng cùng nhóm thì token vẫn hợp lệ. Nay đổi ký tự ở giữa. Chạy 5 lần đơn + 3 lần toàn bộ đều xanh | ✅ |

## Kiểm chứng hiện tại

- **464 test backend xanh** (67 unit + 397 integration), build 0 warning — đo `dotnet test` 14/09/2026.
- **23 test E2E / 10 spec — XANH HẾT** (14/09/2026). Chạy cả bộ phải
  `GIOI_HAN_TAN_SUAT=false` — xem nợ N15.
- Frontend `tsc -b` + `vite build` sạch, `oxlint` không lỗi.
- **PostgreSQL + MinIO thật**: 42 bảng, 24 migration áp sạch, luồng đầu-cuối chạy tay đủ từ tạo
  trung tâm tới thu học phí.
