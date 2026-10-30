using WebApplication1.Dtos;
using WebApplication1.Models;

namespace WebApplication1.Services;

// ProductService ที่สิบทอด IProductService มา
public class ProductService : IProductService
{
    //เก็บข้อมูลใน memory ใช้ lock กัน thread ซ้ำกัน (ไม่ใช้ DB)
    private readonly List<Product> _products = [];
    private int _nextId = 1;
    
    private readonly object _lock = new(); // เพราะ Service นี้ลงทะเบียนเป็น **Singleton**
    
    public IEnumerable<ProductResponseDto> FindAll()
    {
        lock (_lock) return _products.Select(ToDto).ToList();
    }
    public ProductResponseDto FindById(int id)
    {
        lock (_lock)
        {
            var product = _products.FirstOrDefault(p => p.Id == id);
            return product is null ? null : ToDto(product);
        }
    }
    public ProductResponseDto Create(CreateProductDto dto)
    {
        lock (_lock)
        {
            var product = new Product
            {
                Id = _nextId++,
                Name = dto.Name,
                Price = dto.Price,
                Stock = dto.Stock
            };
            _products.Add(product);
            return ToDto(product);
        }
    }
    public ProductResponseDto Update(int id, UpdateProductDto dto)
    {
        lock (_lock)
        {
            var product = _products.FirstOrDefault(p => p.Id == id);
            if (product is null) return null;

            product.Name = dto.Name;
            product.Price = dto.Price;
            product.Stock = dto.Stock;
            return ToDto(product);
        }
    }
    public bool Delete(int id)
    {
        lock (_lock)
        {
            var product = _products.FirstOrDefault(p => p.Id == id);
            return product is not null && _products.Remove(product);
        }
    }

    private static ProductResponseDto ToDto(Product p) =>
        new(p.Id, p.Name, p.Price, p.Stock, p.CreateAt, p.UpdateAt);
}