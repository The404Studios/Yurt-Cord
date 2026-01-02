using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

/// <summary>
/// Background service that provides an admin console interface for server management.
/// Runs on a dedicated thread to handle console input without blocking the web server.
/// </summary>
public class ServerConsoleService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServerConsoleService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public ServerConsoleService(
        IServiceProvider serviceProvider,
        ILogger<ServerConsoleService> logger,
        IHostApplicationLifetime appLifetime)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a moment for the server to fully start
        await Task.Delay(2000, stoppingToken);

        PrintHelp();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Console.Write("\n[ADMIN] > ");
                var input = await ReadLineAsync(stoppingToken);

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                var command = parts[0].ToLowerInvariant();
                var args = parts.Skip(1).ToArray();

                await ProcessCommand(command, args, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing console command");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private async Task<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        // Read console input on a background thread to allow cancellation
        return await Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable || Console.In.Peek() != -1)
                {
                    return Console.ReadLine() ?? string.Empty;
                }
                Thread.Sleep(100);
            }
            return string.Empty;
        }, cancellationToken);
    }

    private async Task ProcessCommand(string command, string[] args, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        switch (command)
        {
            case "help":
            case "?":
                PrintHelp();
                break;

            case "status":
                await PrintStatus(db);
                break;

            case "users":
                await PrintUsers(db, args);
                break;

            case "online":
                PrintOnlineUsers();
                break;

            case "broadcast":
                await BroadcastMessage(args);
                break;

            case "kick":
                await KickUser(args);
                break;

            case "ban":
                await BanUser(db, authService, args);
                break;

            case "unban":
                await UnbanUser(db, args);
                break;

            case "promote":
                await PromoteUser(db, args);
                break;

            case "demote":
                await DemoteUser(db, args);
                break;

            case "stats":
                await PrintDetailedStats(db);
                break;

            case "clear":
                Console.Clear();
                PrintBanner();
                break;

            case "gc":
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Console.WriteLine("Garbage collection completed.");
                Console.WriteLine($"Memory: {GC.GetTotalMemory(true) / 1024 / 1024} MB");
                break;

            case "shutdown":
            case "stop":
            case "exit":
                Console.WriteLine("Initiating graceful shutdown...");
                _appLifetime.StopApplication();
                break;

            default:
                Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                break;
        }
    }

    private void PrintBanner()
    {
        Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║                    YURT CORD ADMIN CONSOLE                    ║
╚═══════════════════════════════════════════════════════════════╝");
    }

    private void PrintHelp()
    {
        Console.WriteLine(@"
═══════════════════════════════════════════════════════════════
                    ADMIN CONSOLE COMMANDS
═══════════════════════════════════════════════════════════════
  status              - Show server status and statistics
  stats               - Show detailed database statistics
  users [count]       - List users (default: 10)
  online              - Show online users across all hubs
  broadcast <msg>     - Send system message to all users
  kick <username>     - Disconnect a user
  ban <username> [reason] [hours] - Ban a user
  unban <username>    - Unban a user
  promote <username> <role> - Promote user (Admin/Mod/VIP/Verified)
  demote <username>   - Demote user to Member
  gc                  - Force garbage collection
  clear               - Clear console
  help                - Show this help
  shutdown/exit       - Graceful server shutdown
═══════════════════════════════════════════════════════════════");
    }

    private Task PrintStatus(DatabaseService db)
    {
        var userCount = db.Users.Count();
        var productCount = db.Products.Count();
        var messageCount = db.Messages.Count();
        var roomCount = db.Rooms.Count();

        var process = System.Diagnostics.Process.GetCurrentProcess();
        var memoryMb = process.WorkingSet64 / 1024 / 1024;
        var cpuTime = process.TotalProcessorTime;
        var uptime = DateTime.Now - process.StartTime;

        Console.WriteLine($@"
═══════════════════════════════════════════════════════════════
                        SERVER STATUS
═══════════════════════════════════════════════════════════════
  Uptime:           {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s
  Memory Usage:     {memoryMb} MB
  CPU Time:         {cpuTime.TotalSeconds:F1}s
  Thread Count:     {process.Threads.Count}
───────────────────────────────────────────────────────────────
  Database Stats:
    Users:          {userCount}
    Products:       {productCount}
    Messages:       {messageCount}
    Rooms:          {roomCount}
───────────────────────────────────────────────────────────────
  Online Users:     {GetTotalOnlineCount()}
═══════════════════════════════════════════════════════════════");

        return Task.CompletedTask;
    }

    private Task PrintUsers(DatabaseService db, string[] args)
    {
        var count = 10;
        if (args.Length > 0 && int.TryParse(args[0], out var parsed))
            count = Math.Min(parsed, 100);

        var users = db.Users.FindAll()
            .OrderByDescending(u => u.CreatedAt)
            .Take(count)
            .ToList();

        Console.WriteLine($"\n═══ Recent Users (showing {users.Count}) ═══");
        Console.WriteLine($"{"Username",-20} {"Role",-12} {"Created",-20} {"Online"}");
        Console.WriteLine(new string('─', 65));

        foreach (var user in users)
        {
            var online = user.IsOnline ? "Yes" : "No";
            Console.WriteLine($"{user.Username,-20} {user.Role,-12} {user.CreatedAt:yyyy-MM-dd HH:mm} {online}");
        }

        return Task.CompletedTask;
    }

    private void PrintOnlineUsers()
    {
        var chatOnline = ChatHub.GetOnlineUserCount();
        var notifOnline = NotificationHub.GetConnectedUserCount();

        Console.WriteLine($@"
═══════════════════════════════════════════════════════════════
                       ONLINE USERS
═══════════════════════════════════════════════════════════════
  ChatHub:          {chatOnline} users
  NotificationHub:  {notifOnline} users
  Total Unique:     ~{Math.Max(chatOnline, notifOnline)} users
═══════════════════════════════════════════════════════════════");
    }

    private int GetTotalOnlineCount()
    {
        return Math.Max(ChatHub.GetOnlineUserCount(), NotificationHub.GetConnectedUserCount());
    }

    private async Task BroadcastMessage(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: broadcast <message>");
            return;
        }

        var message = string.Join(" ", args);

        using var scope = _serviceProvider.CreateScope();
        var notificationHub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

        await NotificationHub.BroadcastSystemNotification(
            notificationHub,
            "System Announcement",
            message);

        Console.WriteLine($"Broadcast sent: {message}");
        _logger.LogInformation("Admin broadcast: {Message}", message);
    }

    private async Task KickUser(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: kick <username>");
            return;
        }

        var username = args[0];

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var chatHub = scope.ServiceProvider.GetRequiredService<IHubContext<ChatHub>>();

        var user = db.Users.FindOne(u => u.Username.ToLower() == username.ToLower());
        if (user == null)
        {
            Console.WriteLine($"User '{username}' not found.");
            return;
        }

        // Send disconnect message via hub
        await chatHub.Clients.Group($"user_{user.Id}").SendAsync("ForceDisconnect", "You have been disconnected by an administrator.");

        Console.WriteLine($"Kick signal sent to {username}.");
        _logger.LogWarning("Admin kicked user: {Username}", username);
    }

    private async Task BanUser(DatabaseService db, AuthService authService, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: ban <username> [reason] [hours]");
            return;
        }

        var username = args[0];
        var reason = args.Length > 1 ? args[1] : "Banned by administrator";
        var hours = args.Length > 2 && int.TryParse(args[2], out var h) ? h : 0; // 0 = permanent

        var user = db.Users.FindOne(u => u.Username.ToLower() == username.ToLower());
        if (user == null)
        {
            Console.WriteLine($"User '{username}' not found.");
            return;
        }

        var ban = new UserBan
        {
            UserId = user.Id,
            Reason = reason,
            BannedBy = "System",
            BannedAt = DateTime.UtcNow,
            ExpiresAt = hours > 0 ? DateTime.UtcNow.AddHours(hours) : null,
            IsActive = true
        };

        db.UserBans.Insert(ban);

        // Kick the user
        using var scope = _serviceProvider.CreateScope();
        var chatHub = scope.ServiceProvider.GetRequiredService<IHubContext<ChatHub>>();
        await chatHub.Clients.Group($"user_{user.Id}").SendAsync("ForceDisconnect", $"You have been banned: {reason}");

        var duration = hours > 0 ? $"{hours} hours" : "permanently";
        Console.WriteLine($"Banned {username} {duration}. Reason: {reason}");
        _logger.LogWarning("Admin banned user: {Username} for {Duration}. Reason: {Reason}", username, duration, reason);
    }

    private Task UnbanUser(DatabaseService db, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: unban <username>");
            return Task.CompletedTask;
        }

        var username = args[0];

        var user = db.Users.FindOne(u => u.Username.ToLower() == username.ToLower());
        if (user == null)
        {
            Console.WriteLine($"User '{username}' not found.");
            return Task.CompletedTask;
        }

        var activeBans = db.UserBans.Find(b => b.UserId == user.Id && b.IsActive).ToList();
        foreach (var ban in activeBans)
        {
            ban.IsActive = false;
            db.UserBans.Update(ban);
        }

        Console.WriteLine($"Unbanned {username}. {activeBans.Count} ban(s) deactivated.");
        _logger.LogInformation("Admin unbanned user: {Username}", username);

        return Task.CompletedTask;
    }

    private Task PromoteUser(DatabaseService db, string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: promote <username> <role>");
            Console.WriteLine("Roles: Admin, Moderator, VIP, Verified, Member");
            return Task.CompletedTask;
        }

        var username = args[0];
        var roleStr = args[1];

        if (!Enum.TryParse<UserRole>(roleStr, true, out var role))
        {
            Console.WriteLine($"Invalid role: {roleStr}");
            Console.WriteLine("Valid roles: Admin, Moderator, VIP, Verified, Member");
            return Task.CompletedTask;
        }

        var user = db.Users.FindOne(u => u.Username.ToLower() == username.ToLower());
        if (user == null)
        {
            Console.WriteLine($"User '{username}' not found.");
            return Task.CompletedTask;
        }

        var oldRole = user.Role;
        user.Role = role;
        db.Users.Update(user);

        Console.WriteLine($"Promoted {username} from {oldRole} to {role}.");
        _logger.LogInformation("Admin promoted user: {Username} from {OldRole} to {NewRole}", username, oldRole, role);

        return Task.CompletedTask;
    }

    private Task DemoteUser(DatabaseService db, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: demote <username>");
            return Task.CompletedTask;
        }

        var username = args[0];

        var user = db.Users.FindOne(u => u.Username.ToLower() == username.ToLower());
        if (user == null)
        {
            Console.WriteLine($"User '{username}' not found.");
            return Task.CompletedTask;
        }

        var oldRole = user.Role;
        user.Role = UserRole.Member;
        db.Users.Update(user);

        Console.WriteLine($"Demoted {username} from {oldRole} to Member.");
        _logger.LogInformation("Admin demoted user: {Username} from {OldRole} to Member", username, oldRole);

        return Task.CompletedTask;
    }

    private Task PrintDetailedStats(DatabaseService db)
    {
        Console.WriteLine($@"
═══════════════════════════════════════════════════════════════
                    DETAILED STATISTICS
═══════════════════════════════════════════════════════════════
  Users:
    Total:          {db.Users.Count()}
    Online:         {db.Users.Count(u => u.IsOnline)}
    Admins:         {db.Users.Count(u => u.Role == UserRole.Admin)}
    Moderators:     {db.Users.Count(u => u.Role == UserRole.Moderator)}
    VIPs:           {db.Users.Count(u => u.Role == UserRole.VIP)}
───────────────────────────────────────────────────────────────
  Content:
    Products:       {db.Products.Count()}
    Active:         {db.Products.Count(p => p.Status == ProductStatus.Active)}
    Orders:         {db.Orders.Count()}
    Reviews:        {db.ProductReviews.Count()}
───────────────────────────────────────────────────────────────
  Social:
    Messages:       {db.Messages.Count()}
    DMs:            {db.DirectMessages.Count()}
    Friendships:    {db.Friendships.Count()}
    Rooms:          {db.Rooms.Count()}
───────────────────────────────────────────────────────────────
  Moderation:
    Active Bans:    {db.UserBans.Count(b => b.IsActive)}
    Active Mutes:   {db.UserMutes.Count(m => m.IsActive)}
    Warnings:       {db.UserWarnings.Count()}
    Reports:        {db.MessageReports.Count(r => r.Status == ReportStatus.Pending)}
═══════════════════════════════════════════════════════════════");

        return Task.CompletedTask;
    }
}
