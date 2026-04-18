using MyApp.Core.Entities;

namespace MyApp.Core.Interfaces;

public interface IProductService
{
    IReadOnlyList<Product> GetAll();
}
