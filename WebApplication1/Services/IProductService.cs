using WebApplication1.Dtos;

namespace WebApplication1.Services;

public interface IProductService
{
    IEnumerable<ProductResponseDto> FindAll();
    ProductResponseDto? Create(CreateProductDto dto);
    ProductResponseDto Update(int id, UpdateProductDto dto);
    ProductResponseDto? FindById(int id);
    bool Delete(int id);
}