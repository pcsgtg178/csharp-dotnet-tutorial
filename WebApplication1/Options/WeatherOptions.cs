namespace WebApplication1.Options;

public class WeatherOptions
{
    public const string SectionName = "Weather";
    
    public string BaseUrl { get; set; } = "https://api.open-meteo.com/";
    public string City { get; set; } = "Bangkok";
    public double Latitude { get; set; } = 13.75;
    public double Longitude { get; set; } = 100.50;
    public string Timezone { get; set; } = "Asia/Bangkok";
    public int FetchIntervalMinutes { get; set; } = 15;
}