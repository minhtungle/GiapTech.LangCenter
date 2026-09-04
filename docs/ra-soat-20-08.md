# Rà soát hệ thống — 20/08/2026

> ⚠️ **Tài liệu của DỰ ÁN CŨ** (quản lý CLB đá bóng phong trào). Từ 05/09/2026 repo này là
> base cho hệ thống quản lý trung tâm ngoại ngữ — **phần nghiệp vụ dưới đây không còn trong
> code**. Giữ lại để tham khảo cách viết đặc tả. Xem [`CLAUDE.md`](../CLAUDE.md) mục 1.

> Cách làm: dựng CLB mới hoàn toàn, đi qua từng luồng như người dùng thật, thử cả **đường chính**
> và **đường sai** (số âm, số quá lớn, xoá thứ đang được dùng, bấm hai lần).
>
> Không đọc code suy đoán — mọi mục dưới đây đều tái hiện được bằng lệnh cụ thể.

## Đã kiểm và ĐÚNG

Những chốt chặn này hoạt động, ghi lại để lần sau không kiểm lại:

| Luồng | Hành vi |
|---|---|
| 17 endpoint đọc với **CLB rỗng hoàn toàn** | Không endpoint nào nổ; đều trả 200 với danh sách rỗng |
| Xoá đối thủ **đang có trận** | Chặn `DOI_THU_DA_CO_TRAN_DAU`, kèm số trận |
| Xoá trận **đã diễn ra** | Chặn `TRAN_DAU_DA_DIEN_RA_KHONG_XOA_DUOC` |
| Xoá **đợt quỹ đã thu tiền** | Chặn `QUY_DA_THU_TIEN_KHONG_XOA_DUOC` |
| Xếp **cùng cầu thủ hai lần** vào đội hình | Chặn `CAU_THU_TRUNG_TRONG_DOI_HINH` |
| Vote MVP cho người **không ra sân** | Chặn `CAU_THU_KHONG_TRONG_DOI_HINH` |
| Vote **hai lần** cùng người | Toggle (bỏ phiếu) — **có chủ ý**, như nút tim |
| **Tự xoá tài khoản** của mình | Chặn `KHONG_TU_XOA_TAI_KHOAN_CUA_MINH` |
| Tỷ số khách **âm** | Chặn `TY_SO_AM` |
| Bàn thắng **âm** trong đánh giá | Chặn `SO_BAN_AM` |
| Trận `DaLenLich` có `tySoKhach` | `KetQua` vẫn `ChuaCo` → thống kê **không** tính. Đúng: cho nhập tỷ số trước, đổi trạng thái sau |

## Thiếu sót tìm được

### 🔴 T1 — Xoá cầu thủ làm tỷ số trận mất căn cứ

**Tái hiện:** cầu thủ ghi 2 bàn trong trận thắng 2-1 → xoá cầu thủ → trận vẫn `2-1`, `KetQua`
vẫn `Thang`, nhưng **tổng bàn trong đánh giá = 0**.

```
ty_so_nha | ty_so_khach | ket_qua | tong_ban_danh_gia
        2 |           1 | Thang   |                 0
```

**Nguyên nhân:** `TranDau.DongBoTySoNha()` chỉ được gọi ở **một chỗ** — khi lưu đánh giá
(`DanhGiaDtos.cs:128`). `XoaCauThuHandler` chỉ `db.CauThus.Remove(cauThu)` rồi lưu; đánh giá bị
Cascade xoá theo mà tỷ số không được tính lại.

**Vì sao nghiêm trọng:** nó phá đúng nguyên tắc *"một nguồn sự thật cho tỷ số"* mà cả FR-10 dựng
lên. Thống kê, biểu đồ, bảng xếp hạng đều đọc từ tỷ số này. Và nó **im lặng** — không ai biết cho
tới khi mở chi tiết trận và thấy tỷ số không khớp bàn thắng.

