# Prompt: Chuyển dữ liệu VIETGEN Academy (hệ cũ) → GiapTech.LangCenter

> Thư mục này nằm sẵn trong repo tại **`scripts/chuyen-doi-vietgen/`**. Mở Claude Code ở gốc repo `GiapTech.LangCenter` và gõ:
> **`Đọc và làm theo scripts/chuyen-doi-vietgen/PROMPT-CHUYEN-DOI.md`**
>
> Trong file này, `THU_MUC_DU_LIEU` = `scripts/chuyen-doi-vietgen`.

---

## Bối cảnh

Tôi cần chuyển dữ liệu **tài khoản, phân quyền, đơn hàng và doanh thu** của trung tâm **VIETGEN Academy** từ hệ thống cũ (SQL Server, đã export JSON) sang dự án này (GiapTech.LangCenter).

Dữ liệu trong `du-lieu/` **đã lọc sẵn**:

- chỉ đơn vị thật VIETGEN Academy (`MaDonViSuDung = F4B89D3A-5246-4D90-83C2-2DB9C3E4D9B7`);
- chỉ 11 bảng và những cột mà `chuyen-doi/sinh_sql.py` dùng;
- đã bỏ hash mật khẩu MD5, thông tin thiết bị và các bảng không chuyển.

**Nguyên tắc tối cao: bám tuyệt đối theo LangCenter.**

- Schema, định nghĩa nghiệp vụ, phân quyền và 11 quy tắc trong `CLAUDE.md` là chuẩn.
- Dữ liệu cũ phải biến đổi cho khớp LangCenter. Thứ gì LangCenter không có chỗ chứa thì **không chuyển**.
- **Không** sửa code, không thêm bảng, không thêm cột, không tạo migration EF cho việc này.

## Nội dung thư mục

```
THU_MUC_DU_LIEU/
├── .gitignore                      ← đã chặn commit du-lieu/ và các file sinh từ dữ liệu thật
├── PROMPT-CHUYEN-DOI.md            ← file này
├── du-lieu/                        ← 11 file JSON đã lọc, CHỨA DỮ LIỆU CÁ NHÂN THẬT
├── tai-lieu/
│   ├── ANH_XA_SANG_LANGCENTER.md   ← ánh xạ cũ → LangCenter đã chốt
│   └── MO_TA_CSDL_tbCapDo_DoanhThu.md  ← mô tả CSDL cũ (tham khảo)
└── chuyen-doi/
    ├── HUONG-DAN.md                ← các bước chạy
    ├── sinh_sql.py                 ← bộ sinh SQL (mặc định đọc ../du-lieu, ghi vào chính thư mục này)
    ├── 01-chuyen-doi.sql           ← nạp dữ liệu: một giao dịch, chỉ INSERT vào trung tâm mới
    ├── 02-dat-mat-khau-tam.sh      ← đặt mật khẩu tạm qua API
    ├── 03-doi-soat.sql             ← đối soát, chỉ đọc
    ├── ky-vong.json                ← con số phải ra sau khi nạp
    ├── bao-cao-lam-sach.csv        ← mọi dòng bị bỏ / cắt / đổi, kèm lý do
    ├── danh-sach-tai-khoan.csv     ← username ↔ nhóm quyền
    └── schema_tu_snapshot.py       ← dựng DDL từ model snapshot, chỉ để thử nhanh
```

## Quyết định đã chốt, không bàn lại

1. **Doanh thu theo LangCenter** (crm.md FR-18, thong-ke-crm.md):
   - Doanh thu = `DANG_KY_KHOA_HOC.so_tien × ty_gia_ve_vnd` (**cam kết**). `so_tien` = `TongSoTien` cũ, `gia_goc` = giá khoá cũ, VND, tỷ giá 1.
   - Doanh số quy cho `KHACH_HANG.created_by_id` = `IdNguoiTao` của khách cũ.
   - Mốc thời gian = `ngay_dang_ky` = `NgayTao` của đơn cũ.
   - Kết quả mong đợi: 1056 đơn, 15.273.437.000đ.
2. **Thu tiền** → `THU_TIEN_DANG_KY`:
   - Bỏ các dòng ≤ 0đ (CHECK `so_tien > 0`).
   - `xac_nhan_du_tien = false`, vì người thu tự quyết.
   - Tiền gốc ngoại tệ ghi vào `ghi_chu`.
   - Kết quả mong đợi: 1115 lần thu, 7.804.775.161đ.
