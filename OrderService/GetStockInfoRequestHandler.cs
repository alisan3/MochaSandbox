using Mocha;

namespace OrderService;

public sealed class GetStockInfoRequestHandler(ILogger<GetStockInfoRequestHandler> logger)
    : IEventRequestHandler<GetStockInfoRequest, GetStockInfoResult>
{
    public async ValueTask<GetStockInfoResult> HandleAsync(
        GetStockInfoRequest request,
        CancellationToken cancellationToken
    )
    {
        const int availableQuantity = 42;
        var inStock = request.Quantity <= availableQuantity;

        logger.LogInformation(
            "Stock check for {OrderId} - {ProductName} x{Quantity}: InStock={InStock}, Available={Available}",
            request.OrderId,
            request.ProductName,
            request.Quantity,
            inStock,
            availableQuantity
        );

        // Send the result back to the saga; correlation is carried by the
        // GetStockInfoResult.CorrelationId (the OrderId).
        // await bus.PublishAsync(
        //     new GetStockInfoResult(request.OrderId, inStock, availableQuantity),
        //     cancellationToken
        // );

        return new GetStockInfoResult(request.OrderId, inStock, availableQuantity);
    }
}
