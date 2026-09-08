# Kế hoạch & tiến độ

> Cập nhật cuối: **2026-09-08**. Nhật ký chi tiết theo ngày: [nhat-ky/](./nhat-ky/README.md).

## Tiến độ tổng

```
Nghiệp vụ  ██████████████░░  17/19 FR chạy đầu-cuối (còn FR-15 Thống kê)
Ba hệ thống ████████████░░░░  CRM xong nghiệp vụ; HRM còn khung trống
Hạ tầng    ████████████░░░░  CI/CD sẵn sàng, chờ VPS thật
Còn lại    ██████░░░░░░░░░░  Dashboard + nghiệp vụ HRM/CRM + 12 nợ kỹ thuật
```

## Trạng thái mã FR

| Mã | Chức năng | Backend | Frontend | Ghi chú |
|---|---|:---:|:---:|---|
| FR-01 | Đăng nhập | ✅ | ✅ | Bộ ba {mã trung tâm, username, mật khẩu}; refresh token xoay vòng |
| FR-02 | Quên mật khẩu | ✅ | ✅ | Chưa cấu hình SMTP thật — email ghi log |
| FR-03 | Người dùng (hồ sơ con người) | ✅ | ✅ | Tách khỏi tài khoản (07/09). Ba bảng hồ sơ theo vai trò. Từ 08/09 chia hai màn: **Nhân sự** (HRM) và **Học viên** (LMS) |
| FR-04 | Tài khoản đăng nhập | ✅ | ✅ | Màn riêng ở cụm Quản trị dùng chung; gán được cho cả bốn vai trò |
| FR-05 | Phân quyền truy cập | ✅ | ✅ | Ma trận **20 chức năng** × 4 thao tác, 4 nhóm dựng sẵn. Từ 08/09 có **tab theo hệ thống** HRM/CRM/LMS |
| FR-06 | Thiết lập chung | ✅ | ✅ | Gồm múi giờ và ngưỡng cảnh báo nợ học phí |
| FR-07 | Lớp học | ✅ | ✅ | Vòng đời nháp → sắp khai giảng → đang học → kết thúc; tên nháp không chiếm chỗ |
| FR-08 | Học viên trong lớp | ✅ | ✅ | Học phí riêng từng người (snapshot lúc ghi danh) |
| FR-09 | Buổi học & sinh lịch | ✅ | ✅ | Sinh theo thứ trong tuần, tối đa 500 buổi / 10 năm. Có **lịch dạng calendar** (FullCalendar) và **view chi tiết buổi 5 tab** |
| FR-10 | Điểm danh | ✅ | ✅ | Hai nguồn: học viên tự khai + giáo viên chốt. Kèm **nhận xét hai chiều**: GV nhận xét từng học viên, học viên nhận xét buổi học |
| FR-11 | Bài tập | ✅ | ✅ | Đính kèm nhiều tệp |
| FR-12 | Bài nộp | ✅ | ✅ | Nộp nhiều lần, giữ lịch sử; chấm điểm qua endpoint riêng |
| FR-13 | Tài liệu | ✅ | ✅ | Gán lớp, hoặc để trống = chung toàn trung tâm |
| FR-14 | Học phí & công nợ | ✅ | ✅ | Sổ thu + bảng công nợ tính động. Phạm vi tách riêng khỏi phạm vi lớp |
| FR-15 | Thống kê / Dashboard | ⬜ | ⬜ | 3 dashboard: Admin / Giáo viên / Học viên |
| FR-16 | Nhật ký hệ thống | ✅ | ✅ | Ghi tự động ở pipeline MediatR + interceptor chụp trường đổi |
| FR-17 | Khách hàng (CRM) | ✅ | ✅ | Bảng riêng, nối `nguoi_dung_id` khi khách vào học. UNIQUE số điện thoại **partial** |
| FR-18 | Doanh thu (CRM) | ✅ | ✅ | Đa tiền tệ VND/USD/EUR/CAD, **tỷ giá chụp lúc đăng ký**; giá gốc snapshot, % tính động |
| FR-19 | Khoá học (CRM) | ✅ | ✅ | Danh mục sản phẩm. Khác `LOP_HOC` (lần mở cụ thể). Đã bán thì ngừng bán, không xoá |

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

### Giai đoạn 5b — Ba hệ thống con HRM · CRM · LMS · 🟡 khung xong, nghiệp vụ chưa

Chia chức năng phân quyền thành ba hệ thống + nhóm dùng chung (08/09). **Không tách service** —
một API, một DB, một lần đăng nhập; mỗi hệ thống hiện chỉ 1–2 module và dữ liệu dùng chung
(`NGUOI_DUNG`, `TENANT`, nhóm quyền) sẽ phải đồng bộ giữa ba DB nếu tách.

Đã chạy: bộ chuyển hệ thống, sidebar lọc theo hệ thống, tab phân quyền, hồ sơ con người tách
đôi (Nhân sự → HRM · Học viên → LMS).

**Chưa có nghiệp vụ** cho 3 module mới — cần chốt trước khi làm:

| Module | Cần chốt |
|---|---|
| Nhân viên kinh doanh (HRM) | Theo dõi gì: chỉ tiêu, hoa hồng, khách hàng phụ trách? |
| Giáo viên — góc nhìn nhân sự (HRM) | Thêm trường gì so với `HO_SO_GIAO_VIEN`: hợp đồng, lương, chấm công? |
| Doanh thu (CRM) | Tính từ đâu: tổng `KHOAN_THU_HOC_PHI`, hay có nguồn thu khác? |

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
| N5 | Job dọn tệp mồ côi trong MinIO (Cascade xoá hàng DB nhưng không xoá object) | Trung bình |
| N6 | Danh mục ngày nghỉ hệ thống (sinh lịch hiện không né ngày lễ) | Trung bình |
| N7 | Import Excel danh sách học viên | Trung bình |
| N9 | Lịch sử chỉnh sửa khoản thu (ai sửa gì lúc nào) | Thấp |
| N10 | Nhắc nợ học phí / thông báo lịch học qua email (`IEmailSender` đã có, chưa nối) | Thấp |
| N11 | Endpoint dọn tenant test + `globalTeardown` cho E2E | Thấp |
| N13 | **Chưa dọn nhật ký cũ** — bảng `NHAT_KY_HE_THONG` tăng vô hạn, cần chính sách lưu giữ trước khi chạy production lâu dài | Trung bình |
| N14 | Màn Học viên (LMS) và Nhân sự (HRM) cho đọc danh sách **toàn trung tâm**, chưa giới hạn "học viên lớp mình" — cần mở rộng `IPhamViLopHoc` cho hồ sơ con người | Trung bình |
| N12 | `Token_bi_sua_chu_ky_thi_bi_tu_choi` **chớp nháy** — đỏ một lần khi chạy toàn bộ (12 giây), xanh khi chạy riêng | Trung bình |

## Kiểm chứng hiện tại

- **264 test backend xanh** (54 unit + 210 integration), build 0 warning.
- Frontend `tsc -b` + `vite build` sạch, `oxlint` không lỗi.
- **PostgreSQL + MinIO thật**: 20 bảng, migration áp sạch, luồng đầu-cuối chạy tay đủ từ tạo
  trung tâm tới thu học phí.
