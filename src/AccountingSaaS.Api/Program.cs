using System.Reflection;
using AccountingSaaS.Api.Filters;
using AccountingSaaS.Api.Middleware;
// using AccountingSaaS.Api.Swagger;
using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Application.Validation;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure;
using AccountingSaaS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console());

if (builder.Environment.IsProduction())
{
    var jwtSecret = builder.Configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(jwtSecret) ||
        jwtSecret.StartsWith("CHANGE_THIS", StringComparison.OrdinalIgnoreCase) ||
        jwtSecret.Length < 32)
    {
        throw new InvalidOperationException("A strong Jwt:Secret must be configured for production.");
    }

    var seedPassword = builder.Configuration["SeedAdmin:Password"];
    if (string.IsNullOrWhiteSpace(seedPassword) || seedPassword == "ChangeMe123!ChangeMe123!")
    {
        throw new InvalidOperationException("SeedAdmin:Password must be changed for production.");
    }

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Length == 0)
    {
        throw new InvalidOperationException("Cors:AllowedOrigins must be configured for production.");
    }
}

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

builder.Services.AddScoped<ValidationFilter>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredCors", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (builder.Environment.IsDevelopment())
        {
            policy
                .WithOrigins(origins.Length == 0 ? ["http://localhost:4200"] : origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AccountingSaaS API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });

 
    options.CustomSchemaIds(type =>
        type.FullName?
            .Replace("+", ".")
            .Replace("`1", "")
            .Replace("`2", "")
        ?? type.Name);

    /*
     * علّقنا الفلاتر مؤقتًا لأن الخطأ:
     * Failed to generate Operation for action
     * ممكن يكون سببه OperationFilter أو SchemaFilter.
     *
     * بعد ما Swagger يشتغل، رجّعهم واحد واحد:
     * 1) رجّع SchemaFilter وجرب.
     * 2) رجّع OperationFilter وجرب.
     */

    // options.SchemaFilter<SwaggerExampleSchemaFilter>();
    // options.OperationFilter<AccountingSwaggerOperationFilter>();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.EnableFilter();
    });
}

app.UseHttpsRedirection();

app.UseCors("ConfiguredCors");

app.UseAuthentication();

app.UseMiddleware<TenantResolverMiddleware>();

app.UseAuthorization();

app.MapControllers();

if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();

    await dbContext.Database.MigrateAsync();

    await SeedData.SeedAsync(
        dbContext,
        userManager,
        roleManager,
        app.Configuration,
        app.Environment);

    await DevelopmentSeedData.SeedAsync(
        dbContext,
        userManager,
        currentTenant,
        app.Configuration,
        app.Environment);
}

app.Run();
