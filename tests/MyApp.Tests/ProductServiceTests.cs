using MyApp.Core.Services;

namespace MyApp.Tests;

public class ProductServiceTests
{
    [Fact]
    public void GetAll_ReturnsSeededProducts()
    {
        var service = new ProductService();

        var products = service.GetAll();

        Assert.Equal(2, products.Count);
        Assert.Contains(products, p => p.Name == "Laptop");
        Assert.Contains(products, p => p.Name == "Keyboard");
    }
}
