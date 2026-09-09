using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Infrastructure.Identity;

/// <summary>
/// Cài đặt <see cref="IPhamViHocPhi"/> — xem interface để biết vì sao tách khỏi phạm vi lớp.
///
/// Nhận ra "người quản lý tài chính" bằng <see cref="ChucNang.LopHocToanTrungTam"/>, cùng cách
/// với phạm vi lớp: suy từ dữ liệu quyền chứ không từ tên nhóm.
/// </summary>
public class PhamViHocPhi(IQuyenService quyenService, ICurrentTenant tenant, ICurrentUser currentUser)
    : IPhamViHocPhi
{
    public async Task<IQueryable<KhoanThuHocPhi>> LocKhoanThu(
        IQueryable<KhoanThuHocPhi> nguon, CancellationToken ct)
    {
        if (await ThayToanBoSo(HanhDong.Xem, ct)) return nguon;

        // Không thấy toàn bộ sổ thì chỉ thấy khoản thu của chính mình — kể cả giáo viên.
        // Học phí là quan hệ giữa học viên và trung tâm, không phải việc của người dạy.
        if (currentUser.UserId is not { } uid) return nguon.Where(_ => false);

        return nguon.Where(k => k.HocVienId == uid);
    }

    public async Task<IQueryable<KhoanThuHocPhi>> LocKhoanThuDuocSua(
        IQueryable<KhoanThuHocPhi> nguon, CancellationToken ct)
    {
        // Sửa/xoá sổ thu là đặc quyền của người quản lý tài chính. Học viên xem được khoản thu
        // của mình nhưng KHÔNG bao giờ sửa được — nếu không thì họ tự xoá nợ.
        if (await ThayToanBoSo(HanhDong.Sua, ct)) return nguon;

        return nguon.Where(_ => false);
    }

    public Task<bool> DuocXemTienCuaLop(CancellationToken ct)
        => ThayToanBoSo(HanhDong.Xem, ct);

    public Task<bool> DuocGhiSo(CancellationToken ct) => ThayToanBoSo(HanhDong.Them, ct);

    public async Task<IQueryable<LopHocHocVien>> LocHocVienTrongLop(
        IQueryable<LopHocHocVien> nguon, CancellationToken ct)
    {
        if (await ThayToanBoSo(HanhDong.Xem, ct)) return nguon;

        if (currentUser.UserId is not { } uid) return nguon.Where(_ => false);

        return nguon.Where(hv => hv.HocVienId == uid);
    }

    /// <summary>
    /// Thấy toàn bộ sổ thu = có quyền trên học phí VÀ thấy được mọi lớp của trung tâm.
    ///
    /// Cần cả hai: chỉ `HocPhi.Xem` thì học viên cũng có (để tự tra nợ), chỉ
    /// `LopHocToanTrungTam` thì chưa nói gì về tiền.
    /// </summary>
    private async Task<bool> ThayToanBoSo(HanhDong hanhDong, CancellationToken ct)
    {
        // TaiKhoanId cho tra quyền, UserId cho lọc dữ liệu — xem chú thích ở PhamViLopHoc.
        if (tenant.TenantId is not { } tid || currentUser.TaiKhoanId is not { } tkId)
            return false;

        return await quyenService.CoQuyenAsync(tid, tkId, ChucNang.HocPhi, hanhDong, ct)
               && await quyenService.CoQuyenAsync(
                   tid, tkId, ChucNang.LopHocToanTrungTam, HanhDong.Xem, ct);
    }
}
