# Yurt Cord

A Discord-like marketplace and chat application built with .NET 8, WPF, and SignalR.

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

### Marketplace
- List items for sale (like Craigslist)
- Category filtering (Software, Games, Services, Digital, etc.)
- Search functionality
- Product detail views
- $1.50 listing fee
- PayPal and Bitcoin payment support

### User System
- User registration and login
- JWT authentication
- Role system:
  - Owner (Gold)
  - Admin (Red)
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

### UI/UX
- Discord-like dark theme
- Smooth animations and transitions
- Custom window chrome
- Responsive design
- Beautiful gradients and shadows

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
│   └── VeaMarketplace.Client/     # WPF Client (Yurt Cord)
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

## Screenshots

The application features a Discord-like interface with:
- Dark theme with purple/pink accent colors
- Animated login screen
- Channel sidebar with voice channels
- Member list with role badges
- Marketplace with product cards
- Profile page with stats and badges

## License

MIT License
