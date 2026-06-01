namespace Contracts;

public static class WellKnown
{
    public static class RoutingKeys
    {
        public const string OrderPlaced = "event.order.placed";
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
