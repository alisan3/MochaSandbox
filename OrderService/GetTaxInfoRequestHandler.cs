using Contracts;
using Mocha;

namespace OrderService;

public sealed class GetTaxInfoRequestHandler(
    IMessageBus bus,
    ILogger<GetTaxInfoRequestHandler> logger
) : IEventRequestHandler<GetTaxInfoRequest>
{
    public async ValueTask HandleAsync(
        GetTaxInfoRequest request,
        CancellationToken cancellationToken
    )
    {
        // 19% VAT on an assumed 9.99 unit price.
        var taxAmount = Math.Round(0.19m * 9.99m * request.Quantity, 2);

        logger.LogInformation(
            "Tax check for {OrderId} - {ProductName} x{Quantity}: Tax={Tax}",
            request.OrderId,
            request.ProductName,
            request.Quantity,
            taxAmount
        );

        await bus.SendAsync(new GetTaxInfoResult(request.OrderId, taxAmount), cancellationToken);
    }
}
