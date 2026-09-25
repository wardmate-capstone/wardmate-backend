using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace WardMate.Services.IAM.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIamApplication(this IServiceCollection services)
    {
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }
}
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(request, ct)));
        var failures = results.SelectMany(x => x.Errors).ToArray();
        if (failures.Length > 0) throw new ValidationException(failures);
        return await next();
    }
}
