using LiteDB;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Data;

public class DatabaseService : IDisposable
{
    private readonly LiteDatabase _database;

    public ILiteCollection<User> Users => _database.GetCollection<User>("users");
    public ILiteCollection<Product> Products => _database.GetCollection<Product>("products");
    public ILiteCollection<ChatMessage> Messages => _database.GetCollection<ChatMessage>("messages");
    public ILiteCollection<ChatChannel> Channels => _database.GetCollection<ChatChannel>("channels");
    public ILiteCollection<Transaction> Transactions => _database.GetCollection<Transaction>("transactions");

    public DatabaseService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LiteDb") ?? "Data/marketplace.db";
        _database = new LiteDatabase(connectionString);

        // Ensure indexes
        Users.EnsureIndex(x => x.Username, true);
        Users.EnsureIndex(x => x.Email, true);
        Products.EnsureIndex(x => x.SellerId);
        Products.EnsureIndex(x => x.Status);
        Messages.EnsureIndex(x => x.Channel);
        Messages.EnsureIndex(x => x.Timestamp);

        // Seed default channels
        SeedDefaultData();
    }

    private void SeedDefaultData()
    {
        if (Channels.Count() == 0)
        {
            var defaultChannels = new List<ChatChannel>
            {
                new() { Name = "general", Description = "General chat for everyone", Icon = "💬", IsDefault = true },
                new() { Name = "marketplace", Description = "Discuss marketplace items", Icon = "🛒" },
                new() { Name = "support", Description = "Get help from the community", Icon = "❓" },
                new() { Name = "vip-lounge", Description = "Exclusive VIP chat", Icon = "⭐", MinimumRole = Shared.Enums.UserRole.VIP },
                new() { Name = "staff", Description = "Staff only channel", Icon = "🛡️", MinimumRole = Shared.Enums.UserRole.Moderator }
            };

            foreach (var channel in defaultChannels)
            {
                Channels.Insert(channel);
            }
        }
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
