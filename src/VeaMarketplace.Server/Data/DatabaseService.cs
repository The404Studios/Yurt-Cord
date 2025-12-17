using LiteDB;
using VeaMarketplace.Shared.Models;
using VeaMarketplace.Server.Services;

namespace VeaMarketplace.Server.Data;

public class DatabaseService : IDisposable
{
    private readonly LiteDatabase _database;

    // Core collections
    public ILiteCollection<User> Users => _database.GetCollection<User>("users");
    public ILiteCollection<Product> Products => _database.GetCollection<Product>("products");
    public ILiteCollection<ChatMessage> Messages => _database.GetCollection<ChatMessage>("messages");
    public ILiteCollection<ChatChannel> Channels => _database.GetCollection<ChatChannel>("channels");
    public ILiteCollection<Transaction> Transactions => _database.GetCollection<Transaction>("transactions");
    public ILiteCollection<Friendship> Friendships => _database.GetCollection<Friendship>("friendships");
    public ILiteCollection<DirectMessage> DirectMessages => _database.GetCollection<DirectMessage>("direct_messages");
    public ILiteCollection<VoiceCall> VoiceCalls => _database.GetCollection<VoiceCall>("voice_calls");
    public ILiteCollection<CustomRole> CustomRoles => _database.GetCollection<CustomRole>("custom_roles");
    public ILiteCollection<Room> Rooms => _database.GetCollection<Room>("rooms");
    public ILiteCollection<StoredFile> StoredFiles => _database.GetCollection<StoredFile>("stored_files");

    // Reviews & Ratings
    public ILiteCollection<ProductReview> ProductReviews => _database.GetCollection<ProductReview>("product_reviews");

    // Orders & Cart
    public ILiteCollection<ProductOrder> Orders => _database.GetCollection<ProductOrder>("orders");
    public ILiteCollection<Cart> Carts => _database.GetCollection<Cart>("carts");
    public ILiteCollection<Coupon> Coupons => _database.GetCollection<Coupon>("coupons");

    // Wishlist
    public ILiteCollection<WishlistItem> WishlistItems => _database.GetCollection<WishlistItem>("wishlist_items");

    // Notifications
    public ILiteCollection<Notification> Notifications => _database.GetCollection<Notification>("notifications");

    // Moderation
    public ILiteCollection<UserBan> UserBans => _database.GetCollection<UserBan>("user_bans");
    public ILiteCollection<UserMute> UserMutes => _database.GetCollection<UserMute>("user_mutes");
    public ILiteCollection<UserWarning> UserWarnings => _database.GetCollection<UserWarning>("user_warnings");
    public ILiteCollection<MessageReport> MessageReports => _database.GetCollection<MessageReport>("message_reports");
    public ILiteCollection<ModerationLog> ModerationLogs => _database.GetCollection<ModerationLog>("moderation_logs");
    public ILiteCollection<AutoModRule> AutoModRules => _database.GetCollection<AutoModRule>("automod_rules");

    // Activity
    public ILiteCollection<UserActivity> UserActivities => _database.GetCollection<UserActivity>("user_activities");

    // Product Bundles
    public ILiteCollection<ProductBundle> ProductBundles => _database.GetCollection<ProductBundle>("product_bundles");

