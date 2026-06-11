using MediatR;

namespace BuildingBlocks.Application;

public interface ICommand : IRequest
{
    Guid Id { get; }
}

public interface ICommand<out TResult> : IRequest<TResult>
{
    Guid Id { get; }
}

public interface IQuery<out TResult> : IRequest<TResult> { }

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand
{ }

public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{ }

public interface IQueryHandler<in TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{ }

// Added interface here so Application layers can hash passwords without referencing Infrastructure
public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
