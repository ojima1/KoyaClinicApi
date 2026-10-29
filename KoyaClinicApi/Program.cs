using KoyaClinicApi.Data;
using KoyaClinicApi.Middleware;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Use native OpenAPI
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        // 1. Ensure Components exists
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();

        // 2. Ensure SecuritySchemes exists
        if (document.Components.SecuritySchemes == null)
        {
            // The compiler wants IOpenApiSecurityScheme here
            document.Components.SecuritySchemes = new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        }

        // 3. Add our API key scheme safely
        document.Components.SecuritySchemes["ApiKey"] = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
            Name = "X-API-Key",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Enter your API Key here."
        };

        return Task.CompletedTask;
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.WebHost.UseWebRoot("wwwroot");


var app = builder.Build();


    app.MapOpenApi();          // Serves the OpenAPI JSON at /openapi/v1.json
    app.MapScalarApiReference(); // UI at /scalar/v1


app.UseStaticFiles();
//app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseMiddleware<AuthAndErrorMiddleware>();
app.MapControllers();
app.Run();