using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// TENANT — một CLB độc lập. Gốc của mọi dữ liệu nghiệp vụ.
///
/// Không kế thừa <see cref="TenantEntity"/>: bảng này ĐỊNH NGHĨA tenant chứ không thuộc về
/// tenant nào, nên không bị Global Query Filter lọc.
///
/// Thiết lập chung của CLB (FR-06) nằm luôn ở đây; tenant mới chưa cấu hình thì dùng mặc định.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>
    /// ID đội người dùng gõ khi đăng nhập (FR-01): mã 7 ký tự SINH TỰ ĐỘNG, duy nhất toàn
    /// hệ thống. Không để người dùng tự đặt vì tên dạng "FC ..." rất dễ trùng.
    /// Luôn lưu dạng hoa — xem <see cref="Common.MaDoi"/>.
    /// </summary>
    public string MaDoi { get; set; } = null!;

    public string TenDoi { get; set; } = null!;
    public string? TenVietTat { get; set; }
    public DateOnly? NgayThanhLap { get; set; }
    public string? LogoUrl { get; set; }
    public string? AnhBiaUrl { get; set; }
    public string? MoTa { get; set; }

    /// <summary>
    /// Bộ áo đấu của CLB, JSON mảng mã màu: <c>["trang","xanhDuong"]</c>.
    ///
    /// Một CLB thường có 2–3 bộ áo (sân nhà, sân khách, áo thủ môn). Bảng chiến thuật chỉ cho
    /// chọn trong bộ này — không thì mỗi trận lại vẽ một màu khác, xem lại lịch sử không nhận
    /// ra đội mình mặc gì.
    ///
    /// Lưu JSON thay vì bảng con: danh sách ngắn, không truy vấn theo từng màu, và thêm thuộc
    /// tính sau này (tên bộ áo, ảnh) không cần migration.
    ///
    /// Null hoặc rỗng = CLB chưa khai — sơ đồ rơi về bảng màu đầy đủ, tránh khoá người dùng
    /// khỏi tính năng chỉ vì chưa vào thiết lập.
    /// </summary>
    public string? MauAoJson { get; set; }

    public ICollection<NguoiDung> NguoiDungs { get; set; } = [];
    public ICollection<CauThu> CauThus { get; set; } = [];
}
