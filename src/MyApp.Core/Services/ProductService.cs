using MyApp.Core.Entities;
using MyApp.Core.Interfaces;

namespace MyApp.Core.Services;

public sealed class ProductService : IProductService
{
    // Temporary in-memory seed to keep core service independent from infrastructure concerns.
    private static readonly IReadOnlyList<Product> Products =
    [
        new Product { Id = 1, Name = "Laptop", Price = 1200m },
        new Product { Id = 2, Name = "Keyboard", Price = 80m }
    ];

    public IReadOnlyList<Product> GetAll() => Products;
}
