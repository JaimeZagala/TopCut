using Microsoft.Extensions.DependencyInjection;
using TopCut.Application.Abstractions;
using TopCut.Domain.Common;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// Resolves every <see cref="IDomainEventHandler{TEvent}"/> registered for each event's concrete
/// type and awaits them in turn. Roughly thirty lines instead of a mediator library, which for five
/// event types and no request pipeline is the whole feature (ADR-0010).
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IReadOnlyCollection<DomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());

            foreach (var handler in services.GetServices(handlerType))
            {
                if (handler is null)
                {
                    continue;
                }

                var handle = handlerType.GetMethod(nameof(IDomainEventHandler<DomainEvent>.HandleAsync))!;
                await (Task)handle.Invoke(handler, [domainEvent, cancellationToken])!;
            }
        }
    }
}
