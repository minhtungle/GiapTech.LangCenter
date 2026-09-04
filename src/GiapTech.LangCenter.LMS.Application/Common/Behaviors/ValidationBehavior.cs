using FluentValidation;
using MediatR;

namespace GiapTech.LangCenter.LMS.Application.Common.Behaviors;

/// <summary>
/// Chạy FluentValidation trước mọi handler. Đặt ở pipeline thay vì gọi trong từng handler
/// để không có handler nào lỡ bỏ qua bước kiểm tra.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var loi = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (loi.Count != 0)
            throw new ValidationException(loi);

        return await next();
    }
}
