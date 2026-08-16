# Phân quyền động theo chức năng + thao tác

> **Quy tắc bất di bất dịch #8.** **Không** dùng `[Authorize(Roles = "...")]` với role cố định. Quyền
> đọc động từ bảng `QUYEN_CHUC_NANG` tại runtime.

## Vì sao không dùng role cố định

Nghiệp vụ (FR-05) cho phép Admin của **mỗi CLB tự định nghĩa nhóm quyền riêng** — tên nhóm và tập quyền
do người dùng tạo ra lúc chạy, không biết trước lúc biên dịch. Role cố định trong attribute không biểu
diễn được điều này.

## Mô hình quyền

```
NGUOI_DUNG ──N:N── QUYEN ──1:N── QUYEN_CHUC_NANG
                                  ├─ ten_chuc_nang  (vd "LichThiDau", "TaiChinh")
                                  └─ hanh_dong      (xem | them | sua | xoa)
```

Một tài khoản gán **nhiều nhóm quyền**; quyền hiệu lực = **hợp (union)** của tất cả các nhóm. Không có
khái niệm "deny" ghi đè — chỉ cộng dồn quyền.

## Cách dùng trên endpoint

```csharp
[RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
public async Task<IActionResult> CapNhatTranDau(...)
```

Dùng hằng số `ChucNang.*` và enum `HanhDong` thay vì chuỗi thô — gõ sai chuỗi sẽ tạo ra một policy
không bao giờ khớp, và lỗi chỉ lộ ra lúc chạy.

### Cơ chế (đã triển khai)

| Thành phần | Vai trò |
|---|---|
| `RequirePermissionAttribute` | Sinh tên policy `Quyen:{chucNang}:{hanhDong}` |
| `QuyenPolicyProvider` | Sinh policy **khi gặp lần đầu** — không phải đăng ký sẵn từng tổ hợp trong `Program.cs` (số tổ hợp = số chức năng × 4 và còn tăng theo mỗi module) |
| `QuyenAuthorizationHandler` | Đọc `tenant_id` + `NameIdentifier` từ claim, hỏi `IQuyenService` |
| `QuyenService` | Truy vấn `NGUOIDUNG_QUYEN → QUYEN → QUYEN_CHUC_NANG`, cache 5 phút |

### Đã kiểm chứng

`PhanQuyenVaCachLyTests` chạy qua API thật. Đã chứng minh test bắt được vi phạm bằng phản chứng: thay
`[RequirePermission]` bằng `[Authorize]` thường → test "thiếu quyền → 403" đỏ ngay.

## Danh mục chức năng

`ten_chuc_nang` là **danh mục đóng** — định nghĩa bằng hằng số trong code (không để người dùng tự nhập
chuỗi tùy ý), tương ứng các module nghiệp vụ:

| `ten_chuc_nang` | Module | FR |
|---|---|---|
| `LichThiDau` | [Lịch thi đấu](../nghiep-vu/lich-thi-dau.md) | FR-07 → FR-11 |
| `ThongKe` | [Thống kê](../nghiep-vu/thong-ke.md) | FR-12 → FR-14 |
| `TaiChinh` | [Tài chính](../nghiep-vu/tai-chinh.md) | FR-15, FR-16 |
| `TaiKhoan` | [Quản trị](../nghiep-vu/quan-tri-he-thong.md) | FR-03 |
| `CauThu` | [Quản trị](../nghiep-vu/quan-tri-he-thong.md) | FR-04 |
| `PhanQuyen` | [Quản trị](../nghiep-vu/quan-tri-he-thong.md) | FR-05 |
| `ThietLapChung` | [Quản trị](../nghiep-vu/quan-tri-he-thong.md) | FR-06 |

Thêm module mới → thêm hằng số **và** cập nhật bảng này trong cùng PR.

## Cache

Truy vấn quyền chạy ở **mọi request** → cần cache **ngắn hạn** (in-memory hoặc Redis, TTL vài phút),
khóa theo `{tenant_id}:{nguoi_dung_id}`.

**Bắt buộc invalidate cache khi:** sửa nhóm quyền (FR-05), gán/gỡ quyền khỏi tài khoản (FR-03), vô hiệu
hóa tài khoản. Quyền bị thu hồi mà cache còn sống là lỗ hổng bảo mật, không phải chỉ là chuyện dữ liệu cũ.

## Quan hệ với multi-tenant

Hai tầng độc lập, **cả hai đều phải đúng**:

- **Tenant** trả lời "được thấy dữ liệu của CLB nào" — xem [multi-tenant.md](./multi-tenant.md).
- **Quyền** trả lời "được làm gì với dữ liệu trong CLB đó".

Bảng `QUYEN` cũng có `tenant_id` — nhóm quyền của CLB A không áp dụng cho CLB B.

## Admin mặc định

Tài khoản `admin` mỗi tenant có toàn quyền. Cách triển khai: seed một nhóm quyền "Quản trị viên" đầy đủ
`xem/them/sua/xoa` cho mọi chức năng (xem [seed data](../database/quy-uoc-migration.md#seed-data)) —
**không** hard-code nhánh `if (user.IsAdmin) return true` bỏ qua hệ phân quyền, để một cơ chế duy nhất
quyết định mọi truy cập.

Riêng thao tác **đổi mật khẩu cho tài khoản khác** là đặc quyền chỉ Admin có (FR-03) — biểu diễn bằng
một `ten_chuc_nang`/`hanh_dong` riêng, không bằng ngoại lệ trong code.
