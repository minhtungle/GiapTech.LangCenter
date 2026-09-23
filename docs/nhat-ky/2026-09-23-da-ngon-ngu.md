# 23/09/2026 — Đa ngôn ngữ: Anh · Trung · Hàn · Nhật

Yêu cầu chủ sản phẩm: *"phát triển thêm đa ngôn ngữ... Chỉ áp dụng cho hệ thống, nội dung nhập
từ người dùng thì không ảnh hưởng"*.

Chốt hai điều trước khi làm: **tôi dịch hết, chủ sản phẩm rà lại sau** và **lưu lựa chọn theo
từng máy** (không cần migration).

## Đo trước khi làm

| | |
|---|---|
| Khoá cần dịch | **1197** (66 nhóm) |
| Tệp hardcode `vi-VN` | **12** |
| Nút chuyển ngôn ngữ | chưa có |
| `.resx` backend | không có — backend chỉ trả mã lỗi |

Con số 12 tệp `vi-VN` là thứ dễ bỏ sót nhất: dịch xong 1197 khoá mà ngày tháng vẫn kiểu Việt
thì mới làm được nửa việc.

## Dịch: 4 tác nhân song song, nhưng KHÔNG tin báo cáo

1197 × 4 ≈ 4800 chuỗi — quá nhiều để làm tuần tự. Giao 4 tác nhân, mỗi tác nhân một ngôn ngữ,
kèm **bảng thuật ngữ bắt buộc** (trung tâm/lớp học/buổi học/học viên/giáo viên/trợ giảng/điểm
danh/học phí/xếp lớp…) để không mỗi chỗ dịch một kiểu.

Cả bốn đều báo "đạt". Tôi vẫn **tự kiểm lại bằng script**: số khoá, placeholder `{{...}}`, chuỗi
tiếng Việt còn sót. Cả bốn đều sạch thật — nhưng nếu không kiểm thì cũng không biết.

## Bài học chính: đừng ghép chuỗi bằng tiền tố

Khoá `buoiHoc.thuTuNgan` là **tiền tố** `'Buổi '` rồi code tự nối số:

```tsx
{t('buoiHoc.thuTuNgan')}{b.thuTu}
```

Trông vô hại, nhưng nó **ngầm giả định trật tự từ của tiếng Việt** — số phải đứng sau. Tiếng
Nhật, Hàn, Trung đếm kiểu **bao quanh** số:

| | Tiền tố (sai) | Có biến (đúng) |
|---|---|---|
| ja | 第3 | **第3回** |
| ko | 제 3 | **3차시** |
| zh | 第 3 | **第 3 次课** |

Bản sai đọc như câu bỏ dở. **Hai tác nhân dịch (Nhật và Hàn) độc lập chỉ ra cùng chỗ này** —
đó là tín hiệu đủ mạnh để không bỏ qua.

Sửa: khoá mang cả biến (`'Buổi {{n}}'`), mỗi ngôn ngữ tự đặt số vào chỗ của mình. Ba chỗ gọi
đổi theo.

**Quy tắc rút ra**: một chuỗi hiển thị phải là **một khoá trọn vẹn**, kể cả khi có biến ở
giữa.

## Hai lỗi khác, cùng một kiểu: "đọc một lần rồi giữ mãi"

**Tiêu đề tab kẹt tiếng Việt.** `nhanDienTab.ts` đọc `document.title` một lần lúc nạp module —
tức đọc chuỗi cứng trong `index.html`. Đổi ngôn ngữ thì chữ đổi, tab không. Nay lấy từ bảng
dịch và có `ngonNgu` trong dependency của effect.

**Script kiểm khoá báo sai.** Nó báo `en.ts` "thừa 2 khoá" trong khi hai tệp giống hệt. Nguyên
nhân: script so bằng **regex**, mà `vi.ts` viết `vaiTro: { GiaoVien: '…' }` gọn trên một dòng
nên regex không thấy khoá con, còn tệp sinh tự động xuống dòng nên thấy.

Tức là script so **cách trình bày** chứ không so **dữ liệu**. Sửa thành đọc qua Node, và nhân
tiện thêm **kiểm chéo mọi ngôn ngữ với `vi.ts`** — trước đó không có gì bắt được khoá thiếu ở
một ngôn ngữ, vì i18next lặng lẽ rơi về tiếng Việt.

## Hai quyết định về định dạng

**Tiếng Anh dùng `en-GB`**, không `en-US`: người dùng hệ thống này quen thứ tự ngày-trước, đổi
sang tháng-trước dễ đọc nhầm hạn học phí.

**Tiền luôn hiện `₫`** dù đang ngôn ngữ nào — đó là số tiền thật của trung tâm, đổi ký hiệu
theo ngôn ngữ là nói sai đơn vị. Chỉ dấu phân cách hàng nghìn đổi theo locale.

## Kết quả

**1198 khoá × 5 ngôn ngữ · 0 lệch placeholder · 60 E2E xanh · 40 vitest xanh.**

5 test E2E mới, trong đó có test canh **dữ liệu người dùng KHÔNG bị dịch** (điều chủ sản phẩm
nói rõ) và test canh **số thứ tự đặt đúng chỗ** — mutation "bỏ đuôi 回" làm nó đỏ.

## Còn nợ

**Bản dịch chưa có người bản ngữ rà.** Thuật ngữ phổ thông thì ổn, nhưng thuật ngữ nghiệp vụ
riêng (*xếp lớp*, *sổ thu*, *chốt sổ*) nên có người bản ngữ xem trước khi dùng cho khách thật.
Các tác nhân đã tự đánh dấu những chỗ họ không chắc — ghi trong `docs/frontend/da-ngon-ngu.md`.
