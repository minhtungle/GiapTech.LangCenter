using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Application.Crm;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>FR-17 — khách hàng (CRM).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/khach-hang")]
public class KhachHangController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<KhachHangDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] bool? daMua,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachKhachHangQuery(
            timKiem, daMua, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuKhachHangCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuKhachHangCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaKhachHangCommand(id), ct);
        return NoContent();
    }

    // ---------- View chi tiết khách hàng: 4 tab ----------

    /// <summary>Tab Thông tin chung — hồ sơ + số liệu tổng hợp.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Xem)]
    public async Task<ActionResult<ChiTietKhachHangDto>> ChiTiet(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayChiTietKhachHangQuery(id), ct));

    /// <summary>Tab Lịch sử chăm sóc.</summary>
    [HttpGet("{id:guid}/cham-soc")]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Xem)]
    public async Task<ActionResult<List<LichSuChamSocDto>>> ChamSoc(
        Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayLichSuChamSocQuery(id), ct));

    [HttpPost("{id:guid}/cham-soc")]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Them)]
    public async Task<ActionResult<Guid>> ThemChamSoc(
        Guid id, [FromBody] LuuChamSocCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null, KhachHangId = id }, ct));

    [HttpPut("cham-soc/{chamSocId:guid}")]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> SuaChamSoc(
        Guid chamSocId, [FromBody] LuuChamSocCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = chamSocId }, ct));

    [HttpDelete("cham-soc/{chamSocId:guid}")]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Xoa)]
    public async Task<IActionResult> XoaChamSoc(Guid chamSocId, CancellationToken ct)
    {
        await sender.Send(new XoaChamSocCommand(chamSocId), ct);
        return NoContent();
    }

    /// <summary>
    /// Khách MUA HÀNG từ màn chăm sóc — ghi đơn hàng **và** một dòng lịch sử chăm sóc trong
    /// CÙNG một transaction (xem `MuaHangHandler`).
    ///
    /// Gác bằng `DoanhThu` chứ không `KhachHang`: đây là ghi dữ liệu tiền.
    /// </summary>
    [HttpPost("{id:guid}/mua-hang")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> MuaHang(
        Guid id, [FromBody] MuaHangCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { KhachHangId = id }, ct));

    /// <summary>
    /// Tab Khoá học tham gia + Số tiền đã đóng — cùng một nguồn dữ liệu, hai góc nhìn.
    ///
    /// Gác bằng `DoanhThu` chứ không `KhachHang`: đây là **số tiền**. Người trực tổng đài xem
    /// được hồ sơ và lịch sử chăm sóc mà không thấy khách đã trả bao nhiêu.
    /// </summary>
    [HttpGet("{id:guid}/dang-ky")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Xem)]
    public async Task<ActionResult<List<DangKyKemThuDto>>> DangKyCuaKhach(
        Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayDangKyCuaKhachQuery(id), ct));
}

/// <summary>FR-19 — danh mục khoá học bán ra (CRM).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/khoa-hoc")]
public class KhoaHocController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.KhoaHoc, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<KhoaHocDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] bool? dangBan,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachKhoaHocQuery(
            timKiem, dangBan, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.KhoaHoc, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuKhoaHocCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.KhoaHoc, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuKhoaHocCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.KhoaHoc, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaKhoaHocCommand(id), ct);
        return NoContent();
    }
}

/// <summary>FR-20 — danh mục sản phẩm bán kèm: sách, học cụ (CRM).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/san-pham")]
public class SanPhamController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.SanPham, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<SanPhamDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] bool? dangBan,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachSanPhamQuery(
            timKiem, dangBan, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.SanPham, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuSanPhamCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.SanPham, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuSanPhamCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.SanPham, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaSanPhamCommand(id), ct);
        return NoContent();
    }
}

