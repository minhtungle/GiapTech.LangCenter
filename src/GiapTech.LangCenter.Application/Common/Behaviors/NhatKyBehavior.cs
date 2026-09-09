using System.Diagnostics;
using System.Text.Json;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;

namespace GiapTech.LangCenter.Application.Common.Behaviors;

/// <summary>
/// Ghi nhật ký mọi LỆNH đi qua MediatR (FR-16).
///
/// Vì sao ở pipeline chứ không rải trong từng handler: 46 lệnh hiện có và mọi lệnh thêm sau
/// này **tự động** được ghi. Rải trong handler thì chỉ cần một người quên là mất vết, mà mất
/// vết chỉ phát hiện được lúc cần tra — tức là quá muộn.
///
/// **Chỉ ghi lệnh GHI, không ghi truy vấn đọc.** Ghi mọi lượt xem sẽ làm bảng phình gấp hàng
/// chục lần mà gần như không ai tra tới; và mỗi lần tải một trang danh sách là vài truy vấn.
///
/// Đăng ký **sau** `ValidationBehavior` để chỉ ghi lệnh đã hợp lệ — request sai định dạng là
/// lỗi client, không phải thao tác nghiệp vụ. Nhưng lỗi **nghiệp vụ** (`AppException`) thì có
/// ghi, vì "ai đó đã cố xoá buổi đã chốt" là thông tin đáng lưu.
/// </summary>
public class NhatKyBehavior<TRequest, TResponse>(IGhiNhatKy ghiNhatKy)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Tiền tố tên type quyết định lệnh nào được ghi.
    ///
    /// Dùng quy ước đặt tên chứ không dùng marker interface: thêm interface vào 46 lệnh là 46
    /// chỗ có thể quên, còn quy ước `...Command` đã được cả dự án tuân thủ và được
    /// `KienTruc/LuatPhuThuocTests` canh.
    /// </summary>
    private static bool LaLenhGhi()
        => typeof(TRequest).Name.EndsWith("Command", StringComparison.Ordinal);

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!LaLenhGhi()) return await next();

        var dongHo = Stopwatch.StartNew();
        var ten = typeof(TRequest).Name;

        try
        {
            var ketQua = await next();
            dongHo.Stop();

            await ghiNhatKy.GhiAsync(
                ten, ThamSoJson(request), thanhCong: true, maLoi: null,
                (int)dongHo.ElapsedMilliseconds, ct);

            return ketQua;
        }
        catch (AppException ex)
        {
            // Lỗi nghiệp vụ: ghi lại vì "ai đó cố xoá buổi đã chốt" hoặc "cố thu tiền cho
            // người ngoài lớp" là thông tin đáng lưu.
            dongHo.Stop();
            await ghiNhatKy.GhiAsync(
                ten, ThamSoJson(request), thanhCong: false, ex.Ma,
                (int)dongHo.ElapsedMilliseconds, ct);
            throw;
        }
        catch (Exception)
        {
            dongHo.Stop();
            await ghiNhatKy.GhiAsync(
                ten, ThamSoJson(request), thanhCong: false, "LOI_HE_THONG",
                (int)dongHo.ElapsedMilliseconds, ct);
            throw;
        }
    }

    /// <summary>
    /// Tên trường KHÔNG được ghi vào nhật ký, so khớp **chứa** và không phân biệt hoa thường.
    ///
    /// Phải lọc ở ĐÂY, không chỉ ở tầng ghi: `GhiNhatKy` lọc `ChiTiet` (đọc từ ChangeTracker,
    /// nên thấy `PasswordHash` đã băm) nhưng `ThamSo` là **command thô** — nó mang mật khẩu
    /// dạng chữ. Kiểm tay đã phát hiện `MAT-KHAU-RAT-BI-MAT` nằm nguyên trong nhật ký.
    /// </summary>
    private static readonly string[] TruongNhayCam =
        ["matkhau", "password", "token", "secret", "hash", "mkmoi"];

    /// <summary>
    /// Tham số dạng JSON, đã che trường nhạy cảm, cắt ở 2000 ký tự.
    ///
    /// Bọc `try` vì có lệnh mang `Stream` hoặc kiểu không tuần tự hoá được, và nhật ký hỏng
    /// không được làm hỏng nghiệp vụ.
    /// </summary>
    private static string? ThamSoJson(TRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);

            using var doc = JsonDocument.Parse(json);
            return Cat(JsonSerializer.Serialize(Che(doc.RootElement)));
        }
        catch
        {
            // Không tuần tự hoá được thì bỏ hẳn tham số — thà mất thông tin hơn ghi ra thứ
            // chưa lọc.
            return null;
        }
    }

    /// <summary>
    /// Che trường nhạy cảm, **đệ quy vào cả đối tượng lồng**.
    ///
    /// Chỉ che cấp một là không đủ: `TaoNguoiDungCommand` mang mật khẩu trong khối
    /// `TaiKhoan` lồng bên trong. Kiểm tay đã bắt được đúng ca này.
    /// </summary>
    private static object? Che(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(
            p => p.Name,
            p => LaNhayCam(p.Name)
                // Giữ tên trường, che giá trị: người đọc nhật ký cần biết lệnh CÓ mang mật
                // khẩu (để hiểu đây là lệnh đổi mật khẩu) mà không thấy nội dung.
                ? "***"
                : Che(p.Value)),

        // Mảng cũng phải đi vào: `DanhSach` của lệnh ghi điểm danh là mảng đối tượng.
        JsonValueKind.Array => e.EnumerateArray().Select(Che).ToList(),

        JsonValueKind.Null => null,
        JsonValueKind.String => e.GetString(),
        JsonValueKind.True or JsonValueKind.False => e.GetBoolean(),
        _ => e.GetRawText()
    };

    private static bool LaNhayCam(string ten)
        => TruongNhayCam.Any(x => ten.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static string Cat(string s) => s.Length > 2000 ? s[..2000] + "…" : s;
}
