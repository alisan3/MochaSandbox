using Microsoft.EntityFrameworkCore;
using Mocha.Sagas.EfCore;

namespace OrderService;

// EF Core DbContext backing the Mocha saga store. The SagaStates table and its
// mapping are provided by Mocha via modelBuilder.AddPostgresSagas().
public sealed class OrderSagaDbContext(DbContextOptions<OrderSagaDbContext> options)
    : DbContext(options)
{
    public DbSet<SagaState> SagaStates => Set<SagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddPostgresSagas();
    }
}
