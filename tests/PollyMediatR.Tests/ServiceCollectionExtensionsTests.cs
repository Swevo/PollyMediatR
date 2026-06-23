namespace PollyMediatR.Tests;

public class ServiceCollectionExtensionsTests
{
    // ── AddPollyMediatR(Action<ResiliencePipelineBuilder>) ────────────────────

    [Fact]
    public void AddPollyMediatR_WithBuilder_RegistersBehavior()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensionsTests).Assembly));

        services.AddPollyMediatR(b => b.AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            MaxRetryAttempts = 1,
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
        }));

        var sp = services.BuildServiceProvider();
        var behaviors = sp.GetServices<IPipelineBehavior<PingRequest, string>>();
        behaviors.Should().ContainSingle(b => b is ResiliencePipelineBehavior<PingRequest, string>);
    }

    [Fact]
    public void AddPollyMediatR_WithBuilder_RegistersPipelineAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddPollyMediatR(_ => { });

        var sp = services.BuildServiceProvider();
        var p1 = sp.GetRequiredService<ResiliencePipeline>();
        var p2 = sp.GetRequiredService<ResiliencePipeline>();

        p1.Should().BeSameAs(p2);
    }

    [Fact]
    public void AddPollyMediatR_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var act = () => services.AddPollyMediatR(_ => { });
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddPollyMediatR_NullConfigure_Throws()
    {
        var services = new ServiceCollection();
        Action<ResiliencePipelineBuilder> configure = null!;
        var act = () => services.AddPollyMediatR(configure);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    // ── AddPollyMediatR(ResiliencePipeline) ───────────────────────────────────

    [Fact]
    public void AddPollyMediatR_WithPipeline_RegistersBehavior()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensionsTests).Assembly));

        services.AddPollyMediatR(ResiliencePipeline.Empty);

        var sp = services.BuildServiceProvider();
        var behaviors = sp.GetServices<IPipelineBehavior<PingRequest, string>>();
        behaviors.Should().ContainSingle(b => b is ResiliencePipelineBehavior<PingRequest, string>);
    }

    [Fact]
    public void AddPollyMediatR_NullPipeline_Throws()
    {
        var services = new ServiceCollection();
        ResiliencePipeline pipeline = null!;
        var act = () => services.AddPollyMediatR(pipeline);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pipeline");
    }

    [Fact]
    public void AddPollyMediatR_NullServicesWithPipeline_Throws()
    {
        IServiceCollection services = null!;
        var act = () => services.AddPollyMediatR(ResiliencePipeline.Empty);
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    // ── End-to-end DI round-trip ──────────────────────────────────────────────

    [Fact]
    public async Task AddPollyMediatR_EndToEnd_RetryRecoversFromFault()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensionsTests).Assembly));

        services.AddPollyMediatR(b =>
            b.AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            }));

        services.AddSingleton<IRequestHandler<PingRequest, string>>(new FaultyHandler(2));

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        var result = await mediator.Send(new PingRequest("e2e"));

        result.Should().Be("recovered");
    }

    [Fact]
    public async Task AddPollyMediatR_EndToEnd_EmptyPipelinePassesThrough()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensionsTests).Assembly));

        services.AddPollyMediatR(ResiliencePipeline.Empty);

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        var result = await mediator.Send(new PingRequest("passthrough"));

        result.Should().Be("Pong: passthrough");
    }
}
