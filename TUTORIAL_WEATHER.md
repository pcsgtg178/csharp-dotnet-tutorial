# TUTORIAL_WEATHER: ดึงข้อมูลสภาพอากาศ + หน้าแสดงผล + Schedule ทุก 10 วินาที

บทเรียนต่อยอดจาก [TUTORIAL.md](TUTORIAL.md) (โครงสร้าง Controller → Service → DI เดิม) สิ่งที่จะได้:

1. เรียก **Open-Meteo API** (open-source ฟรี ไม่ต้องใช้ API key) แล้วส่งข้อมูลกลับผ่าน API ของเรา
2. **หน้าเว็บ (View)** แสดงข้อมูลสภาพอากาศล่าสุด
3. **Schedule** ดึงข้อมูลทุก 10 วินาทีด้วย `BackgroundService` + `PeriodicTimer` แล้วเก็บค่าล่าสุดไว้ใน memory

## สารบัญ

1. [ภาพรวม](#1-ภาพรวม)
2. [ทำความรู้จัก Open-Meteo](#2-ทำความรู้จัก-open-meteo)
3. [ขั้นตอนที่ 1: Configuration](#3-ขั้นตอนที่-1-configuration)
4. [ขั้นตอนที่ 2: Model และ DTO](#4-ขั้นตอนที่-2-model-และ-dto)
5. [ขั้นตอนที่ 3: Weather Client (เรียก API ภายนอก)](#5-ขั้นตอนที่-3-weather-client-เรียก-api-ภายนอก)
6. [ขั้นตอนที่ 4: Weather Store (เก็บค่าล่าสุด)](#6-ขั้นตอนที่-4-weather-store-เก็บค่าล่าสุด)
7. [ขั้นตอนที่ 5: Background Service (Schedule ทุก 10 วินาที)](#7-ขั้นตอนที่-5-background-service-schedule-ทุก-10-วินาที)
8. [ขั้นตอนที่ 6: Controller](#8-ขั้นตอนที่-6-controller)
9. [ขั้นตอนที่ 7: ลงทะเบียนใน Program.cs](#9-ขั้นตอนที่-7-ลงทะเบียนใน-programcs)
10. [ขั้นตอนที่ 8: หน้าเว็บ (View)](#10-ขั้นตอนที่-8-หน้าเว็บ-view)
11. [ทดสอบ](#11-ทดสอบ)
12. [ข้อควรระวังและ Troubleshooting](#12-ข้อควรระวังและ-troubleshooting)
13. [ทางเลือก: Cron จริงด้วย Quartz.NET](#13-ทางเลือก-cron-จริงด้วย-quartznet)

---

## 1. ภาพรวม

```
                  ทุก 10 วินาที
┌──────────────────────────────┐
│ WeatherBackgroundService     │──┐
└──────────────────────────────┘  │ 1. เรียก
                                  ▼
                      ┌──────────────────────┐   HTTP    ┌──────────────┐
                      │ OpenMeteoWeatherClient│─────────▶│  Open-Meteo  │
                      └──────────────────────┘           └──────────────┘
                                  │ 2. บันทึกผล
                                  ▼
                      ┌──────────────────────┐
                      │ WeatherStore (memory)│  ← เก็บค่าล่าสุด
                      └──────────────────────┘
                                  ▲ 3. อ่านค่า
Browser ──GET /api/weather/latest─┤
  ▲                               │
  └── หน้า index.html (fetch ทุก 10 วินาที) ◀── WeatherController
```

โครงสร้างไฟล์ที่เพิ่ม:

```
WebApplication1/
├── Controllers/`WeatherController`.cs
├── Dtos/WeatherDtos.cs
├── Options/WeatherOptions.cs
├── Services/
│   ├── IWeatherClient.cs
│   ├── OpenMeteoWeatherClient.cs
│   ├── IWeatherStore.cs
│   ├── WeatherStore.cs
│   └── WeatherBackgroundService.cs
├── wwwroot/index.html
├── Program.cs               (แก้ไข)
└── appsettings.json         (แก้ไข)
```

> ใน Controller ตั้งชื่อ `WeatherController` (route `/api/weather`) เพื่อไม่ชนกับ `WeatherForecastController` เดิมที่ route `/weatherforecast`

---

## 2. ทำความรู้จัก Open-Meteo

ลองเรียกดูก่อน (กรุงเทพฯ lat 13.75, lon 100.52):

```bash
curl "https://api.open-meteo.com/v1/forecast?latitude=13.75&longitude=100.52&current=temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code&timezone=Asia%2FBangkok"
```

ผลลัพธ์ (ตัดบางส่วน):

```json
{
  "latitude": 13.75,
  "longitude": 100.5,
  "timezone": "Asia/Bangkok",
  "current_units": { "temperature_2m": "°C", "wind_speed_10m": "km/h" },
  "current": {
    "time": "2026-10-23T16:00",
    "interval": 900,
    "temperature_2m": 31.2,
    "relative_humidity_2m": 62,
    "wind_speed_10m": 9.4,
    "weather_code": 3
  }
}
```

| field | ความหมาย |
|---|---|
| `temperature_2m` | อุณหภูมิที่ความสูง 2 เมตร (°C) |
| `relative_humidity_2m` | ความชื้นสัมพัทธ์ (%) |
| `wind_speed_10m` | ความเร็วลม (km/h) |
| `weather_code` | รหัสสภาพอากาศแบบ WMO (0 = ท้องฟ้าโปร่ง, 3 = มีเมฆมาก, 61 = ฝนเล็กน้อย ฯลฯ) |

> **หมายเหตุ:** Open-Meteo อัปเดตค่า `current` ทุก ~15 นาที การเรียกทุก 10 วินาทีจึงมักได้ค่าเดิม เหมาะกับการเรียนรู้เรื่อง schedule เท่านั้น ส่วนการใช้งานจริงแนะนำ 5–15 นาที (ดูหัวข้อ 12)

---

## 3. ขั้นตอนที่ 1: Configuration

แก้ `appsettings.json` เพิ่ม section `Weather` (เก็บเมือง พิกัด และความถี่ไว้ในไฟล์ตั้งค่า)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Weather": {
    "BaseUrl": "https://api.open-meteo.com/",
    "City": "Bangkok",
    "Latitude": 13.75,
    "Longitude": 100.52,
    "Timezone": "Asia/Bangkok",
    "FetchIntervalSeconds": 10
  }
}
```

> ให้คง section อื่นที่มีอยู่เดิมในไฟล์ไว้ แล้วเพิ่มเฉพาะ `"Weather"`

สร้าง `WebApplication1/Options/WeatherOptions.cs` เพื่อ bind ค่าจาก config เป็นคลาส

```csharp
namespace WebApplication1.Options;

public class WeatherOptions
{
    public const string SectionName = "Weather";

    public string BaseUrl { get; set; } = "https://api.open-meteo.com/";
    public string City { get; set; } = "Bangkok";
    public double Latitude { get; set; } = 13.75;
    public double Longitude { get; set; } = 100.52;
    public string Timezone { get; set; } = "Asia/Bangkok";
    public int FetchIntervalSeconds { get; set; } = 10;
}
```

---

## 4. ขั้นตอนที่ 2: Model และ DTO

สร้าง `WebApplication1/Dtos/WeatherDtos.cs`

```csharp
using System.Text.Json.Serialization;

namespace WebApplication1.Dtos;

// ---------- โครงสร้างตอบกลับจาก Open-Meteo (รับเข้า) ----------
public class OpenMeteoResponse
{
    [JsonPropertyName("current")]
    public OpenMeteoCurrent? Current { get; set; }
}

public class OpenMeteoCurrent
{
    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;

    [JsonPropertyName("temperature_2m")]
    public double Temperature { get; set; }

    [JsonPropertyName("relative_humidity_2m")]
    public int Humidity { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public double WindSpeed { get; set; }

    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }
}

// ---------- ข้อมูลที่ API ของเราส่งกลับให้ Client (ส่งออก) ----------
public record WeatherResponseDto(
    string City,
    double TemperatureC,
    int Humidity,
    double WindSpeedKmh,
    int WeatherCode,
    string Description,
    DateTime FetchedAtUtc
);
```

ทำไมแยก 2 ชุด? `OpenMeteoResponse` ผูกกับรูปแบบของ API ภายนอก ถ้าเขาเปลี่ยน เราแก้แค่ที่นี่ ส่วน `WeatherResponseDto` คือสัญญา (contract) ที่เรามีกับ Client ของเรา

---

## 5. ขั้นตอนที่ 3: Weather Client (เรียก API ภายนอก)

### 5.1 Interface — `Services/IWeatherClient.cs`

```csharp
using WebApplication1.Dtos;

namespace WebApplication1.Services;

public interface IWeatherClient
{
    Task<WeatherResponseDto> GetCurrentAsync(CancellationToken cancellationToken = default);
}
```

### 5.2 Implementation — `Services/OpenMeteoWeatherClient.cs`

ใช้ **Typed HttpClient** (`HttpClient` ถูกจัดการ lifetime ให้โดย `IHttpClientFactory` ไม่ต้อง `new HttpClient()` เอง)

```csharp
using System.Globalization;
using Microsoft.Extensions.Options;
using WebApplication1.Dtos;
using WebApplication1.Options;

namespace WebApplication1.Services;

public class OpenMeteoWeatherClient : IWeatherClient
{
    private readonly HttpClient _http;
    private readonly WeatherOptions _options;

    public OpenMeteoWeatherClient(HttpClient http, IOptions<WeatherOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<WeatherResponseDto> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        // ใช้ InvariantCulture กันปัญหาทศนิยมเป็น "," ในบางภาษา
        var lat = _options.Latitude.ToString(CultureInfo.InvariantCulture);
        var lon = _options.Longitude.ToString(CultureInfo.InvariantCulture);

        var url = $"v1/forecast?latitude={lat}&longitude={lon}" +
                  "&current=temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code" +
                  $"&timezone={Uri.EscapeDataString(_options.Timezone)}";

        var result = await _http.GetFromJsonAsync<OpenMeteoResponse>(url, cancellationToken);

        if (result?.Current is null)
            throw new InvalidOperationException("Open-Meteo returned no 'current' data.");

        var c = result.Current;
        return new WeatherResponseDto(
            City: _options.City,
            TemperatureC: c.Temperature,
            Humidity: c.Humidity,
            WindSpeedKmh: c.WindSpeed,
            WeatherCode: c.WeatherCode,
            Description: DescribeWeatherCode(c.WeatherCode),
            FetchedAtUtc: DateTime.UtcNow);
    }

    // แปลงรหัส WMO เป็นข้อความ (ย่อ)
    private static string DescribeWeatherCode(int code) => code switch
    {
        0 => "ท้องฟ้าโปร่ง",
        1 or 2 => "มีเมฆบางส่วน",
        3 => "มีเมฆมาก",
        45 or 48 => "หมอก",
        >= 51 and <= 57 => "ฝนปรอย",
        >= 61 and <= 67 => "ฝนตก",
        >= 71 and <= 77 => "หิมะ",
        >= 80 and <= 82 => "ฝนฟ้าคะนอง (ซู่)",
        >= 95 and <= 99 => "พายุฝนฟ้าคะนอง",
        _ => "ไม่ทราบสภาพอากาศ"
    };
}
```

> `GetFromJsonAsync` เป็น extension จาก `System.Net.Http.Json` (เปิดใช้อัตโนมัติด้วย Implicit Usings ของ Web SDK)

---

## 6. ขั้นตอนที่ 4: Weather Store (เก็บค่าล่าสุด)

### `Services/IWeatherStore.cs`

```csharp
using WebApplication1.Dtos;

namespace WebApplication1.Services;

public interface IWeatherStore
{
    WeatherResponseDto? Latest { get; }
    void Set(WeatherResponseDto weather);
}
```

### `Services/WeatherStore.cs`

```csharp
using WebApplication1.Dtos;

namespace WebApplication1.Services;

public class WeatherStore : IWeatherStore
{
    // volatile: ให้ thread อื่นเห็นค่าล่าสุดทันที (record เป็น immutable จึงไม่ต้องใช้ lock)
    private volatile WeatherResponseDto? _latest;

    public WeatherResponseDto? Latest => _latest;

    public void Set(WeatherResponseDto weather) => _latest = weather;
}
```

Store นี้จะถูกลงทะเบียนเป็น **Singleton** เพราะทั้ง BackgroundService (ผู้เขียน) และ Controller (ผู้อ่าน) ต้องเห็นข้อมูลชุดเดียวกัน

---

## 7. ขั้นตอนที่ 5: Background Service (Schedule ทุก 10 วินาที)

`BackgroundService` คืองานที่รันเบื้องหลังตลอดอายุแอป เริ่มทำงานพร้อมแอปโดยอัตโนมัติ

สร้าง `Services/WeatherBackgroundService.cs`

```csharp
using Microsoft.Extensions.Options;
using WebApplication1.Options;

namespace WebApplication1.Services;

public class WeatherBackgroundService : BackgroundService
{
    private readonly IWeatherClient _client;
    private readonly IWeatherStore _store;
    private readonly ILogger<WeatherBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public WeatherBackgroundService(
        IWeatherClient client,
        IWeatherStore store,
        IOptions<WeatherOptions> options,
        ILogger<WeatherBackgroundService> logger)
    {
        _client = client;
        _store = store;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.FetchIntervalSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Weather scheduler started. Interval = {Interval}", _interval);

        // ดึงทันที 1 ครั้งตอนเริ่มแอป (ไม่ต้องรอ 10 วินาทีแรก)
        await FetchOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FetchOnceAsync(stoppingToken);
        }
    }

    private async Task FetchOnceAsync(CancellationToken ct)
    {
        try
        {
            var weather = await _client.GetCurrentAsync(ct);
            _store.Set(weather);
            _logger.LogInformation("Weather updated: {City} {Temp}°C", weather.City, weather.TemperatureC);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // แอปกำลังปิด ไม่ถือเป็น error
        }
        catch (Exception ex)
        {
            // สำคัญ: ต้องจับ exception ไว้ ไม่เช่นนั้น service จะหยุดทำงานถาวร
            _logger.LogWarning(ex, "Failed to fetch weather. Will retry on next tick.");
        }
    }
}
```

จุดสำคัญ:
- `PeriodicTimer` เหมาะกว่า `Task.Delay` ในลูป เพราะ tick ตรงเวลาสม่ำเสมอ ไม่เหลื่อมตามเวลาที่งานใช้
- ต้อง `try/catch` ในรอบการทำงาน ถ้า network ล่มชั่วคราว schedule จะยังวิ่งต่อ
- ส่ง `stoppingToken` ต่อไปให้ทุก async call เพื่อให้ปิดแอปได้เร็ว (graceful shutdown)

---

## 8. ขั้นตอนที่ 6: Controller

สร้าง `Controllers/WeatherController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Dtos;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]   // → /api/weather
public class WeatherController : ControllerBase
{
    private readonly IWeatherClient _client;
    private readonly IWeatherStore _store;

    public WeatherController(IWeatherClient client, IWeatherStore store)
    {
        _client = client;
        _store = store;
    }

    // GET /api/weather/current  → เรียก Open-Meteo สด ๆ แล้วส่งกลับทันที (ข้อ 1)
    [HttpGet("current")]
    public async Task<ActionResult<WeatherResponseDto>> GetCurrent(CancellationToken ct)
    {
        try
        {
            return Ok(await _client.GetCurrentAsync(ct));
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Weather provider is unavailable.");
        }
    }

    // GET /api/weather/latest  → ค่าล่าสุดที่ schedule เก็บไว้ (ข้อ 2 และ 3)
    [HttpGet("latest")]
    public ActionResult<WeatherResponseDto> GetLatest()
    {
        var latest = _store.Latest;
        return latest is null
            ? NotFound("No weather data yet. Please try again in a few seconds.")
            : Ok(latest);
    }
}
```

| Endpoint | ทำอะไร | เมื่อไรใช้ |
|---|---|---|
| `GET /api/weather/current` | เรียก Open-Meteo ทันที | ต้องการค่าสดตอนนี้ |
| `GET /api/weather/latest` | อ่านค่าล่าสุดจาก store (เร็ว ไม่เรียก API ภายนอก) | หน้าเว็บ / Client ทั่วไป |

---

## 9. ขั้นตอนที่ 7: ลงทะเบียนใน Program.cs

เพิ่มส่วนที่เกี่ยวข้อง (ตำแหน่งตามคอมเมนต์) โดยคงโค้ดเดิมจากบทเรียน Product ไว้

```csharp
using WebApplication1.Options;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// --- (จากบทเรียนก่อนหน้า) ---
builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddProblemDetails();

// --- Weather ---
builder.Services.Configure<WeatherOptions>(
    builder.Configuration.GetSection(WeatherOptions.SectionName));

builder.Services.AddHttpClient<IWeatherClient, OpenMeteoWeatherClient>((sp, http) =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherOptions>>().Value;
    http.BaseAddress = new Uri(opt.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(8);   // ต้องน้อยกว่าความถี่ schedule
});

builder.Services.AddSingleton<IWeatherStore, WeatherStore>();
builder.Services.AddHostedService<WeatherBackgroundService>();   // เริ่ม schedule อัตโนมัติ

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();   // ให้ "/" เปิด wwwroot/index.html
app.UseStaticFiles();    // เสิร์ฟไฟล์ใน wwwroot

app.UseAuthorization();
app.MapControllers();

app.Run();
```

สิ่งที่เพิ่ม:

| โค้ด | หน้าที่ |
|---|---|
| `Configure<WeatherOptions>` | bind section `Weather` เข้าคลาส (ใช้ผ่าน `IOptions<T>`) |
| `AddHttpClient<TInterface, TImpl>` | ลงทะเบียน Typed HttpClient |
| `AddSingleton<IWeatherStore, ...>` | store ค่าล่าสุด (ใช้ร่วมกัน) |
| `AddHostedService<...>` | เริ่ม BackgroundService พร้อมแอป |
| `UseDefaultFiles` + `UseStaticFiles` | เสิร์ฟหน้า HTML จาก `wwwroot` (**ต้องเรียก `UseDefaultFiles` ก่อน `UseStaticFiles`**) |

> `IHttpClientFactory` สร้าง `OpenMeteoWeatherClient` เป็น **Transient** แต่ `WeatherBackgroundService` (Singleton) รับมาตัวเดียวและถือไว้ตลอด ในบทเรียนนี้ใช้ได้เพราะ client ไม่มี state แต่ถ้าแอปรันนานมาก ๆ (เป็นวัน) และต้องการให้ DNS refresh ให้ใช้ `IHttpClientFactory.CreateClient()` ในทุกรอบแทน

---

## 10. ขั้นตอนที่ 8: หน้าเว็บ (View)

สร้างโฟลเดอร์ `WebApplication1/wwwroot/` แล้วสร้าง `index.html` (หน้า HTML ธรรมดา ใช้ JavaScript `fetch` ดึง `/api/weather/latest` ทุก 10 วินาที)

```html
<!DOCTYPE html>
<html lang="th">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Weather Dashboard - สภาพอากาศล่าสุด</title>
  <meta name="description" content="แสดงข้อมูลสภาพอากาศล่าสุดจาก Open-Meteo อัปเดตอัตโนมัติ" />
  <style>
    :root { color-scheme: dark; }
    * { box-sizing: border-box; }
    body {
      margin: 0; min-height: 100vh; display: grid; place-items: center;
      font-family: system-ui, "Segoe UI", sans-serif; color: #e8f0ff;
      background: radial-gradient(circle at 20% 20%, #2b5fd9 0%, #0f1b3d 55%, #070b1a 100%);
    }
    .card {
      width: min(92vw, 420px); padding: 2rem; border-radius: 24px; text-align: center;
      background: rgba(255,255,255,.08); backdrop-filter: blur(14px);
      border: 1px solid rgba(255,255,255,.15); box-shadow: 0 20px 60px rgba(0,0,0,.45);
    }
    h1 { margin: 0; font-size: 1.1rem; font-weight: 600; letter-spacing: .08em; opacity: .85; }
    .temp { font-size: 5rem; font-weight: 700; margin: .5rem 0 0; transition: transform .3s; }
    .temp.pulse { transform: scale(1.06); }
    .desc { font-size: 1.2rem; margin-bottom: 1.5rem; opacity: .9; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: .8rem; }
    .item { padding: .9rem; border-radius: 14px; background: rgba(255,255,255,.07); }
    .item small { display: block; opacity: .65; margin-bottom: .2rem; }
    .item strong { font-size: 1.25rem; }
    .footer { margin-top: 1.5rem; font-size: .8rem; opacity: .7; }
    .dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%;
           background: #f5a623; margin-right: .4rem; }
    .dot.ok { background: #3ddc84; animation: blink 1.5s infinite; }
    .dot.err { background: #ff5c5c; }
    @keyframes blink { 50% { opacity: .35; } }
  </style>
</head>
<body>
  <main class="card">
    <h1 id="city">กำลังโหลด...</h1>
    <p class="temp" id="temp">--°</p>
    <p class="desc" id="desc">&nbsp;</p>

    <section class="grid" aria-label="รายละเอียด">
      <div class="item"><small>ความชื้น</small><strong id="humidity">--</strong></div>
      <div class="item"><small>ความเร็วลม</small><strong id="wind">--</strong></div>
    </section>

    <p class="footer">
      <span class="dot" id="dot"></span><span id="status">กำลังเชื่อมต่อ...</span>
    </p>
  </main>

  <script>
    const REFRESH_MS = 10_000;
    const $ = (id) => document.getElementById(id);

    async function loadWeather() {
      try {
        const res = await fetch('/api/weather/latest');
        if (res.status === 404) {
          setStatus('รอข้อมูลชุดแรก...', ''); return;
        }
        if (!res.ok) throw new Error(res.status);

        const w = await res.json();
        $('city').textContent = w.city;
        $('temp').textContent = `${w.temperatureC.toFixed(1)}°C`;
        $('desc').textContent = w.description;
        $('humidity').textContent = `${w.humidity}%`;
        $('wind').textContent = `${w.windSpeedKmh.toFixed(1)} km/h`;

        const t = new Date(w.fetchedAtUtc).toLocaleTimeString('th-TH');
        setStatus(`อัปเดตล่าสุด ${t}`, 'ok');

        $('temp').classList.add('pulse');
        setTimeout(() => $('temp').classList.remove('pulse'), 300);
      } catch (e) {
        setStatus('เชื่อมต่อเซิร์ฟเวอร์ไม่ได้', 'err');
      }
    }

    function setStatus(text, cls) {
      $('status').textContent = text;
      $('dot').className = 'dot ' + cls;
    }

    loadWeather();
    setInterval(loadWeather, REFRESH_MS);
  </script>
</body>
</html>
```

> ชื่อ field ใน JSON (`city`, `temperatureC`, ...) เป็น **camelCase** เพราะ ASP.NET Core แปลงให้อัตโนมัติ

---

## 11. ทดสอบ

```bash
dotnet watch --project WebApplication1
```

1. เปิดเบราว์เซอร์ที่ `http://localhost:5102/` ควรเห็นหน้า Weather Dashboard และข้อมูลอัปเดตทุก 10 วินาที
2. ดู log ใน terminal ควรเห็นข้อความทุก 10 วินาที:

   ```
   info: WebApplication1.Services.WeatherBackgroundService[0]
         Weather updated: Bangkok 31.2°C
   ```

3. ทดสอบ API ตรง ๆ (เพิ่มลง `WebApplication1.http`)

   ```http
   ### เรียก Open-Meteo สด ๆ
   GET http://localhost:5102/api/weather/current

   ### ค่าล่าสุดที่ schedule เก็บไว้
   GET http://localhost:5102/api/weather/latest
   ```

4. ทดลองเปลี่ยนเมือง: แก้ `Latitude`/`Longitude`/`City` ใน `appsettings.json` เช่น เชียงใหม่ (18.79, 98.98) แล้วรีสตาร์ทแอป

   > PowerShell ใช้ `curl.exe` แทน `curl`

---

## 12. ข้อควรระวังและ Troubleshooting

| ปัญหา | สาเหตุ / วิธีแก้ |
|---|---|
| `/api/weather/latest` ได้ 404 ตอนเพิ่งเปิดแอป | ข้อมูลชุดแรกยังไม่มา รอสักครู่หรือเช็ก log ว่าเรียก Open-Meteo ได้หรือไม่ |
| Log เตือน `Failed to fetch weather` | เช็กอินเทอร์เน็ต / proxy ของเครื่อง / `BaseUrl` ใน config |
| เปิด `/` แล้วไม่เจอหน้าเว็บ | ลืมสร้าง `wwwroot/index.html` หรือเรียก `UseDefaultFiles` หลัง `UseStaticFiles` |
| ค่าไม่เปลี่ยนทั้งที่เรียกทุก 10 วินาที | ปกติ เพราะ Open-Meteo อัปเดตค่าทุก ~15 นาที |
| `Unable to resolve service for type 'IWeatherStore'` | ลืม `AddSingleton<IWeatherStore, WeatherStore>()` |
| ทศนิยมใน URL ผิด (`13,75`) | ต้อง `ToString(CultureInfo.InvariantCulture)` (ในโค้ดตัวอย่างทำไว้แล้ว) |
| หน้าเว็บเรียก API แล้วเด้งเป็น HTTPS | `UseHttpsRedirection` เปิดอยู่ ใช้ `https://localhost:7084` หรือรัน profile `http` |

### เรื่องควรรู้

- **Rate limit / มารยาท:** Open-Meteo ฟรีสำหรับใช้งานไม่เชิงพาณิชย์ (จำกัดประมาณ 10,000 ครั้ง/วัน) ถ้าเรียกทุก 10 วินาทีจะอยู่ที่ ~8,640 ครั้ง/วัน ใกล้ขีดจำกัด จึงควรปรับ `FetchIntervalSeconds` เป็น 300–900 เมื่อใช้งานจริง
- **State หายเมื่อรีสตาร์ท:** store เป็น in-memory ถ้าต้องการเก็บประวัติให้เปลี่ยนไปใช้ฐานข้อมูล (ดูหัวข้อ 10 ใน [TUTORIAL.md](TUTORIAL.md))
- **หลาย instance:** ถ้า deploy หลาย instance แต่ละตัวจะรัน schedule เอง (เรียก API ซ้ำ) ควรย้ายงาน schedule ไปไว้ที่เดียว หรือใช้ Quartz/Hangfire พร้อม distributed lock
- **Polling vs Push:** หน้าเว็บ poll ทุก 10 วินาที ถ้าต้องการ real-time จริงให้ศึกษา **SignalR** หรือ Server-Sent Events

---

## 13. ทางเลือก: Cron จริงด้วย Quartz.NET

หากต้องการใช้ **cron expression** (เช่น `*/10 * * * * ?` = ทุก 10 วินาที, `0 */15 * * * ?` = ทุก 15 นาที) ใช้ Quartz.NET แทน BackgroundService ได้

```bash
dotnet add WebApplication1 package Quartz.Extensions.Hosting
```

```csharp
using Quartz;
using WebApplication1.Services;

// Job
public class WeatherJob(IWeatherClient client, IWeatherStore store, ILogger<WeatherJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            store.Set(await client.GetCurrentAsync(context.CancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch weather.");
        }
    }
}

// Program.cs  (แทนที่ AddHostedService<WeatherBackgroundService>)
builder.Services.AddQuartz(q =>
{
    var key = new JobKey("weather-job");
    q.AddJob<WeatherJob>(opts => opts.WithIdentity(key));
    q.AddTrigger(t => t
        .ForJob(key)
        .WithCronSchedule("*/10 * * * * ?"));   // Quartz cron มี 6-7 ฟิลด์ (เริ่มจากวินาที)
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
```

| | BackgroundService + PeriodicTimer | Quartz.NET |
|---|---|---|
| ความซับซ้อน | ต่ำ ไม่ต้องติดตั้ง package | ปานกลาง ต้องเพิ่ม package |
| รูปแบบเวลา | ช่วงเวลาคงที่ | cron expression, ตารางเวลาซับซ้อน |
| เหมาะกับ | งานวนซ้ำง่าย ๆ | งานตามเวลา เช่น "ทุกวัน 08:00" |

---

กลับไปอ่านพื้นฐานได้ที่ [TUTORIAL.md](TUTORIAL.md)
