using System.Text;
using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.API.Middleware;
using GiapTech.LangCenter.API.RateLimit;
using GiapTech.LangCenter.Application;
using GiapTech.LangCenter.API.Services;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // Nhận và trả enum dạng CHUỖI ("Xem", "Them"...) thay vì số.
        // Số thứ tự enum là chi tiết nội bộ: client gửi 2 mà không biết 2 là gì rất dễ sai,
        // và chèn một giá trị mới vào giữa enum sẽ âm thầm đổi nghĩa dữ liệu client đã lưu.
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
// Phản hồi lỗi model state: trả MÃ LỖI, không trả chi tiết .NET (thêm 15/09/2026).
//
// Mặc định ASP.NET trả `ValidationProblemDetails` với message tiếng Anh của
// `System.Text.Json`, và message đó chứa **tên namespace + class nội bộ**:
//
//   "The JSON value could not be converted to
//    GiapTech.LangCenter.API.Controllers.V1.LopHocController+SinhLichBody.
//    Path: $.ngayKhaiGiang | LineNumber: 0 | BytePositionInLine: 27"
//
// Hai vấn đề: lộ cấu trúc nội bộ cho client (và `traceId`, `LineNumber` — thông tin chỉ người
// vận hành cần), và vi phạm quy tắc #3 — API phải trả mã lỗi để frontend tự dịch, không trả
// câu tiếng Anh. `ExceptionMiddleware` không cứu được vì đây không phải exception: model
// binding thất bại trước khi vào action.
//
// Giữ `truong` (tên trường) vì nó là dữ liệu của client, không phải của hệ thống — form cần
// biết ô nào sai. Nhưng bỏ message: frontend đã có bản dịch theo mã lỗi.
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var truong = ctx.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .Select(x => x.Key)
            // Bỏ tiền tố `$.` mà System.Text.Json thêm vào, và tên tham số kỹ thuật như `body`.
            .Select(x => x.TrimStart('$', '.'))
            .Where(x => x.Length > 0 && x != "body")
            .Distinct()
            .ToArray();

        return new BadRequestObjectResult(new
        {
            errorCode = MaLoi.DuLieuKhongHopLe,
            duLieu = truong.Length > 0 ? new { truong } : null
        });
    };
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpContextAccessor();
builder.Services.ThemGioiHanTanSuat();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IThongTinYeuCau, ThongTinYeuCau>();

builder.Services.ThemApplication();
builder.Services.ThemInfrastructure(builder.Configuration);

// --- API versioning theo URL segment (ADR-0003) ---
builder.Services
    .AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.AssumeDefaultVersionWhenUnspecified = true;
        // Báo cho client biết version nào còn hỗ trợ / đã deprecate qua response header.
        o.ReportApiVersions = true;
    })
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });

// --- JWT ---
var jwtSecret = builder.Configuration["JWT_SECRET"];
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "langcenter-api";

// FAIL-FAST, không bọc trong `if` im lặng (sửa 15/09/2026).
//
// Bản cũ: thiếu `JWT_SECRET` thì bỏ qua cả khối này — app vẫn `Run()`, `/health` vẫn trả 200,
// deploy vẫn xanh, rồi **mọi lần đăng nhập là 500**. Kiểm độ dài chỉ có ở `TokenService` tức
// lúc PHÁT HÀNH token, quá muộn để cứu. Triệu chứng khó chẩn nhất có thể: hạ tầng báo khoẻ
// nhưng không ai đăng nhập được.
//
// Compose (`JWT_SECRET:?...`) và `scripts/trien-khai.sh` đã chắn, nhưng chỉ chắn đường deploy
// chuẩn — chạy `dotnet run` tay hoặc orchestrator khác thì lọt. Chặn ngay tại ứng dụng.
//
// `IsDevelopment` không được miễn: dev thiếu khoá cũng gặp đúng lỗi 500 khó hiểu đó, và thông
// báo rõ ràng lúc khởi động là thứ giúp người mới vào dự án nhiều nhất.
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "Thiếu JWT_SECRET. Đặt biến môi trường này (tối thiểu 32 ký tự) trước khi chạy API — "
        + "xem docs/ha-tang/bien-moi-truong.md.");
}

if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        $"JWT_SECRET chỉ có {jwtSecret.Length} ký tự, cần tối thiểu 32 để chữ ký HS256 đủ mạnh.");
}

{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtIssuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                // Mặc định của .NET cho lệch 5 phút; siết về 0 để token hết hạn là hết hạn.
                ClockSkew = TimeSpan.Zero
            };
        });
}

// --- Phân quyền động (quy tắc #9) ---
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, QuyenPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, QuyenAuthorizationHandler>();

builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "LangCenter LMS API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Dán access token nhận được từ /api/v1/auth/dang-nhap"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme, Id = "Bearer"
            }
        }] = []
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<GiapTech.LangCenter.Infrastructure.Persistence.AppDbContext>("database");

