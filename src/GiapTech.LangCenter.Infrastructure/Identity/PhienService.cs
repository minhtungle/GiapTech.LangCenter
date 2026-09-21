using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GiapTech.LangCenter.Infrastructure.Identity;

/// <inheritdoc cref="IPhienService"/>
public class PhienService(IMemoryCache cache) : IPhienService
{
    public void XoaCache(Guid taiKhoanId) => cache.Remove(IPhienService.Khoa(taiKhoanId));
}
