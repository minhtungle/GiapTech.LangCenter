# ADR-0006: Đặt tên bằng tiếng Anh toàn hệ thống, và bốn cột audit chuẩn

- **Ngày:** 12/09/2026
- **Trạng thái:** Đã chốt
- **Bối cảnh liên quan:** [ADR-0005](./0005-mot-source-va-doi-ten-langcenter.md) (một source),
  [quy-uoc-migration.md](../../database/quy-uoc-migration.md) (quy ước cũ — bị ADR này thay thế)

## Bối cảnh

Hai yêu cầu của chủ sản phẩm, đến cùng một phiên và chạm cùng một tập tệp:

1. **Nhất quán tiếng Anh** cho tên bảng, cột, lớp, hàm, biến — lý do: *chuẩn nghề nghiệp, dự
   định open-source*.
2. **Bốn cột audit** (`người tạo · người sửa · ngày tạo · ngày sửa`) trên mọi bảng để tra được
   lịch sử dữ liệu khi cần.

Đo trên codebase ngày 12/09/2026:

| | Số lượng |
|---|---|
| Định danh tiếng Việt trong C# | **452 / 534** class·record·interface |
| Bảng · cột DB | **37 bảng · 391 cột** |
| Mã nguồn | ~82.000 dòng C# · 17.600 dòng TS |
| Tài liệu dẫn chiếu tên Việt | 34 file |
| Migration đã áp | 21 |

Trạng thái audit trước ADR này:

| Cột | Phạm vi |
|---|---|
| `ngay_tao` · `ngay_cap_nhat` | **37/37 bảng** — ở `BaseEntity`, `AppDbContext` tự gán |
| `nguoi_tao_id` | **4/37 bảng** — thêm lẻ tẻ khi có nhu cầu, không theo chuẩn |
| người sửa | **0/37** |

## Quyết định

### 1. Toàn bộ định danh sang tiếng Anh, **gồm cả tên bảng và cột DB**

Thay thế quy ước cũ *"tiếng Việt không dấu"* trong `quy-uoc-migration.md`.

Giữ nguyên `UPPER_SNAKE_CASE` cho bảng, `lower_snake_case` cho cột, `PascalCase` cho entity C# —
chỉ đổi **ngôn ngữ**, không đổi **kiểu chữ**.

**Route frontend giữ tiếng Việt** (`/lms/hoc-vien`): đó là thứ người dùng Việt Nam thấy trên
thanh địa chỉ. Đổi sang `/lms/students` không phục vụ mục tiêu open-source mà lại phá bookmark
và link đã gửi. Ranh giới: **mã nguồn tiếng Anh, giao diện và URL tiếng Việt.**

Chuỗi hiển thị và bản dịch i18n **không đổi** — chúng vốn đã là dữ liệu, không phải định danh.

