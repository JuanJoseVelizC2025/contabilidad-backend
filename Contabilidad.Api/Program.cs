using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Contabilidad.Api.Data;
using Contabilidad.Api.Models;
using Contabilidad.Api.Services; // NUEVO
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ContabilidadDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Conexion")));

builder.Services.AddHttpContextAccessor(); // NUEVO
builder.Services.AddScoped<Auditoria>(); // NUEVO
builder.Services.AddScoped<PartidaServicio>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<CorreoServicio>();

builder.Services.AddControllers(o => o.Filters.Add<AuditoriaFilter>()) // NUEVO
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddCors(opt => opt.AddPolicy("front", p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

var app = builder.Build();

// Usuario inicial (solo si la tabla Usuario esta vacia)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContabilidadDbContext>();
    if (!db.Set<Usuario>().Any())
    {
        db.Set<Usuario>().Add(new Usuario
        {
            NombreUsuario = "admin",
            ClaveHash = BCrypt.Net.BCrypt.HashPassword("Admin123*"),
            NombreCompleto = "Administrador del sistema",
            Correo = "admin@empresa.com",
            IdRol = 1,
            Activo = true
        });
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("front");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();