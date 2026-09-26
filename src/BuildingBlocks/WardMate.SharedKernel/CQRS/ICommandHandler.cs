using MediatR;
using WardMate.SharedKernel.Common;

namespace WardMate.SharedKernel.CQRS;

/// <summary>Handler for commands that return a Result without a value.</summary>
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

/// <summary>Handler for commands that return a typed Result value.</summary>
public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
