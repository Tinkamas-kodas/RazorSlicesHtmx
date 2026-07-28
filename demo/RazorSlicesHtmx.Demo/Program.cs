using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RazorSlicesHtmx.Demo.Data;
using RazorSlicesHtmx.Demo.Features.About;
using RazorSlicesHtmx.Demo.Features.HowTo.Endpoints;
using RazorSlicesHtmx.Demo.Features.Form.Endpoints;
using RazorSlicesHtmx.Demo.Features.Items.Endpoints;
using RazorSlicesHtmx.Demo.Shared.Rendering;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5();
builder.Services.AddSingleton<IFeaturePageRenderer, DemoFeaturePageRenderer>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("items-db"));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFeature<AboutEndpoints>("/about");
builder.Services.AddFeature<FormEndpoints>("/form");
builder.Services.AddFeature<ItemsEndpoints>("/items");
builder.Services.AddFeature<HowToEndpoints>("/howto");
var app = builder.Build();
var features = app.Services.GetRequiredService<FeatureRegistry>();

AppDbInitializer.Seed(app.Services);

app.UseStaticFiles();
app.UseHtmxErrorHandling();

features.MapEndpoints(app);

app.MapFeaturePages(features);

app.Run();
