using RshtmxApp.Shared.Rendering;
//#if (fluentValidation)
using FluentValidation;
//#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorSlicesHtmx();
builder.Services.AddAntiforgery();
//#if (bootstrap5)
builder.Services.AddRazorSlicesHtmxBootstrap5();
//#endif
builder.Services.AddSingleton<IFeaturePageRenderer, AppFeaturePageRenderer>();
//#if (fluentValidation)
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
//#endif
builder.Services.Configure<RazorSlicesHtmxOptions>(options =>
{
	options.FeatureRegistration
		.AddFeature<HomeEndpoints>("home")
		.AddFeature<ItemsEndpoints>("items");
});

var app = builder.Build();
var features = app.Services.GetRequiredService<FeatureRegistry>();

app.UseStaticFiles();
app.Use(async (context, next) =>
{
	var antiforgery = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
	var tokens = antiforgery.GetAndStoreTokens(context);
	context.Items["RequestVerificationToken"] = tokens.RequestToken ?? string.Empty;

	await next();
});
app.UseHtmxErrorHandling();
app.UseAntiforgery();

features.MapEndpoints(app);
app.MapFeaturePages(features);

app.Run();
