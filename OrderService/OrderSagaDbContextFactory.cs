using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderService;

// Used only by `dotnet ef` at design time (e.g. to add migrations). At runtime
// the DbContext is configured by Aspire via AddNpgsqlDbContext("sagadb").
public sealed class OrderSagaDbContextFactory : IDesignTimeDbContextFactory<OrderSagaDbContext>
{
    public OrderSagaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderSagaDbContext>()
            .UseNpgsql("Host=localhost;Database=sagadb;Username=postgres;Password=postgres")
            .Options;

        return new OrderSagaDbContext(options);
    }
}
