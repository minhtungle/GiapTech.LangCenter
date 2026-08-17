using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>DOIHINH_TRANDAU — cầu thủ tham gia trận và vị trí (FR-10 tab a).</summary>
public class DoiHinhTranDau : TenantEntity
{
    public Guid TranDauId { get; set; }
    public TranDau TranDau { get; set; } = null!;

    public Guid CauThuId { get; set; }
    public CauThu CauThu { get; set; } = null!;

    public string? ViTri { get; set; }
    public bool LaDuBi { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// SODO_CHIENTHUAT — sơ đồ kéo-thả, quan hệ 1—1 với trận (FR-10 tab b).
/// Toạ độ cầu thủ lưu JSON vì hình dạng dữ liệu do UI quyết định, không truy vấn theo từng ô.
/// </summary>
public class SoDoChienThuat : TenantEntity
{
    public Guid TranDauId { get; set; }
    public TranDau TranDau { get; set; } = null!;

    /// <summary>
    /// Nội dung sơ đồ: <c>{ loaiSan, hiep1: {viTri, doiThu}, hiep2: {...} }</c>.
    ///
    /// Hai hiệp nằm trong cùng một bản ghi vì chúng luôn được đọc/ghi cùng nhau và quan hệ
    /// với trận vẫn là 1—1. Tách thành hai hàng sẽ phải thêm cột "hiệp" và mọi truy vấn đều
    /// phải nhớ lọc nó.
    ///
    /// Bản ghi cũ chỉ có <c>{viTri, doiThu}</c> phẳng — frontend đọc được cả hai dạng, coi
    /// dạng phẳng là hiệp 1. Không có bước này thì mọi sơ đồ đã lưu trước đây biến mất khỏi
    /// màn hình (quy tắc #1).
    /// </summary>
    public string SoDoJson { get; set; } = "{}";
    public string? GhiChuChienThuat { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// DANHGIA_CAUTHU — đánh giá từng cầu thủ sau trận (FR-10 tab c).
/// Nguồn dữ liệu của bảng xếp hạng MVP theo bàn thắng / cứu thua / chỉ số kỹ năng (FR-14).
/// </summary>
public class DanhGiaCauThu : TenantEntity
{
    public Guid TranDauId { get; set; }
    public TranDau TranDau { get; set; } = null!;

    public Guid CauThuId { get; set; }
    public CauThu CauThu { get; set; } = null!;

    public int SoBanGhiDuoc { get; set; }
    public int SoBanCuuThua { get; set; }

    /// <summary>Bộ chỉ số kỹ năng dạng JSON — thêm tiêu chí mới không cần migration.</summary>
    public string? ChiSoKyNang { get; set; }

    public string? GhiChu { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// VOTE_MVP — bình chọn cầu thủ xuất sắc (FR-10 tab c).
///
/// Quy tắc bất di bất dịch #8: mỗi người tối đa 1 tim/trận.
/// Ràng buộc UNIQUE(tran_dau_id, nguoi_vote_id) đặt ở tầng DB — chặn ở UI là không đủ,
/// hai request đồng thời vẫn lọt qua kiểm tra tại tầng ứng dụng.
/// </summary>
public class VoteMvp : TenantEntity
{
    public Guid TranDauId { get; set; }
    public TranDau TranDau { get; set; } = null!;

    /// <summary>Tài khoản thực hiện vote.</summary>
    public Guid NguoiVoteId { get; set; }

    /// <summary>Cầu thủ được vote (hồ sơ cầu thủ, vì người được vote có thể chưa có tài khoản).</summary>
    public Guid CauThuDuocVoteId { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
