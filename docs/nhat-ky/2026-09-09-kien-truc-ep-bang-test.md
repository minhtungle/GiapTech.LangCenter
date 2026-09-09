# 2026-09-09 (Kiến trúc) — Ép quy tắc bằng test, không đổi sang ABP

## Câu hỏi

Chủ sản phẩm: *"mã nguồn phải tuân theo 1 bộ kiến trúc (VD: abp boilerplate) để có sự chặt chẽ,
chính xác chứ không được không có quy tắc"*.

## Vì sao KHÔNG chuyển sang ABP

| | |
|---|---|
| **Trùng lặp** | ABP có multi-tenant, phân quyền động, audit log — dự án đã tự làm cả ba, đã kiểm chứng, đã có test canh |
| **Xung đột thiết kế** | ABP dùng Identity đầy đủ, giả định **username duy nhất toàn cục**. Dự án cố tình không dùng vì trái multi-tenant (`UNIQUE(tenant_id, username)`) — và có test canh đúng chiều ngược đó |
| **Chi phí** | Viết lại ~14.400 dòng theo convention của người khác, trong khi không có vấn đề nào cần giải |
| **Đánh mất** | 380 test đang xanh, phần lớn viết ra *vì đã gặp lỗi thật* |

ABP đúng khi bắt đầu từ đầu. Dự án đang ở giữa với hạ tầng đã chạy.

Điều quan trọng hơn: **"chặt chẽ" không đến từ boilerplate mà từ việc ép**. Boilerplate không
ngăn ai viết sai; test đỏ thì ngăn.

## Đã có bao nhiêu, đo thật

11 quy tắc bất di bất dịch, và **3 test canh** (trước hôm nay). Nâng lên **6**.

## Chỗ thật sự còn hở — tìm bằng cách đếm

```
122 endpoint · 115 có [RequirePermission] · 6 có [AllowAnonymous] · 4 KHÔNG CÓ GÌ
```

Bốn cái đó: `AuthController.DoiMatKhau`, `ToiController.{CauHinh,HeThongs,Quyen}`. **Cả bốn đều
đúng** — chỉ trả/đổi dữ liệu của chính người gọi, nên `ICurrentUser` đã là lớp giới hạn. Nhưng
**không có gì ép**: thêm một endpoint quên gác thì lọt qua im lặng, mà đó là lỗi nghiêm trọng
nhất (ai đăng nhập cũng gọi được, kể cả học viên gọi endpoint quản trị).

## Ba test mới, cùng một khuôn

Khuôn lấy từ `CachLyTenantTests`: **danh sách ngoại lệ có khai lý do** + **test chiều ngược** để
danh sách không lạc hậu. Ai vi phạm phải dừng lại viết ra lý do, hoặc nhận ra mình quên.

1. **`MoiEndpointPhaiDuocGacTests`** (3 test) — reflection quét mọi action; tính cả attribute đặt
   ở **cấp controller**, không chỉ trên action. Chốt luôn số endpoint ẩn danh (6).
2. **`RanhGioiHeThongConTests`** (2 test) — quét **mã nguồn** bằng regex chứ không reflection: sau
   khi biên dịch thì "namespace nào gọi namespace nào" tan vào IL, không phân biệt được `Crm` gọi
   `DaoTao` với chiều ngược. Hiện đúng một cầu nối: FR-21.
3. **`MoiEntityPhaiCoConfigTests`** (2 test) — kiểm qua **tên bảng `SNAKE_CASE`**, vì đó là dấu
   hiệu tin cậy cho "đã đi qua `IEntityTypeConfiguration`" (EF mặc định lấy tên `DbSet` là
   `LopHocs`, có chữ thường).

Đã kiểm bằng **đột biến mã**, không tin test xanh là đủ:

| Đột biến | Kết quả |
|---|---|
| Bỏ `[RequirePermission]` khỏi `PhongBanController.Cay` | ❌ đỏ, nêu đúng tên `PhongBanController.Cay` |
| Cho `NhanSu` `using` sang `DaoTao.LopHoc` | ❌ đỏ, nêu đúng `PhongBanDtos.cs → DaoTao` |

## Phát hiện ngoài dự kiến: 9 cột `text` vô hạn

Test #3 đỏ ngay lần chạy đầu — không phải test sai mà **schema sai thật**. Kiểm trong DB:

```
NGUOI_DUNG.anh_dai_dien_url | text | ∞      TENANT.logo_url    | text | ∞
NGUOI_DUNG.dia_chi          | text | ∞      TENANT.anh_bia_url | text | ∞
QUYEN.mo_ta                 | text | ∞      TENANT.anh_qr_url  | text | ∞
TAI_KHOAN.password_hash     | text | ∞      NHAT_KY.chi_tiet   | text | ∞
                                            NHAT_KY.tham_so    | text | ∞
```

Không test nào bắt được trước đó: build xanh, migration sinh bình thường, chỉ schema là sai. Hậu
quả im lặng — một trường ghi chú nhận được 10 MB văn bản, và `MaximumLength` mà FluentValidation
kiểm ở tầng ứng dụng **không có ràng buộc DB nào khớp**.

Xử lý: giới hạn 7 cột, **miễn 2 cột JSON** của nhật ký (độ dài không đoán trước, cắt bớt là mất
bằng chứng mà nhật ký chỉ ghi thêm không sửa).

### Làm hẹp cột là quy tắc #1 — đo trước, hỏi trước

`AlterColumn` từ `text` sang `varchar(n)` là **làm hẹp**. Đã đo trước khi hỏi: dữ liệu dài nhất
**84 ký tự** (`password_hash`), xa dưới mọi giới hạn dự kiến. Hỏi chủ sản phẩm rồi mới làm.

Đối chiếu sau khi áp: 41 tenant · 57 tài khoản · 166 quyền · 64 người dùng — **không đổi**. Và
kiểm cả hai chiều của `password_hash`: đăng nhập bằng hash **cũ** OK, tạo trung tâm mới (ghi hash
**mới**) OK.

## Kèm ADR-0005

Ghi lại hai quyết định của hôm nay để lần sau khỏi tranh luận từ đầu: **một source** (không tách
ba theo HRM/CRM/LMS) và **đổi tên** về `GiapTech.LangCenter`, kèm số đo và ba dấu hiệu xét lại.

## Còn nợ

- Ranh giới `RanhGioiHeThongConTests` chỉ quét `Application/`. Tầng `API/Controllers` vẫn gọi
  chéo tự do (`LopHocController` gọi `Application.Crm` cho FR-21) — đúng vì controller là chỗ
  ghép, nhưng nếu muốn siết thì cần khai danh sách tương tự.
- Chưa canh: mỗi `Command` phải có `Validator` tương ứng. Quên validator thì dữ liệu rác vào DB
  mà không lỗi nào.