var app = builder.Build();

// Áp migration lúc khởi động. Với một VPS chạy Docker Compose, đây là cách đơn giản và
// đủ dùng; nếu về sau chạy nhiều bản sao API cùng lúc thì phải tách thành bước riêng
// trong pipeline, vì nhiều instance cùng migrate sẽ tranh nhau.
if (app.Configuration.GetValue("TU_DONG_MIGRATE", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<GiapTech.LangCenter.Infrastructure.Persistence.AppDbContext>();

    // Provider in-memory (integration test) không có khái niệm migration.
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();

    // Cấp cho nhóm quản trị của trung tâm CŨ những chức năng vừa thêm vào danh mục. Không có
    // bước này thì thêm module mới = admin của mọi trung tâm đang chạy bị 403 trên tính năng
    // đó, âm thầm. Idempotent nên chạy mỗi lần khởi động là an toàn.
    await scope.ServiceProvider
        .GetRequiredService<GiapTech.LangCenter.Infrastructure.Persistence.Seed.BoKhuyetQuyenQuanTri>()
        .ChayAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Bắt exception sớm nhất để mọi lỗi phía sau đều thành { errorCode } (quy tắc #3).
app.UseMiddleware<ExceptionMiddleware>();

// Giới hạn tần suất cho endpoint ẩn danh (nợ N3). Đặt SAU ExceptionMiddleware để 429 cũng đi
// qua cùng đường trả lỗi, nhưng TRƯỚC Authentication: chặn được request rác mà không phải
// giải mã JWT hay truy vấn DB cho nó.
//
// Mặc định BẬT. Integration test tắt nó (xem GioiHanTanSuat.CauHinhBat) vì TestServer không có
// TCP thật nên mọi test dùng chung một phân vùng IP và đốt hết hạn mức của nhau.
if (app.Configuration.GetValue(GioiHanTanSuat.CauHinhBat, true))
    app.UseRateLimiter();

// Chỉ redirect HTTPS khi chạy trực tiếp. Sau Caddy, TLS đã kết thúc ở proxy nên bật cái này
// sẽ đá cả healthcheck lẫn request thật sang cổng HTTPS mà container không nghe.
if (!app.Configuration.GetValue("SAU_REVERSE_PROXY", false))
    app.UseHttpsRedirection();

app.UseAuthentication();
// PHẢI nằm giữa Authentication và Authorization: cần claim đã giải mã, và phải xong trước
// khi handler phân quyền truy vấn DB (truy vấn đó cần tenant để lọc).
app.UseMiddleware<TenantMiddleware>();
// Chặn mọi endpoint nghiệp vụ khi còn cờ PhaiDoiMatKhau (FR-01). Đặt trước Authorization
// để không phụ thuộc việc frontend có tôn trọng cờ trong response đăng nhập hay không.
app.UseMiddleware<BuocDoiMatKhauMiddleware>();
// Một phiên mỗi tài khoản (20/09/2026): chặn token của phiên đã bị đăng nhập nơi khác đẩy ra.
// Đặt SAU `BuocDoiMatKhau` để người bị buộc đổi mật khẩu nhận đúng mã lỗi của họ trước — mã đó
// có đường đi tiếp (màn đổi mật khẩu), còn "phiên bị đẩy ra" thì chỉ còn nước đăng nhập lại.
app.UseMiddleware<PhienDuyNhatMiddleware>();
app.UseAuthorization();

app.MapControllers();

// Healthcheck cho docker compose và Uptime Kuma. Không cần xác thực — nó phải trả lời được
// cả khi hệ thống đang hỏng, và không tiết lộ gì ngoài trạng thái sống/chết.
app.MapHealthChecks("/health").AllowAnonymous();

// Cờ tính năng cho frontend, đọc TRƯỚC khi đăng nhập nên phải ẩn danh.
//
// Không nhét vào /health: đó là endpoint hạ tầng cho docker compose và Uptime Kuma, phải trả
// lời được cả khi hệ thống đang hỏng và không tiết lộ gì ngoài sống/chết.
//
// Chỉ khai những gì frontend cần để KHÔNG hiện lối vào dẫn tới ngõ cụt. Không khai tên môi
// trường: "Production"/"Development" là thông tin thừa với người dùng và thừa với người dò.
app.MapGet("/api/v1/tinh-nang", () => Results.Ok(new
{
    // Giữ cờ này thay vì xoá: nó là hợp đồng với frontend, và nếu sau này cần đóng đăng ký
    // (spam quá nhiều chẳng hạn) thì đổi ở đây là xong, không phải sửa cả trang đăng nhập.
    dangKyTrungTam = true,
})).AllowAnonymous();

app.Run();

/// <summary>Điểm neo cho integration test qua WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program { }
