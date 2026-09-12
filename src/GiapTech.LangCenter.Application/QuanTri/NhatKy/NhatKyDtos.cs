using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.QuanTri.NhatKy;

/// <summary>
/// FR-16 — một dòng nhật ký thao tác.
///
/// Chỉ có QUERY, **không có Command nào**: nhật ký chỉ ghi thêm, không sửa không xoá. Nhật ký
/// sửa được thì không còn là nhật ký.
/// </summary>
public record NhatKyDto(
    Guid Id,
    DateTimeOffset ThoiDiem,
    string TenLenh,
    string? ChucNang,
    HanhDongNhatKy HanhDong,
    Guid? NguoiDungId,
    string? Username,
    string? HoTen,
    bool ThanhCong,
    string? MaLoi,
    string? ThamSo,
    string? ChiTiet,
    int SoBanGhiAnhHuong,
    string? DiaChiIp,
    int SoMiliGiay);

public record LayNhatKyQuery(
    string? TimKiem = null,
    string? ChucNang = null,
    HanhDongNhatKy? HanhDong = null,
    Guid? NguoiDungId = null,
    bool? ChiThatBai = null,
    DateTimeOffset? TuNgay = null,
    DateTimeOffset? DenNgay = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<NhatKyDto>>;

public class LayNhatKyHandler(IAppDbContext db)
    : IRequestHandler<LayNhatKyQuery, KetQuaTrang<NhatKyDto>>
{
    public async Task<KetQuaTrang<NhatKyDto>> Handle(
        LayNhatKyQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();

        // Query Filter lo phần cách ly tenant. Không cần tầng phạm vi riêng: nhật ký gác bằng
        // `NhatKyHeThong.Xem` mà chỉ nhóm quản trị có, và họ được xem toàn bộ trung tâm.
        var q = db.NhatKyHeThongs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(n => n.TenLenh.ToLower().Contains(tu)
                             || (n.Username != null && n.Username.ToLower().Contains(tu))
                             || (n.HoTen != null && n.HoTen.ToLower().Contains(tu)));
        }

        if (!string.IsNullOrWhiteSpace(request.ChucNang))
            q = q.Where(n => n.ChucNang == request.ChucNang);

        if (request.HanhDong is { } hd) q = q.Where(n => n.HanhDong == hd);
        if (request.NguoiDungId is { } nd) q = q.Where(n => n.NguoiDungId == nd);
        if (request.ChiThatBai == true) q = q.Where(n => !n.ThanhCong);
        if (request.TuNgay is { } tn) q = q.Where(n => n.CreatedAt >= tn);
        if (request.DenNgay is { } dn) q = q.Where(n => n.CreatedAt <= dn);

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            // Mới nhất trước: người tra nhật ký gần như luôn muốn biết "vừa rồi ai làm gì".
            .OrderByDescending(n => n.CreatedAt)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(n => new NhatKyDto(
                n.Id, n.CreatedAt, n.TenLenh, n.ChucNang, n.HanhDong,
                n.NguoiDungId, n.Username, n.HoTen,
                n.ThanhCong, n.MaLoi, n.ThamSo, n.ChiTiet,
                n.SoBanGhiAnhHuong, n.DiaChiIp, n.SoMiliGiay))
            .ToListAsync(ct);

        return new KetQuaTrang<NhatKyDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}