    public DatabaseService(IConfiguration configuration)
    {
        var configuredPath = configuration.GetConnectionString("LiteDb") ?? "Data/marketplace.db";

        // Build connection string with shared mode for concurrent access
        // This prevents "use EnterTransaction() before EnterLock(name)" errors
        var connectionString = $"Filename={configuredPath};Connection=Shared";
        _database = new LiteDatabase(connectionString);

        // Ensure indexes
        Users.EnsureIndex(x => x.Username, true);
        Users.EnsureIndex(x => x.Email, true);
        Products.EnsureIndex(x => x.SellerId);
        Products.EnsureIndex(x => x.Status);
        Messages.EnsureIndex(x => x.Channel);
        Messages.EnsureIndex(x => x.Timestamp);
        Friendships.EnsureIndex(x => x.RequesterId);
        Friendships.EnsureIndex(x => x.AddresseeId);
        DirectMessages.EnsureIndex(x => x.SenderId);
        DirectMessages.EnsureIndex(x => x.RecipientId);
        DirectMessages.EnsureIndex(x => x.Timestamp);
        VoiceCalls.EnsureIndex(x => x.CallerId);
        VoiceCalls.EnsureIndex(x => x.RecipientId);
        CustomRoles.EnsureIndex(x => x.Name);
        CustomRoles.EnsureIndex(x => x.Position);
        Rooms.EnsureIndex(x => x.OwnerId);
        Rooms.EnsureIndex(x => x.IsPublic);
        Rooms.EnsureIndex(x => x.Name);

        // Reviews indexes
        ProductReviews.EnsureIndex(x => x.ProductId);
        ProductReviews.EnsureIndex(x => x.UserId);
        ProductReviews.EnsureIndex(x => x.CreatedAt);

        // Orders indexes
        Orders.EnsureIndex(x => x.BuyerId);
        Orders.EnsureIndex(x => x.SellerId);
        Orders.EnsureIndex(x => x.Status);
        Orders.EnsureIndex(x => x.CreatedAt);

        // Cart indexes
        Carts.EnsureIndex(x => x.UserId, true);

        // Wishlist indexes
        WishlistItems.EnsureIndex(x => x.UserId);
        WishlistItems.EnsureIndex(x => x.ProductId);

        // Notification indexes
        Notifications.EnsureIndex(x => x.UserId);
        Notifications.EnsureIndex(x => x.IsRead);
        Notifications.EnsureIndex(x => x.CreatedAt);

        // Moderation indexes
        UserBans.EnsureIndex(x => x.UserId);
        UserBans.EnsureIndex(x => x.IsActive);
        UserMutes.EnsureIndex(x => x.UserId);
        UserMutes.EnsureIndex(x => x.IsActive);
        UserWarnings.EnsureIndex(x => x.UserId);
        MessageReports.EnsureIndex(x => x.Status);
        MessageReports.EnsureIndex(x => x.ReportedUserId);
        ModerationLogs.EnsureIndex(x => x.ModeratorId);
        ModerationLogs.EnsureIndex(x => x.CreatedAt);

        // Activity indexes
        UserActivities.EnsureIndex(x => x.UserId);
        UserActivities.EnsureIndex(x => x.CreatedAt);

        // Coupon indexes
        Coupons.EnsureIndex(x => x.Code, true);
        Coupons.EnsureIndex(x => x.IsActive);

        // Bundle indexes
        ProductBundles.EnsureIndex(x => x.SellerId);
        ProductBundles.EnsureIndex(x => x.IsActive);

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

        if (CustomRoles.Count() == 0)
        {
            var defaultRoles = new List<CustomRole>
            {
                new() { Name = "Owner", Color = "#FFD700", Position = 100, IsHoisted = true, Permissions = new List<string> { RolePermissions.Administrator } },
                new() { Name = "Admin", Color = "#E74C3C", Position = 90, IsHoisted = true, Permissions = new List<string> { RolePermissions.Administrator } },
                new() { Name = "Moderator", Color = "#9B59B6", Position = 80, IsHoisted = true, Permissions = new List<string> { RolePermissions.ManageMessages, RolePermissions.KickMembers, RolePermissions.MuteMembers } },
                new() { Name = "VIP", Color = "#00FF88", Position = 70, IsHoisted = true, Permissions = new List<string> { RolePermissions.ViewChannels, RolePermissions.SendMessages } },
                new() { Name = "Verified", Color = "#3498DB", Position = 60, IsHoisted = true, Permissions = new List<string> { RolePermissions.ViewChannels, RolePermissions.SendMessages } },
                new() { Name = "Member", Color = "#95A5A6", Position = 10, IsHoisted = false, Permissions = new List<string> { RolePermissions.ViewChannels, RolePermissions.SendMessages } }
            };

            foreach (var role in defaultRoles)
            {
                CustomRoles.Insert(role);
            }
        }
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
