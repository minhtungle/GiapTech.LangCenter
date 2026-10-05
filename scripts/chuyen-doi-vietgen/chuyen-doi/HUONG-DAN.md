# Chuyển dữ liệu VIETGEN Academy → GiapTech.LangCenter

Dữ liệu trong `../du-lieu/` đã lọc sẵn: chỉ đơn vị thật **VIETGEN Academy** (`F4B89D3A-…`), chỉ 11 bảng và các cột script dùng. Bám schema và quy tắc của LangCenter (migration mới nhất `20261005133522_ThemDuoiTenDangNhap`). Ánh xạ chi tiết: `../tai-lieu/ANH_XA_SANG_LANGCENTER.md`.

## Các file

| File | Việc |
|---|---|
| `01-chuyen-doi.sql` | Nạp phòng ban, người dùng, tài khoản, nhóm quyền, khoá học, khách, đơn, thu tiền. **Một giao dịch**, chỉ `INSERT` vào trung tâm mới |
| `02-dat-mat-khau-tam.sh` | Đặt mật khẩu tạm qua API (`POST /tai-khoan/{id}/dat-lai-mat-khau`), xuất CSV mật khẩu |
| `03-doi-soat.sql` | Chỉ đọc: đếm dòng, doanh thu theo tháng / nhân viên / đội nhóm, tình trạng thu tiền |
| `ky-vong.json` | Con số phải ra sau khi chạy |
| `bao-cao-lam-sach.csv` | Mọi dòng bị bỏ, cắt, đổi, kèm lý do. **Đọc trước khi chạy** |
| `danh-sach-tai-khoan.csv` | Username mới ↔ username cũ, nhóm quyền, trạng thái |
| `sinh_sql.py` | Bộ sinh `01-chuyen-doi.sql` từ JSON cũ. Sửa luật ở đây rồi sinh lại, không sửa tay file SQL |
| `schema_tu_snapshot.py` | Dựng DDL từ model snapshot, chỉ để chạy thử nhanh không cần dotnet |

## Các bước

**0. Đọc `bao-cao-lam-sach.csv`**, nhất là những dòng ghi "KIỂM TRA LẠI":

- **173 tài khoản đổi đuôi** `@vietgenacademy.edu.vn` → `@vietgeneducation.edu.vn` (chốt 06/10/2026);
- 1 username hỏng mã hoá → sinh lại từ họ tên viết thường không dấu;
- 14 số tài khoản ngân hàng nhập tự do;
- 25 nhóm khách trùng SĐT: **18 hồ sơ cùng tên được GỘP** (đơn dồn sang hồ sơ tạo trước), 9 hồ sơ khác tên giữ riêng và ghi số vào ghi chú.

Muốn đổi luật thì sửa `sinh_sql.py`, rồi chạy `python3 sinh_sql.py` (mặc định đọc `../du-lieu`, ghi đè các file trong thư mục này).

**1. Môi trường thử trước, prod sau.** Backup trước khi chạy:

```bash
docker exec lms-pg pg_dump -U langcenter -Fc langcenter > truoc-chuyen-doi.dump
```

**2. Tạo trung tâm** bằng màn chủ hệ thống (`/chu/trung-tam`), tên "VIETGEN Academy".

- Ghi lại **mã trung tâm** và **mật khẩu admin** mà hệ thống sinh ra.
- Đăng nhập admin một lần để đổi mật khẩu.
- Không thêm dữ liệu gì khác. Script tự huỷ nếu trung tâm không còn "trắng".

**3. Nạp dữ liệu**

```bash
docker exec -i lms-pg psql -U langcenter -d langcenter -v ON_ERROR_STOP=1 \
  -v ma_trung_tam=XXXXXXX < 01-chuyen-doi.sql
```

- Kết thúc phải in `COMMIT` và `XONG.`.
- Nếu gặp `ERROR` thì toàn bộ đã được rollback, không có gì bị ghi.
- Chạy lại lần hai sẽ bị chặn, vì lúc đó trung tâm đã có dữ liệu.

**4. Restart API** để xoá cache quyền (cache sống 5 phút).

**5. Đặt mật khẩu tạm**

```bash
MA_TRUNG_TAM=XXXXXXX ADMIN_PASS='<mật khẩu admin mới>' API=http://localhost:5229 \
  ./02-dat-mat-khau-tam.sh
```

- Kết quả là file `mat-khau-tam-XXXXXXX.csv` (quyền 600). File này **chứa mật khẩu**: phát xong thì xoá.
- Mỗi người phải đổi mật khẩu ở lần đăng nhập đầu.
- 28 tài khoản bị khoá ở hệ cũ không được đặt mật khẩu. Mở lại tài khoản nào thì admin đặt mật khẩu cho tài khoản đó trên giao diện.

**6. Đối soát**

```bash
docker exec -i lms-pg psql -U langcenter -d langcenter -v ma_trung_tam=XXXXXXX < 03-doi-soat.sql
```

Kết quả phải khớp `ky-vong.json`:

- 173 người dùng chuyển sang, cộng admin của hệ thống;
- 145 tài khoản hoạt động;
- **939 khách** (957 trừ 18 hồ sơ đã gộp);
- 1056 đơn, doanh thu 15.273.437.000đ;
- 1115 lần thu, tổng 7.804.775.161đ.

Kiểm thêm trên giao diện:

- Đăng nhập thử một tài khoản mỗi nhóm.
- Mở màn Doanh thu và Thống kê CRM, lọc theo đội nhóm và theo nhân viên.

**7. Gửi cho từng người:** mã trung tâm, username (xem `danh-sach-tai-khoan.csv`) và mật khẩu tạm.

## Những điểm theo LangCenter cần báo trước cho người dùng

- **Username đổi đuôi thành `@vietgeneducation.edu.vn`** — phần tên giữ như cũ, nhưng đuôi KHÁC email họ quen ở hệ cũ. Phải nói rõ khi phát mật khẩu, nếu không ai cũng gõ đuôi cũ và nhận "sai mật khẩu". Gõ đủ cả đuôi khi đăng nhập. Muốn tài khoản tạo mới sau này tự nối đuôi thì admin khai *Đuôi tên đăng nhập* của trung tâm trên giao diện.
- **Doanh thu tính theo cam kết** (15,27 tỷ), không theo tiền đã đóng như hệ cũ (7,80 tỷ). Doanh số được tính cho người tạo khách.
- **887 đơn hiện "Còn thiếu".** Đơn nào không còn đòi thì người thu tự đánh dấu "Đã nhận đủ tiền".
- **Sale thấy toàn bộ khách của trung tâm.** LangCenter không có phạm vi dữ liệu theo từng người.
- **Không chuyển:** chỉ tiêu doanh thu tháng, cấp bậc NS-1…NS-5, giới tính, quyền xem từng khách. Bản JSON gốc giữ làm lưu trữ.
