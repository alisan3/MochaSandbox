using Contracts;
using Mocha;

namespace OrderService;

public sealed class GetDiscountInfoRequestHandler(
    IMessageBus bus,
    ILogger<GetDiscountInfoRequestHandler> logger
) : IEventRequestHandler<GetDiscountInfoRequest>
{
    public async ValueTask HandleAsync(
        GetDiscountInfoRequest request,
        CancellationToken cancellationToken
    )
    {
        // Simple bulk discount: 10% when ordering 3 or more units.
        var discountPercentage = request.Quantity >= 3 ? 10m : 0m;

        logger.LogInformation(
            "Discount check for {OrderId} - {ProductName} x{Quantity}: Discount={Discount}%",
            request.OrderId,
            request.ProductName,
            request.Quantity,
            discountPercentage
        );

        await bus.SendAsync(
            new GetDiscountInfoResult(request.OrderId, discountPercentage),
            cancellationToken
        );
    }
}
