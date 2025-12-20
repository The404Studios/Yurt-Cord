using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Server.Helpers;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for high concurrency
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 10000;
    options.Limits.MaxConcurrentUpgradedConnections = 10000;
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB max request body
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);

    // Enable HTTP/2 for better multiplexing
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

// Configure thread pool for high concurrency
ThreadPool.SetMinThreads(100, 100);

// Add memory cache for frequently accessed data
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 500 * 1024 * 1024; // 500MB cache limit
});

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/javascript",
        "text/css",
        "text/html",
        "text/plain"
    });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(userIp, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 100
            });
    });

    // Specific rate limit for API endpoints
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 500,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 50
            }));

    // Stricter rate limit for auth endpoints
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Add output caching for frequently accessed endpoints
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(policy => policy.Expire(TimeSpan.FromSeconds(30)));
    options.AddPolicy("Products", policy => policy.Expire(TimeSpan.FromMinutes(1)));
    options.AddPolicy("Discovery", policy => policy.Expire(TimeSpan.FromMinutes(5)));
    options.AddPolicy("Static", policy => policy.Expire(TimeSpan.FromHours(1)));
});

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings for better client compatibility
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure SignalR with high-bandwidth streaming support
builder.Services.AddSignalR(options =>
{
    // Upload limit: 30MB for high-quality video streaming
    options.MaximumReceiveMessageSize = 30 * 1024 * 1024; // 30MB upload

    // Streaming buffer sizes for optimal performance
    options.StreamBufferCapacity = 50; // Buffer 50 items for smooth streaming

    // Keep connections alive during streaming
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);

    // Enable detailed errors for debugging
    options.EnableDetailedErrors = true;

    // Maximum parallel hub invocations per client (for concurrent streams)
    options.MaximumParallelInvocationsPerClient = 10;
})
.AddJsonProtocol(options =>
{
    // Serialize enums as strings for client compatibility (matches controller JSON options)
    options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
});

// Database
builder.Services.AddSingleton<DatabaseService>();

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<FriendService>();
builder.Services.AddScoped<DirectMessageService>();
builder.Services.AddScoped<VoiceCallService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddScoped<DiscoveryService>();
builder.Services.AddScoped<ActivityService>();
builder.Services.AddSingleton<RoleConfigurationService>();

// JWT Authentication - get secret from environment or configuration
var jwtSecret = Environment.GetEnvironmentVariable("VEA_JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"];

if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
{
    // Generate a secure random key for this session
    Console.WriteLine("WARNING: VEA_JWT_SECRET not set or too short. Generating secure random key for this session.");
    Console.WriteLine("Set VEA_JWT_SECRET environment variable for production use.");
    jwtSecret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero // Strict token expiry validation
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogDebug("Authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

// CORS for WPF client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("SignalR", policy =>
    {
        policy.WithOrigins("http://162.248.94.23:5000", "https://162.248.94.23:5000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable response compression early in the pipeline
app.UseResponseCompression();

// Enable rate limiting
app.UseRateLimiter();

// Enable output caching
app.UseOutputCache();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<VoiceHub>("/hubs/voice");
app.MapHub<FriendHub>("/hubs/friends");
app.MapHub<ProfileHub>("/hubs/profile");
app.MapHub<RoomHub>("/hubs/rooms");
app.MapHub<ContentHub>("/hubs/content");
app.MapHub<NotificationHub>("/hubs/notifications");

// Initialize server directories with proper error handling
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
if (!ServerPaths.InitializeDirectories(startupLogger))
{
    startupLogger.LogWarning("Some directories could not be created - server may not function correctly");
}

// Load role configuration from JSON file on startup
var roleConfigService = app.Services.GetRequiredService<RoleConfigurationService>();
roleConfigService.LoadRolesFromConfig();
startupLogger.LogInformation("Role configuration loaded from {Path}", ServerPaths.RolesConfigPath);

Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║   ██╗   ██╗██╗   ██╗██████╗ ████████╗     ██████╗ ██████╗     ║
║   ╚██╗ ██╔╝██║   ██║██╔══██╗╚══██╔══╝    ██╔════╝██╔═══██╗    ║
║    ╚████╔╝ ██║   ██║██████╔╝   ██║       ██║     ██║   ██║    ║
║     ╚██╔╝  ██║   ██║██╔══██╗   ██║       ██║     ██║   ██║    ║
║      ██║   ╚██████╔╝██║  ██║   ██║       ╚██████╗╚██████╔╝    ║
║      ╚═╝    ╚═════╝ ╚═╝  ╚═╝   ╚═╝        ╚═════╝ ╚═════╝     ║
║                                                               ║
║             Marketplace & Chat Server                         ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
");

app.Run();
