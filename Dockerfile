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

# ── Stage 2: Extract Linux native OpenCvSharp library from NuGet ─────────────
# .nupkg files are ZIP archives — we download and unzip to get libOpenCvSharpExtern.so
FROM ubuntu:22.04 AS native
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl unzip ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fSL \
    "https://www.nuget.org/api/v2/package/OpenCvSharp4.runtime.linux-x64/4.9.0.20240103" \
    -o /tmp/ocvruntime.zip \
    && unzip -q /tmp/ocvruntime.zip -d /tmp/ocvruntime \
    && mkdir -p /ocv \
    && cp /tmp/ocvruntime/runtimes/linux-x64/native/libOpenCvSharpExtern.so /ocv/ \
    && rm -rf /tmp/ocvruntime.zip /tmp/ocvruntime

# ── Stage 3: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime
WORKDIR /app

# Install OpenCV system shared libraries required by libOpenCvSharpExtern.so
RUN apt-get update && apt-get install -y --no-install-recommends \
    libopencv-dev \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build   /app/publish                      .
COPY --from=native  /ocv/libOpenCvSharpExtern.so      .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "BarcodeScanner.Api.dll"]
