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
    -o /app/publish

# ── Stage 2: Download OpenCvSharp Linux native library ───────────────────────
# OpenCvSharp4.runtime.win only provides Windows DLL.
# For Linux we download the pre-built libOpenCvSharpExtern.so from GitHub releases.
FROM ubuntu:22.04 AS native
RUN apt-get update && apt-get install -y --no-install-recommends curl tar ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fSL \
    "https://github.com/shimat/opencvsharp/releases/download/4.9.0.20240103/OpenCvSharpExtern-ubuntu.22.04-x64.tar.gz" \
    -o /tmp/ocvextern.tar.gz \
    && mkdir -p /ocv \
    && tar -xzf /tmp/ocvextern.tar.gz -C /ocv \
    && rm /tmp/ocvextern.tar.gz

# ── Stage 3: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime
WORKDIR /app

# Install OpenCV system shared libraries required by libOpenCvSharpExtern.so
RUN apt-get update && apt-get install -y --no-install-recommends \
    libopencv-dev \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Copy Linux native OpenCvSharp wrapper
COPY --from=native /ocv/libOpenCvSharpExtern.so .

# Listen on port 80 inside the container
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "BarcodeScanner.Api.dll"]
