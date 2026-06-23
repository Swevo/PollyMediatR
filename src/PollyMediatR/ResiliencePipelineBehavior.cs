// <copyright file="ResiliencePipelineBehavior.cs" company="Justin Bannister">
// Copyright (c) Justin Bannister. All rights reserved.
// </copyright>

namespace PollyMediatR;

/// <summary>
/// A MediatR pipeline behaviour that executes every request inside a Polly v8
/// <see cref="ResiliencePipeline"/>.  Register it via
/// <see cref="PollyMediatRServiceCollectionExtensions.AddPollyMediatR(IServiceCollection, Action{ResiliencePipelineBuilder})"/>
/// or one of its overloads.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public sealed class ResiliencePipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ResiliencePipeline _pipeline;

    /// <summary>
    /// Initialises a new instance of <see cref="ResiliencePipelineBehavior{TRequest,TResponse}"/>
    /// using the supplied <paramref name="pipeline"/>.
    /// </summary>
    public ResiliencePipelineBehavior(ResiliencePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
    }

    /// <inheritdoc/>
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        _pipeline
            .ExecuteAsync(ct => new ValueTask<TResponse>(next(ct)), cancellationToken)
            .AsTask();
}
