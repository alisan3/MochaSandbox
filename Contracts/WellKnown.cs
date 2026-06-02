namespace Contracts;

public static class WellKnown
{
    public static class RoutingKeys
    {
        public const string All = "event.order.#";
        public const string OrderPlaced = "event.order.placed";
        public const string OrderCancelled = "event.order.cancelled";
    }

    public static class Exchanges
    {
        public const string Events = "events.exchange";
    }

    public static class Queues
    {
        public const string OrderServiceEvents = "orderservice.events.queue";
    }
}
