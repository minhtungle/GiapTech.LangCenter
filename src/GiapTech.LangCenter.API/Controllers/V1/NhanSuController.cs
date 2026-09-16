using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Application.NhanSu;
using GiapTech.LangCenter.Application.QuanTri.NguoiDung;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// HRM — nhân sự: nhân viên, giáo viên, trợ giảng. **Không gồm học viên.**
///
/// Vì sao là controller RIÊNG chứ không thêm tham số vào `/nguoi-dung`:
///
/// `/nguoi-dung` gác bằng `ChucNang.TaiKhoan` — chức năng **dùng chung** — và giáo viên cũng
/// gọi nó để chọn học viên vào lớp. Nếu đưa màn Nhân sự lên đúng endpoint đó thì trưởng phòng
/// nhân sự phải được cấp quyền `TaiKhoan`, tức là thấy luôn cả tài khoản đăng nhập của mọi
/// người — việc tách hai màn hình sẽ chẳng đổi được gì ở tầng API, nơi duy nhất chặn thật.
///
/// Endpoint này gác bằng `NhanSu` (HRM). Người quản lý nhân sự cần đúng quyền HRM,
/// không cần quyền quản trị tài khoản.
///
/// Hồ sơ con người vẫn là **một bảng `NGUOI_DUNG` duy nhất** — HRM và LMS nhìn cùng dữ liệu,
/// khác góc nhìn. Không có bảng nhân sự thứ hai (chốt 08/09/2026): hai nguồn sự thật cho cùng
/// một con người thì sửa tên một bên là bên kia sai, đúng lỗi đã gặp với tài khoản/người dùng.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/nhan-su")]
public class NhanSuController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Các vai trò nhân sự — phạm vi CỐ ĐỊNH của màn hình, người dùng không mở rộng được.
    ///
    /// Trợ giảng đi cùng giáo viên vì họ dùng chung hồ sơ `HO_SO_GIAO_VIEN`.
    ///
    /// `NhanVienKinhDoanh` thêm 16/09/2026: về mặt quyền và hồ sơ nó giống `NhanVien`, nên vào
    /// cùng màn này. Thiếu nó ở đây thì tạo được người nhưng **danh sách không hiện ra** và
    /// `/nhan-su/{id}` trả 404 — lỗi im lặng, không có gì đỏ.
    /// </summary>
    private static readonly LoaiNguoiDung[] VaiTroNhanSu =
    [
        LoaiNguoiDung.NhanVien, LoaiNguoiDung.NhanVienKinhDoanh,
        LoaiNguoiDung.GiaoVien, LoaiNguoiDung.TroGiang
    ];

    /// <summary>
    /// Danh sách nhân sự. Lọc ba vai trò ở **server** — lọc trên trang đã tải thì phân trang
    /// và tổng số bản ghi đều sai.
    ///
    /// `loaiNguoiDung` là bộ lọc người dùng chọn trong ô "Vai trò"; nó **không** phá được phạm
    /// vi ba vai trò ở trên, nên gõ thẳng `?loaiNguoiDung=HocVien` vào URL vẫn không ra học
    /// viên nào.
    /// </summary>
    [HttpGet]
    [RequirePermission(ChucNang.NhanSu, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<NguoiDungDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] LoaiNguoiDung? loaiNguoiDung,
        [FromQuery] TrangThaiNhanSu? trangThaiNhanSu,
        [FromQuery] Guid? phongBanId,
        [FromQuery] bool gomPhongBanCon = false,
        [FromQuery] Guid? chucVuId = null,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
    {
        // Bộ lọc chọn ra học viên thì bỏ qua — phạm vi màn hình thắng bộ lọc.
        var loc = loaiNguoiDung is { } l && VaiTroNhanSu.Contains(l) ? l : (LoaiNguoiDung?)null;

        // Tham số CÓ TÊN: query có 4 tham số `Guid?` liền nhau, truyền theo vị trí thì thêm
        // một bộ lọc vào giữa là lệch im lặng — lọc theo phòng hoá ra lọc theo chức vụ.
        return Ok(await sender.Send(new LayDanhSachNguoiDungQuery(
            TimKiem: timKiem,
            LoaiNguoiDung: loc,
            TrangThaiNhanSu: trangThaiNhanSu,
            Trang: new ThamSoTrang(trang, soDong),
            TrongCacLoai: VaiTroNhanSu,
            PhongBanId: phongBanId,
            GomPhongBanCon: gomPhongBanCon,
            ChucVuId: chucVuId), ct));
    }

    /// <summary>
    /// Chi tiết một hồ sơ nhân sự — cho view riêng ở `/hrm/nhan-su/{id}` (09/09/2026).
    ///
    /// Dùng lại `LayDanhSachNguoiDungQuery` với `VaiTroNhanSu` thay vì viết query mới: nhờ đó
    /// **phạm vi ba vai trò được ép ở cùng một chỗ**. Gõ id của một học viên vào URL sẽ nhận
    /// 404, không phải hồ sơ học viên — thứ mà một query riêng rất dễ để lọt.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.NhanSu, HanhDong.Xem)]
    public async Task<ActionResult<NguoiDungDto>> ChiTiet(Guid id, CancellationToken ct)
    {
        var kq = await sender.Send(new LayDanhSachNguoiDungQuery(
            null, null, null, new ThamSoTrang(1, 1), VaiTroNhanSu, id), ct);

        var u = kq.DuLieu.FirstOrDefault();
        return u is null ? NotFound() : Ok(u);
    }

    [HttpPost]
    [RequirePermission(ChucNang.NhanSu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoNguoiDungCommand command, CancellationToken ct)
    {
        BaoDamLaNhanSu(command.LoaiNguoiDung);
        return Ok(await sender.Send(command, ct));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.NhanSu, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatNguoiDungCommand command, CancellationToken ct)
    {
        BaoDamLaNhanSu(command.LoaiNguoiDung);
        await sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    // ---------- Tệp hồ sơ (FR-23) ----------

    /// <summary>
    /// Tải tệp vào hồ sơ: hợp đồng, bằng cấp scan, CCCD scan.
    ///
    /// Gác bằng `Sua` chứ không `Them`: không tạo hồ sơ mới, chỉ bổ sung vào hồ sơ đã có.
    /// </summary>
    [HttpPost("{id:guid}/tep")]
    [RequirePermission(ChucNang.NhanSu, HanhDong.QuanLyTep)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<TepHoSoDaTaiDto>> TaiTep(
        Guid id, IFormFile tep, CancellationToken ct, [FromForm] string? tenHienThi = null)
    {
        await using var s = tep.OpenReadStream();
        return Ok(await sender.Send(new TaiTepHoSoCommand(
            id, s, tep.ContentType, tep.FileName, tenHienThi), ct));
    }

    /// <summary>
    /// Đổi tên hiển thị của một tệp đã có (16/09/2026).
    ///
    /// Gác bằng `QuanLyTep` — cùng quyền với tải lên và xoá: ba việc này là một nhóm "quản lý
    /// tệp hồ sơ", tách ra thành quyền riêng chỉ làm ma trận phân quyền rậm thêm mà không ai
    /// cấu hình khác đi.
    /// </summary>
    [HttpPut("tep/{tepId:guid}/ten")]
    [RequirePermission(ChucNang.NhanSu, HanhDong.QuanLyTep)]
    public async Task<IActionResult> DoiTenTep(
        Guid tepId, [FromBody] DoiTenTepBody than, CancellationToken ct)
    {
        await sender.Send(new DoiTenTepHoSoCommand(tepId, than.TenMoi), ct);
        return NoContent();
    }

    /// <summary>Thân của lệnh đổi tên — record riêng để Swagger sinh schema đúng.</summary>
    public record DoiTenTepBody(string TenMoi);

    /// <summary>
    /// Xem tệp online hoặc tải về (10/09/2026).
    ///
    /// Gác bằng **`Xem`**, không `Sua`: đọc hợp đồng của mình không phải hành vi sửa hồ sơ.
    ///
    /// `?taiVe=true` đổi `Content-Disposition` từ `inline` sang `attachment`. Một endpoint hai
    /// chế độ chứ không hai route: cùng một phép kiểm quyền và cùng một truy vấn, tách ra chỉ
    /// nhân đôi chỗ có thể quên gác.
    ///
    /// **`Content-Type` lấy từ DB, và whitelist HRM (`LoaiTepHoSo`) mới là thứ giữ an toàn cho
    /// `inline`**: trả `inline` cho tệp do người dùng tải lên là đường XSS lưu trữ kinh điển
    /// nếu loại tệp có thể là SVG/HTML. Ở đây danh sách chỉ có PDF/Word/Excel nên không có
    /// nhánh nào chạy script; `X-Content-Type-Options: nosniff` chặn trình duyệt tự đoán lại.
    /// </summary>
    [HttpGet("tep/{tepId:guid}")]
    [RequirePermission(ChucNang.NhanSu, HanhDong.DocTep)]
    public async Task<IActionResult> XemTep(
        Guid tepId, [FromQuery] bool taiVe, CancellationToken ct)
    {
        var tep = await sender.Send(new XemTepHoSoQuery(tepId), ct);

        Response.Headers["X-Content-Type-Options"] = "nosniff";

        // `fileDownloadName` null ⇒ ASP.NET Core KHÔNG đặt `Content-Disposition: attachment`,
        // tệp hiện ngay trong tab/iframe. Có tên ⇒ tải về đúng tên gốc.
        return taiVe
            ? File(tep.NoiDung, tep.LoaiNoiDung, tep.TenGoc)
            : File(tep.NoiDung, tep.LoaiNoiDung);
    }

    [HttpDelete("tep/{tepId:guid}")]
    [RequirePermission(ChucNang.NhanSu, HanhDong.QuanLyTep)]
    public async Task<IActionResult> XoaTep(Guid tepId, CancellationToken ct)
    {
        await sender.Send(new XoaTepHoSoCommand(tepId), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.NhanSu, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaNguoiDungCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Chặn dùng endpoint HRM để tạo/sửa HỌC VIÊN.
    ///
    /// Không kiểm thì người có quyền HRM tạo được học viên qua đây — lách đúng ranh giới vừa
    /// dựng, và bằng cách gọi API trực tiếp thì UI ẩn gì cũng vô nghĩa.
    /// </summary>
    private static void BaoDamLaNhanSu(LoaiNguoiDung loai)
    {
        if (!VaiTroNhanSu.Contains(loai))
            throw new Application.Common.Exceptions.AppException("KHONG_PHAI_NHAN_SU");
    }
}
