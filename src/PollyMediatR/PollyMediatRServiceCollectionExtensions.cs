// <copyright file="PollyMediatRServiceCollectionExtensions.cs" company="Justin Bannister">
// Copyright (c) Justin Bannister. All rights reserved.
// </copyright>

namespace PollyMediatR;

/// <summary>
/// Extension methods for registering <see cref="ResiliencePipelineBehavior{TRequest,TResponse}"/>
/// with the Microsoft dependency-injection container.
/// </summary>
public static class PollyMediatRServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="ResiliencePipelineBehavior{TRequest,TResponse}"/> as an open-generic
    /// MediatR pipeline behaviour, building the <see cref="ResiliencePipeline"/> from
    /// <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// A delegate that configures the <see cref="ResiliencePipelineBuilder"/>
    /// (e.g. adds retry, timeout, circuit-breaker strategies).
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPollyMediatR(
        this IServiceCollection services,
        Action<ResiliencePipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        return services.AddPollyMediatR(builder.Build());
    }

    /// <summary>
    /// Registers a <see cref="ResiliencePipelineBehavior{TRequest,TResponse}"/> as an open-generic
    /// MediatR pipeline behaviour using a pre-built <paramref name="pipeline"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="pipeline">
    /// A fully configured <see cref="ResiliencePipeline"/> to wrap every MediatR request with.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPollyMediatR(
        this IServiceCollection services,
        ResiliencePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(pipeline);

        services.AddSingleton(pipeline);
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ResiliencePipelineBehavior<,>));

        return services;
    }
}
