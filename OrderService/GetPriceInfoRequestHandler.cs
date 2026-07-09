using Contracts;
using Mocha;

namespace OrderService;

public sealed class GetPriceInfoRequestHandler(
    IMessageBus bus,
    ILogger<GetPriceInfoRequestHandler> logger
) : IEventRequestHandler<GetPriceInfoRequest>
{
    public async ValueTask HandleAsync(
        GetPriceInfoRequest request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Price check for {OrderId} - {ProductName} x {Quantity}",
            request.OrderId,
            request.ProductName,
            request.Quantity
        );

        // Send the result back to the saga; correlation is carried by the
        await bus.SendAsync(
            new GetPriceInfoResult(request.OrderId, 9.99m * request.Quantity),
            cancellationToken
        );
    }
}
