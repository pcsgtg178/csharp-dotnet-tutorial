# TUTORIAL: สร้าง Web API ด้วยสถาปัตยกรรมของโปรเจกต์นี้

บทเรียนนี้สอนสร้าง API ด้วย **ASP.NET Core Web API (.NET 10) แบบ Controller-based** ตามโครงสร้างของโปรเจกต์ `WebApplication1`
โดยเราจะสร้าง API จัดการสินค้า (`Product`) แบบ CRUD ครบวงจร

## สารบัญ

1. [ภาพรวมสถาปัตยกรรม](#1-ภาพรวมสถาปัตยกรรม)
2. [ทำความเข้าใจโค้ดตัวอย่างที่มีอยู่](#2-ทำความเข้าใจโค้ดตัวอย่างที่มีอยู่)
3. [ขั้นตอนที่ 1: สร้าง Model](#3-ขั้นตอนที่-1-สร้าง-model)
4. [ขั้นตอนที่ 2: สร้าง DTO](#4-ขั้นตอนที่-2-สร้าง-dto)
5. [ขั้นตอนที่ 3: สร้าง Service (Business Logic)](#5-ขั้นตอนที่-3-สร้าง-service-business-logic)
6. [ขั้นตอนที่ 4: ลงทะเบียน Dependency Injection](#6-ขั้นตอนที่-4-ลงทะเบียน-dependency-injection)
7. [ขั้นตอนที่ 5: สร้าง Controller](#7-ขั้นตอนที่-5-สร้าง-controller)
8. [ทดสอบ API](#8-ทดสอบ-api)
9. [Validation และการจัดการ Error](#9-validation-และการจัดการ-error)
10. [ขั้นต่อไป (ต่อฐานข้อมูลจริง)](#10-ขั้นต่อไป-ต่อฐานข้อมูลจริง)
11. [Checklist และข้อควรระวัง](#11-checklist-และข้อควรระวัง)

---

## 1. ภาพรวมสถาปัตยกรรม

โปรเจกต์นี้ใช้รูปแบบ **Controller-based Web API** ซึ่ง request จะไหลดังนี้:

```
Client (Browser / Postman / curl)
        │  HTTP Request
        ▼
┌─────────────────────────────┐
│ Middleware Pipeline         │  ← Program.cs (HttpsRedirection, Authorization, ...)
└─────────────────────────────┘
        ▼
┌─────────────────────────────┐
│ Controller                  │  ← รับ request / ตรวจ input / คืน HTTP status
└─────────────────────────────┘
        ▼
┌─────────────────────────────┐
│ Service                     │  ← Business Logic (ฉีดเข้ามาด้วย DI)
└─────────────────────────────┘
        ▼
  Data (ตอนนี้ in-memory → ภายหลังเปลี่ยนเป็น Database ได้)
```

### โครงสร้างโฟลเดอร์ที่เราจะได้

```
WebApplication1/
├── WebApplication1.slnx
└── WebApplication1/
    ├── Controllers/
    │   ├── WeatherForecastController.cs   (มีอยู่แล้ว)
    │   └── ProductsController.cs          (สร้างใหม่)
    ├── Models/
    │   └── Product.cs                     (สร้างใหม่)
    ├── Dtos/
    │   └── ProductDtos.cs                 (สร้างใหม่)
    ├── Services/
    │   ├── IProductService.cs             (สร้างใหม่)
    │   └── ProductService.cs              (สร้างใหม่)
    ├── Program.cs
    └── appsettings.json
```

| ชั้น (Layer) | หน้าที่ |
|---|---|
| **Controller** | รับ HTTP request, เรียก Service, ส่ง HTTP response (200, 201, 404 ฯลฯ) |
| **Service** | เก็บ Business Logic ทั้งหมด ไม่รู้จัก HTTP |
| **Model** | โครงสร้างข้อมูลหลักของระบบ (Entity) |
| **DTO** | ข้อมูลที่รับเข้า/ส่งออกกับ Client (ไม่เปิดเผย Model ตรง ๆ) |

---

## 2. ทำความเข้าใจโค้ดตัวอย่างที่มีอยู่

### `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();   // เปิดใช้งาน Controller
builder.Services.AddOpenApi();       // สร้างเอกสาร OpenAPI

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                // เปิด /openapi/v1.json เฉพาะ Development
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();                // map route จาก [Route] ใน Controller

app.Run();
```

- ส่วน **`builder.Services...`** = ลงทะเบียน service (DI container)
- ส่วน **`app.Use...` / `app.Map...`** = กำหนด middleware pipeline และ endpoint

### `Controllers/WeatherForecastController.cs`

```csharp
[ApiController]                 // เปิด behavior ของ API เช่น auto validation (400)
[Route("[controller]")]         // [controller] = ชื่อคลาสตัดคำว่า Controller → /weatherforecast
public class WeatherForecastController : ControllerBase
{
    [HttpGet(Name = "GetWeatherForecast")]   // GET /weatherforecast
    public IEnumerable<WeatherForecast> Get() { ... }
}
```

จุดสำคัญ:
- สืบทอดจาก `ControllerBase` (ไม่ใช่ `Controller` เพราะไม่ต้องใช้ View)
- `[ApiController]` + `[Route]` + `[HttpGet]` คือหัวใจของการสร้าง endpoint

---

## 3. ขั้นตอนที่ 1: สร้าง Model

สร้างไฟล์ `WebApplication1/Models/Product.cs`

```csharp
namespace WebApplication1.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## 4. ขั้นตอนที่ 2: สร้าง DTO

ทำไมต้องใช้ DTO? เพื่อ **ควบคุมว่า Client ส่งอะไรเข้ามาได้ / เห็นอะไรกลับไป** เช่น Client ไม่ควรกำหนด `Id` หรือ `CreatedAt` เอง

สร้างไฟล์ `WebApplication1/Dtos/ProductDtos.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Dtos;

// ข้อมูลที่ Client ส่งมาเพื่อสร้างสินค้า
public class CreateProductDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }
}

// ข้อมูลที่ Client ส่งมาเพื่อแก้ไขสินค้า
public class UpdateProductDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }
}

// ข้อมูลที่ส่งกลับไปให้ Client
public record ProductResponseDto(int Id, string Name, decimal Price, int Stock, DateTime CreatedAt);
```

---

## 5. ขั้นตอนที่ 3: สร้าง Service (Business Logic)

### 5.1 Interface

สร้างไฟล์ `WebApplication1/Services/IProductService.cs`

```csharp
using WebApplication1.Dtos;

namespace WebApplication1.Services;

public interface IProductService
{
    IEnumerable<ProductResponseDto> GetAll();
    ProductResponseDto? GetById(int id);
    ProductResponseDto Create(CreateProductDto dto);
    ProductResponseDto? Update(int id, UpdateProductDto dto);
    bool Delete(int id);
}
```

### 5.2 Implementation (เก็บข้อมูลใน memory ก่อน)

สร้างไฟล์ `WebApplication1/Services/ProductService.cs`

```csharp
using WebApplication1.Dtos;
using WebApplication1.Models;

namespace WebApplication1.Services;

public class ProductService : IProductService
{
    private readonly List<Product> _products = [];
    private int _nextId = 1;
    private readonly object _lock = new();

    public IEnumerable<ProductResponseDto> GetAll()
    {
        lock (_lock) return _products.Select(ToDto).ToList();
    }

    public ProductResponseDto? GetById(int id)
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

    public ProductResponseDto? Update(int id, UpdateProductDto dto)
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
        new(p.Id, p.Name, p.Price, p.Stock, p.CreatedAt);
}
```

> **หมายเหตุ:** ใช้ `lock` เพราะ Service นี้ลงทะเบียนเป็น **Singleton** (ข้อมูลใน memory ต้องอยู่ข้าม request) และหลาย request อาจเข้าพร้อมกัน

---

## 6. ขั้นตอนที่ 4: ลงทะเบียน Dependency Injection

แก้ไข `Program.cs` เพิ่มบรรทัด `AddSingleton` ก่อน `builder.Build()`

```csharp
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ลงทะเบียน Service
builder.Services.AddSingleton<IProductService, ProductService>();

var app = builder.Build();
// ... ส่วนที่เหลือเหมือนเดิม
```

อายุของ Service (Lifetime) ที่ควรรู้:

| Lifetime | ความหมาย | ใช้เมื่อ |
|---|---|---|
| `AddSingleton` | สร้าง 1 ตัวตลอดอายุแอป | cache, ข้อมูล in-memory |
| `AddScoped` | สร้าง 1 ตัวต่อ 1 request | งานที่ใช้ DbContext (แนะนำส่วนใหญ่) |
| `AddTransient` | สร้างใหม่ทุกครั้งที่ถูกขอ | งานเบา ๆ ไม่เก็บ state |

---

## 7. ขั้นตอนที่ 5: สร้าง Controller

สร้างไฟล์ `WebApplication1/Controllers/ProductsController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Dtos;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]            // → /api/products
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    // DI จะส่ง IProductService เข้ามาให้อัตโนมัติ
    public ProductsController(IProductService service)
    {
        _service = service;
    }

    // GET /api/products
    [HttpGet]
    public ActionResult<IEnumerable<ProductResponseDto>> GetAll()
        => Ok(_service.GetAll());

    // GET /api/products/1
    [HttpGet("{id:int}", Name = "GetProductById")]
    public ActionResult<ProductResponseDto> GetById(int id)
    {
        var product = _service.GetById(id);
        return product is null ? NotFound() : Ok(product);
    }

    // POST /api/products
    [HttpPost]
    public ActionResult<ProductResponseDto> Create(CreateProductDto dto)
    {
        var created = _service.Create(dto);
        // คืน 201 Created พร้อม header Location ชี้ไปยัง resource ใหม่
        return CreatedAtRoute("GetProductById", new { id = created.Id }, created);
    }

    // PUT /api/products/1
    [HttpPut("{id:int}")]
    public ActionResult<ProductResponseDto> Update(int id, UpdateProductDto dto)
    {
        var updated = _service.Update(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    // DELETE /api/products/1
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
        => _service.Delete(id) ? NoContent() : NotFound();
}
```

### สรุป HTTP Method และ Status Code

| Method | URL | ความหมาย | Status สำเร็จ | Status ไม่สำเร็จ |
|---|---|---|---|---|
| GET | `/api/products` | ดึงทั้งหมด | 200 | - |
| GET | `/api/products/{id}` | ดึงตาม id | 200 | 404 |
| POST | `/api/products` | สร้างใหม่ | 201 | 400 |
| PUT | `/api/products/{id}` | แก้ไข | 200 | 400 / 404 |
| DELETE | `/api/products/{id}` | ลบ | 204 | 404 |

---

## 8. ทดสอบ API

รันแอป (รันจากโฟลเดอร์ solution):

```bash
dotnet watch --project WebApplication1
```

URL เริ่มต้น: `http://localhost:5102` (ดูใน `launchSettings.json`)

### วิธีที่ 1: ไฟล์ `.http` (Visual Studio / VS Code)

เพิ่มลงใน `WebApplication1/WebApplication1.http`

```http
@host = http://localhost:5102

### ดึงสินค้าทั้งหมด
GET {{host}}/api/products

### สร้างสินค้า
POST {{host}}/api/products
Content-Type: application/json

{
  "name": "Keyboard",
  "price": 1290.50,
  "stock": 10
}

### ดึงสินค้าตาม id
GET {{host}}/api/products/1

### แก้ไขสินค้า
PUT {{host}}/api/products/1
Content-Type: application/json

{
  "name": "Mechanical Keyboard",
  "price": 1590,
  "stock": 8
}

### ลบสินค้า
DELETE {{host}}/api/products/1
```

### วิธีที่ 2: curl

```bash
# สร้าง
curl -X POST http://localhost:5102/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","price":1290.50,"stock":10}'

# ดึงทั้งหมด
curl http://localhost:5102/api/products
```

> **Windows PowerShell:** `curl` เป็น alias ของ `Invoke-WebRequest` ให้ใช้ `curl.exe` แทน และ escape เครื่องหมาย `"` ใน JSON หรือใช้ไฟล์ `.http` จะง่ายกว่า

### วิธีที่ 3: OpenAPI document

เปิด `http://localhost:5102/openapi/v1.json` (เฉพาะ Development) แล้วนำไป import ใน Postman / Insomnia ได้

---

## 9. Validation และการจัดการ Error

### Validation อัตโนมัติ

เพราะมี `[ApiController]` เมื่อ DTO ไม่ผ่าน Data Annotation (เช่น `Price = -5`) ระบบจะตอบ **400 Bad Request** ให้เองโดยไม่ต้องเขียนโค้ดเพิ่ม:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Price": ["The field Price must be between 0.01 and 1000000."]
  }
}
```

### Global Exception Handler (แนะนำ)

เพื่อให้ error ที่ไม่คาดคิดตอบเป็นรูปแบบเดียวกัน (ProblemDetails) เพิ่มใน `Program.cs`:

```csharp
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();   // วางไว้ต้น pipeline
```

---

## 10. ขั้นต่อไป (ต่อฐานข้อมูลจริง)

ตอนนี้ข้อมูลหายเมื่อรีสตาร์ทแอป ให้เปลี่ยนมาใช้ **Entity Framework Core** โดยไม่ต้องแก้ Controller เพราะเราแยก Service ไว้แล้ว

1. ติดตั้ง package (ตัวอย่าง SQLite)

   ```bash
   dotnet add WebApplication1 package Microsoft.EntityFrameworkCore.Sqlite
   dotnet add WebApplication1 package Microsoft.EntityFrameworkCore.Design
   ```

2. สร้าง `Data/AppDbContext.cs`

   ```csharp
   using Microsoft.EntityFrameworkCore;
   using WebApplication1.Models;

   namespace WebApplication1.Data;

   public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
   {
       public DbSet<Product> Products => Set<Product>();
   }
   ```

3. ลงทะเบียนใน `Program.cs` และเก็บ connection string ใน `appsettings.json`

   ```csharp
   builder.Services.AddDbContext<AppDbContext>(opt =>
       opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));
   ```

   ```json
   "ConnectionStrings": { "Default": "Data Source=app.db" }
   ```

4. เขียน `ProductService` ใหม่ให้ใช้ `AppDbContext` (เมธอดเป็น `async`/`Task`) แล้วเปลี่ยนการลงทะเบียนเป็น **`AddScoped`**

5. สร้างและอัปเดตฐานข้อมูล

   ```bash
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate --project WebApplication1
   dotnet ef database update --project WebApplication1
   ```

---

## 11. Checklist และข้อควรระวัง

### ขั้นตอนเมื่อต้องการเพิ่ม API ใหม่ (สรุปสั้น)

- [ ] สร้าง **Model** (`Models/`)
- [ ] สร้าง **DTO** พร้อม validation (`Dtos/`)
- [ ] สร้าง **Interface + Service** (`Services/`)
- [ ] ลงทะเบียน DI ใน `Program.cs`
- [ ] สร้าง **Controller** (`Controllers/`)
- [ ] ทดสอบด้วยไฟล์ `.http`

### ข้อควรระวัง

- **Controller ควรบางที่สุด** — ห้ามใส่ business logic ใน Controller
- **อย่าส่ง Model/Entity ออกไปตรง ๆ** — ใช้ DTO เสมอ
- **ชื่อ Route** ใช้พหูพจน์ (`/api/products`) และใช้ HTTP Method บอกการกระทำ ไม่ใส่คำกริยาใน URL
- **Singleton + state** ต้องระวัง thread-safety (ตัวอย่างนี้ใช้ `lock`)
- เมื่อใช้ฐานข้อมูล ให้ใช้ **`async/await`** และ `AddScoped`
- `app.UseHttpsRedirection()` เปิดอยู่ — หากเรียกผ่าน HTTP แล้วถูก redirect ให้ทดสอบผ่าน HTTPS (`https://localhost:7084`) หรือรันด้วย profile `http`

### Troubleshooting

| ปัญหา | วิธีแก้ |
|---|---|
| `Unable to resolve service for type 'IProductService'` | ลืมลงทะเบียน `AddSingleton/AddScoped` ใน `Program.cs` |
| 404 ทั้งที่มี Controller | ตรวจ `[Route]` และ `app.MapControllers()` |
| 415 Unsupported Media Type | ลืมใส่ header `Content-Type: application/json` |
| 400 ทั้งที่ส่งข้อมูลถูก | ตรวจชื่อ field ใน JSON และ Data Annotation ใน DTO |
| Port ถูกใช้งานแล้ว | แก้ `applicationUrl` ใน `launchSettings.json` |

---

อ่านเรื่องการ Build / Deploy ต่อได้ที่ [README.md](README.md)