3. **Sản phẩm cũ là khoá học** → `KHOA_HOC`, không phải `SAN_PHAM`.
4. **Tài khoản**:
   - Mỗi user cũ sinh 1 `NGUOI_DUNG` + 1 `TAI_KHOAN`, giữ GUID cũ cho `NGUOI_DUNG.id`.
   - Username = giữ **phần tên** cũ, **đổi đuôi** sang `@vietgeneducation.edu.vn` cho cả 173 tài khoản (chốt 06/10/2026; hệ cũ dùng `@vietgenacademy.edu.vn`). Giữ hoa/thường vì đăng nhập so khớp chính xác. Hợp lệ với validator `^[a-zA-Z0-9._-]+(@[a-zA-Z0-9.-]+)?$`. Username hỏng mã hoá → sinh lại từ họ tên viết thường không dấu.
   - Script **không** ghi `TENANT.duoi_ten_dang_nhap`: đó là thiết lập của trung tâm, admin tự khai trên giao diện nếu muốn (chỉ là gợi ý khi tạo tài khoản mới).
   - `KichHoat = 0` hoặc `TrangThai = 0` → `TAI_KHOAN.trang_thai = 1` (VoHieuHoa).
   - Bỏ user vai trò "Khách" (tài khoản dùng thử).
   - Kết quả mong đợi: 173 người, 145 tài khoản hoạt động, 939 khách.
5. **Mật khẩu MD5 không chuyển.**
   - `password_hash` là chuỗi giữ chỗ `CHUYEN-DOI:CHUA-DAT-MAT-KHAU`. `AppPasswordHasher.KiemTra` bắt `FormatException` nên trả false → đăng nhập bị từ chối.
   - Mật khẩu thật đặt qua API, không ghi hash tay (cùng cách `scripts/dong-bo-mat-khau-dev.sh`).
6. **`loai_nguoi_dung`**: Giáo viên → 1 (kèm `HO_SO_GIAO_VIEN`); NVKD, NVKD master, Trưởng phòng KD → 4; còn lại → 0.
7. **Nhóm quyền**:
   - SUPER ADMIN dùng nhóm seed "Quản trị viên"; Giáo viên dùng nhóm seed "Giáo viên".
   - Thêm 6 nhóm mới, ma trận nằm trong `NHOM_MOI` của `sinh_sql.py`: Ban giám đốc, Nhân viên kinh doanh, Nhân viên kinh doanh - master, Trưởng phòng kinh doanh, Điều phối lớp, Điều phối lớp - master.
   - Chỉ dùng cặp có trong `ChucNang.ThaoTacTheoChucNang`.
8. **Phòng ban**: 10 phòng.
   - `tag_vai_tro = 0` (KinhDoanh) cho "Phòng kinh doanh" và các team "Leader …"; `= 1` (GiaoVien) cho "Tiếng Anh" và "Tiếng Đức".
   - `nguoi_quan_ly_id` lấy người quản lý đầu tiên.
9. **SĐT khách trùng** (25 nhóm / 52 khách, partial UNIQUE `(tenant_id, so_dien_thoai)`): **cùng tên** (sau khi bỏ dấu và hậu tố số) → **gộp** về hồ sơ tạo trước, dồn đơn sang — 18 hồ sơ, còn 939 khách. **Khác tên** → giữ số ở hồ sơ đầu, 9 hồ sơ sau để `NULL` và ghi số vào `ghi_chu`.
10. **Không chuyển**:
    - `QuyenTruyCap`, vì LangCenter không có phạm vi dữ liệu CRM theo người;
    - chỉ tiêu doanh thu `tbNguoiDung_DoanhThu` / `tbCoCauToChuc_DoanhThu`;
    - cấp độ NS-1…NS-5, giới tính, `IdChucVu` (thiếu bảng nguồn).
