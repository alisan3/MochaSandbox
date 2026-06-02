using Contracts;
using Mocha;

namespace OrderService;

public sealed class OrderCancelledHandler(ILogger<OrderCancelledHandler> logger)
    : IEventHandler<OrderCancelled>
{
    public ValueTask HandleAsync(OrderCancelled message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order cancelled: {OrderId}", message.OrderId);

        return ValueTask.CompletedTask;
    }
}
