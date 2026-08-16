using GiapTech.SoccerRoom.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace GiapTech.SoccerRoom.API.Authorization;

/// <summary>
/// Sinh policy "Quyen:{chucNang}:{hanhDong}" khi ASP.NET Core gặp lần đầu.
///
/// Vì sao cần: hệ phân quyền của ta có (số chức năng × 4 thao tác) tổ hợp và còn tăng theo
/// mỗi module mới. Đăng ký sẵn từng policy trong Program.cs sẽ thành danh sách dài phải nhớ
/// cập nhật — quên một dòng thì endpoint ném lỗi "policy not found" lúc chạy chứ không phải
/// lúc build. Sinh động thì không có gì để quên.
/// </summary>
public class QuyenPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(RequirePermissionAttribute.TienTo, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        var phan = policyName[RequirePermissionAttribute.TienTo.Length..].Split(':');

        if (phan.Length != 2 || !Enum.TryParse<HanhDong>(phan[1], out var hanhDong))
            return await base.GetPolicyAsync(policyName);

        return new AuthorizationPolicyBuilder()
            .AddRequirements(new QuyenRequirement(phan[0], hanhDong))
            .Build();
    }
}
