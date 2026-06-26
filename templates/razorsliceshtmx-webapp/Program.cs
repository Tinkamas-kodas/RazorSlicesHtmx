using RshtmxApp.Shared.Rendering;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorSlicesHtmx();
//#if (bootstrap5)
builder.Services.AddRazorSlicesHtmxBootstrap5();
//#endif
builder.Services.AddSingleton<IFeaturePageRenderer, AppFeaturePageRenderer>();
//#if (fluentValidation)
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
//#endif
builder.Services.AddSingleton(sp => FeatureRegistry.Discover(typeof(Program).Assembly, sp));

var app = builder.Build();
var features = app.Services.GetRequiredService<FeatureRegistry>();

app.UseStaticFiles();
app.UseHtmxErrorHandling();

features.MapEndpoints(app);
app.MapFeaturePages(features);

app.Run();
