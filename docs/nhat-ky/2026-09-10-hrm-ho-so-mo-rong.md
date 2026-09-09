# 2026-09-10 (HRM) — FR-23 hồ sơ mở rộng + chia tab view chi tiết

## FR-23: chọn bảng riêng cho liên kết MXH

Yêu cầu "liên kết mxh (được thêm nhiều)" có ba cách làm, và chữ **"nhiều"** loại ngay cách một:

| Cách | Vì sao bỏ / chọn |
|---|---|
| Vài cột trên `NGUOI_DUNG` (`facebook`, `zalo`…) | Thêm mạng mới = thêm cột + migration. Và "được thêm nhiều" không diễn đạt được: một người hai Facebook là chuyện thật |
| Cột `jsonb` | **Mảng không mang `tenant_id`** → nằm ngoài Global Query Filter (quy tắc #2). Tự loại |
| **Bảng `LIEN_KET_MXH`** ✅ | Có `tenant_id`, có Query Filter, thêm loại mạng chỉ là thêm giá trị enum |

Cố ý **không** `UNIQUE(nguoi_dung_id, loai)`: hai Facebook (cá nhân + fanpage) là hợp lệ.

`duong_dan` **không** validate là URL — Zalo thường chỉ là số điện thoại. UI chỉ mở tab mới khi
giá trị bắt đầu bằng `http(s)://`.

`cccd` **không** unique: dữ liệu nhập tay thường để trống, ép duy nhất sẽ chặn lưu hồ sơ thứ hai
chỉ vì cả hai cùng rỗng. Trùng CCCD là việc **cảnh báo ở UI**, không phải ràng buộc DB.

Tệp hồ sơ dùng `TEP_DINH_KEM` với **cột FK thứ sáu** `nguoi_dung_id` — và cái giá của phương án
"N cột FK loại trừ nhau" hiện ra đúng lúc này: thêm một cột **buộc viết lại `CHECK`** đếm "đúng
một cột khác null". Đã lường trước từ giai đoạn 3, vẫn rẻ hơn 5 bảng riêng.

Handler tải tệp đặt ở `TepHoSoCommands.cs` **riêng**, không thành nhánh thứ sáu của `TaiTepCommand`:
nhánh đó phụ thuộc `IPhamViLopHoc`, kéo logic HRM vào sẽ làm `RanhGioiHeThongConTests` đỏ.

## Lỗi: thiếu `Include` làm danh sách MXH không thay thế được

Handler cập nhật thiếu `.Include(u => u.LienKetMxhs)` nên `RemoveRange(nd.LienKetMxhs)` chạy trên
collection **rỗng**: gửi danh sách rỗng không xoá được liên kết cũ, gửi danh sách mới thì **cộng
thêm** vào cũ thay vì thay thế.

Đây là biến thể của quy tắc #1 mà không form nào lộ ra: nhìn UI thấy "lưu xong", chỉ F5 mới thấy
danh sách dài ra. 2 test bắt được, và mutation chứng minh chúng đỏ thật khi bỏ `Include`.

Bài học ghi vào `NguoiDungDtos.cs` ngay tại dòng `Include`: **`RemoveRange` trên navigation chưa
`Include` là no-op im lặng**, không lỗi biên dịch, không exception.

## Chia tab view chi tiết hồ sơ nhân sự

Yêu cầu: *"chia tab cho tệp hồ sơ và thông tin chung"*. Lý do thật đằng sau nó là thứ tự đọc —
thẻ tệp xếp dọc dưới thông tin đẩy phần thông tin lên cao, và người có nhiều tệp phải cuộn mới
xem hết, trong khi **thông tin chung mới là thứ người ta vào view này để xem**.

Ba chi tiết bắt chước sẵn từ view chi tiết khách hàng, không phát minh lại:

1. **Khoá i18n gắn sẵn trên hằng `CAC_TAB`**, không ghép `` `tab_${ma}` `` — mã tab dùng gạch
   ngang cho URL đẹp, khoá i18n dùng camelCase, ghép động sinh `tab_thong-tin` không khớp khoá
   nào và i18next trả về **chính chuỗi khoá** cho người dùng thấy (lỗi thật 07/09 ở chi tiết lớp).
2. **Mã tab trong `?tab=`** chứ không `useState`: gửi link được, và F5 không âm thầm về tab đầu.
3. **`replace: true`** — bấm qua lại hai tab không sinh một mục lịch sử mỗi lần, nút Back về danh
   sách nhân sự. Tab mặc định **bỏ hẳn `?tab=`** thay vì `?tab=thong-tin`: URL của trạng thái mặc
   định nên là URL ngắn nhất.

Số tệp hiện trên **nhãn tab** để biết có gì bên đó mà không cần bấm sang; badge đếm cũ ở tiêu đề
trong thẻ thành trùng lặp nên bỏ.

### Hai lỗi tự gây ra khi chia tab

**1. `{/* comment */}` ngay sau `{tab === 'tep' && (`.** Bọc khối cũ bằng splice theo chỉ số dòng
làm comment JSX rơi vào **trong expression container** — ở đó `{/*...*/}` không phải comment mà là
object literal, và `tsc` báo lỗi ở dòng **303**, cách chỗ sai 70 dòng. Rà cân bằng thẻ đóng/mở của
đúng khối đó cho kết quả "cân" nên hướng đó không dẫn tới đâu; chỉ khi đọc **dòng mở đầu** khối
mới thấy. Bài học: lỗi JSX báo ở đâu không phải nơi sai — đọc từ ranh giới khối vừa sửa.

**2. Splice theo chỉ số dòng không tự thụt lề.** Khối trong fragment thiếu 2 space. Build vẫn xanh
(JSX không quan tâm thụt lề) nên không có gì nhắc.

Cả hai đều là hệ quả của việc dùng script splice cho một thay đổi lẽ ra nên làm bằng vài lệnh sửa
có mỏ neo văn bản.

### Đột biến để chứng minh test có răng

`e2e/chi-tiet-nhan-su.spec.ts` canh thứ **thực sự dễ mất**: hai khối **loại trừ nhau**. Bỏ điều
kiện `tab === 'tep'` trả UI về đúng trạng thái trước khi chia tab, mà `npm run build` **vẫn xanh**
vì đó là JSX hợp lệ.

Thử luôn: đổi thành `{true && (` → spec đỏ tại đúng dòng khẳng định "khối tệp phải ẩn ở tab thông
tin". Kiểm cả chiều ngược (khối thông tin ẩn ở tab tệp), `?tab=` sống qua F5, và badge đếm.

Dùng `isVisible()` chứ không `count()` cho mọi phép kiểm "có thấy không" — giữ một khuôn duy nhất,
vì `count()` từng cho kết quả sai ở chỗ khác (`<dialog>` đóng vẫn giữ con trong DOM).

## Còn nợ lại

4 test E2E đỏ **có sẵn từ trước**, không liên quan thay đổi hôm nay: `dang-nhap-tra-ma.spec.ts` và
`quan-tri.spec.ts` còn chờ chữ **"đội"** thời dự án bóng đá, trong khi app đã trả "Không tìm thấy
**trung tâm** tương ứng". Là test cũ lạc hậu, không phải lỗi app → ghi thành nợ, không mở rộng
phạm vi commit này.

Chạy cả bộ E2E vẫn phải `GIOI_HAN_TAN_SUAT=false`: mỗi test tự tạo một trung tâm, mà
`/dang-ky-trung-tam` giới hạn 10 req/phút mỗi IP → không tắt là 429 và triệu chứng hiện ra dưới
dạng "Không tạo được trung tâm", rất dễ hiểu nhầm là API chết.
