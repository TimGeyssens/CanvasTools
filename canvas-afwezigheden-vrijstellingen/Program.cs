using CanvasAfwezighedenVrijstellingen.Services;

var builder = WebApplication.CreateBuilder(args);

// When running locally with ASPNETCORE_ENVIRONMENT=Production, static web assets from referenced packages
// (including Blazor's _framework/* scripts) are not loaded unless explicitly enabled.
builder.WebHost.UseStaticWebAssets();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<CanvasService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
