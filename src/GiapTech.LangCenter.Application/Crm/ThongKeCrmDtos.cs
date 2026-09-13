using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.Crm;

/// <summary>
/// FR-28 — thống kê CRM (14/09/2026).
///
/// **Doanh số của một đơn tính cho NGƯỜI TẠO HỒ SƠ KHÁCH** (`KHACH_HANG.CreatedById`), chốt
/// với chủ sản phẩm 14/09. Ba cột từng là ứng viên, mỗi cột trả lời một câu khác:
///
/// | Cột | Câu hỏi | Vì sao không chọn |
/// |---|---|---|
/// | `KHACH_HANG.CreatedById` | ai **mang khách về** | ✅ đã chọn — ổn định, không đổi theo thời gian |
/// | `DANG_KY.CreatedById` | ai **nhập đơn** | lệch khi kế toán nhập hộ |
/// | `LICH_SU_CHAM_SOC.NguoiPhuTrachId` | ai **đang chăm** | doanh số QUÁ KHỨ đổi khi chuyển người chăm |
///
/// Cột thứ ba là cái bẫy đáng nói: nó phản ánh thực tế bán hàng hôm nay, nhưng báo cáo tháng
/// trước sẽ **tự đổi số** mỗi lần bàn giao khách — thứ kế toán không chấp nhận.
///
/// **Đơn cũ không có người tạo** (dữ liệu trước 12/09) gom vào nhóm `null` để UI hiện "Không
/// xác định". Không bịa, không bỏ qua: bỏ qua thì tổng các phần nhỏ hơn tổng thật mà không ai
/// biết vì sao.
/// </summary>
public record ThongKeCrmDto(
    /// <summary>Số dẫn đầu — tổng doanh thu kỳ này, quy VND.</summary>
    decimal TongDoanhThu,
    /// <summary>Kỳ trước cùng độ dài, để tính % thay đổi. Null = không có dữ liệu kỳ trước.</summary>
    decimal? DoanhThuKyTruoc,
    int SoDon,
    /// <summary>Giá trị đơn trung bình. 0 khi chưa có đơn nào — không chia cho 0.</summary>
    decimal GiaTriDonTb,
    /// <summary>Đã thu thực tế (sổ thu), quy VND. Khác `TongDoanhThu` — đăng ký là CAM KẾT.</summary>
    decimal DaThu,
    List<DiemTheoThoiGian> TheoThoiGian,
    List<PhanBoDto> TheoCaNhan,
    List<PhanBoDto> TheoDoiNhom,
    List<PhanBoDto> TheoSanPham,
    List<PhanBoDto> TheoNguon,
    List<BuocPheuDto> Pheu);

/// <summary>Một mốc thời gian trên đường doanh thu.</summary>
public record DiemTheoThoiGian(string Nhan, DateTimeOffset Moc, decimal DoanhThu, int SoDon);

/// <summary>
/// Một lát của phép chia — người, đội, sản phẩm hay nguồn.
///
/// `Ten` null = nhóm "không xác định" (đơn cũ chưa có người tạo, người chưa gán phòng ban).
/// Để UI tự đặt tên thay vì backend nhét chuỗi tiếng Việt — quy tắc #3.
/// </summary>
public record PhanBoDto(Guid? Id, string? Ten, decimal DoanhThu, int SoDon);

/// <summary>Một bước trong phễu bán hàng, kèm tỷ lệ chuyển đổi từ bước trước.</summary>
public record BuocPheuDto(TrangThaiKhachHang TrangThai, int SoKhach);

public record LayThongKeCrmQuery(
    DateTimeOffset? TuNgay = null,
    DateTimeOffset? DenNgay = null) : IRequest<ThongKeCrmDto>;

