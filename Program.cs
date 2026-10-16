
// // Original source: Microsoft Learn - "Add JWT Bearer authentication in ASP.NET Core"
// // URL: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt
// // Accessed: 3 September 2026
// //
// // Adapted for: RaceDay API - JWT authentication, token validation, and role-based
// // authorization. Modified to include custom claim configuration, issuer/audience
// // validation, and integration with Entity Framework Core for user data.
// //
using System.Text;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PROG_API.Data;
using PROG_API.Services;

var builder = WebApplication.CreateBuilder(args);
var databaseName = builder.Configuration["RaceDayDbName"] ?? "RaceDayDb";
var useInMemoryDatabase = builder.Configuration.GetValue<bool?>("UseInMemoryDatabase") ?? false;
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useInMemoryDatabase || string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseInMemoryDatabase(databaseName);
        return;
    }

    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
    });
});

builder.Services.AddScoped<AuthService>();

var configuredJwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(configuredJwtKey) && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Jwt:Key must be configured outside the Development environment.");
}

var jwtKey = configuredJwtKey ?? "RaceDayDevelopmentKey-ReplaceInProduction123!";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "RaceDayApi";
var audience = builder.Configuration["Jwt:Audience"] ?? "RaceDayClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "REST API for RaceDay events, categories, enrolments, payments, and results."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT as: Bearer {token}"
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedData.Seed(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
