namespace PollyMediatR.Tests;

// ── Shared test infrastructure ────────────────────────────────────────────────

public record PingRequest(string Message) : IRequest<string>;

public class PingHandler : IRequestHandler<PingRequest, string>
{
    public Task<string> Handle(PingRequest request, CancellationToken ct)
        => Task.FromResult($"Pong: {request.Message}");
}

public record VoidRequest : IRequest;

public class VoidHandler : IRequestHandler<VoidRequest>
{
    public Task Handle(VoidRequest request, CancellationToken ct) => Task.CompletedTask;
}

public class FaultyHandler : IRequestHandler<PingRequest, string>
{
    private int _calls;
    private readonly int _failCount;
    private readonly Exception _ex;

    public FaultyHandler(int failCount, Exception? ex = null)
    {
        _failCount = failCount;
        _ex = ex ?? new InvalidOperationException("transient");
    }

    public Task<string> Handle(PingRequest request, CancellationToken ct)
    {
        if (++_calls <= _failCount)
            throw _ex;
        return Task.FromResult("recovered");
    }
}

public class SlowHandler : IRequestHandler<PingRequest, string>
{
    private readonly TimeSpan _delay;
    public SlowHandler(TimeSpan delay) => _delay = delay;

    public async Task<string> Handle(PingRequest request, CancellationToken ct)
    {
        await Task.Delay(_delay, ct);
        return "slow result";
    }
}

// ── ResiliencePipelineBehavior tests ──────────────────────────────────────────

public class ResiliencePipelineBehaviorTests
{
    private static IMediator BuildMediator(ResiliencePipeline pipeline)
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ResiliencePipelineBehaviorTests).Assembly));
        services.AddPollyMediatR(pipeline);
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PassThrough_ReturnsHandlerResult()
    {
        var mediator = BuildMediator(ResiliencePipeline.Empty);

        var result = await mediator.Send(new PingRequest("hello"));

        result.Should().Be("Pong: hello");
    }

    [Fact]
    public async Task Handle_PassThrough_CancellationTokenIsForwarded()
    {
        using var cts = new CancellationTokenSource();
        var mediator = BuildMediator(ResiliencePipeline.Empty);

        var act = async () => await mediator.Send(new PingRequest("x"), cts.Token);

        await act.Should().NotThrowAsync();
    }

    // ── Retry ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RetryPipeline_RetriesTransientFaults()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ResiliencePipelineBehaviorTests).Assembly));
        services.AddPollyMediatR(pipeline);

        // Singleton so the call counter is preserved across retry attempts
        services.AddSingleton<IRequestHandler<PingRequest, string>>(new FaultyHandler(2));

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var result = await mediator.Send(new PingRequest("retry"));

        result.Should().Be("recovered");
    }

    [Fact]
    public async Task Handle_RetryPipeline_ThrowsWhenRetriesExhausted()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ResiliencePipelineBehaviorTests).Assembly));
        services.AddPollyMediatR(pipeline);
        services.AddSingleton<IRequestHandler<PingRequest, string>>(new FaultyHandler(5));

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var act = async () => await mediator.Send(new PingRequest("exhaust"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Timeout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TimeoutPipeline_CancelsSlowHandler()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromMilliseconds(50))
            .Build();

        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ResiliencePipelineBehaviorTests).Assembly));
        services.AddPollyMediatR(pipeline);
        services.AddTransient<IRequestHandler<PingRequest, string>>(
            _ => new SlowHandler(TimeSpan.FromSeconds(10)));

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var act = async () => await mediator.Send(new PingRequest("slow"));

        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    [Fact]
    public async Task Handle_TimeoutPipeline_DoesNotThrowWhenFastEnough()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(5))
            .Build();

        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ResiliencePipelineBehaviorTests).Assembly));
        services.AddPollyMediatR(pipeline);
        services.AddTransient<IRequestHandler<PingRequest, string>>(
            _ => new SlowHandler(TimeSpan.FromMilliseconds(10)));

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var result = await mediator.Send(new PingRequest("fast"));

        result.Should().Be("slow result");
    }

    // ── Combined pipeline ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RetryAndTimeoutPipeline_SucceedsAfterRetry()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(10))
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ResiliencePipelineBehaviorTests).Assembly));
        services.AddPollyMediatR(pipeline);
        services.AddSingleton<IRequestHandler<PingRequest, string>>(new FaultyHandler(2));

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var result = await mediator.Send(new PingRequest("combined"));

        result.Should().Be("recovered");
    }

    // ── IRequest (void) ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_VoidRequest_CompletesSuccessfully()
    {
        var mediator = BuildMediator(ResiliencePipeline.Empty);

        var act = async () => await mediator.Send(new VoidRequest());

        await act.Should().NotThrowAsync();
    }
}

// ── Constructor / null-guard tests ────────────────────────────────────────────

public class ResiliencePipelineBehaviorConstructorTests
{
    [Fact]
    public void Constructor_NullPipeline_Throws()
    {
        var act = () => new ResiliencePipelineBehavior<PingRequest, string>(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("pipeline");
    }

    [Fact]
    public void Constructor_ValidPipeline_DoesNotThrow()
    {
        var act = () => new ResiliencePipelineBehavior<PingRequest, string>(ResiliencePipeline.Empty);

        act.Should().NotThrow();
    }
}
