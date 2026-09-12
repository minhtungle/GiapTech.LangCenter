# 2026-09-12 — Thử đổi toàn bộ sang tiếng Anh, và vì sao phải hoàn nguyên

Ngày này có hai yêu cầu: **bốn cột audit** trên mọi bảng, và **nhất quán tiếng Anh** toàn hệ
thống. Cái đầu xong gọn. Cái sau đi được 60% rồi phải quay lại — đây là nhật ký của lần đó.

## Phần đã xong: bốn cột audit

Đo trước khi làm:

| Cột | Phạm vi cũ |
|---|---|
| `ngay_tao`, `ngay_cap_nhat` | 37/37 bảng |
| `nguoi_tao_id` | **4**/37 — thêm lẻ tẻ, không theo chuẩn |
| người sửa | **0**/37 |

Đưa cả bốn vào `BaseEntity`, `AppDbContext` tự gán — cùng cách đã dùng cho `TenantId`. Chỗ nào để
handler tự điền là chỗ sẽ có người quên.

### Chi tiết dễ bỏ sót: `CreatedById` không được đổi khi sửa

```csharp
case EntityState.Modified:
    entry.Entity.UpdatedById = nguoiHienTai;
    entry.Property(nameof(BaseEntity.CreatedById)).IsModified = false;
```

Thiếu dòng thứ hai thì **người sửa âm thầm trở thành người tạo**, và không có gì báo vì cả hai
đều là `Guid` hợp lệ.

### Test suýt vô dụng

Viết test canh đúng chỗ đó, chạy xanh. Tiêm đột biến (bỏ hẳn dòng `IsModified = false`) — **test
vẫn xanh**.

Lý do: test tạo và sửa bằng **cùng một người**, nên `CreatedById` bị ghi đè bằng đúng giá trị cũ.
Phải tạo người thứ hai và `Assert.NotEqual(taoBoi, suaBoi)` thì đột biến mới đỏ.

Nếu không kiểm đột biến, tôi đã giao một test vô dụng ở đúng chỗ nguy hiểm nhất.

## Phần thất bại: đổi tên tiếng Anh

### Đã đi được tới đâu

Chuẩn bị kỹ: từ điển thuật ngữ đối chiếu **khớp 100%** với 37 bảng, 149 cột, 24 hằng thật
(không thiếu, không thừa). Backup DB. **Diễn tập trên bản sao** trước — 37/37 bảng khớp số dòng,
157 index và 71 FK còn nguyên.

Rồi chạy thật: DB đổi xong, 37 entity + 33 `DbSet` + 24 hằng đổi xong, build sạch, API khởi động
được, đăng nhập chạy.

Và dừng ở **148/376 test đỏ**, **108 tên trường JSON** frontend chưa sửa.

### Sai lầm 1: tưởng "đổi DB" và "đổi code" là hai bước

Kế hoạch 5 bước, bước 2 = rename bảng/cột, bước 3 = đổi property C#. Nghe hợp lý — làm bước 2,
kiểm, rồi bước 3.

**Không tách được.** `UseSnakeCaseNamingConvention()` suy tên cột **TỪ** tên property. Cột
`role_name` buộc property phải là `RoleName`. Làm nửa vời thì EF sinh SQL trỏ cột không tồn tại:

```
42703: column r.ten_quyen does not exist
```

API không khởi động nổi. Và property đổi → **tên trường JSON đổi** → mọi test và toàn bộ
frontend phải sửa theo. Ba việc này là **một**.

Tôi chỉ phát hiện khi chạy thật, sau khi đã báo cáo "bước 2 xong".

### Sai lầm 2: regex hàng loạt trên 100.000 dòng

Ba lỗi **ngữ nghĩa** mà trình biên dịch không thấy:

| Lỗi | Vì sao lọt |
|---|---|
| `SoTaiKhoan` trong `QuyenDto` (ĐẾM tài khoản) → `BankAccountNo` (số TK ngân hàng) | Cùng tên, khác nghĩa ở hai chỗ. Build xanh, nghĩa sai hoàn toàn |
| `HanhDong` → `Action` | Đụng `System.Action` ở mọi delegate — kể cả file không liên quan |
| Chuỗi `"NguoiDung"` trong `SuyChucNang()` → `"Person"` | Đó là **chuỗi khớp tên lệnh**, mà tên lệnh (`TaoNguoiDungCommand`) không đổi |

Lỗi thứ ba đáng sợ nhất: nhật ký mất trường chức năng, build xanh, và **chỉ đúng một test** bắt
được. Nếu test đó không tồn tại thì lỗi đã đi vào production.

Bài học: **regex không phân biệt được "định danh cần đổi" với "chuỗi tham chiếu tới định danh
khác"**. Chuỗi văn bản phải xem bằng mắt.

### Vì sao chọn hoàn nguyên thay vì làm tiếp

Chủ sản phẩm chọn A (hoàn nguyên) sau khi nghe hai đường. Lý lẽ: còn 148 test + 108 tên trường
phải sửa, và **mỗi lượt regex tiếp theo là một cơ hội sinh thêm lỗi ngữ nghĩa** như ba cái trên
— loại lỗi không có gì bắt được ngoài đọc bằng mắt.

## Hoàn nguyên: làm ngược thứ tự

Điểm cần cẩn thận: migration rename **đã áp lên DB**, nên phải rollback DB **trước**, rồi mới
hoàn nguyên code — làm ngược lại thì mất file migration cần cho lệnh rollback.

1. Backup hiện trạng (254K)
2. `git stash -u` công việc dở — lấy lại được nếu đổi ý
3. Sinh SQL **đảo chiều** từ chính từ điển đã dùng
4. **Diễn tập trên bản sao** — 0 bảng tiếng Anh còn sót, 5/5 bảng tiếng Việt về đúng
5. Chạy thật, `DELETE` migration khỏi `__EFMigrationsHistory`
6. `git checkout -- . && git clean -fd`

Kết quả: 442 test xanh, dữ liệu `W686AE9` khớp từng bảng (8 người, 1 lớp, 3 khoá, 166 quyền).

## Sản phẩm còn lại của ngày

Không phải công cốc:

- **Bốn cột audit** đang chạy trên 37/37 bảng
- **[ADR-0006](../kien-truc/adr/0006-dat-ten-tieng-anh-va-cot-audit.md)** với từ điển thuật ngữ
  đã đối chiếu khớp 100% dữ liệu thật — ai làm lại thì bắt đầu từ đó
- **[quy-uoc-code.md](../quy-uoc-code.md)** — quy ước viết mã cho toàn dự án, mục 9 ghi hẳn
  "đổi tên hàng loạt: đọc trước khi làm"

## Bài học

**Một kế hoạch chia bước chỉ đúng nếu các bước thật sự độc lập.** Tôi chia 5 bước mà không kiểm
xem bước 2 và 3 có tách rời được không — chúng bị buộc vào nhau bởi một dòng cấu hình
(`UseSnakeCaseNamingConvention`) mà tôi biết là có nhưng không nghĩ tới hệ quả.

Và: **"build xanh + test xanh" không đủ để nói một lần đổi tên là an toàn.** Ba lỗi tệ nhất hôm
nay đều đi qua được cả hai cổng đó. Thứ bắt được chúng là chạy hệ thống thật và đọc từng chuỗi
bằng mắt.
