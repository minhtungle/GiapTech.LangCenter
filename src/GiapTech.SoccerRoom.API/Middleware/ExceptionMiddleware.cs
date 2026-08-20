using FluentValidation;
using Microsoft.EntityFrameworkCore;
using GiapTech.SoccerRoom.Application.Common.Exceptions;

namespace GiapTech.SoccerRoom.API.Middleware;

/// <summary>
/// Bắt exception và trả response chuẩn dạng { errorCode, duLieu } — quy tắc #3.
///
/// Không bao giờ trả exception.Message ra client: message có thể chứa tên bảng, câu SQL,
/// đường dẫn file, và luôn là một ngôn ngữ cố định. Chi tiết chỉ đi vào log.
/// </summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Dữ liệu không hợp lệ");
            await TraLoi(context, StatusCodes.Status400BadRequest, MaLoi.DuLieuKhongHopLe,
                new Dictionary<string, object>
                {
                    ["truong"] = ex.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorCode ?? e.ErrorMessage).ToArray())
                });
        }
        catch (KhongTimThayException ex)
        {
            logger.LogInformation(ex, "Không tìm thấy");
            await TraLoi(context, StatusCodes.Status404NotFound, ex.Ma, ex.DuLieu);
        }
        catch (KhongDuQuyenException ex)
        {
            logger.LogWarning("Từ chối truy cập: {ChucNang}/{HanhDong}", ex.ChucNang, ex.HanhDong);
            await TraLoi(context, StatusCodes.Status403Forbidden, ex.Ma, ex.DuLieu);
        }
        catch (AppException ex)
        {
            logger.LogWarning(ex, "Lỗi nghiệp vụ {MaLoi}", ex.Ma);
            await TraLoi(context, StatusCodes.Status400BadRequest, ex.Ma, ex.DuLieu);
        }
        catch (DbUpdateException ex) when (LaViPhamUnique(ex))
        {
            // UNIQUE index ở tầng DB là chốt chặn CUỐI cho các ràng buộc "chỉ một": vote MVP,
            // phản hồi tham gia, đối thủ theo mã đội, lời mời đang chờ. Kiểm ở tầng ứng dụng
            // (`AnyAsync` rồi `Add`) không đủ — hai request song song đều thấy "chưa có".
            //
            // Không có nhánh này thì vi phạm UNIQUE thành 500 "Lỗi hệ thống", người dùng tưởng
            // app hỏng trong khi thực ra họ bấm hai lần và lần thứ hai bị chặn đúng.
            logger.LogWarning(ex, "Vi phạm ràng buộc UNIQUE — thao tác trùng");
            await TraLoi(context, StatusCodes.Status409Conflict, "THAO_TAC_TRUNG", null);
        }
        catch (Exception ex)
        {
            // Lỗi ngoài dự kiến: ghi log đầy đủ, trả về client mã chung không lộ nội tình.
            logger.LogError(ex, "Lỗi hệ thống ngoài dự kiến");
            await TraLoi(context, StatusCodes.Status500InternalServerError, MaLoi.LoiHeThong, null);
        }
    }

    private static async Task TraLoi(
        HttpContext context, int statusCode, string maLoi, Dictionary<string, object>? duLieu)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { errorCode = maLoi, duLieu });
    }

    /// <summary>
    /// Vi phạm UNIQUE của PostgreSQL — SQLSTATE 23505.
    ///
    /// Đọc mã SQLSTATE thay vì so chuỗi message: message đổi theo phiên bản và ngôn ngữ server.
    /// Dùng `Npgsql.PostgresException` gián tiếp qua tên kiểu để Middleware không phải tham
    /// chiếu Npgsql — API vốn không biết provider nào đang chạy.
    /// </summary>
    private static bool LaViPhamUnique(DbUpdateException ex)
    {
        for (var e = ex.InnerException; e is not null; e = e.InnerException)
        {
            if (e.GetType().GetProperty("SqlState")?.GetValue(e) as string == "23505")
                return true;
        }

        return false;
    }
}
