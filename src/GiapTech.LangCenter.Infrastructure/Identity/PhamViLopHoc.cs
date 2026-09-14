using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Infrastructure.Identity;

/// <summary>
/// Cài đặt <see cref="IPhamViLopHoc"/> — xem interface để biết vì sao cần tầng này.
///
/// Nhận ra "người quản trị" bằng chức năng <see cref="ChucNang.LopHocToanTrungTam"/>, KHÔNG
/// bằng tên nhóm quyền: tên là chuỗi admin tự sửa được, đổi "Quản trị viên" thành "Ban giám
/// hiệu" không được phép làm mất quyền quản trị. Hỏi qua `IQuyenService` nên dùng lại cache
/// 5 phút đã có, không thêm truy vấn nào.
/// </summary>
public class PhamViLopHoc(
    IQuyenService quyenService,
    ICurrentTenant tenant,
    ICurrentUser currentUser,
    IAppDbContext db)
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
            (l.TrangThai != TrangThaiLopHoc.Nhap || l.CreatedById == uid)
            && (l.GiaoVienChinhId == uid
                || l.CreatedById == uid
                || l.TroGiangs.Any(tg => tg.TroGiangId == uid)
                || l.HocViens.Any(hv => hv.HocVienId == uid)));
    }

    public async Task<IQueryable<NguoiDung>> LocHocVienTheoPhamVi(
        IQueryable<NguoiDung> nguon, CancellationToken ct)
    {
        // Người xem được MỌI lớp thì cũng xem được mọi học viên — cùng một loại người điều
        // hành, và họ đã thấy toàn bộ danh sách lớp kèm sĩ số rồi.
        if (await ThayMoiLop(HanhDong.Xem, ct)) return nguon;

        if (currentUser.UserId is not { } uid) return nguon.Where(_ => false);

        // Đi từ bảng GHI DANH chứ không từ `NguoiDung`: entity người dùng cố ý không có
        // navigation ngược về lớp (nó là hồ sơ con người, không phải thực thể của LMS).
        var hocVienCuaLopMinh = db.LopHocHocViens
            .Where(lh => lh.LopHoc.GiaoVienChinhId == uid
                         || lh.LopHoc.TroGiangs.Any(tg => tg.TroGiangId == uid))
            .Select(lh => lh.HocVienId);

        return nguon.Where(n =>
            // Chính mình — học viên có `TaiKhoan.Xem` để xem hồ sơ của bản thân.
            n.Id == uid
            || hocVienCuaLopMinh.Contains(n.Id));
    }
}
