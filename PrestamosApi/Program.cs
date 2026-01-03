using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrestamosApi.Data;
using PrestamosApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<PrestamosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Services
builder.Services.AddScoped<IPrestamoService, PrestamoService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IGananciasService, GananciasService>();
builder.Services.AddScoped<ITwilioService, TwilioService>();

// Background Service para notificaciones
builder.Services.AddHostedService<NotificationBackgroundService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Create database and apply migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PrestamosDbContext>();
    try
    {
        context.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating database: {ex.Message}");
    }
}

app.Run();
