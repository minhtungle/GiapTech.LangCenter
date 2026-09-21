using Xunit;

/*
  TẮT chạy song song giữa các lớp test (20/09/2026).

  Từ khi có "một phiên mỗi tài khoản", đăng nhập lại cùng một nick sẽ **đẩy phiên trước ra**.
  Mà **43 lớp test dùng chung nick `manager`** của tenant A, và xUnit mặc định chạy các lớp
  song song trên CÙNG một server in-memory — nên lớp này đăng nhập là đá văng phiên của lớp
  kia, đỏ rải rác ở những chỗ chẳng liên quan gì tới đăng nhập.

  Triệu chứng rất dễ quy oan: 6 test đỏ cùng một mốc thời gian, mỗi lần chạy lại đỏ ở nhóm
  khác, và **chạy riêng từng test thì xanh**.

  Vì sao tắt song song thay vì cho mỗi lớp một nick riêng:

  - Sửa 43 lớp là thay đổi lớn, dễ sót, và mỗi lớp thêm một tài khoản làm dữ liệu seed phình ra.
  - Bộ integration chạy ~50 giây khi tuần tự — chưa tới mức phải đánh đổi.
  - Quan trọng nhất: chạy song song **cùng một tài khoản** vốn đã là điều mà sản phẩm nay CẤM.
    Giữ song song là bắt test mô phỏng một tình huống người dùng thật không gặp.

  Nếu sau này bộ test chậm tới mức cần song song lại: cho mỗi lớp một tài khoản riêng
  (`manager-<tên lớp>`), đừng bật lại cờ này.
*/

[assembly: CollectionBehavior(DisableTestParallelization = true)]
