# Quy ước viết mã (coding convention)

> Áp dụng cho **toàn bộ** dự án: C#, TypeScript, SQL, tài liệu.
> Chỗ nào mâu thuẫn với tài liệu khác thì file này thắng, trừ [11 quy tắc bất di bất
> dịch](../CLAUDE.md#2-mười-một-quy-tắc-bất-di-bất-dịch).

---

## 1. Ngôn ngữ đặt tên

Đây là câu hỏi hay gây tranh cãi nhất, nên ghi rõ **ranh giới**:

| Chỗ | Ngôn ngữ | Ví dụ |
|---|---|---|
| Tên bảng, cột DB | **Tiếng Việt không dấu** | `LOP_HOC`, `hoc_phi_ap_dung` |
| Entity, DTO, Command, Query, property C# | **Tiếng Việt không dấu** | `LopHoc`, `HocPhiApDung` |
| Biến, hàm, component TypeScript | **Tiếng Việt không dấu** | `hocVienChon`, `layMaLoi()` |
| Tên **migration** | **Tiếng Anh** | `ThemCotAudit`, `AddLopHocKhoaHoc` |
| Từ vựng **kỹ thuật** | **Tiếng Anh** | `Repository`, `Handler`, `Dto`, `Id`, `Url` |
| Chuỗi hiển thị | **Khoá i18n**, không hard-code | `t('lopHoc.themHocVien')` |
| Route frontend | **Tiếng Việt không dấu** | `/lms/hoc-vien` |
| Endpoint API | **Tiếng Việt không dấu, gạch ngang** | `/api/v1/lop-hoc/cho-xep-lop` |

> **Đã cân nhắc chuyển toàn bộ sang tiếng Anh** ([ADR-0006](./kien-truc/adr/0006-dat-ten-tieng-anh-va-cot-audit.md))
> vì mục tiêu open-source. Đã thử một lượt 12/09/2026 và **hoàn nguyên** — xem mục "Lần thử và vì
> sao dừng" trong ADR đó trước khi định làm lại. Quy ước hiện hành vẫn là **tiếng Việt không dấu**.

**Không trộn hai thứ tiếng trong một định danh.** `LayDanhSachChoXepLopQuery` là tiếng Việt +
hậu tố kỹ thuật `Query` — hợp lệ. `GetDanhSachLopHoc` là trộn — không hợp lệ.

---

## 2. Kiểu chữ

| Đối tượng | Kiểu | Ví dụ |
|---|---|---|
| Bảng DB | `UPPER_SNAKE_CASE` | `LOP_HOC_HOC_VIEN` |
| Cột DB | `lower_snake_case` | `hoc_phi_ap_dung` |
| Khoá chính | luôn là `id` | `id` |
| Khoá ngoại | `<bảng_đích_số_ít>_id` | `lop_hoc_id`, `hoc_vien_id` |
| Class, record, interface, property C# | `PascalCase` | `LopHocDto`, `HocPhi` |
| Interface | tiền tố `I` | `IPhamViLopHoc` |
| Biến cục bộ, tham số C# | `camelCase` | `lopHocId`, `ct` |
| Hằng số C# | `PascalCase`, không `SCREAMING_CASE` | `ChucNang.LopHoc` |
| Component React | `PascalCase` | `ChoXepLop.tsx` |
| Hook, hàm, biến TS | `camelCase` | `useQuyen`, `duocSuaLop` |
| Hằng TS dùng chung | `UPPER_SNAKE_CASE` | `CAC_HINH_THUC`, `TOI_DA_KHOA` |
| Trường JSON API | `camelCase` (tự động) | `hocPhiApDung` |

Ánh xạ C# ↔ cột DB do `UseSnakeCaseNamingConvention()` lo — **không rải `[Column]`** khắp Domain
(quy tắc #10). Cần tên khác quy ước thì `HasColumnName` trong `IEntityTypeConfiguration`.

> ⚠️ Convention này suy tên cột **từ** tên property. Đổi tên property = **đổi tên cột** = cần
> migration. Đây là chỗ đã làm hỏng một lần (ADR-0006).

---

## 3. Đặt tên theo tầng

### Domain

Danh từ thuần, không hậu tố: `LopHoc`, `NguoiDung`, `KhoanThuHocPhi`.

Entity nghiệp vụ **bắt buộc** kế thừa `TenantEntity` (quy tắc #2). Bốn cột audit
(`CreatedAt`/`UpdatedAt`/`CreatedById`/`UpdatedById`) có sẵn từ `BaseEntity`, `AppDbContext`
tự gán — đừng gán tay.

### Application (CQRS)

| Loại | Khuôn | Ví dụ |
|---|---|---|
| Query | `Lay<Gì>Query` | `LayDanhSachChoXepLopQuery` |
| Command tạo | `Tao<Gì>Command` | `TaoLopHocCommand` |
| Command sửa | `CapNhat<Gì>Command` | `CapNhatLopHocCommand` |
| Command lưu (tạo **hoặc** sửa) | `Luu<Gì>Command` | `LuuKhachHangCommand` |
| Command xoá | `Xoa<Gì>Command` | `XoaKhoaHocCommand` |
| Handler | `<TênLệnh>Handler` | `TaoLopHocHandler` |
| Validator | `<TênLệnh>Validator` | `TaoLopHocValidator` |
| DTO | `<Gì>Dto` | `LopHocDto`, `HocVienTrongLopDto` |

**Tên lệnh không được đổi tuỳ tiện**: `GhiNhatKy.SuyChucNang()` khớp chức năng phân quyền bằng
**chuỗi con của tên lệnh**. Đổi `TaoNguoiDungCommand` → nhật ký mất trường chức năng, mà build
vẫn xanh.

### Frontend

| Loại | Khuôn |
|---|---|
| Trang | `PascalCase.tsx` theo màn hình — `ChoXepLop.tsx` |
| Kiểu dùng chung | `<mien>Types.ts` — `lopHocTypes.ts`, `crmTypes.ts` |
| Query key | mảng, phần tử đầu là tài nguyên — `['lop-hoc', id, 'hoc-vien']` |

---

## 4. Quy ước `null` trong lệnh cập nhật

**Quan trọng nhất trong tài liệu này.** Quy tắc #1 đã bị vi phạm hai lần vì chỗ này.

| Giá trị gửi lên | Nghĩa |
|---|---|
| `null` | **KHÔNG gửi** → giữ nguyên giá trị hiện có |
| `""` (chuỗi rỗng) | **Chủ động xoá** → ghi `null` xuống DB |
| Danh sách rỗng `[]` | **Chủ động bỏ hết** |
| Cờ riêng (`BoGioiHanSucChua`) | Khi `null` đã mang nghĩa khác |

Hệ quả bắt buộc:

- Trường nào lệnh cập nhật ghi đè thì **phải** có trong DTO trả về **và** trong form.
- Form ẩn một ô → **không gửi trường đó**, đừng gửi `null` cứng.
- Danh sách con (`LIEN_KET_MXH`, `LOP_HOC_KHOA_HOC`): `.Include()` trước `RemoveRange` — thiếu
  `Include` thì `RemoveRange` thành **no-op im lặng**.

---

## 5. Chú thích

Chú thích trả lời **"vì sao"**, không phải "làm gì" — diff đã nói "làm gì".

```csharp
// TỐT: nói vì sao, và hậu quả nếu làm khác
// Đếm TRƯỚC khi phân trang, sau khi lọc — nếu không thanh phân trang báo sai số trang.
var tong = await q.CountAsync(ct);

// XẤU: lặp lại điều code đã nói
// Đếm tổng số dòng
var tong = await q.CountAsync(ct);
```

Ba loại chú thích **đáng viết**:

1. **Bẫy đã sập**: `// Thiếu Include thì RemoveRange thành no-op im lặng — lỗi đã gặp 10/09/2026.`
2. **Phương án đã loại**: `// Không dùng EF.Functions.ILike: đó là hàm Npgsql, Application không được phụ thuộc provider (quy tắc #10).`
3. **Ràng buộc không hiển nhiên**: `// UserId (con người) chứ không TaiKhoanId — lẫn hai thứ này trả rỗng im lặng.`

Ghi **ngày** khi nhắc một quyết định (`chốt 12/09/2026`) để người đọc biết nó còn mới không.

---

## 6. Test

| Loại | Ở đâu | Canh gì |
|---|---|---|
| Unit | `Application.UnitTests` | Hàm thuần, luật kiến trúc |
| Integration | `API.IntegrationTests` | Endpoint, phân quyền, cách ly tenant |
| E2E | `frontend/e2e` | Luồng người dùng thật trên trình duyệt |

Tên test: `Cau_mo_ta_hanh_vi_bang_tieng_Viet`. Đọc tên là biết nó canh gì —
`Duyet_lech_khoa_hoc_thi_canh_bao_nhung_van_cho_phep_khi_dong_y`.

### Ba yêu cầu bắt buộc

1. **Kiểm chiều ngược.** Test "giáo viên KHÔNG thấy tiền" phải đi kèm "admin THẤY tiền" — thiếu
   nó thì trả `null` cho mọi người cũng xanh, và tính năng coi như không tồn tại.
2. **Chứng minh bằng đột biến.** Sửa code cho sai đúng một chỗ, xác nhận **đúng test đó** đỏ.
   Test không qua bước này có thể vô dụng mà không ai biết.
3. **Ràng buộc "chỉ một"** → thêm `InlineData` vào `DongThoiTests` (quy tắc #8).

> Bài học 12/09/2026: một test audit tạo và sửa bằng **cùng một người** nên đột biến (bỏ hẳn
> chốt giữ `CreatedById`) vẫn qua — giá trị ghi đè bằng đúng giá trị cũ. Phải dùng **hai người
> khác nhau** mới bắt được.

### Lưu ý về tenant dùng chung

Mọi test trong một file thường dùng **chung một tenant** (xem helper `Client()`), nên dữ liệu
tích luỹ giữa các test. Đừng assert con số tuyệt đối (`Assert.Equal(5, tong)`) — lọc theo dữ liệu
của chính test đó.

---

## 7. Trả lỗi

API trả **mã lỗi**, frontend dịch (quy tắc #3):

```csharp
throw new AppException("KHOA_HOC_KHONG_KHOP_LOP")
{
    // DuLieu để frontend dựng câu cụ thể mà API vẫn chỉ trả MÃ
    DuLieu = new Dictionary<string, object> { ["khoaCuaDon"] = ten }
};
```

Mã lỗi `UPPER_SNAKE_CASE`, thêm vào **cả** `MaLoi.cs` **và** `i18n.ts` — thiếu bản dịch thì người
dùng thấy chuỗi mã lỗi trên màn hình, mà build **không** đỏ.

> `check-i18n-keys.py` chỉ kiểm chiều **thiếu**, không kiểm **trùng khoá**. Khoá trùng chỉ bị
> `tsc` bắt (`TS1117`).

---

## 8. Trước khi mở PR

- [ ] `dotnet build` — 0 warning (`TreatWarningsAsErrors` đang bật)
- [ ] `dotnet test` — xanh
- [ ] `npx tsc -b && npm run build && npx oxlint src e2e`
- [ ] `python3 scripts/check-doc-links.py` và `check-i18n-keys.py`
- [ ] Đổi schema/API → cập nhật tài liệu **cùng PR** (quy tắc #4)
- [ ] Test mới đã chứng minh bằng đột biến
- [ ] Đổi hợp đồng API (tên trường JSON) → sửa frontend **cùng commit**

---

## 9. Đổi tên hàng loạt — đọc trước khi làm

Rút từ lần thất bại 12/09/2026 ([ADR-0006](./kien-truc/adr/0006-dat-ten-tieng-anh-va-cot-audit.md)):

1. **Đừng regex toàn bộ codebase.** Ba lỗi ngữ nghĩa đã lọt qua trình biên dịch: `SoTaiKhoan`
   (đếm tài khoản) thành `BankAccountNo` (số TK ngân hàng), `HanhDong` đụng `System.Action`, và
   một **chuỗi** `"NguoiDung"` bị đổi làm hỏng nhật ký.
2. **Chuỗi văn bản phải xem bằng mắt.** `"..."` có thể trỏ tới tên lệnh, khoá i18n, hoặc giá trị
   nằm trong dữ liệu — regex không phân biệt được.
3. **Đổi property = đổi cột DB = đổi tên trường JSON.** Ba việc này là một.
4. **Làm theo module nhỏ**, commit sau mỗi module, hệ thống luôn chạy được.
5. **Backup DB và diễn tập trên bản sao** trước khi chạy migration đụng dữ liệu.


## 10. Phép tính hiển thị — tách ra hàm thuần, có test

Thêm 14/09/2026 sau lỗi **417%**: phễu bán hàng tính tỷ lệ so với bước liền trước, cho ra một
con số lớn hơn 100% trên dữ liệu thật. Công thức nằm trong biểu thức nhúng giữa JSX nên **không
gì canh được** — `tsc` xanh, 468 test backend xanh, và nó chỉ lộ khi có đủ dữ liệu để tỷ lệ vượt
100%. Với 2-3 bản ghi thì mọi tỷ lệ đều dưới 100% và trông bình thường.

**Quy ước:** phép tính có thể sai về *ý nghĩa* (không chỉ sai kiểu) thì tách ra hàm thuần kèm
test — đừng nhúng thẳng vào component.

Thuộc loại này: phần trăm · tỷ lệ · phép chia · gộp nhóm · quy đổi đơn vị. Không thuộc: ghép
chuỗi, chọn màu theo enum, định dạng ngày.

```
frontend/src/components/bieu-do/tinh-toan.ts        ← hàm thuần
frontend/src/components/bieu-do/tinh-toan.test.ts   ← test
npm test                                             # vitest run, ~0.5s
```

Hai câu hỏi bắt buộc cho mọi phép chia hiển thị:

| Hỏi | Vì sao |
|---|---|
| Mẫu số bằng 0 thì sao? | `Infinity`/`NaN` hiện lên màn hình |
| Kết quả có thể vượt 100% không? | Nếu có mà không nên có → **công thức sai bản chất**, không phải sai số |

Câu thứ hai là câu bắt được lỗi 417%: mẫu số chọn sai (bước trước thay vì tổng) trong khi các
bước **loại trừ nhau**.

---

## 11. Chức năng mới phải khai quyền trong cùng PR

Từ 14/09/2026, thêm một màn hình hay một endpoint mà **chưa khai quyền** là chưa xong việc —
ngang với chưa viết test. Lý do rất cụ thể: quyền khai thiếu thì ô không hiện trên màn phân
quyền, **không ai cấp được**, và endpoint trả 403 cho mọi người **kể cả quản trị**. Triệu
chứng khó chẩn: đăng nhập bình thường, mọi màn cũ chạy bình thường, chỉ đúng một màn hỏng.

Sáu bước, chi tiết ở
[phan-quyen-dong.md](./backend/phan-quyen-dong.md#-thêm-chức-năng-mới-bắt-buộc-khai-quyền-trong-cùng-pr):

1. Hằng vào `ChucNang` + phân loại trong `HeThongCua`.
2. **Thao tác vào `ChucNang.ThaoTacTheoChucNang`** — thao tác THẬT của nghiệp vụ, không mặc
   định bốn ô CRUD.
3. Gác endpoint bằng đúng cặp vừa khai.
4. Nhãn tiếng Việt vào `i18n.ts` (`chucNang` + `hanhDong`) và giá trị vào `export type
   HanhDong` trong `quyen.ts`.
5. Cân nhắc cấp cho nhóm mặc định.
6. Cập nhật bảng chức năng × thao tác trong tài liệu.

### Đặt tên thao tác: mô tả VIỆC, không mô tả thao tác CRUD gần nhất

Bốn ô CRUD không diễn đạt nổi nghiệp vụ thật. "Chốt buổi học" và "sửa điểm danh" đều phải mượn
`Sua` — nên không tách được quyền giáo viên chính với trợ giảng, dù đó chính là khác biệt cần
phân quyền. Thấy mình định viết "thao tác này gần giống `Sua`" là dấu hiệu cần thao tác mới.

Thêm giá trị vào `enum HanhDong` với **số ≥ 10**. **Không bao giờ** chèn vào giữa hay đổi số
0–3: `QUYEN_CHUC_NANG.hanh_dong` lưu số nguyên, đổi `Sua` từ 2 thành 3 là âm thầm biến quyền
"Sửa" của mọi nhóm thành "Xoá".

### Ba câu hỏi trước khi khai một ô quyền

| Hỏi | Vì sao |
|---|---|
| Có endpoint nào đọc ô này không? | Không có = **ô chết**, người cấu hình tick mà không có tác dụng |
| Đây là quyền gọi endpoint hay quyền **phạm vi dữ liệu**? | Phạm vi cấp thừa thì **rò rỉ dữ liệu mà không báo gì** — phải khai vào `ChucNang.PhamViDuLieu` để UI hiện thành nhóm riêng |
| `Xem` ở đây nghĩa là "của mình" hay "của mọi người"? | Nếu là "của mọi người" thì việc-của-mình phải là `TuLam` riêng. Cấp nhầm `NhanXetBuoiHoc.Xem` cho học viên = cho họ đọc phản hồi riêng của bạn cùng lớp |

Bốn chốt chặn tự động bắt cả hai chiều (endpoint dùng quyền chưa khai **và** quyền khai không
ai dùng) — xem bảng ở
[phan-quyen-dong.md](./backend/phan-quyen-dong.md#-thêm-chức-năng-mới-bắt-buộc-khai-quyền-trong-cùng-pr).
