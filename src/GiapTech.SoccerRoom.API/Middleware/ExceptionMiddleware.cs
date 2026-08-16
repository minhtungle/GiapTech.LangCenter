using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;

namespace GiapTech.SoccerRoom.API.Middleware;

/// <summary>
/// Bắt exception và trả response chuẩn dạng { errorCode, duLieu } — quy tắc #2.
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
}
