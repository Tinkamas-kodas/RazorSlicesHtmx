using FluentValidation;
using Microsoft.EntityFrameworkCore;
using razr_slices_htmx2.Data;
using RazorSlicesHtmx.AspNetCore.Extensions;
using RazorSlicesHtmx.Bootstrap5.Extensions;
using razr_slices_htmx2.Shared.Rendering;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5();
builder.Services.AddSingleton<IFeaturePageRenderer, DemoFeaturePageRenderer>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("items-db"));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddSingleton(sp => FeatureRegistry.Discover(typeof(Program).Assembly, sp));

var app = builder.Build();
var features = app.Services.GetRequiredService<FeatureRegistry>();

AppDbInitializer.Seed(app.Services);

app.UseStaticFiles();

features.MapEndpoints(app);

app.MapFeaturePages(features);

app.Run();
