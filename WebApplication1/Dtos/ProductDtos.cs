using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Dtos;

// ข้อมูลที่ Client ส่งมาเพื่อสร้างสินค้า
public class CreateProductDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 1_000_000_000)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }
}

// ข้อมูลที่ Client ส่งมาเพื่อแก้ไขสินค้า
public class UpdateProductDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 1_000_000_000)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }
}

// ข้อมูลที่ Client รับกลับไป
public record ProductResponseDto(
    int Id,
    string Name,
    decimal Price,
    int Stock,
    DateTime CreateAt,
    DateTime UpdateAt
);
