var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// TODO(bootstrap): còn thiếu — xem CLAUDE.md mục 6
//   - EF Core + Npgsql + DbContext (16 entity, docs/database/erd.md)
//   - Multi-tenant middleware + Global Query Filter (docs/backend/multi-tenant.md)
//   - ASP.NET Core Identity + JWT Bearer
//   - Asp.Versioning.Mvc: /api/v1/ (docs/kien-truc/adr/0003-api-versioning.md)
//   - Phân quyền động [RequirePermission] (docs/backend/phan-quyen-dong.md)
//   - MediatR + FluentValidation pipeline behavior (docs/backend/cqrs-mediatr.md)

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Điểm neo để integration test dựng được host qua <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program { }
