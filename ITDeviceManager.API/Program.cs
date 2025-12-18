using System.Text;
using ITDeviceManager.API.Data;
using ITDeviceManager.API.Services;
using ITDeviceManager.API.Utils;
using ITDeviceManager.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 配置Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

// 使用Serilog替换默认日志提供程序
builder.Host.UseSerilog();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
}

builder.Services.AddDbContext<DeviceContext>(options =>
{
    options.UseSqlite(connectionString, sqliteOptions =>
    {
        // 配置命令超时（秒）
        sqliteOptions.CommandTimeout(60);
    });

    // 启用敏感数据日志（生产环境应关闭）
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});

// Register services
builder.Services.AddScoped<INetworkDiscoveryService, NetworkDiscoveryService>();
builder.Services.AddScoped<IWakeOnLanService, WakeOnLanService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMagicPacketListenerService, ITDeviceManager.API.Services.MagicPacketListenerService>();

// 注册后台服务
builder.Services.AddHostedService<ITDeviceManager.API.Services.DeviceDiscoveryBackgroundService>();
builder.Services.AddHostedService<ITDeviceManager.API.Services.MagicPacketBackgroundService>();

// Configure JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrEmpty(jwtSecretKey))
{
    throw new InvalidOperationException("JWT SecretKey is not configured in appsettings.json");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ClockSkew = TimeSpan.Zero // Remove default 5 minute clock skew
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Authentication failed: {Exception}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var username = context.Principal?.Identity?.Name;
            logger.LogInformation("Token validated for user: {Username}", username);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.MaxDepth = 32;
        // 添加自定义 DateTime 转换器，将 UTC 时间转换为中国标准时间 (UTC+8)
        options.JsonSerializerOptions.Converters.Add(new ChinaDateTimeConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IT Device Manager API",
        Version = "v1",
        Description = "API for managing IT devices, network discovery, and Wake-on-LAN operations"
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});

// Add CORS - restrictive policy for security
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "https://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("RestrictedCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });

    // Development-only permissive policy
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (true || app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "IT Device Manager API v1");
        c.RoutePrefix = "swagger"; // Move Swagger UI to /swagger path
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // 启用静态文件服务

// 设置默认文件
app.UseDefaultFiles();

// Use appropriate CORS policy based on environment
app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "RestrictedCors");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Apply database migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DeviceContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        context.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");

        // 启用 SQLite WAL 模式以优化并发性能
        // WAL (Write-Ahead Logging) 允许读操作和写操作并发执行
        var connection = context.Database.GetDbConnection();
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            // 启用 WAL 模式
            command.CommandText = "PRAGMA journal_mode=WAL;";
            var walResult = command.ExecuteScalar();
            logger.LogInformation("SQLite WAL mode enabled: {Result}", walResult);

            // 优化缓存大小（2000 页 = ~8MB，适合小型应用）
            command.CommandText = "PRAGMA cache_size=-8000;";
            command.ExecuteNonQuery();

            // 设置同步模式为 NORMAL（平衡性能和安全性）
            command.CommandText = "PRAGMA synchronous=NORMAL;";
            command.ExecuteNonQuery();

            // 设置忙碌超时（5秒）- 防止并发写入时立即失败
            command.CommandText = "PRAGMA busy_timeout=5000;";
            command.ExecuteNonQuery();

            logger.LogInformation("SQLite performance optimizations applied.");
        }
        connection.Close();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database.");

        // In development, optionally fall back to EnsureCreated for quick setup
        if (builder.Environment.IsDevelopment())
        {
            logger.LogWarning("Falling back to EnsureCreated() in development mode.");
            context.Database.EnsureCreated();
        }
        else
        {
            throw; // Fail fast in production
        }
    }
}

app.Run();