/// <summary>
/// FR-18 — doanh thu: đăng ký khoá học (CRM).
///
/// Gác bằng `DoanhThu` chứ không `KhachHang`: người trực tổng đài nhập khách mới cần quyền
/// khách hàng mà **không** nên thấy số tiền của mọi đơn hàng.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doanh-thu")]
public class DoanhThuController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<DangKyDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] Guid? khachHangId,
        [FromQuery] Guid? khoaHocId,
        [FromQuery] DateTimeOffset? tuNgay,
        [FromQuery] DateTimeOffset? denNgay,
        [FromQuery] Guid? sanPhamId,
        [FromQuery] LoaiDonHang? loai,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDoanhThuQuery(
            timKiem, khachHangId, khoaHocId, tuNgay, denNgay,
            new ThamSoTrang(trang, soDong), sanPhamId, loai), ct));

    /// <summary>
    /// Tổng hợp trên TOÀN BỘ tập đã lọc, không chỉ trang đang xem — endpoint riêng vì cộng
    /// trên trang hiện tại là số vô nghĩa mà người dùng rất dễ tin là tổng thật.
    /// </summary>
    [HttpGet("tong-hop")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Xem)]
    public async Task<ActionResult<TongHopDoanhThuDto>> TongHop(
        [FromQuery] string? timKiem,
        [FromQuery] Guid? khachHangId,
        [FromQuery] Guid? khoaHocId,
        [FromQuery] DateTimeOffset? tuNgay,
        [FromQuery] DateTimeOffset? denNgay,
        [FromQuery] Guid? sanPhamId,
        [FromQuery] LoaiDonHang? loai,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayTongHopDoanhThuQuery(
            timKiem, khachHangId, khoaHocId, tuNgay, denNgay, sanPhamId, loai), ct));

    [HttpPost]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuDangKyCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuDangKyCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaDangKyCommand(id), ct);
        return NoContent();
    }

    // ---------- Sổ thu tiền của một đăng ký ----------

    /// <summary>
    /// Ghi một lần khách đóng tiền. Đăng ký là **cam kết**; đây là tiền thật đã nhận.
    /// </summary>
    [HttpPost("{id:guid}/thu-tien")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> ThuTien(
        Guid id, [FromBody] LuuThuTienCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null, DangKyId = id }, ct));

    [HttpPut("thu-tien/{thuId:guid}")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> SuaThuTien(
        Guid thuId, [FromBody] LuuThuTienCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = thuId }, ct));

    [HttpDelete("thu-tien/{thuId:guid}")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Xoa)]
    public async Task<IActionResult> XoaThuTien(Guid thuId, CancellationToken ct)
    {
        await sender.Send(new XoaThuTienCommand(thuId), ct);
        return NoContent();
    }

    // ---------- Yêu cầu xếp lớp (FR-21) — cầu nối CRM → LMS ----------

    /// <summary>
    /// Sale bán xong một khoá thì gửi yêu cầu sang bên đào tạo xếp lớp.
    ///
    /// Gác bằng <see cref="ChucNang.DoanhThu"/> chứ không <see cref="ChucNang.LopHoc"/>: đây là
    /// hành động của người BÁN trên đơn của mình, không phải hành động xếp lớp. Người bán không
    /// cần và không nên có quyền vào module lớp học.
    /// </summary>
    [HttpPost("{id:guid}/yeu-cau-xep-lop")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> GuiYeuCauXepLop(
        Guid id, [FromBody] GuiYeuCauXepLopCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { DangKyId = id }, ct));
    /// <summary>
    /// FR-28 — thống kê CRM: doanh thu theo thời gian · cá nhân · đội nhóm · mặt hàng · nguồn,
    /// kèm phễu bán hàng và công nợ.
    ///
    /// Gác bằng `DoanhThu.Xem` chứ không `KhachHang.Xem`: đây là **số tiền toàn trung tâm**, và
    /// người trực tổng đài có quyền khách hàng nhưng không nên thấy doanh số của cả đội.
    /// </summary>
    [HttpGet("~/api/v{version:apiVersion}/thong-ke-crm")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Xem)]
    public async Task<ActionResult<ThongKeCrmDto>> ThongKe(
        [FromQuery] DateTimeOffset? tuNgay,
        [FromQuery] DateTimeOffset? denNgay,
        [FromQuery] LoaiThongKe loai = LoaiThongKe.KhoaHoc,
        [FromQuery] List<Guid>? chiMuc = null,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayThongKeCrmQuery(tuNgay, denNgay, loai, chiMuc), ct));
}