### 2. Bốn cột audit vào `BaseEntity`, tự gán ở `AppDbContext`

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? CreatedById { get; set; }   // mới
    public Guid? UpdatedById { get; set; }   // mới
}
```

Tự gán giống hai cột thời gian đã làm: không handler nào phải nhớ, nên không có chỗ để quên.

**Nullable, không NOT NULL.** Ba lý do: hàng đã có từ trước không truy ngược được ai tạo; lệnh
chạy bởi hệ thống (seeder, job) không có người dùng; và người tạo có thể đã bị xoá khỏi hệ thống.

**FK `SetNull`**: nhân viên nghỉ việc bị xoá thì dữ liệu nghiệp vụ **không** được biến mất theo.

### 3. Bốn cột này KHÔNG thay thế `AUDIT_LOG`

Hai cơ chế trả lời hai câu hỏi khác nhau — giữ cả hai:

| | Cột trên bảng | `AUDIT_LOG` (FR-16) |
|---|---|---|
| "Ai sửa **lần cuối**" | 1 truy vấn, hiện được trên UI | phải quét log |
| "**Toàn bộ** lịch sử sửa" | không (chỉ giữ lần cuối) | có, kèm trường nào đổi |

Cột là **ảnh chụp hiện tại**; log là **dòng thời gian**. Bỏ log để dùng cột sẽ mất lịch sử; bỏ
cột để dùng log thì mỗi màn hình phải join vào bảng lớn nhất hệ thống.

## Vì sao đổi tên bây giờ chứ không sau

- **VPS chưa triển khai.** Rename bảng trên production là việc phải lên lịch bảo trì, thông báo
  người dùng, và có kế hoạch khôi phục. Bây giờ chỉ có một DB dev.
- **Chỉ 21 migration.** Con số này chỉ tăng.
- Mỗi tháng chờ thêm là thêm vài nghìn dòng phải sửa.

## Ba chỗ nguy hiểm, xử lý riêng

### Hằng số phân quyền được LƯU TRONG DATABASE

`ChucNang.LopHoc = "LopHoc"` không chỉ là hằng số C# — chuỗi đó nằm trong cột
`QUYEN_CHUC_NANG.ten_chuc_nang` của dữ liệu đang chạy.

Đổi hằng mà không migrate dữ liệu = **mất sạch phân quyền của mọi trung tâm**. Nguy hơn rename
bảng, vì rename sai thì lỗi nổ ngay còn cái này thì hệ thống vẫn chạy và âm thầm từ chối mọi thao
tác.

→ Migration phải `UPDATE` cả dữ liệu, không chỉ `ALTER`. Có test canh giá trị hằng khớp dữ liệu.

### Tên sai sẵn được sửa luôn

- `NGUOIDUNG_QUYEN` thực chất nối **`TAI_KHOAN`** ↔ `QUYEN`, không phải `NGUOI_DUNG` →
  `ACCOUNT_ROLE`.
- `DANG_KY_KHOA_HOC` nay chứa **cả sản phẩm** (nợ N17) → `SALES_ORDER`. Đóng luôn nợ đó.

### Hai từ không dịch thẳng được

- `NGUOI_DUNG` → **`PERSON`**, không phải `User`: con người sống lâu hơn tài khoản, và `USER_ACCOUNT`
  mới là thứ đăng nhập. Dịch cả hai thành `User*` sẽ tái tạo đúng sự nhầm lẫn đã phải tách ra
  ngày 07/09/2026.
- `LOP_HOC` → **`CLASS_SECTION`**, không phải `Class`: `class` là từ khoá của **cả C# lẫn
  TypeScript**. `CourseClass` cũng bị loại vì gây tưởng lớp thuộc về đúng một khoá — thực tế một
  lớp dạy tới 3 khoá.

## Phương án đã loại

**Chỉ đổi mã C#, giữ tên bảng tiếng Việt.** Rẻ hơn và không đụng dữ liệu, nhưng tạo một tầng dịch
ngầm vĩnh viễn: `entity.ToTable("LOP_HOC")` cho class `ClassSection`. Người mới phải học hai bộ
từ vựng, và mọi truy vấn SQL tay vẫn bằng tiếng Việt — trái mục tiêu open-source.

**Đổi dần từng module.** Nghe an toàn hơn nhưng thực tế tệ hơn: có giai đoạn dài codebase lẫn hai
thứ tiếng, và mỗi PR trong giai đoạn đó phải quyết định dùng hệ nào.

## Hệ quả

- `quy-uoc-migration.md` viết lại phần quy ước đặt tên.
- Từ điển thuật ngữ đầy đủ: [THUAT-NGU.md](../THUAT-NGU.md).
- Thêm test canh: entity mới phải có đủ 4 cột audit; hằng `ChucNang` khớp dữ liệu DB.
- 21 migration cũ **không sửa lại** (đã áp) — lịch sử migration sẽ lẫn hai hệ tên. Chấp nhận:
  sửa lại migration đã áp là rủi ro lớn hơn nhiều so với một lần đọc khó.