**Xử lý:** trong `XoaCauThuHandler`, tìm mọi trận cầu thủ đó có đánh giá, và gọi `DongBoTySoNha`
lại sau khi xoá. Cùng cách với `GhiNhanThuHandler` gọi lại tiến độ quỹ.

### 🔴 T2 — Thu quỹ QUÁ số phải đóng, không cảnh báo

**Tái hiện:** khoản phải đóng `100.000₫` → thu `999.000.000₫` → nhận `204`, tiến độ hiện
`999.000.000 / 100.000`, người đó tính là "đã đóng đủ".

**Nguyên nhân:** validator chỉ có `SoTienDaDong >= 0` (`QuyDtos.cs:312`), không so với
`SoTienCanDong`.

**Vì sao nghiêm trọng:** đây là **tiền**. Thủ quỹ gõ thêm ba số 0 thì số dư quỹ sai hàng trăm
triệu, và số đó lan vào thẻ "Số dư quỹ", "Đã thu", "Còn phải thu" ở màn Tài chính. Không có bước
nào chặn hay hỏi lại.

**Xử lý:** chặn ở tầng ứng dụng với mã lỗi riêng (`THU_QUA_SO_PHAI_DONG`), kèm số phải đóng trong
`duLieu` để UI nói rõ. **Không** tự cắt xuống — cắt âm thầm là sửa số tiền người dùng gõ.

Cân nhắc: có ca hợp lệ nào cần thu quá không? Đóng thừa để bù đợt sau — nhưng đó phải là **hai
khoản** (đợt này đủ, đợt sau một phần), không phải một khoản vượt mức.

### 🟡 T3 — Chỉ số kỹ năng ngoài thang 1–10

**Tái hiện:** gửi `chiSoKyNang = {"tanCong": 99}` → nhận `204`.

**Nguyên nhân:** `ChiSoKyNang` là `string?` chứa JSON tự do, không có validator nào đọc vào trong.

**Vì sao vừa:** UI chỉ cho chọn 1–10 nên người dùng bình thường không gặp. Nhưng radar sẽ vẽ điểm
ra ngoài khung, và điểm trung bình trong bảng xếp hạng bị kéo lệch.

**Xử lý:** validator đọc JSON, kiểm mọi giá trị trong `[1,10]` và mọi khoá thuộc 6 chỉ số đã biết.

### 🟡 T4 — Bàn thắng một cầu thủ không có giới hạn trên

**Tái hiện:** `soBanGhiDuoc = 500` → nhận, tỷ số thành `500-1`.

**Vì sao vừa:** gõ nhầm thì thấy ngay trên UI, tự sửa được. Nhưng một giới hạn mềm (≤ 50) sẽ chặn
lỗi gõ mà không cản ai.

### 🟢 T5 — Màn Tổng quan trống

Đã ghi thành nợ **N6** hôm nay. Không phải lỗi, là chức năng chưa làm.

## Đã xử lý ngay trong phiên này

| Việc | Trạng thái |
|---|---|
| **T2** thu quá số phải đóng | ✅ Chặn `THU_QUA_SO_PHAI_DONG`, trả kèm số phải đóng |
| **T1** tỷ số sau khi xoá cầu thủ | ✅ Tính lại, chỉ trên trận có liên quan |
| **T3** chỉ số kỹ năng ngoài thang | ✅ `Domain/Common/ChiSoKyNang.cs` + validator, có test đồng bộ FE/BE |
| **T4** bàn thắng không giới hạn | ✅ Giới hạn mềm 50 |
| **T6** *(mới, phát sinh khi sửa T2)* | ✅ Ô nhập trả về giá trị thật khi bị từ chối |

### T6 — ô nhập giữ con số vừa bị từ chối

