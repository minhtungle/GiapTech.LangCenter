using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.HocLieu;

/// <summary>Tệp đính kèm trả về client — không lộ khoá kho, chỉ id để tải qua API.</summary>
public record TepDto(Guid Id, string TenGoc, string LoaiNoiDung, long KichThuoc);

/// <summary>
/// Việc dùng chung cho mọi loại đính kèm: tạo hàng DB và dọn tệp khỏi kho khi xoá.
///
/// Gom một chỗ vì năm loại đính kèm có luồng y hệt nhau — ba bản sao là ba cơ hội quên bước
/// xoá tệp khỏi kho, để lại tệp mồ côi vĩnh viễn.
/// </summary>
public static class TepDinhKemChung
{
    /// <summary>Xoá tệp khỏi kho rồi xoá hàng DB. Thứ tự này để không mất dấu khoá nếu lỗi.</summary>
    public static async Task XoaTeps(
        IAppDbContext db, ILuuTruTep luuTru,
        IEnumerable<Domain.Entities.TepDinhKem> teps, CancellationToken ct)
    {
        var ds = teps.ToList();
        if (ds.Count == 0) return;

        foreach (var t in ds) await luuTru.Xoa(t.KhoaLuuTru, ct);

        db.TepDinhKems.RemoveRange(ds);
    }

    /// <summary>Đọc một tệp theo id, có kiểm cách ly tenant ở tầng kho lưu trữ.</summary>
    public static async Task<TepTaiVe> TaiVe(
        IAppDbContext db, ILuuTruTep luuTru, Guid tepId, CancellationToken ct)
    {
        var tep = await db.TepDinhKems.FirstOrDefaultAsync(t => t.Id == tepId, ct)
                  ?? throw new KhongTimThayException($"Tep {tepId}");

        // Kho lưu trữ không có Query Filter — `TaiVe` tự kiểm tiền tố tenant trong khoá.
        return await luuTru.TaiVe(tep.KhoaLuuTru, ct)
               ?? throw new KhongTimThayException($"Tep {tepId}");
    }
}
