namespace GiapTech.LangCenter.LMS.Application.Common.Interfaces;

/// <summary>Tệp đọc về từ kho lưu trữ, kèm tên gốc để trình duyệt tải xuống đúng tên.</summary>
public record TepTaiVe(Stream NoiDung, string LoaiNoiDung, string TenGoc);

/// <summary>Kết quả tải lên: khoá để lưu DB + metadata để hiển thị.</summary>
public record TepDaTaiLen(string Khoa, string TenGoc, string LoaiNoiDung, long KichThuoc);

/// <summary>
/// Kho lưu trữ TỆP tài liệu (PDF, Word, Excel, PowerPoint, ảnh, nén) — dùng cho tài liệu
/// giảng dạy, đề bài và bài học viên nộp.
///
/// **Tách khỏi <see cref="ILuuTruAnh"/> chứ không mở rộng nó**: hai kho có ràng buộc khác hẳn
/// nhau. Ảnh đại diện/logo chỉ nhận 4 định dạng raster và tối đa 5 MB vì chúng hiển thị
/// inline trên mọi trang; tệp bài tập cần PDF/DOCX và hạn mức lớn hơn nhiều. Nới lỏng
/// `ILuuTruAnh` để dùng chung sẽ cho phép tải PDF lên làm logo — và làm đổ các test đang canh
/// đúng hành vi đó.
///
/// Khoá vẫn mang tenant ở đầu (<c>{tenantId}/{loai}/{guid}{ext}</c>) — kho lưu trữ không có
/// Global Query Filter, cách ly phải nằm ngay trong đường dẫn (quy tắc #2).
/// </summary>
public interface ILuuTruTep
{
    /// <summary>
    /// Tải tệp lên, trả về khoá + metadata. Ném <c>LOAI_TEP_KHONG_HO_TRO</c>,
    /// <c>TEP_QUA_LON</c>, <c>TEP_RONG</c> khi đầu vào không hợp lệ.
    /// </summary>
    Task<TepDaTaiLen> TaiLen(
        Stream noiDung, string loaiNoiDung, string tenGoc, string loai, CancellationToken ct);

    /// <summary>Đọc tệp theo khoá. Trả null nếu không có hoặc khoá thuộc tenant khác.</summary>
    Task<TepTaiVe?> TaiVe(string khoa, CancellationToken ct);

    /// <summary>Xoá tệp. Không ném khi khoá không tồn tại — cùng lý do với ảnh.</summary>
    Task Xoa(string khoa, CancellationToken ct);
}
