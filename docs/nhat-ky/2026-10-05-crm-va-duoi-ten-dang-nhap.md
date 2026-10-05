# 2026-10-05

Bốn đợt việc nhỏ, chung một chủ đề: **form hỏi đúng thứ nó chịu trách nhiệm**.

## Đã làm

1. **Lỗi nhập liệu nói rõ sai ở đâu** (`17ee5f8`) — trước đó mọi lỗi 400 đều hiện "dữ liệu
   không hợp lệ" vì `layMaLoi` chỉ đọc `errorCode` ở gốc, không đọc `duLieu.truong` do
   FluentValidation trả về. Tách `layMaLoi` khỏi `api.ts` sang `lib/maLoi.ts` để test chạy
   được ở môi trường node (import `api.ts` kéo theo cả axios instance + interceptor).
2. **Form khách hàng chỉ hỏi thông tin liên hệ** (`362a8bf`) — bỏ "Nối với hồ sơ học viên" và
   "hình thức thanh toán" khỏi cả form tạo lẫn form sửa.
3. **Xác nhận "đã nhận đủ tiền" cho từng đơn** (`7de478e`) — cột `XacNhanDuTien` trên
   `THU_TIEN_DANG_KY`.
4. **Đuôi tên đăng nhập theo trung tâm** — cột `TENANT.duoi_ten_dang_nhap`, helper
   `DuoiTenDangNhapHelper.GhepAsync`.

## Quyết định

### Nối đuôi ở tầng Application, không ở form và không ở DB

Ba chỗ đều nối được, nhưng hai chỗ kia sai:

- **Ở form**: tài khoản tạo được từ **hai** đường — màn Tài khoản (FR-04) và màn tạo người kèm
  tài khoản (FR-03). Hai chỗ ghép là hai chỗ để lệch. Và ai gọi thẳng API đều bỏ qua được.
- **Ở DB**: `UNIQUE(tenant_id, username)` sẽ kiểm trên chuỗi **chưa** nối, nên hai người cùng
  phần đầu vẫn lọt qua rồi đụng nhau sau.

Nối ở Application, **trước** khi kiểm trùng, là chỗ duy nhất mọi đường đi qua.

### Cờ "đã nhận đủ" là cột riêng, không suy từ phép trừ

`cam kết − đã thu` trả lời "còn thiếu bao nhiêu TIỀN". Nó không trả lời "trung tâm còn đòi
nữa không". Hai câu lệch nhau ở miễn phần lẻ, giảm giá sau khi chốt, trả bằng hiện vật, xoá
nợ — đều là ca thường, không phải ca hiếm. Không phân biệt thì những đơn đó nằm mãi trong
danh sách nhắc nợ. `ConThieu` giữ nguyên là số thật kể cả sau khi xác nhận đủ.

### Đuôi không áp dụng ngược cho tài khoản đã có

Đổi username của người đang dùng là đổi thứ họ gõ mỗi sáng. Đó phải là quyết định tường minh,
không phải hệ quả của một lần sửa thiết lập (quy tắc #1).

## Vướng mắc & phát hiện

### Bỏ ô khỏi form thì lệnh cập nhật xoá mất dữ liệu

Bỏ "Nối với hồ sơ học viên" khỏi form, nhưng form vẫn gửi `nguoiDungId` — và giờ gửi `null`.
Thử trên dữ liệu thật: **17 liên kết khách hàng ↔ người dùng bị xoá sạch** chỉ bằng một lần
bấm Lưu. Khôi phục lại rồi sửa thành gửi `dangSua?.nguoiDungId`.

Đây đúng là lỗi 16/08/2026 mặc áo mới, và lần này nó đến từ hướng ngược: không phải *quên*
thêm ô, mà là *cố ý bỏ* ô. Quy tắc #1 ghi "trường nào lệnh ghi đè thì phải có trong form" —
nhưng bỏ ô đi cũng vi phạm nếu lệnh vẫn ghi đè trường đó.

### Gõ sẵn cả đuôi rồi vẫn để tick

Form điền sẵn đuôi để người dùng **nhìn thấy** nó. Hệ quả là họ hay gõ luôn cả đuôi vào ô. Nếu
chỉ xét ô tick thì ra `nv1@abc.com@abc.com`. Nên `GhepAsync` xét cả `ten.Contains('@')`.

Mutation testing: bỏ điều kiện này thì `Go_san_ca_duoi_thi_khong_noi_hai_lan` đỏ; bỏ điều
kiện `!noiDuoi` thì `Khong_tick_thi_giu_nguyen_ten_ngan` đỏ. Cả hai mutant đều chết.

### `maLoi.ts` phải tách khỏi `api.ts`

Muốn viết test cho hàm đọc mã lỗi thì không import được `api.ts` — nó dựng axios instance và
đăng ký interceptor ngay lúc import, cần `window`. Tách hàm thuần ra file riêng.

## Việc kế tiếp

- **Đổi 70+ tài khoản dev của W686AE9 sang đuôi mới** — chủ sản phẩm đã chốt làm. Chưa chạy
  được vì Docker Desktop đang tắt, mà cả hai migration hôm nay cũng chưa áp vào DB dev.
- Hai migration chờ áp: `XacNhanDuTienChoLanThu`, `ThemDuoiTenDangNhap`.
