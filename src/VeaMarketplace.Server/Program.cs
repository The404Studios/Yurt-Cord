using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;

var builder = WebApplication.CreateBuilder(args);

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

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "YurtCordSuperSecretKey12345678901234567890";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
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

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<VoiceHub>("/hubs/voice");
app.MapHub<FriendHub>("/hubs/friends");
app.MapHub<ProfileHub>("/hubs/profile");
app.MapHub<RoomHub>("/hubs/rooms");

// Ensure data directory exists
Directory.CreateDirectory("Data");

// Ensure upload directories exist
Directory.CreateDirectory("uploads");
Directory.CreateDirectory("uploads/avatars");
Directory.CreateDirectory("uploads/banners");
Directory.CreateDirectory("uploads/attachments");
Directory.CreateDirectory("uploads/thumbnails");

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
