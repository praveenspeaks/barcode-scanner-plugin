# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BarcodeScanner.Core/BarcodeScanner.Core.csproj BarcodeScanner.Core/
COPY BarcodeScanner.Api/BarcodeScanner.Api.csproj   BarcodeScanner.Api/
RUN dotnet restore BarcodeScanner.Api/BarcodeScanner.Api.csproj

COPY BarcodeScanner.Core/ BarcodeScanner.Core/
COPY BarcodeScanner.Api/  BarcodeScanner.Api/
RUN dotnet publish BarcodeScanner.Api/BarcodeScanner.Api.csproj \
    -c Release \
    -o /app/publish

# ── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime
WORKDIR /app

# libgdiplus is needed by System.Drawing (used internally by ImageSharp fallbacks)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "BarcodeScanner.Api.dll"]
