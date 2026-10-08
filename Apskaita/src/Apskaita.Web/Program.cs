using Apskaita.Web.Layout.Rendering;
using Apskaita.Web.Features.Home.Endpoints;
using Apskaita.Web.Features.Admin.ClientsRegistry.Endpoints;
using Apskaita.Web.Features.Admin.IssuerRegistry.Endpoints;
using Apskaita.Web.Features.Admin.UnitRegistry.Endpoints;
using Apskaita.Web.Features.Admin.ProjectRegistry.Endpoints;
using Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Endpoints;
using Apskaita.Web.Features.Admin.SystemConfig.Endpoints;
using Apskaita.Web.Features.Admin.UserRegistry.Endpoints;
using FluentValidation;
using Apskaita.Entities;
using Apskaita.Web.Features.TimeTracking.Endpoints;
using Apskaita.Web.Features.TimeTracking.Validators;
using Apskaita.Web.Shared.Routing;
using AdminFeatureType=Apskaita.Web.Features.Admin.ClientsRegistry.Models.ClientListQuery;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorSlicesHtmx();

builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFeatureUri, HttpFeatureUri>();
builder.Services.AddRazorSlicesHtmxBootstrap5();
builder.Services.AddSingleton<IFeaturePageRenderer, AppFeaturePageRenderer>();
builder.Services.AddFeature<HomeEndpoints>("/home");
builder.Services.AddFeature<ClientsEndpoints>("/admin/clients");
builder.Services.AddFeature<IssuersEndpoints>("/admin/issuers");
builder.Services.AddFeature<UnitsEndpoints>("/admin/units");
builder.Services.AddFeature<ProjectsEndpoints>("/admin/projects");
builder.Services.AddFeature<InvoiceTemplatesEndpoints>("/admin/invoice-templates");
builder.Services.AddFeature<SystemSettingsEndpoints>("/admin/system-settings");
builder.Services.AddFeature<UsersEndpoints>("/admin/users");
builder.Services.AddFeature<TimeTrackingEndpoints>("/time-tracking");
builder.Services.AddValidatorsFromAssemblyContaining<AdminFeatureType>();
builder.Services.AddValidatorsFromAssemblyContaining<TimeTrackingModelValidator>();
builder.Services.AddDataAccess(builder.Configuration.GetConnectionString("Default"));

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
