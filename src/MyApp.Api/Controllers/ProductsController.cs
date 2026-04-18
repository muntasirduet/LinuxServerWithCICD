using Microsoft.AspNetCore.Mvc;
using MyApp.Core.Interfaces;

namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok(productService.GetAll());
}
