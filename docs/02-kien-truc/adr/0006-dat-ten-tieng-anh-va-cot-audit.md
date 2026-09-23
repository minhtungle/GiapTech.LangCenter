# ADR-0006: Đặt tên bằng tiếng Anh toàn hệ thống, và bốn cột audit chuẩn

- **Ngày:** 12/09/2026
- **Trạng thái:** Cột audit **đã làm xong**; đổi tên tiếng Anh **đã chốt nhưng HOÃN thi hành**
  (xem "Lần thử 12/09/2026 và vì sao dừng" ở cuối)
- **Bối cảnh liên quan:** [ADR-0005](0005-mot-source-va-doi-ten-langcenter.md) (một source),
  [quy-uoc-migration.md](../../05-database/quy-uoc-migration.md) (quy ước cũ — bị ADR này thay thế)

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
| `nguoi_tao_id` | **4/37 bảng** — thêm lẻ tẻ khi có nhu cầu, không theo chuẩn. **Gộp vào `created_by_id` ngày 13/09/2026** (xem mục bổ sung cuối ADR) |
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
- Từ điển thuật ngữ đầy đủ: [THUAT-NGU.md](../../01-tong-quan/thuat-ngu.md).
- Thêm test canh: entity mới phải có đủ 4 cột audit; hằng `ChucNang` khớp dữ liệu DB.
- 21 migration cũ **không sửa lại** (đã áp) — lịch sử migration sẽ lẫn hai hệ tên. Chấp nhận:
  sửa lại migration đã áp là rủi ro lớn hơn nhiều so với một lần đọc khó.

---

## Lần thử 12/09/2026 và vì sao dừng

Phần **cột audit** đã hoàn thành và đang chạy. Phần **đổi tên** đã thử một lượt rồi **hoàn
nguyên** — ghi lại đây để lần sau không lặp lại cùng sai lầm.

### Đã đi được tới đâu

Đổi xong tên **37 bảng + 149 cột** trên DB thật (dữ liệu nguyên vẹn 100%, đối chiếu từng bảng),
**37 entity C#**, **33 `DbSet`**, **24 hằng `ChucNang`** kèm `UPDATE` dữ liệu. Build sạch, API
khởi động được, đăng nhập chạy.

Rồi dừng ở **148/376 test đỏ** và **108 tên trường JSON** frontend chưa sửa.

### Sai lầm 1: tưởng tách được "đổi DB" khỏi "đổi code"

Kế hoạch chia 5 bước, bước 2 = "rename bảng/cột", bước 3 = "đổi property C#". **Không tách
được**: `UseSnakeCaseNamingConvention()` suy tên cột **từ** tên property, nên cột `role_name`
buộc property phải là `RoleName`. Đổi một nửa thì EF sinh SQL trỏ cột không tồn tại — API không
khởi động nổi.

Và property đổi → **tên trường JSON đổi** → mọi test và toàn bộ frontend phải sửa theo. Ba việc
này là **một**, không phải ba bước.

### Sai lầm 2: regex hàng loạt trên 100.000 dòng

Ba lỗi **ngữ nghĩa** mà trình biên dịch không thấy:

| Lỗi | Hậu quả |
|---|---|
| `SoTaiKhoan` trong `QuyenDto` (ĐẾM tài khoản) → `BankAccountNo` (số TK ngân hàng) | Sai nghĩa hoàn toàn, build xanh |
| `HanhDong` → `Action` | Đụng `System.Action`, hỏng cả delegate không liên quan |
| Chuỗi `"NguoiDung"` trong `SuyChucNang()` → `"Person"` | Nhật ký mất trường chức năng — **chỉ 1 test bắt được** |

Lỗi thứ ba đáng sợ nhất: chuỗi đó khớp với **tên lệnh** (`TaoNguoiDungCommand`), mà tên lệnh là
class CQRS — ADR này không đổi chúng. Regex không phân biệt được "định danh cần đổi" với "chuỗi
tham chiếu tới định danh khác".

