using Microsoft.AspNetCore.Mvc;
using WebApplication1.Dtos;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]  //ระบุว่าเป็น API Controller ไม่ใช่ MVC
[Route("api/[controller]")]  //ระบุ route: /api/products
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    // GET /api/products
    [HttpGet]
    public ActionResult<IEnumerable<ProductResponseDto>> FindAll()
    {
        return Ok(_service.FindAll());
    }

    // GET /api/products/1
    [HttpGet("{id:int}", Name = "FindById")]
    public ActionResult<ProductResponseDto> FindById(int id)
    {
        var product = _service.FindById(id);

        if (product is null)
        {
            return NotFound($"Product with ID {id} not found");
        }

        return Ok(product);
    }

    // POST /api/products
    [HttpPost]
    public ActionResult<ProductResponseDto> Create(CreateProductDto dto)
    {
        var created = _service.Create(dto);

        return CreatedAtRoute("FindById", new { id = created.Id }, created);
    }

    // PUT /api/products/1
    [HttpPut("{id:int}")]
    public ActionResult<ProductResponseDto> Update(int id, UpdateProductDto dto)
    {
        var updated = _service.Update(id, dto);
        if (updated is null)
        {
            return NotFound($"Product with ID {id} not found");
        }
        return Ok(updated);
    }

    // DELETE /api/products/1
    [HttpDelete("{id:int}")]
    public ActionResult Delete(int id)
    {
        var deleted = _service.Delete(id);
        return deleted ? NoContent() : NotFound($"Product with ID {id} not found");
    }
}