public class LayThongKeCrmHandler(IAppDbContext db, IMuiGioTrungTam muiGio)
    : IRequestHandler<LayThongKeCrmQuery, ThongKeCrmDto>
{
    public async Task<ThongKeCrmDto> Handle(LayThongKeCrmQuery request, CancellationToken ct)
    {
        var tz = await muiGio.LayMuiGio(ct);
        var bayGio = DateTimeOffset.UtcNow;

        // Mặc định 12 tháng gần nhất. Lấy mốc theo MÚI GIỜ TRUNG TÂM rồi mới quy về tuyệt đối:
        // đơn lúc 8h sáng ngày 1 là 1h UTC cùng ngày, nhưng đơn 6h sáng là 23h UTC hôm trước —
        // cắt kỳ theo UTC sẽ đẩy đơn sáng sớm sang kỳ trước (bài học FR-15).
        var homNay = TimeZoneInfo.ConvertTime(bayGio, tz).Date;
        var den = request.DenNgay ?? new DateTimeOffset(homNay.AddDays(1), tz.GetUtcOffset(homNay));
        var tu = request.TuNgay
                 ?? new DateTimeOffset(homNay.AddMonths(-11).AddDays(1 - homNay.Day),
                                       tz.GetUtcOffset(homNay));

        // Kỳ trước CÙNG ĐỘ DÀI, ngay sát kỳ này — so "tháng này với tháng trước" chỉ có nghĩa
        // khi hai kỳ dài bằng nhau.
        var doDai = den - tu;
        var tuKyTruoc = tu - doDai;

        var donTrongKy = db.DangKyKhoaHocs
            .Where(d => d.NgayDangKy >= tu && d.NgayDangKy < den);

        // `SoTien * TyGiaVeVnd` — quy về VND bằng tỷ giá CHỤP LÚC ĐĂNG KÝ, không đọc động.
        // Đọc động thì báo cáo quý trước tự đổi số mỗi lần tỷ giá nhảy.
        var tongDoanhThu = await donTrongKy.SumAsync(d => d.SoTien * d.TyGiaVeVnd, ct);
        var soDon = await donTrongKy.CountAsync(ct);

        var doanhThuKyTruoc = await db.DangKyKhoaHocs
            .Where(d => d.NgayDangKy >= tuKyTruoc && d.NgayDangKy < tu)
            .SumAsync(d => (decimal?)(d.SoTien * d.TyGiaVeVnd), ct);

        var daThu = await db.ThuTienDangKys
            .Where(t => t.NgayThu >= tu && t.NgayThu < den)
            .SumAsync(t => t.SoTien * t.DangKy.TyGiaVeVnd, ct);

        // ---------- Theo thời gian: gom theo THÁNG ----------
        var theoThang = await donTrongKy
            .GroupBy(d => new { d.NgayDangKy.Year, d.NgayDangKy.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                DoanhThu = g.Sum(d => d.SoTien * d.TyGiaVeVnd),
                SoDon = g.Count()
            })
            .ToListAsync(ct);

        var theoThoiGian = theoThang
            .Select(x => new DiemTheoThoiGian(
                $"{x.Month:00}/{x.Year}",
                new DateTimeOffset(new DateTime(x.Year, x.Month, 1), TimeSpan.Zero),
                x.DoanhThu, x.SoDon))
            .OrderBy(x => x.Moc)
            .ToList();

        // ---------- Theo cá nhân: người TẠO HỒ SƠ KHÁCH ----------
        var theoCaNhan = (await donTrongKy
                .GroupBy(d => new { d.KhachHang.CreatedById, Ten = d.KhachHang.CreatedBy!.HoTen })
                .Select(g => new PhanBoDto(
                    g.Key.CreatedById, g.Key.Ten,
                    g.Sum(d => d.SoTien * d.TyGiaVeVnd), g.Count()))
                .ToListAsync(ct))
            .OrderByDescending(x => x.DoanhThu)
            .ToList();

        // ---------- Theo đội nhóm: phòng ban của người đó ----------
        var theoDoiNhom = (await donTrongKy
                .GroupBy(d => new
                {
                    Id = d.KhachHang.CreatedBy!.PhongBanId,
                    Ten = d.KhachHang.CreatedBy!.PhongBan!.Ten
                })
                .Select(g => new PhanBoDto(
                    g.Key.Id, g.Key.Ten,
                    g.Sum(d => d.SoTien * d.TyGiaVeVnd), g.Count()))
                .ToListAsync(ct))
            .OrderByDescending(x => x.DoanhThu)
            .ToList();

        // ---------- Theo mặt hàng: khoá học HOẶC sản phẩm ----------
        // Hai FK loại trừ nhau nên gom hai lượt rồi nối, thay vì một GroupBy có điều kiện —
        // EF dịch được cả hai, nhưng hai lượt đọc rõ hơn khi cần sửa.
        var theoKhoa = await donTrongKy
            .Where(d => d.KhoaHocId != null)
            .GroupBy(d => new { Id = d.KhoaHocId, Ten = d.KhoaHoc!.Ten })
            .Select(g => new PhanBoDto(
                g.Key.Id, g.Key.Ten, g.Sum(d => d.SoTien * d.TyGiaVeVnd), g.Count()))
            .ToListAsync(ct);

        var theoSp = await donTrongKy
            .Where(d => d.SanPhamId != null)
            .GroupBy(d => new { Id = d.SanPhamId, Ten = d.SanPham!.Ten })
            .Select(g => new PhanBoDto(
                g.Key.Id, g.Key.Ten, g.Sum(d => d.SoTien * d.TyGiaVeVnd), g.Count()))
            .ToListAsync(ct);

        var theoSanPham = theoKhoa.Concat(theoSp)
            .OrderByDescending(x => x.DoanhThu).ToList();

        // ---------- Theo nguồn khách ----------
        var theoNguon = (await donTrongKy
                .GroupBy(d => d.KhachHang.Nguon)
                .Select(g => new
                {
                    Nguon = g.Key,
                    DoanhThu = g.Sum(d => d.SoTien * d.TyGiaVeVnd),
                    SoDon = g.Count()
                })
                .ToListAsync(ct))
            .Select(x => new PhanBoDto(null, x.Nguon.ToString(), x.DoanhThu, x.SoDon))
            .OrderByDescending(x => x.DoanhThu)
            .ToList();

        // ---------- Phễu bán hàng ----------
        // Trạng thái hiện tại = `TrangThaiSau` của lần chăm sóc MỚI NHẤT. Khách chưa có lần
        // chăm sóc nào coi là `Moi` — xem chú thích ở `TrangThaiKhachHang`.
        var trangThaiTungKhach = await db.KhachHangs
            .Select(k => k.LichSuChamSocs
                .OrderByDescending(l => l.ThoiDiem)
                .Select(l => (TrangThaiKhachHang?)l.TrangThaiSau)
                .FirstOrDefault())
            .ToListAsync(ct);

        var dem = trangThaiTungKhach
            .Select(t => t ?? TrangThaiKhachHang.Moi)
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        // Giữ ĐỦ 4 bước kể cả bước có 0 khách: phễu thiếu bước là phễu đọc sai, và "0 khách ở
        // bước Tư vấn" tự nó là thông tin.
        var pheu = Enum.GetValues<TrangThaiKhachHang>()
            .Select(t => new BuocPheuDto(t, dem.GetValueOrDefault(t)))
            .ToList();

        return new ThongKeCrmDto(
            tongDoanhThu,
            doanhThuKyTruoc,
            soDon,
            soDon == 0 ? 0 : tongDoanhThu / soDon,
            daThu,
            theoThoiGian,
            theoCaNhan,
            theoDoiNhom,
            theoSanPham,
            theoNguon,
            pheu);
    }
}