11. **Nạp bằng SQL thẳng, không qua EF**, vì `AppDbContext.SaveChanges` ghi đè `created_at = now`. Nạp vào **một trung tâm mới tạo**; script tự huỷ nếu trung tâm không còn trắng (quy tắc #1).

## Việc cần làm (theo thứ tự, báo cáo sau mỗi bước)

### Bước 1 – Đọc và nắm bối cảnh (chỉ đọc)

- Đọc `CLAUDE.md`, `docs/05-database/erd.md`, `docs/06-nghiep-vu/crm.md`, `docs/06-nghiep-vu/thong-ke-crm.md`, `docs/03-backend/phan-quyen-dong.md`, `Domain/Common/ChucNang.cs`, `Domain/Common/NhomQuyenMacDinh.cs`.
- Đọc `THU_MUC_DU_LIEU/tai-lieu/ANH_XA_SANG_LANGCENTER.md`, `THU_MUC_DU_LIEU/chuyen-doi/HUONG-DAN.md`, `THU_MUC_DU_LIEU/chuyen-doi/sinh_sql.py`.
- Tóm tắt lại cho tôi trong 10 dòng: anh hiểu thế nào về việc cần làm. Có chỗ nào trong ánh xạ **mâu thuẫn với code hiện tại** thì liệt kê.

### Bước 2 – Bảo vệ dữ liệu cá nhân

- `THU_MUC_DU_LIEU/.gitignore` đã chặn: `du-lieu/`, `mat-khau-tam-*.csv`, `chuyen-doi/danh-sach-tai-khoan.csv`, `chuyen-doi/bao-cao-lam-sach.csv`, `chuyen-doi/01-chuyen-doi.sql`, `chuyen-doi/ky-vong.json`. Hai file cuối cũng sinh từ dữ liệu thật, nên chặn luôn.
- Xác nhận bằng `git check-ignore -v` cho từng đường dẫn trên, và `git status --untracked-files=all scripts/chuyen-doi-vietgen` chỉ còn hiện file script và tài liệu.
- **Không commit** dữ liệu cá nhân, mật khẩu tạm, hay file SQL chứa dữ liệu.
- Nhánh `main` đang có thay đổi chưa commit của tôi (`TaiKhoan`, `Tenant`, `Configurations`, `frontend/edu-temp/`…). **Không đụng, không stash, không commit** chúng. Không có gì cần commit ở bước này.

### Bước 3 – Đối chiếu schema hiện tại

- Script được viết theo migration `20261005133522_ThemDuoiTenDangNhap`.
- Kiểm tra có migration nào **mới hơn** đụng tới các bảng: `NGUOI_DUNG, TAI_KHOAN, QUYEN, QUYEN_CHUC_NANG, NGUOIDUNG_QUYEN, PHONG_BAN, HO_SO_GIAO_VIEN, LIEN_KET_MXH, KHOA_HOC, KHACH_HANG, DANG_KY_KHOA_HOC, THU_TIEN_DANG_KY`.
- Kiểm tra `ChucNang.ThaoTacTheoChucNang` còn khớp `THAO_TAC_HOP_LE` trong `sinh_sql.py`.
- Lệch ở đâu thì **sửa `sinh_sql.py`** (không sửa tay file SQL), sinh lại:
  ```bash
  python3 THU_MUC_DU_LIEU/chuyen-doi/sinh_sql.py
  ```
  rồi báo tôi phần đã đổi.

### Bước 4 – Chạy thử trên DB local thật (không phải prod)

1. Dựng một DB PostgreSQL **riêng cho thử**, không dùng DB dev đang có dữ liệu (quy tắc #1). Ví dụ một database mới `langcenter_chuyen_doi` trên container `lms-pg`, áp đủ migration bằng `dotnet ef database update`, với `DesignTimeDbContextFactory` / connection string trỏ vào DB đó.
2. Chạy API trỏ vào DB thử. Tạo trung tâm "VIETGEN Academy" bằng luồng chủ hệ thống (`/api/v1/chu-he-thong`, ADR-0009), hoặc `TRUNG_TAM_DAU_TIEN_MAT_KHAU` nếu DB rỗng. Ghi lại `ma_trung_tam` và mật khẩu admin.
3. Chạy `01-chuyen-doi.sql` với `-v ma_trung_tam=...`. Phải kết thúc bằng `COMMIT` và `XONG.`.
4. Restart API (cache quyền 5 phút) → chạy `02-dat-mat-khau-tam.sh` → chạy `03-doi-soat.sql`.
5. So toàn bộ con số với `ky-vong.json`.
6. Kiểm thử bằng API, với một tài khoản mỗi nhóm quyền:
   - đăng nhập (`/api/v1/auth/dang-nhap`, kỳ vọng `phaiDoiMatKhau = true`);
   - đổi mật khẩu;
   - gọi `GET /api/v1/doanh-thu/tong-hop` (đúng endpoint theo controller) và màn thống kê CRM; xác nhận tổng doanh thu và lọc theo đội nhóm / nhân viên ra số khớp `03-doi-soat.sql`;
   - xác nhận tài khoản NVKD bị 403 ở chức năng không được cấp (ví dụ `PhanQuyen`).
7. Kiểm thêm:
   - chạy `01-chuyen-doi.sql` lần hai phải bị chặn;
   - tenant khác trong DB không đổi;
   - `GET /api/v1/quyen` hiện đủ 8 nhóm, không có ô chết.

### Bước 5 – Hoàn thiện theo quy ước dự án (hỏi trước khi commit)

- Thư mục đã nằm ở `scripts/chuyen-doi-vietgen/`. Phần được commit (không bị `.gitignore` chặn) gồm `PROMPT-CHUYEN-DOI.md`, `chuyen-doi/sinh_sql.py`, `02-dat-mat-khau-tam.sh`, `03-doi-soat.sql`, `HUONG-DAN.md`, `schema_tu_snapshot.py`, `tai-lieu/*.md`.
- Rà `tai-lieu/MO_TA_CSDL_tbCapDo_DoanhThu.md` và `tai-lieu/ANH_XA_SANG_LANGCENTER.md`: nếu có dòng dữ liệu cá nhân cụ thể (họ tên, email, SĐT) thì đề xuất xoá trước khi commit.
- Quy tắc #4: cập nhật tài liệu trong cùng PR:
  - thêm mục vào `docs/07-ha-tang/runbook.md`, hoặc một file riêng trong `docs/07-ha-tang/`, trỏ tới `scripts/chuyen-doi-vietgen/chuyen-doi/HUONG-DAN.md`;
  - ghi nhật ký ngày `docs/nhat-ky/` theo mẫu `docs/nhat-ky/README.md`;
  - thêm dòng `CHANGELOG.md`.
- Chạy `python3 scripts/check-doc-links.py`.
- Quy tắc #5: tạo nhánh mới, ví dụ `chuyen-doi-du-lieu-vietgen`. Chỉ `git add` đúng các file của việc này, không `git add -A` / `git add .`. Không push thẳng `main`, không force-push. **Hỏi tôi trước khi commit và push.**

### Bước 6 – Chuẩn bị chạy thật (chỉ chuẩn bị, KHÔNG tự chạy)

Viết checklist chạy trên VPS theo `docs/07-ha-tang/` (container, đường dẫn, biến môi trường thật):

- backup `pg_dump` trước;
- tạo trung tâm;
- `01` → restart API → `02` → `03`;
- phát file mật khẩu tạm rồi xoá;
- phương án quay lui (khôi phục dump).

**Tuyệt đối không chạy gì trên production.** Tôi tự chạy theo checklist.

## Những điều cấm

- Không sửa entity, cấu hình EF, migration, handler hay controller của LangCenter để "cho vừa" dữ liệu cũ.
- Không `UPDATE` / `DELETE` hàng có sẵn của bất kỳ tenant nào. Không `docker compose down -v`, không reset DB dev đang dùng.
- Không ghi hash mật khẩu tay. Không tắt `phai_doi_mat_khau` cho tài khoản chuyển sang.
- Không commit dữ liệu cá nhân, hash MD5 hay mật khẩu tạm.
- Không thêm `xac_nhan_du_tien = true`, không đổi định nghĩa doanh thu.
- Gặp tình huống buộc phải đụng dữ liệu đang có hoặc phải lệch khỏi ánh xạ đã chốt → **dừng lại hỏi tôi** (quy tắc #1).

## Việc chủ sản phẩm đã quyết (06/10/2026)

- **Đổi đuôi username** cho cả 173 tài khoản sang `@vietgeneducation.edu.vn`. Username hỏng mã hoá sinh lại từ họ tên viết thường không dấu — quy tắc mà 137/174 username cũ đang theo.
- **Gộp 18 hồ sơ khách** trùng SĐT **và cùng tên**; khác tên thì giữ riêng.
- **Giữ nguyên ma trận quyền theo thiết kế LangCenter** — duyệt cả 12 ô thuộc `ChucNang.CanCanNhac` (`DoanhThu.ThuTien` cho 3 nhóm kinh doanh; `XepLop.Duyet`, `LopHocToanTrungTam.Xem/Sua` cho điều phối lớp; thêm `LopHoc.HoanTat/Huy`, `BuoiHoc.Huy` cho điều phối lớp - master).
- 14 số tài khoản ngân hàng nhập tự do: giữ bản gốc trong `ghi_chu`, người dùng tự kiểm lại sau.
