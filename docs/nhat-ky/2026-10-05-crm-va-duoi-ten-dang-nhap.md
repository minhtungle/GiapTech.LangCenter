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

### "70+ tài khoản" thật ra là 13

Ghi chú trong phiên trước nói phải đổi tên "70+ tài khoản dev". Đếm trên DB: **13 tài khoản**,
76 **người dùng**. 63 người còn lại là học viên demo không có tài khoản — con số 76 bị đọc
nhầm thành số tài khoản, và cảnh báo "thay đổi lớn" vì thế phóng đại hơn thực tế.

## Đã chạy trên DB dev

Hai migration đã áp. Kiểm sau khi áp: 13 tài khoản · 76 người dùng · 72 lần thu · **17 liên
kết khách hàng ↔ người dùng** — khớp đúng con số đã khôi phục sau sự cố ở trên.

13 nick của W686AE9 đã nối đuôi `@vietgeneducation.edu.vn` bằng
`scripts/them-duoi-ten-dang-nhap-dev.sh` (xem trước mặc định, `GHI=1` mới ghi, sao lưu tên cũ
ra CSV trước khi UPDATE). Thử đăng nhập lại 5 vai trò — đều 200 với tên đầy đủ, 400 với tên
ngắn. Đúng thiết kế: `DangNhapCommand` **không** đoán thêm đuôi, nên `UNIQUE(tenant_id,
username)` giữ nguyên nghĩa.

Script `UPDATE` thẳng DB chứ không gọi API, vì **không có endpoint nào đổi username** — cố ý,
ở production đổi username là việc phải cân nhắc từng ca. Khác với mật khẩu (ghi hash tay là
tự cài thuật toán băm thứ hai), username là chuỗi thô nên UPDATE không làm lệch gì.

Trước đó đã thử ba ca tạo tài khoản trên hệ thống thật: tick → nối, bỏ tick → giữ nguyên, gõ
sẵn cả đuôi + vẫn tick → không nối hai lần. Cả ba đúng, rồi dọn tài khoản thử.

## Việc kế tiếp

- `frontend/edu-temp/` (60MB, chưa theo dõi) vẫn chờ quyết định về giấy phép và cách dùng.
