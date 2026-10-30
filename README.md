# WebApplication1

ASP.NET Core Web API (.NET 10) with controllers and OpenAPI support.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git
- (Optional) Visual Studio 2026 / VS Code with C# Dev Kit
- (Optional) Docker, for container deployment

Verify:

```bash
dotnet --version   # should be 10.x
```

## Project Structure

```
WebApplication1/
├── WebApplication1.slnx
└── WebApplication1/
    ├── Controllers/
    ├── Properties/launchSettings.json
    ├── Program.cs
    ├── WeatherForecast.cs
    ├── appsettings.json
    └── WebApplication1.csproj
```

## 1. Setup

```bash
git clone <your-repo-url>
cd WebApplication1

dotnet restore
```

Trust the HTTPS dev certificate (first time only):

```bash
dotnet dev-certs https --trust
```

## 2. Build & Run

All commands are run from the solution folder (or add `--project WebApplication1`).

| Purpose | Command |
|---|---|
| Build | `dotnet build` |
| Run (HTTP) | `dotnet run --project WebApplication1 --launch-profile http` |
| Run (HTTPS) | `dotnet run --project WebApplication1 --launch-profile https` |
| Hot reload | `dotnet watch --project WebApplication1` |
| Release build | `dotnet build -c Release` |

Default URLs:

- HTTP: http://localhost:5102
- HTTPS: https://localhost:7084
- OpenAPI document (Development only): `/openapi/v1.json`

Test the API using [WebApplication1.http](WebApplication1/WebApplication1.http) in Visual Studio / VS Code, or:

```bash
curl http://localhost:5102/weatherforecast
```

### Run from Visual Studio

Open `WebApplication1.slnx`, choose the `http` or `https` profile, then press `F5`.

## 3. Configuration

- `appsettings.json` – base settings
- `appsettings.Development.json` – development overrides
- Environment: `ASPNETCORE_ENVIRONMENT=Development|Staging|Production`
- Change the port: `ASPNETCORE_URLS=http://0.0.0.0:8080`

## 4. Deployment Options

First, publish the app:

```bash
dotnet publish WebApplication1/WebApplication1.csproj -c Release -o ./publish
```

### Option A: Run directly on a server (Kestrel)

```bash
cd publish
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://0.0.0.0:8080
dotnet WebApplication1.dll
```

Requires the ASP.NET Core 10 runtime on the server. Put Nginx/Apache/Caddy in front as a reverse proxy for production, and run it as a `systemd` service (Linux) or Windows Service.

### Option B: IIS (Windows)

1. Install the **.NET 10 Hosting Bundle** on the server.
2. Create an IIS site pointing to the `publish` folder (Application Pool: *No Managed Code*).
3. Make sure the site has permission to read the folder, then browse the site.

### Option C: Docker

Create `Dockerfile` in the solution root:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish WebApplication1/WebApplication1.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "WebApplication1.dll"]
```

Build and run:

```bash
docker build -t webapplication1 .
docker run -d -p 8080:8080 --name webapp webapplication1
```

Then open http://localhost:8080/weatherforecast.

### Option D: Azure App Service

```bash
az login
az group create -n my-rg -l southeastasia
az appservice plan create -n my-plan -g my-rg --sku B1 --is-linux
az webapp create -n <unique-app-name> -g my-rg -p my-plan --runtime "DOTNETCORE:10.0"
cd publish && zip -r ../publish.zip . && cd ..
az webapp deploy -n <unique-app-name> -g my-rg --src-path publish.zip --type zip
```

Alternatively, in Visual Studio: **Right-click project → Publish → Azure**.

### Option E: Self-contained / single file (no runtime needed)

```bash
dotnet publish WebApplication1/WebApplication1.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish-win
# other RIDs: linux-x64, linux-arm64, osx-arm64
```

## Notes

- `app.UseHttpsRedirection()` is enabled; behind a reverse proxy, terminate TLS at the proxy or configure forwarded headers.
- OpenAPI endpoint is only mapped in the `Development` environment.

## Troubleshooting

| Problem | Fix |
|---|---|
| `The specified framework 'Microsoft.NETCore.App', version '10.0.0' was not found` | Install the .NET 10 SDK/runtime |
| HTTPS certificate warning | `dotnet dev-certs https --trust` |
| Port already in use | Change `applicationUrl` in `launchSettings.json` or set `ASPNETCORE_URLS` |
