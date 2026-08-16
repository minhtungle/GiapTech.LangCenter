using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Application.LichThiDau.TranDau;

/// <summary>
/// FR-07 — bộ lọc trận đấu. **Dùng chung với FR-12 (Thống kê)**, không dựng hai bộ song song.
///
/// Mọi trường đều tuỳ chọn: bỏ trống nghĩa là không lọc theo tiêu chí đó.
/// </summary>
public record BoLocTranDau(
    DateOnly? TuNgay = null,
    DateOnly? DenNgay = null,
    // KetQua/TrangThai là danh sách: cho phép lọc nhiều giá trị cùng lúc (vd thắng và hòa).
    List<KetQuaTranDau>? KetQua = null,
    List<TrangThaiTranDau>? TrangThai = null,
    Guid? DoiThuId = null,
    int? BanThangToiThieu = null,
    int? BanThangToiDa = null,
    int? BanThuaToiThieu = null,
    int? BanThuaToiDa = null);

public static class BoLocTranDauExtensions
{
    /// <summary>
    /// Áp bộ lọc lên truy vấn. Tách thành extension để FR-07 (lịch) và FR-12 (thống kê) dùng
    /// đúng một cài đặt — hai bản sao sẽ trôi lệch nhau ngay lần sửa đầu tiên.
    /// </summary>
    public static IQueryable<Domain.Entities.TranDau> ApBoLoc(
        this IQueryable<Domain.Entities.TranDau> q, BoLocTranDau loc)
    {
        if (loc.TuNgay is { } tu)
        {
            var moc = new DateTimeOffset(tu.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            q = q.Where(t => t.ThoiGian >= moc);
        }

        if (loc.DenNgay is { } den)
        {
            // Lấy hết ngày "đến": người dùng chọn 31/12 là muốn cả các trận trong ngày đó,
            // không phải chỉ tới 00:00.
            var moc = new DateTimeOffset(den.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            q = q.Where(t => t.ThoiGian <= moc);
        }

        if (loc.KetQua is { Count: > 0 } kq)
            q = q.Where(t => kq.Contains(t.KetQua));

        if (loc.TrangThai is { Count: > 0 } tt)
            q = q.Where(t => tt.Contains(t.TrangThai));

        if (loc.DoiThuId is { } dt)
            q = q.Where(t => t.DoiThuId == dt);

        if (loc.BanThangToiThieu is { } btMin)
            q = q.Where(t => t.TySoNha >= btMin);

        if (loc.BanThangToiDa is { } btMax)
            q = q.Where(t => t.TySoNha <= btMax);

        if (loc.BanThuaToiThieu is { } bthMin)
            q = q.Where(t => t.TySoKhach >= bthMin);

        if (loc.BanThuaToiDa is { } bthMax)
            q = q.Where(t => t.TySoKhach <= bthMax);

        return q;
    }
}
