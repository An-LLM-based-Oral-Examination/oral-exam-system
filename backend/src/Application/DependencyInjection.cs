using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using MediatR;
using OralExamination.Application.Common.Behaviors;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.TryAddSingleton<IOfficialExamRandomGenerator>(CryptoOfficialExamRandomGenerator.Instance);
        services.AddScoped<Features.QuestionBank.ReviewRequests.QuestionReviewRequestService>();

        return services;
    }
}
