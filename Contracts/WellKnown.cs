namespace Contracts;

public static class WellKnown
{
    public static class RoutingKeys
    {
        public const string All = "event.order.#";
        public const string OrderPlaced = "event.order.placed";
        public const string OrderCancelled = "event.order.cancelled";

        // Saga command + internal stock RPC (direct exchanges, exact match)
        public const string StartOrderSaga = "command.order.start-saga";
        public const string GetStockInfoRequest = "request.stock.get-info";
        public const string GetStockInfoResult = "response.stock.get-info";

        public const string GetPriceInfoRequest = "request.price.get-info";
        public const string GetPriceInfoResult = "response.price.get-info";

        public const string GetShippingInfoRequest = "request.shipping.get-info";
        public const string GetShippingInfoResult = "response.shipping.get-info";

        public const string GetTaxInfoRequest = "request.tax.get-info";
        public const string GetTaxInfoResult = "response.tax.get-info";

        public const string GetDiscountInfoRequest = "request.discount.get-info";
        public const string GetDiscountInfoResult = "response.discount.get-info";

        // Internal saga completion signal (saga sends it to itself).
        public const string OrderReadyToComplete = "internal.order.ready-to-complete";
    }

    public static class Exchanges
    {
        public const string Events = "events.exchange";
        public const string Commands = "commands.exchange";
        public const string Rpc = "rpc.exchange";
    }

    public static class Queues
    {
        public const string OrderServiceQueue = "orderservice.events.queue";
        public const string OrderSagaQueue = "orderservice.saga.queue";
        public const string StockQueue = "orderservice.stock.queue";
    }
}