Phát hiện khi **xem ảnh chụp** sau khi sửa T2: server từ chối `999.000.000₫`, thông báo hiện đúng,
nhưng **ô vẫn hiện con số đó** trong khi cột "Còn thiếu" báo số cũ. Người dùng thấy hai con số mâu
thuẫn.

Nguyên nhân: ô dùng `key={id}-${soTienDaDong}` để dựng lại khi dữ liệu đổi. Nhưng khi bị **từ
chối** thì số tiền trong DB **không đổi** → key không đổi → React giữ nguyên ô. Sửa bằng cách thêm
biến đếm số lần từ chối vào `key`.

## Kế hoạch xử lý — ưu tiên đưa vào dùng được

Sắp theo **tỉ lệ (rủi ro nếu bỏ) / (công sức)**, không theo thứ tự tìm ra.

| Thứ tự | Việc | Ước tính | Vì sao trước |
|:---:|---|---|---|
Còn lại hai việc, cả hai **không chặn** việc dùng nội bộ:

| Thứ tự | Việc | Ước tính | Vì sao |
|:---:|---|---|---|
| **1** | **N3** — rate limit ở Caddy | ~1 giờ | 🔴 **Chặn cứng trước khi mở ra Internet.** Đăng ký CLB đã mở tự do (FR-18) nên script tạo được vô hạn CLB rác, và `POST /moi-qua-link/xem` là endpoint ẩn danh |
| **2** | **N6** — màn Tổng quan | ~2 giờ | 🟢 Không chặn gì, nhưng là màn ĐẦU TIÊN người dùng thấy sau đăng nhập |

**Hệ thống dùng được nội bộ ngay bây giờ.**

### Chưa xét trong lần này

- **Hiệu năng dưới tải** — chưa đo với 50+ CLB, 1000+ trận.
- **Đồng thời** — hai người sửa cùng đợt quỹ, hai người chấp nhận cùng lời mời link.
- **Trình duyệt cũ / mobile thật** — chỉ kiểm Chromium ở 1440px và 1280px.


---

# Rà soát vòng hai — cùng ngày

Vòng một kiểm luồng đơn lẻ. Vòng hai nhắm vào bốn thứ chưa chạm: **đồng thời**, **phân quyền từng
endpoint**, **cách ly tenant qua id trực tiếp**, và biên dữ liệu còn lại.

## Đã kiểm và ĐÚNG

