using Enrollify.API.Middleware;
using Enrollify.Application;
using Enrollify.Infrastructure;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Fail fast on unsafe production config. Any of these can be overridden per environment
// with environment variables (e.g. Jwt__Key) without touching appsettings.json.
const string CommittedDevJwtKey = "EnrollifySecretKey2024!@#$%^&*()_+SuperSecure256Bit";
if (builder.Environment.IsProduction())
{
    var jwtKey = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey == CommittedDevJwtKey)
        throw new InvalidOperationException(
            "Jwt:Key is missing or still set to the committed development value. " +
            "Configure a strong, secret signing key for Production (e.g. via the Jwt__Key environment variable).");
}

// Add services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Enrollify API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpContextAccessor();

// CORS origins come from the "AllowedOrigins" config array (overridable via
// AllowedOrigins__0 etc.); the Angular dev server is the default when none are configured.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
if (allowedOrigins == null || allowedOrigins.Length == 0)
    allowedOrigins = new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var tenantProvider = scope.ServiceProvider.GetRequiredService<Enrollify.Domain.Interfaces.ITenantProvider>();
    tenantProvider.SetTenantId(Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"));
    await context.Database.MigrateAsync();
    await ApplicationDbContextSeed.SeedAsync(context);
}

app.Run();
