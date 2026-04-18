using Microsoft.EntityFrameworkCore;
using MyApp.Core.Entities;
using MyApp.Infrastructure.Data;

namespace MyApp.Infrastructure.Repositories;

public sealed class ProductRepository(AppDbContext dbContext)
{
    public Task<List<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        dbContext.Products.AsNoTracking().ToListAsync(cancellationToken);
}
