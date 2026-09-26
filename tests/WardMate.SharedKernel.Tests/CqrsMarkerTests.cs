using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class CqrsMarkerTests
{
    private sealed record PingCommand(string Message) : ICommand<string>;

    private sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
    {
        public Task<Result<string>> Handle(PingCommand request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success($"pong: {request.Message}"));
    }

    private sealed record VoidCommand : ICommand;

    private sealed class VoidCommandHandler : ICommandHandler<VoidCommand>
    {
        public Task<Result> Handle(VoidCommand request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success());
    }

    private sealed record PingQuery(int Number) : IQuery<int>;

    private sealed class PingQueryHandler : IQueryHandler<PingQuery, int>
    {
        public Task<Result<int>> Handle(PingQuery request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(request.Number * 2));
    }

    [Fact]
    public async Task MediatR_DispatchesTypedCommandSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CqrsMarkerTests).Assembly));
        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new PingCommand("hello"));

        Assert.True(result.IsSuccess);
        Assert.Equal("pong: hello", result.Value);
    }

    [Fact]
    public async Task MediatR_DispatchesVoidCommandSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CqrsMarkerTests).Assembly));
        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new VoidCommand());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task MediatR_DispatchesQuerySuccessfully()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CqrsMarkerTests).Assembly));
        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new PingQuery(21));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }
}
