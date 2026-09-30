using CanvasAfspraaksloten.Services;

var builder = WebApplication.CreateBuilder(args);

// When running locally with ASPNETCORE_ENVIRONMENT=Production, static web assets from referenced packages
// (including Blazor's _framework/* scripts) are not loaded unless explicitly enabled.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<CanvasService>();

var app = builder.Build();

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
