using Contracts;
using Mocha;

namespace OrderService;

public sealed class GetShippingInfoRequestHandler(
    IMessageBus bus,
    ILogger<GetShippingInfoRequestHandler> logger
) : IEventRequestHandler<GetShippingInfoRequest>
{
    public async ValueTask HandleAsync(
        GetShippingInfoRequest request,
        CancellationToken cancellationToken
    )
    {
        var shippingCost = 4.99m + 0.50m * request.Quantity;
        const int estimatedDeliveryDays = 3;

        logger.LogInformation(
            "Shipping check for {OrderId} - {ProductName} x{Quantity}: Cost={Cost}, ETA={Eta} days",
            request.OrderId,
            request.ProductName,
            request.Quantity,
            shippingCost,
            estimatedDeliveryDays
        );

        await bus.SendAsync(
            new GetShippingInfoResult(request.OrderId, shippingCost),
            cancellationToken
        );
    }
}
