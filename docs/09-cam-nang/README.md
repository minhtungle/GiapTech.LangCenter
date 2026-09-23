# Cẩm nang — rút tỉa để dùng cho dự án khác

Thư mục này **khác** phần còn lại của `docs/`.

Các thư mục kia mô tả *dự án này làm thế nào*, và giả định người đọc đang làm việc trong chính
nó. Thư mục này viết cho **người ngoài**: một lập trình viên — hoặc một phiên trợ lý AI khác —
sắp xây một hệ thống **khác**, muốn biết *cách làm nào đáng học và bẫy nào phải tránh*.

Vì vậy mỗi bài ở đây:

- **Đọc được độc lập.** Không cần biết gì về trung tâm ngoại ngữ. Khi buộc phải lấy ví dụ từ
  nghiệp vụ của dự án, bài viết nói rõ đó chỉ là ví dụ.
- **Lấy mã mẫu từ mã thật**, không bịa ra cho đẹp.
- **Nói rõ bẫy nào đã vấp thật** (có bằng chứng trong chú thích, test, hoặc nhật ký) và bẫy nào
  mới chỉ là rủi ro nhìn thấy trước.

## Danh sách

| Bài | Nội dung |
|---|---|
| [01 — Multi-tenant](./01-multi-tenant.md) | Cách ly dữ liệu nhiều khách hàng trên một database: Global Query Filter, năm cái bẫy đã vấp, và cách canh để không hỏng lại |

*(Các bài tiếp theo sẽ thêm dần: phân quyền động, xác thực JWT + refresh token, test canh kiến
trúc, đa ngôn ngữ, nhận diện tenant qua domain.)*

## Ý tưởng đáng mang đi nhất

Nếu chỉ đọc được một thứ từ thư mục này, hãy đọc phần **"test chiều ngược"** trong bài
[01 — Multi-tenant](./01-multi-tenant.md).

Ý tưởng: một quy tắc kiến trúc chỉ sống được nếu có test **bắt cả chiều ngược lại**. Không chỉ
"mọi thứ trong danh sách phải đúng", mà còn "mọi thứ **ngoài** danh sách phải có lý do viết ra".
Không có vế thứ hai, danh sách ngoại lệ sẽ lặng lẽ lạc hậu và quy tắc mất hiệu lực mà không ai
biết.

Dự án này áp dụng nó cho bảy test canh kiến trúc. Nó rẻ, và nó bắt người thêm mã mới phải **dừng
lại viết ra lý do** — hoặc nhận ra là mình quên.
