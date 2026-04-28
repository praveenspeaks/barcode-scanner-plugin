using BarcodeScanner.Core;
using BarcodeScanner.Core.Config;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "BarcodeScanner API", Version = "v1" });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var scannerOptions = builder.Configuration
    .GetSection("ScannerOptions")
    .Get<ScannerOptions>() ?? new ScannerOptions();

builder.Services.AddSingleton(scannerOptions);
builder.Services.AddSingleton<BarcodeScannerEngine>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
