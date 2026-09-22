namespace GiapTech.LangCenter.API;

/// <summary>
/// Cờ tính năng bật/tắt bằng cấu hình (22/09/2026).
///
/// **Một nguồn sự thật duy nhất** cho cả endpoint `/tinh-nang` (frontend đọc để ẩn/hiện lối
/// vào) và chốt chặn thật trong controller. Hai chỗ tự đọc cấu hình riêng thì sẽ có ngày lệch
/// nhau — cờ nói "bật" mà endpoint trả 404, đúng cái ngõ cụt mà cờ sinh ra để tránh.
/// </summary>
public static class TinhNang
{
    /// <summary>
    /// Cho phép tự đăng ký trung tâm qua `POST /dang-ky-trung-tam`.
    ///
    /// **Mặc định TẮT** từ 22/09/2026 (chủ sản phẩm chốt đóng hẳn). Mặc định tắt chứ không
    /// mặc định bật: quên cấu hình ở môi trường thật thì hậu quả là "không ai tạo được trung
    /// tâm" (phiền, dễ thấy) chứ không phải "ai cũng tạo được" (âm thầm, nguy hiểm).
    ///
    /// Bật lại: đặt biến môi trường <c>CHO_TU_DANG_KY=true</c>. Bộ E2E cần nó vì mỗi test tự
    /// tạo một trung tâm.
    /// </summary>
    public const string KhoaChoTuDangKy = "CHO_TU_DANG_KY";

    public static bool ChoTuDangKy(IConfiguration config) =>
        bool.TryParse(config[KhoaChoTuDangKy], out var bat) && bat;
}
