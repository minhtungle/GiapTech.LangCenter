using GiapTech.LangCenter.Domain.Entities;

namespace GiapTech.LangCenter.Application.QuanTri.Email;

/// <summary>
/// Mẫu email mặc định viết sẵn trong mã (FR-31).
///
/// ## Vì sao phải có, không để trống
///
/// Trung tâm chưa soạn mẫu mà hệ thống để trống thì email **không gửi được** cho tới khi có
/// người vào soạn — mà người đó không biết mình cần làm việc đó. Học viên mới không nhận được
/// thông tin đăng nhập, khách để lại số điện thoại không nhận được hồi âm, và không có lỗi nào
/// báo cho ai.
///
/// Mẫu mặc định làm cho hệ thống **chạy được ngay từ lúc tạo trung tâm**; soạn lại là việc
/// cải thiện, không phải điều kiện tiên quyết.
///
/// ## Danh sách biến là HỢP ĐỒNG
///
/// <see cref="BienCuaLoai"/> quyết định giao diện hiện nút chèn biến nào, và
/// <see cref="ThayBien"/> chỉ thay đúng những biến đó. Thêm biến vào mẫu mặc định mà quên khai
/// ở đây thì nó **không được thay** và học viên nhận nguyên chuỗi `{{...}}` giữa câu văn.
/// </summary>
public static class MauMacDinh
{
    /// <summary>
    /// Biến dùng được cho từng loại mẫu.
    ///
    /// Mỗi loại một bộ riêng chứ không dùng chung một danh sách: `{{soTienConThieu}}` vô nghĩa
    /// trong email chào mừng, và hiện nó lên chỉ khiến người soạn chèn nhầm.
    /// </summary>
    public static readonly IReadOnlyDictionary<LoaiMauEmail, string[]> BienCuaLoai =
        new Dictionary<LoaiMauEmail, string[]>
        {
            // Năm biến đầu là thông tin ĐĂNG NHẬP; bốn biến sau là HỒ SƠ đã khai hộ người
            // được tạo (09/10/2026) — để họ soát lại và báo sai ngay, thay vì phát hiện sai
            // số điện thoại vào lúc trung tâm cần gọi gấp.
            [LoaiMauEmail.ChaoMungHocVien] =
                ["tenHocVien", "tenTrungTam", "maTrungTam", "tenDangNhap", "matKhauTam",
                 "vaiTro", "emailHoSo", "soDienThoai", "ngaySinh", "duongDanDangNhap"],
            [LoaiMauEmail.TraLoiLienHe] =
                ["tenKhach", "tenTrungTam", "hotline"],
            [LoaiMauEmail.NhacNoHocPhi] =
                ["tenHocVien", "tenTrungTam", "tenLop", "soTienConThieu", "hanDong"],
            [LoaiMauEmail.NhacLichHoc] =
                ["tenHocVien", "tenTrungTam", "tenLop", "thoiGian", "phongHoc", "tenGiaoVien"],
        };

    /// <summary>Tiêu đề và thân mặc định cho từng loại.</summary>
    public static (string TieuDe, string NoiDungHtml) Cua(LoaiMauEmail loai) => loai switch
    {
        LoaiMauEmail.ChaoMungHocVien => (
            "Chào mừng bạn đến với {{tenTrungTam}}",
            """
            <p>Chào {{tenHocVien}},</p>
            <p>Tài khoản của bạn tại <strong>{{tenTrungTam}}</strong> đã sẵn sàng.</p>
            <p>
              Mã trung tâm: <strong>{{maTrungTam}}</strong><br>
              Tên đăng nhập: <strong>{{tenDangNhap}}</strong><br>
              Mật khẩu tạm: <strong>{{matKhauTam}}</strong>
            </p>
            <p>Đăng nhập tại: <a href="{{duongDanDangNhap}}">{{duongDanDangNhap}}</a></p>
            <p>Bạn sẽ được yêu cầu đổi mật khẩu ở lần đăng nhập đầu tiên.</p>
            <p>Hồ sơ chúng tôi đang lưu của bạn:</p>
            <p>
              Vai trò: {{vaiTro}}<br>
              Email: {{emailHoSo}}<br>
              Điện thoại: {{soDienThoai}}<br>
              Ngày sinh: {{ngaySinh}}
            </p>
            <p>Nếu có thông tin nào chưa đúng, bạn báo lại giúp trung tâm nhé.</p>
            <p>Chúc bạn học tốt!</p>
            """),

        LoaiMauEmail.TraLoiLienHe => (
            "{{tenTrungTam}} đã nhận thông tin của bạn",
            """
            <p>Chào {{tenKhach}},</p>
            <p>
              Cảm ơn bạn đã quan tâm tới <strong>{{tenTrungTam}}</strong>. Chúng tôi đã nhận
              được thông tin và sẽ liên hệ lại với bạn trong thời gian sớm nhất.
            </p>
            <p>Nếu cần trao đổi ngay, bạn có thể gọi <strong>{{hotline}}</strong>.</p>
            """),

        LoaiMauEmail.NhacNoHocPhi => (
            "Nhắc học phí lớp {{tenLop}}",
            """
            <p>Chào {{tenHocVien}},</p>
            <p>
              {{tenTrungTam}} xin nhắc bạn về khoản học phí lớp <strong>{{tenLop}}</strong>
              còn thiếu <strong>{{soTienConThieu}}</strong>, hạn đóng {{hanDong}}.
            </p>
            <p>Nếu bạn đã thanh toán, vui lòng bỏ qua email này.</p>
            """),

        LoaiMauEmail.NhacLichHoc => (
            "Nhắc lịch học lớp {{tenLop}}",
            """
            <p>Chào {{tenHocVien}},</p>
            <p>
              Bạn có buổi học lớp <strong>{{tenLop}}</strong> vào <strong>{{thoiGian}}</strong>
              tại {{phongHoc}}, giáo viên {{tenGiaoVien}}.
            </p>
            <p>Hẹn gặp bạn tại lớp!</p>
            """),

        _ => throw new ArgumentOutOfRangeException(nameof(loai), loai, "Loại mẫu email chưa khai mẫu mặc định")
    };

    /// <summary>
    /// Thay biến trong chuỗi — CHỈ những biến đã khai cho <paramref name="loai"/>.
    ///
    /// **Biến lạ được GIỮ NGUYÊN dạng `{{...}}`**, không thay bằng chuỗi rỗng. Chuỗi rỗng làm
    /// câu văn cụt mà không ai biết vì sao — còn `{{tenSai}}` nằm chình ình giữa email thì
    /// người nhận báo lại ngay, và người soạn sửa được.
    ///
    /// Lọc theo <see cref="BienCuaLoai"/> chứ không thay mọi khoá được truyền vào: nơi gọi có
    /// thể lỡ truyền thừa, và lúc đó một biến của loại khác sẽ âm thầm được thay — đúng kiểu
    /// lỗi mà danh sách biến sinh ra để chặn.
    /// </summary>
    public static string ThayBien(
        LoaiMauEmail loai, string mau, IReadOnlyDictionary<string, string?> giaTri)
    {
        if (string.IsNullOrEmpty(mau)) return mau;

        var duocPhep = BienCuaLoai.TryGetValue(loai, out var ds) ? ds : [];

        var ketQua = mau;
        foreach (var ten in duocPhep)
        {
            if (giaTri.TryGetValue(ten, out var gt))
                ketQua = ketQua.Replace($"{{{{{ten}}}}}", gt ?? string.Empty, StringComparison.Ordinal);
        }

        return ketQua;
    }
}
