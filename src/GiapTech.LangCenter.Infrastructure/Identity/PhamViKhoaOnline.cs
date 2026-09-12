using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Infrastructure.Identity;

/// <summary>
/// Cài đặt <see cref="IPhamViKhoaOnline"/> — xem interface để biết ba nhánh và vì sao cần
/// tầng riêng.
/// </summary>
public class PhamViKhoaOnline(
    IQuyenService quyenService,
    ICurrentTenant tenant,
    ICurrentUser currentUser)
    : IPhamViKhoaOnline
{
    public async Task<bool> DuocSoanNoiDung(HanhDong hanhDong, CancellationToken ct)
    {
        // TaiKhoanId cho việc TRA QUYỀN, UserId cho việc LỌC DỮ LIỆU. Lẫn hai thứ này sẽ trả
        // về rỗng một cách im lặng, không có lỗi biên dịch.
        if (tenant.TenantId is not { } tid || currentUser.TaiKhoanId is not { } tkId)
            return false;

        return await quyenService.CoQuyenAsync(tid, tkId, ChucNang.KhoaOnline, hanhDong, ct);
    }

    public async Task<IQueryable<KhoaOnline>> LocKhoa(
        IQueryable<KhoaOnline> nguon, CancellationToken ct)
    {
        if (await DuocSoanNoiDung(HanhDong.Xem, ct)) return nguon;

        if (currentUser.UserId is not { } uid) return nguon.Where(_ => false);

        return nguon.Where(k =>
            // Khoá NHÁP không ai ngoài người soạn thấy — soạn dở không phải nội dung, và học
            // viên thấy một khoá trống rỗng sẽ tưởng mình mua hụt.
            k.TrangThai != TrangThaiKhoaOnline.Nhap
            && (k.GhiDanhs.Any(g => g.HocVienId == uid)
                // Khoá có bài công khai thì ai cũng THẤY được khoá — nếu không thì bài công
                // khai thành vô hình, người ta chỉ tới được nó bằng cách gõ thẳng địa chỉ.
                || k.BaiHocs.Any(b => b.CongKhai)));
    }

    public async Task<IQueryable<BaiHocOnline>> LocBaiHoc(
        IQueryable<BaiHocOnline> nguon, CancellationToken ct)
    {
        if (await DuocSoanNoiDung(HanhDong.Xem, ct)) return nguon;

        if (currentUser.UserId is not { } uid) return nguon.Where(_ => false);

        // `DateTimeOffset.UtcNow` trực tiếp như mọi handler khác của dự án. So hạn dùng UTC
        // chứ không múi giờ trung tâm: hạn là một THỜI ĐIỂM tuyệt đối, không phải một ngày —
        // khác với "buổi học hôm nay" ở FR-15 vốn phải quy về ngày địa phương.
        var bayGio = DateTimeOffset.UtcNow;

        return nguon.Where(b =>
            b.KhoaOnline.TrangThai != TrangThaiKhoaOnline.Nhap
            && (
                // Nhánh 1 — ghi danh CÒN HẠN. `NgayHetHan == null` là học vĩnh viễn.
                b.KhoaOnline.GhiDanhs.Any(g =>
                    g.HocVienId == uid
                    && (g.NgayHetHan == null || g.NgayHetHan > bayGio))
                // Nhánh 2 — bài công khai: đọc được kể cả khi CHƯA ghi danh hoặc ĐÃ hết hạn.
                // Đây là lý do phép lọc không viết được ở mức khoá.
                || b.CongKhai));
    }
}
