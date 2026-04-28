# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore — copy csproj files first for better layer caching
COPY BarcodeScanner.Core/BarcodeScanner.Core.csproj BarcodeScanner.Core/
COPY BarcodeScanner.Api/BarcodeScanner.Api.csproj   BarcodeScanner.Api/
RUN dotnet restore BarcodeScanner.Api/BarcodeScanner.Api.csproj

# Copy source and publish
COPY BarcodeScanner.Core/ BarcodeScanner.Core/
COPY BarcodeScanner.Api/  BarcodeScanner.Api/
RUN dotnet publish BarcodeScanner.Api/BarcodeScanner.Api.csproj \
    -c Release \
    -r linux-x64 \
    --no-self-contained \
    -o /app/publish

# ── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime
WORKDIR /app

# Install OpenCV native dependencies required by OpenCvSharp4
RUN apt-get update && apt-get install -y --no-install-recommends \
    libopencv-core4.5d \
    libopencv-imgproc4.5d \
    libopencv-imgcodecs4.5d \
    libopencv-photo4.5d \
    libopencv-video4.5d \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Listen on port 80 inside the container
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "BarcodeScanner.Api.dll"]
