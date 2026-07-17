# PollyMediatR

[![NuGet](https://img.shields.io/nuget/v/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR)
[![CI](https://github.com/Swevo/PollyMediatR/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyMediatR/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

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

## Related Packages

| Package | Downloads | Description |
|---|---|---|
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers — expose circuit-breaker state (Closed, HalfOpen, Open, Isolated) as /health endpoint responses |
| [PollyOpenTelemetry](https://www.nuget.org/packages/PollyOpenTelemetry) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenTelemetry.svg)](https://www.nuget.org/packages/PollyOpenTelemetry) | OpenTelemetry instrumentation for Polly v8 resilience pipelines |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Backoff delay strategies for Polly v8 resilience pipelines |
| [PollyGrpc](https://www.nuget.org/packages/PollyGrpc) | [![Downloads](https://img.shields.io/nuget/dt/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc) | Polly v8 resilience interceptor for gRPC |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience pipelines for Entity Framework Core — wrap every EF Core query and SaveChanges with retry, timeout and circuit-breaker via a single AddPollyResilience() call |
| [PollyRabbitMQ](https://www.nuget.org/packages/PollyRabbitMQ) | [![Downloads](https://img.shields.io/nuget/dt/PollyRabbitMQ.svg)](https://www.nuget.org/packages/PollyRabbitMQ) | Polly v8 resilience for RabbitMQ.Client v7+ — retry, circuit-breaker, and timeout for IChannel operations, with built-in RabbitMqTransientErrors predicate covering AlreadyClosedException, BrokerUnreachableException, OperationInterruptedException, and ConnectFailureException |
| [PollyMailKit](https://www.nuget.org/packages/PollyMailKit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMailKit.svg)](https://www.nuget.org/packages/PollyMailKit) | Polly v8 resilience pipelines for MailKit — retry, timeout, and circuit-breaker for SmtpClient.SendAsync and any MailKit SMTP operation |
| [PollyMassTransit](https://www.nuget.org/packages/PollyMassTransit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMassTransit.svg)](https://www.nuget.org/packages/PollyMassTransit) | Polly v8 resilience pipelines for MassTransit — retry, timeout, and circuit-breaker for IBus.Publish and ISendEndpointProvider.Send |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI API calls |
| [PollyAzureEventHub](https://www.nuget.org/packages/PollyAzureEventHub) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureEventHub.svg)](https://www.nuget.org/packages/PollyAzureEventHub) | Polly v8 resilience pipelines for Azure Event Hubs — retry, timeout, and circuit-breaker for EventHubProducerClient and EventHubConsumerClient |
| [PollySignalR](https://www.nuget.org/packages/PollySignalR) | [![Downloads](https://img.shields.io/nuget/dt/PollySignalR.svg)](https://www.nuget.org/packages/PollySignalR) | Polly v8 reconnect policy for SignalR |
| [PollyElasticsearch](https://www.nuget.org/packages/PollyElasticsearch) | [![Downloads](https://img.shields.io/nuget/dt/PollyElasticsearch.svg)](https://www.nuget.org/packages/PollyElasticsearch) | Polly v8 resilience pipelines for Elastic.Clients.Elasticsearch 8+ — retry, timeout, and circuit-breaker for any Elasticsearch operation, plus a built-in ElasticTransientErrors predicate covering rate limiting (429), service unavailability (503), gateway timeouts (504), and connection failures |
| [PollyHangfire](https://www.nuget.org/packages/PollyHangfire) | [![Downloads](https://img.shields.io/nuget/dt/PollyHangfire.svg)](https://www.nuget.org/packages/PollyHangfire) | Polly v8 resilience pipelines for Hangfire — retry, timeout, and circuit-breaker for IBackgroundJobClient.Enqueue and Schedule |
| [PollySendGrid](https://www.nuget.org/packages/PollySendGrid) | [![Downloads](https://img.shields.io/nuget/dt/PollySendGrid.svg)](https://www.nuget.org/packages/PollySendGrid) | Polly v8 resilience pipelines for SendGrid — retry, timeout, and circuit-breaker for ISendGridClient.SendEmailAsync |
| [PollyAzureKeyVault](https://www.nuget.org/packages/PollyAzureKeyVault) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureKeyVault.svg)](https://www.nuget.org/packages/PollyAzureKeyVault) | Polly v8 resilience pipelines for Azure Key Vault — retry, timeout, and circuit-breaker for SecretClient, KeyClient, and CertificateClient |
| [PollyAzureQueueStorage](https://www.nuget.org/packages/PollyAzureQueueStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureQueueStorage.svg)](https://www.nuget.org/packages/PollyAzureQueueStorage) | Polly v8 resilience pipelines for Azure Queue Storage — retry, timeout, and circuit-breaker for Azure.Storage.Queues QueueClient |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | [![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis) | Polly v8 resilience for StackExchange.Redis |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience for Azure Service Bus — retry, circuit breaker, and timeout for sending and receiving messages |
| [PollyKafka](https://www.nuget.org/packages/PollyKafka) | [![Downloads](https://img.shields.io/nuget/dt/PollyKafka.svg)](https://www.nuget.org/packages/PollyKafka) | Polly v8 resilience for Confluent.Kafka — retry, circuit breaker, and timeout for producers and consumers |
| [PollyAzureTableStorage](https://www.nuget.org/packages/PollyAzureTableStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureTableStorage.svg)](https://www.nuget.org/packages/PollyAzureTableStorage) | Polly v8 resilience pipelines for Azure Table Storage — retry, timeout, and circuit-breaker for Azure.Data.Tables TableClient |
| [PollyCaching](https://www.nuget.org/packages/PollyCaching) | [![Downloads](https://img.shields.io/nuget/dt/PollyCaching.svg)](https://www.nuget.org/packages/PollyCaching) | A caching resilience strategy for Polly v8 pipelines |
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | [![Downloads](https://img.shields.io/nuget/dt/PollyChaos.svg)](https://www.nuget.org/packages/PollyChaos) | Chaos engineering and fault-injection resilience strategies for Polly v8 pipelines |
| [PollyBulkhead](https://www.nuget.org/packages/PollyBulkhead) | [![Downloads](https://img.shields.io/nuget/dt/PollyBulkhead.svg)](https://www.nuget.org/packages/PollyBulkhead) | Bulkhead isolation strategy for Polly v8 resilience pipelines |

## 💼 Need .NET consulting?

The author of this package is available for consulting on **Polly v8 resilience**, **Azure cloud architecture**, and **clean .NET design**.

**[→ solidqualitysolutions.com](https://www.solidqualitysolutions.com/)** · **[LinkedIn](https://www.linkedin.com/in/justbannister/)**
## License

MIT