| Nhóm | Kết quả |
|---|---|
| **Cách ly tenant qua id trực tiếp** — 9 đường (GET/PUT/DELETE cầu thủ, trận, quỹ, mẫu đội hình, đóng góp, khoản chi, đội hình) | Tất cả **404**. Global Query Filter kín cả chiều đọc và ghi |
| **Phân quyền** — 16 endpoint ghi với tài khoản chỉ có quyền Xem | Tất cả **403**. Không hở chỗ nào |
| **Thu tiền đồng thời** (10 request cùng một khoản) | `100.000/100.000` — không cộng dồn |
| **Vote MVP đồng thời** (10 request) | 1 phiếu. UNIQUE ở DB làm việc (quy tắc #8) |
| **Trả lời lời mời đăng ký đồng thời** | Mỗi người đúng 1 hàng. UNIQUE `(loi_moi_id, cau_thu_id)` làm việc |

## 🔴 T7 — Ba luồng thiếu UNIQUE, đồng thời tạo bản ghi trùng

**Tái hiện:** 5 request đồng thời, mỗi luồng.

| Luồng | Trước sửa | Đúng |
|---|---:|---:|
| Chấp nhận lời mời link | **5 trận + 5 đối thủ trùng** | 1 |
| Gửi lời mời thách đấu | **5 lời mời đang chờ** | 1 |
| Tạo link mời | **5 link đang chờ** | 1 |

**Nguyên nhân chung:** mọi ràng buộc "chỉ một" được kiểm bằng `AnyAsync`/`FirstOrDefaultAsync` rồi
`Add`. Hai request song song **đều thấy "chưa có"** và đều ghi.

Vote MVP và phản hồi tham gia **không bị** — vì chúng đã có UNIQUE index ở DB. Đó chính là bằng
chứng cách sửa đúng là gì.

**Xử lý:** thêm ba UNIQUE index, mỗi cái có **filter** riêng:

| Index | Filter | Vì sao cần filter |
|---|---|---|
| `UQ_DOI_THU_tenant_ma_doi_he_thong` | `ma_doi_he_thong IS NOT NULL` | Đối thủ tên gõ tay trùng bao nhiêu cũng được — "FC Sông Hàn" của tôi và của bạn là hai đội khác nhau |
| `UQ_LOI_MOI_BAT_DOI_dang_cho` | `trang_thai = 0` | Đá xong rồi mời lại lần sau là hợp lệ |
| `UQ_LOI_MOI_LINK_dang_cho` | `trang_thai = 0 AND thu_hoi_luc IS NULL` | Thu hồi rồi thì gửi lại được |

Thiếu filter thì UNIQUE chặn luôn ca hợp lệ — tệ hơn lỗi ban đầu.

Kèm theo: middleware xử lý `DbUpdateException` với SQLSTATE **23505** → trả **409 `THAO_TAC_TRUNG`**
thay vì 500 "Lỗi hệ thống". Không có nhánh này thì người dùng bấm hai lần sẽ tưởng app hỏng.

**Đã kiểm lại trên PostgreSQL thật sau khi sửa:** cả ba luồng, 5 request đồng thời → **1 bản ghi**.

## Hai lần tôi sai trong vòng này

**Đọc sai ca F.** Tôi thấy "9 phản hồi ThamGia" sau 5 request đồng thời và tưởng là lỗi. Kiểm kỹ:
9 là số **người** trả lời trong bộ dữ liệu mẫu, và mỗi người đúng 1 hàng. Không phải lỗi.

**Đoán sai tên bảng khi dọn dữ liệu.** Viết `QUYEN_CHUCNANG` thay vì `QUYEN_CHUC_NANG` — transaction
rollback, **không mất dữ liệu gì**. Lấy tên thật từ `pg_tables` rồi chạy lại.

## Giới hạn của bộ test

`DongThoiTests` **không mô phỏng được đua thật**: provider InMemory không có UNIQUE index và không
chạy song song ở tầng DB. Nó canh phần kiểm được — ràng buộc **được khai** trong model với filter
đúng, và tầng ứng dụng trả mã lỗi đúng cho request thứ hai.

Việc chặn đua thật chỉ kiểm được bằng tay trên PostgreSQL. Đã làm, kết quả ở trên.

## 🟡 T8 — Quân đối thủ trên bảng chiến thuật hiện dấu "?"

**Tìm thấy khi chạy hệ thống và xem ảnh chụp**, không test nào bắt được.

Bộ dữ liệu mẫu sinh quân đối thủ **không có `so`** (số áo), nên `SoDoSan` rơi về fallback và hiện
dấu `?` trên mọi áo đỏ — bảng chiến thuật trông như dữ liệu lỗi.

**Xử lý:** thêm `so = idx + 1` cho quân đối thủ. **Không** lật `y` ở dữ liệu — `SoDoSan` tự lật khi
vẽ (`yHienThi = 100 - y` cho đối thủ), lật sẵn sẽ nhân đôi phép lật và hai thủ môn lại chồng nhau.

Đã kiểm lại bằng ảnh: số áo hiện hết, thủ môn đối thủ ở đầu sân, thủ môn nhà ở đáy sân — hai đội
đối diện nhau đúng.

## Chưa xét sau hai vòng

- **Hiệu năng dưới tải** — chưa đo với 50+ CLB, 1000+ trận.
- **Trình duyệt cũ / mobile thật** — chỉ Chromium ở 1280–1440px.
- **Khôi phục sau sự cố** — chưa thử restore từ backup.
