using System.Text;
using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.API.Middleware;
using GiapTech.SoccerRoom.Application;
using GiapTech.SoccerRoom.API.Services;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

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
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "soccerroom-api";

if (!string.IsNullOrEmpty(jwtSecret))
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

// --- Phân quyền động (quy tắc #8) ---
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, QuyenPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, QuyenAuthorizationHandler>();

builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "SoccerRoom API", Version = "v1" });
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Bắt exception sớm nhất để mọi lỗi phía sau đều thành { errorCode } (quy tắc #2).
app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
// PHẢI nằm giữa Authentication và Authorization: cần claim đã giải mã, và phải xong trước
// khi handler phân quyền truy vấn DB (truy vấn đó cần tenant để lọc).
app.UseMiddleware<TenantMiddleware>();
// Chặn mọi endpoint nghiệp vụ khi còn cờ PhaiDoiMatKhau (FR-01). Đặt trước Authorization
// để không phụ thuộc việc frontend có tôn trọng cờ trong response đăng nhập hay không.
app.UseMiddleware<BuocDoiMatKhauMiddleware>();
app.UseAuthorization();

app.MapControllers();

// TODO(bootstrap): còn thiếu — xem CLAUDE.md mục 6
//   - FR-02 quên mật khẩu (SMTP)
//   - Refresh token: hiện mới phát hành, chưa lưu và chưa có endpoint đổi mới
//   - Frontend shadcn-admin

app.Run();

/// <summary>Điểm neo cho integration test qua WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program { }