### Cách làm cho lần sau

1. **Theo từng module nhỏ**, không phải 37 bảng cùng lúc: một bảng + entity + DTO + frontend của
   nó + test của nó → commit → sang bảng kế. Mỗi commit hệ thống vẫn chạy được.
2. **Để test dẫn đường**: chạy test, sửa đúng chỗ đỏ, lặp lại. Không regex rồi mới chạy test.
3. **Rà chuỗi văn bản riêng**: mọi `"..."` chứa tên định danh phải xem bằng mắt — chúng có thể
   trỏ tới tên lệnh, khoá i18n, hay giá trị dữ liệu.
4. Tên trường JSON là **hợp đồng API**. Đổi nó là breaking change; phải sửa frontend trong cùng
   commit, nếu không hệ thống hỏng lúc chạy mà build vẫn xanh.

### Việc còn nợ

Từ điển thuật ngữ ở trên **vẫn dùng được** — nó đã đối chiếu khớp 100% với 37 bảng, 149 cột và
24 hằng thật. Ai làm tiếp thì bắt đầu từ đó, theo cách ở mục trên.

---

## Bổ sung 13/09/2026 — gộp cột trùng nghĩa và thêm khoá ngoại

Lần thêm 4 cột audit (12/09) bỏ sót hai việc, phát hiện khi chủ sản phẩm hỏi về khách vãng lai.

### 1. Bốn bảng có HAI cột cùng nghĩa

`LOP_HOC`, `KHACH_HANG`, `BAI_TAP`, `BAI_KIEM_TRA` đã có sẵn `nguoi_tao_id` từ trước; thêm
`created_by_id` vào `BaseEntity` khiến chúng có cả hai. Cả hai đều gán `ICurrentUser.UserId`,
cùng trỏ `NGUOI_DUNG` — **cùng nghĩa, không phải hai câu hỏi khác nhau**.

Hai nguồn sự thật cho một câu hỏi thì sớm muộn cũng lệch. Đã gộp về `created_by_id`, chuyển dữ
liệu bằng `COALESCE(created_by_id, nguoi_tao_id)` — ưu tiên giá trị mới hơn nếu cả hai cùng có.

### 2. Cột audit KHÔNG có khoá ngoại

Nghiêm trọng hơn. Chú thích `BaseEntity` viết *"trỏ `PERSON.id`"* nhưng **không gì ép điều đó**:
`created_by_id` là `uuid` trần, chứa được GUID rác hoặc trỏ người đã bị xoá. Trong khi
`nguoi_tao_id` — cột cũ mà nó thay thế — **có** FK `SET NULL`. Gộp mà không thêm FK là bước lùi.

Nay áp FK cho **cả hai cột trên mọi entity** ở `AppDbContext.ApDungKhoaNgoaiChoCotAudit` (74 FK),
`SET NULL` khi người bị xoá. Không `CASCADE`: xoá một người không được kéo theo mọi bản ghi họ
từng tạo.

> **Dấu vết audit sai còn tệ hơn không có dấu vết** — người đọc tin vào nó.

### Hai cái bẫy khi khai FK toàn cục

| Bẫy | Triệu chứng | Cách đúng |
|---|---|---|
| `HasOne(typeof(NguoiDung))` | EF không biết quan hệ đi qua property nào → tạo **cột bóng** `CreatedById1` bên cạnh cột thật. **108 cột rác** trong snapshot | `HasOne(nameof(BaseEntity.CreatedBy))` — khai theo tên navigation |
| `NGUOI_DUNG` tự tham chiếu | Hai navigation cùng trỏ `NguoiDung` → EF tưởng là hai đầu của một quan hệ 1-1, ném *"dependent side could not be determined"* | Khai hai quan hệ tách rời, mỗi cái một `HasForeignKey` |

Cả hai đều chỉ lộ ra khi **sinh migration**, không phải lúc build — `dotnet build` xanh cả hai lần.
