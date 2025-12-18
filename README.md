# Plugin

A modern marketplace and community platform built with .NET 8, WPF, and SignalR. Featuring a sleek orange and black theme with smooth animations.

## Features

### Chat System
- Real-time IRC-style chat with SignalR
- Multiple channels (text and voice)
- Typing indicators
- Message history
- System notifications (join/leave)

### Voice Channels
- Real-time voice communication
- Voice activity visualization
- Mute/Deafen controls
- Audio level indicators
- Screen sharing support

### Marketplace
- List items for sale
- Category filtering (Software, Games, Services, Digital, etc.)
- Advanced search functionality
- Product detail views
- $1.50 listing fee
- PayPal and Bitcoin payment support
- Product reviews and ratings

### User System
- User registration and login
- JWT authentication
- Role system:
  - Owner (Gold)
  - Admin (Orange)
  - Moderator (Purple)
  - VIP (Green)
  - Verified (Blue)
  - Member (Gray)
- Rank system based on sales:
  - Legend
  - Elite
  - Diamond
  - Platinum
  - Gold
  - Silver
  - Bronze
  - Newcomer

### Social Features
- Friend system with requests
- Direct messaging
- User profiles with customization
- Activity feed
- Leaderboards

### Moderation
- Ban system (temporary and permanent)
- Mute system
- Warning system
- Auto-moderation with filters
- Moderation logs

### UI/UX
- Premium dark theme with orange accent
- Orange glow effects on interactive elements
- Smooth animations and transitions
- Custom window chrome
- Responsive design
- Modern button hover effects with scale and glow

## Project Structure

```
VeaMarketplace/
├── src/
│   ├── VeaMarketplace.Shared/     # Shared models and DTOs
│   │   ├── Models/
│   │   ├── DTOs/
│   │   └── Enums/
│   ├── VeaMarketplace.Server/     # ASP.NET Core Server
│   │   ├── Controllers/
│   │   ├── Hubs/
│   │   ├── Services/
│   │   └── Data/
│   └── VeaMarketplace.Client/     # WPF Client (Plugin)
│       ├── Views/
│       ├── ViewModels/
│       ├── Controls/
│       ├── Services/
│       ├── Styles/
│       └── Converters/
└── VeaMarketplace.sln
```

## Getting Started

### Prerequisites
- .NET 8 SDK
- Windows 10/11 (for WPF client)
- Visual Studio 2022 (recommended)

### Running the Server

```bash
cd src/VeaMarketplace.Server
dotnet run
```

The server will start at `http://localhost:5000`

### Running the Client

```bash
cd src/VeaMarketplace.Client
dotnet run
```

Or open the solution in Visual Studio and run the client project.

### Building

```bash
dotnet build VeaMarketplace.sln
```

## Configuration

### Server (appsettings.json)
```json
{
  "Jwt": {
    "Secret": "YourSecretKey"
  },
  "Payment": {
    "ListingFee": 1.50,
    "SalesFeePercent": 5.0,
    "PayPal": {
      "ClientId": "",
      "ClientSecret": ""
    },
    "Bitcoin": {
      "WalletAddress": ""
    }
  }
}
```

## Technologies Used

- **Backend**
  - ASP.NET Core 8
  - SignalR for real-time communication
  - LiteDB for data persistence
  - JWT for authentication
  - BCrypt for password hashing

- **Frontend**
  - WPF (.NET 8)
  - CommunityToolkit.Mvvm
  - NAudio for voice
  - Custom animations and styles

## Theme

Plugin features a premium dark theme with:
- Deep black backgrounds (#0A0A0B, #111113, #18181B)
- Vibrant orange accent (#FF6B00)
- Fire gradient effects (orange to gold)
- Orange glow on hover states
- Smooth scale animations on buttons
- High contrast text for readability

## License

MIT License
