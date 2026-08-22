using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// CAU_THU — hồ sơ cầu thủ (FR-04).
///
/// Độc lập hoàn toàn với tài khoản đăng nhập: một cầu thủ có thể chưa có tài khoản
/// nhưng vẫn nằm trong đội hình, được đánh giá và có tên trong danh sách đóng quỹ.
/// </summary>
public class CauThu : TenantEntity
{
    public string HoTen { get; set; } = null!;
    public string? AnhDaiDien { get; set; }
    public DateOnly? NgaySinh { get; set; }
    public DateOnly? NgayThamGia { get; set; }
    public string? GhiChu { get; set; }

    /// <summary>
    /// Số áo cố định của cầu thủ. Null khi chưa đặt.
    ///
    /// Nguồn mặc định cho áo trên bảng chiến thuật — trước đây phải gõ tay số áo cho từng
    /// người ở từng trận. Sơ đồ vẫn ghi đè được cho trận riêng lẻ (mượn áo, trùng số).
    /// **Không đặt UNIQUE**: CLB phong trào hay trùng số, và ràng buộc cứng sẽ chặn cả việc
    /// nhập liệu bình thường.
    /// </summary>
    public int? SoAo { get; set; }

    /// <summary>
    /// Vị trí sở trường (GK, CB, ST…). Null khi chưa đặt.
    /// Dùng làm gợi ý khi xếp sơ đồ, không ràng buộc cầu thủ chỉ được đá vị trí này.
    /// </summary>
    public string? ViTriSoTruong { get; set; }

    /// <summary>
    /// Cầu thủ đã dừng hoạt động với nhóm. Mặc định `false` = đang đá.
    ///
    /// Vì sao cần: trước 21/08 chỉ có **xoá cứng**, mà xoá bị chặn nếu cầu thủ từng đóng quỹ
    /// (FK `DONGGOP_QUY` là Restrict — dữ liệu tài chính phải giữ vết). Nên người đá lâu năm rồi
    /// nghỉ thì không xoá được, cũng không có cách đánh dấu: cứ nằm mãi trong mọi danh sách chọn
    /// người, mọi lời mời đăng ký, mọi ô select đội hình.
    ///
    /// Cờ này KHÔNG phải "xoá mềm". Khác biệt quan trọng:
    /// - Lịch sử **giữ nguyên và vẫn tính**: bàn thắng, phiếu MVP, số trận của họ là lịch sử thật
    ///   của CLB. Ẩn khỏi thống kê sẽ làm tỷ số trận không còn khớp tổng bàn thắng cầu thủ — đúng
    ///   lỗi im lặng mà `XoaCauThuHandler` phải tính lại tỷ số để tránh.
    /// - Chỉ ẩn khỏi các chỗ **chọn người cho việc sắp tới**: mời đăng ký, xếp đội hình, thu quỹ.
    ///
    /// Quyết định của chủ sản phẩm 21/08.
    /// </summary>
    public bool DaNghi { get; set; }

    /// <summary>
    /// Ngày dừng hoạt động. Null khi còn đá.
    ///
    /// Ghi riêng chứ không dùng `NgayCapNhat`: sửa số áo cũng đổi `NgayCapNhat`, nên nó không trả
    /// lời được "nghỉ từ khi nào" — thứ trưởng nhóm cần khi rà lại đội hình mùa trước.
    /// </summary>
    public DateOnly? NgayNghi { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public ICollection<DoiHinhTranDau> DoiHinhs { get; set; } = [];
    public ICollection<DanhGiaCauThu> DanhGias { get; set; } = [];
    public ICollection<DongGopQuy> DongGops { get; set; } = [];
}
