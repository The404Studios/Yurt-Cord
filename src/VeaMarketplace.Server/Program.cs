using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure SignalR with increased message size for audio data
builder.Services.AddSignalR(options =>
{
    // Increase max message size to 1MB for audio/video streaming
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    options.EnableDetailedErrors = true;
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

// Ensure data directory exists
Directory.CreateDirectory("Data");

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
