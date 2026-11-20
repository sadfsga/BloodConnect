using System.Text;
using System.Text.Json.Serialization;
using BloodConnect.Api.Data;
using BloodConnect.Api.Options;
using BloodConnect.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Settings ----------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<MatchingOptions>(builder.Configuration.GetSection("Matching"));

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

// Fail fast: never run with a missing or weak signing key.
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.StartsWith("SET_ME") || jwt.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or too weak. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<random string, 32+ chars>\"");
}

// ---------- Database ----------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ---------- Our services ----------
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<DonorMatchingService>();

// ---------- Web API ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// ---------- Authentication (JWT) ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// ---------- CORS (only while developing, so the web dashboard can call us) ----------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(o => o.AddPolicy("Dev", p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();   // interactive docs at /scalar
    app.UseCors("Dev");
}

app.UseHttpsRedirection();
app.UseDefaultFiles();   // serves wwwroot/index.html at /
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// In development: apply migrations automatically and fill an empty DB with demo data.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();
