using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Entities;
using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Infrastructure.Identity;

/// <summary>
/// Cài đặt <see cref="IPhamViLopHoc"/> — xem interface để biết vì sao cần tầng này.
///
/// Nhận ra "người quản trị" bằng chức năng <see cref="ChucNang.LopHocToanTrungTam"/>, KHÔNG
/// bằng tên nhóm quyền: tên là chuỗi admin tự sửa được, đổi "Quản trị viên" thành "Ban giám
/// hiệu" không được phép làm mất quyền quản trị. Hỏi qua `IQuyenService` nên dùng lại cache
/// 5 phút đã có, không thêm truy vấn nào.
/// </summary>
public class PhamViLopHoc(IQuyenService quyenService, ICurrentTenant tenant, ICurrentUser currentUser)
    : IPhamViLopHoc
{
    public async Task<bool> ThayMoiLop(HanhDong hanhDong, CancellationToken ct)
    {
        // TaiKhoanId cho việc TRA QUYỀN (quyền gán cho tài khoản), UserId cho việc LỌC DỮ
        // LIỆU (khoá ngoại trỏ tới người). Lẫn hai thứ này sẽ trả về rỗng một cách im lặng.
        if (tenant.TenantId is not { } tid || currentUser.TaiKhoanId is not { } tkId)
            return false;

        return await quyenService.CoQuyenAsync(
            tid, tkId, ChucNang.LopHocToanTrungTam, hanhDong, ct);
    }

    public async Task<IQueryable<LopHoc>> LocTheoPhamVi(
        IQueryable<LopHoc> nguon, HanhDong hanhDong, CancellationToken ct)
    {
        if (await ThayMoiLop(hanhDong, ct)) return nguon;

        // Chưa đăng nhập thì không thấy gì — an toàn hơn là thấy tất cả.
        if (currentUser.UserId is not { } uid) return nguon.Where(_ => false);

        return nguon.Where(l =>
            // Lớp nháp chỉ người tạo mới thấy: nó chưa phải lớp thật, và giáo viên nhìn thấy
            // tên mình trong một lớp admin còn đang nghĩ sẽ tưởng đã được phân công.
            (l.TrangThai != TrangThaiLopHoc.Nhap || l.NguoiTaoId == uid)
            && (l.GiaoVienChinhId == uid
                || l.NguoiTaoId == uid
                || l.TroGiangs.Any(tg => tg.TroGiangId == uid)
                || l.HocViens.Any(hv => hv.HocVienId == uid)));
    }
}
