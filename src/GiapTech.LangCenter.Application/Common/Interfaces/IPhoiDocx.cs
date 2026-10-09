namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Đọc key và điền giá trị vào phôi .docx (09/10/2026).
///
/// ## Vì sao là interface ở Application chứ không gọi thẳng
///
/// Việc này đụng **định dạng tệp** (zip + OpenXML), thuộc hạ tầng — cùng loại với
/// <see cref="ILuuTruTep"/>. Handler chỉ cần biết "đọc được key gì" và "điền xong trả tệp".
/// </summary>
public interface IPhoiDocx
{
    /// <summary>
    /// Danh sách key `{{...}}` trong tệp, theo thứ tự xuất hiện, đã loại trùng.
    ///
    /// Ném <c>PHOI_KHONG_DOC_DUOC</c> nếu tệp không phải .docx hợp lệ.
    /// </summary>
    IReadOnlyList<string> DocKey(Stream docx);

    /// <summary>
    /// Trả về bản .docx mới đã thay `{{key}}` bằng giá trị tương ứng.
    ///
    /// Key không có trong <paramref name="giaTri"/> **giữ nguyên `{{...}}`** thay vì xoá:
    /// chuỗi rỗng làm câu cụt mà không ai biết vì sao, còn `{{tenSai}}` nằm giữa trang thì
    /// người cầm bản in báo lại ngay. Cùng quy ước với mẫu email (`MauMacDinh.ThayBien`).
    /// </summary>
    Stream DienGiaTri(Stream docx, IReadOnlyDictionary<string, string?> giaTri);
}
