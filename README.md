# PollyMediatR

[![NuGet](https://img.shields.io/nuget/v/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR)
[![CI](https://github.com/Swevo/PollyMediatR/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyMediatR/actions)

**Polly v8 resilience pipelines for MediatR** — add retry, timeout, circuit-breaker, rate-limiting, hedging and chaos engineering to any MediatR request handler with a single line of registration. No changes to handler code required.

```csharp
services.AddPollyMediatR(pipeline =>
    pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
        })
        .AddTimeout(TimeSpan.FromSeconds(5)));
```

Every `IRequest<T>` handler is now automatically wrapped with retry + timeout — zero changes to existing handlers.

---

## Why PollyMediatR?

MediatR's `IPipelineBehavior<TRequest, TResponse>` is the natural place to apply cross-cutting resilience concerns, but wiring it up with Polly v8 requires boilerplate. PollyMediatR does the wiring for you.

| Without PollyMediatR | With PollyMediatR |
|---|---|
| Write a custom `IPipelineBehavior` per pipeline | One `AddPollyMediatR(...)` call |
| Manually inject `ResiliencePipeline` into each behavior | Pipeline registered & injected automatically |
| Duplicate retry/timeout logic across query & command handlers | Single pipeline applied to all handlers |
| Must update handlers to apply new resilience policies | Zero changes to existing handlers |

---

## Installation

```bash
dotnet add package PollyMediatR
```

Targets **net6.0**, **net8.0**, and **net9.0**.

Dependencies: `Polly.Core 8.*`, `MediatR 12.*`, `Microsoft.Extensions.DependencyInjection.Abstractions 8.*`

---

## Quick start

### 1. Register with an inline builder (recommended)

```csharp
using Polly.Retry;
using PollyMediatR;

services.AddPollyMediatR(pipeline =>
    pipeline.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromMilliseconds(200),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = new PredicateBuilder().Handle<Exception>(),
    }));
```

### 2. Register with a pre-built pipeline

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions { ... })
    .AddTimeout(TimeSpan.FromSeconds(10))
    .Build();

services.AddPollyMediatR(pipeline);
```

### 3. Use normally with MediatR — nothing changes

```csharp
// Handler — no Polly code needed
public class GetOrderHandler : IRequestHandler<GetOrderQuery, Order>
{
    public async Task<Order> Handle(GetOrderQuery request, CancellationToken ct)
    {
        return await _db.Orders.FindAsync(request.Id, ct); // retried automatically
    }
}

// At the call site — identical to normal MediatR usage
var order = await mediator.Send(new GetOrderQuery(id));
```

---

## ASP.NET Core example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<Program>());

builder.Services.AddPollyMediatR(pipeline =>
    pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(100),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutException>(),
        })
        .AddTimeout(TimeSpan.FromSeconds(30))
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15),
        }));
```

---

## Pipeline order

Polly strategies are applied outer-to-inner (left-to-right). The recommended order is:

```
[Timeout] → [Retry] → [Circuit Breaker] → [Handler]
```

```csharp
pipeline
    .AddTimeout(TimeSpan.FromSeconds(10))    // 1. Overall deadline
    .AddRetry(retryOptions)                  // 2. Retry on failure
    .AddCircuitBreaker(cbOptions)            // 3. Open circuit if overloaded
```

---

## Combining with chaos engineering (PollyChaos)

Use [PollyChaos](https://www.nuget.org/packages/PollyChaos) to harden your handlers in test/staging:

```csharp
services.AddPollyMediatR(pipeline =>
    pipeline
        .AddRetry(retryOptions)
        .AddChaosFault(injectionRate: 0.1)); // inject faults 10% of the time
```

---

## Related packages

| Package | Downloads | Description |
|---|---|---|
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI — retry on 429, Retry-After, circuit breaker |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience for EF Core queries and SaveChanges |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Jitter, linear & custom backoff for Polly v8 retry |
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | [![Downloads](https://img.shields.io/nuget/dt/PollyChaos.svg)](https://www.nuget.org/packages/PollyChaos) | Fault & latency injection (Simmy for Polly v8) |
| [PollyCaching](https://www.nuget.org/packages/PollyCaching) | [![Downloads](https://img.shields.io/nuget/dt/PollyCaching.svg)](https://www.nuget.org/packages/PollyCaching) | Cache-aside resilience strategy for Polly v8 |
| [PollyBulkhead](https://www.nuget.org/packages/PollyBulkhead) | [![Downloads](https://img.shields.io/nuget/dt/PollyBulkhead.svg)](https://www.nuget.org/packages/PollyBulkhead) | Bulkhead / concurrency limiter for Polly v8 |
| [PollyOpenTelemetry](https://www.nuget.org/packages/PollyOpenTelemetry) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenTelemetry.svg)](https://www.nuget.org/packages/PollyOpenTelemetry) | OpenTelemetry metrics & tracing for Polly v8 |

---

## License

MIT
