# OVERSEER

> **Observe • Connect • Control**

A cutting-edge marketplace and community platform built with .NET 8, WPF, and SignalR. Featuring a stunning cyberpunk aesthetic with neon glow effects, animated matrix backgrounds, and a fully circular UI design.

<!-- Add your screenshots here -->
<!-- ![Overseer Dashboard](images/dashboard.png) -->
<!-- ![Overseer Chat](images/chat.png) -->
<!-- ![Overseer Marketplace](images/marketplace.png) -->

---

## Design Philosophy

Overseer features a futuristic **cyberpunk design system** with:

- **Matrix-style animated backgrounds** - Cascading code rain effect
- **Circular/Pill-shaped UI elements** - Rounded corners on all components (24-40px radius)
- **Neon glow effects** - Primary green (#00FF88), secondary cyan (#00CCFF), accent magenta (#FF00FF)
- **Deep space backgrounds** - Ultra-dark surfaces (#050510, #101020)
- **Hover interactions** - Elements illuminate with enhanced glow on interaction

---

## Features

### Real-Time Chat System
- SignalR-powered real-time messaging
- Multiple channels (text and voice)
- Typing indicators with neon effects
- Message history and search
- System notifications with animated transitions

### Voice Communication
- Crystal-clear real-time voice
- Animated voice activity visualization
- Mute/Deafen controls with circular buttons
- Audio level bars with glow effects
- Screen sharing support

### Marketplace
- List items for sale with beautiful product cards
- Category filtering (Software, Games, Services, Digital, etc.)
- Advanced search with instant results
- Product detail views with image galleries
- $1.50 listing fee
- PayPal and Bitcoin payment support
- Star ratings and review system

### User System
- Secure registration and login
- JWT authentication with refresh tokens
- **Role Hierarchy:**
  | Role | Color | Glow |
  |------|-------|------|
  | Owner | Gold | #FFD700 |
  | Admin | Red | #FF4444 |
  | Moderator | Purple | #B464FF |
  | VIP | Neon Green | #00FF88 |
  | Verified | Cyan | #00CCFF |
  | Member | Gray | #8080A0 |

- **Rank System** based on sales activity:
  - Legend, Elite, Diamond, Platinum, Gold, Silver, Bronze, Newcomer

### Social Features
- Friend system with glowing request indicators
- Direct messaging with typing animations
- Customizable user profiles
- Activity feed with real-time updates
- Leaderboards with animated rank badges

### Moderation Tools
- Ban system (temporary and permanent)
- Mute system with duration controls
- Warning system with tracking
- Auto-moderation with content filters
- Detailed moderation logs

---

## Project Structure

```
Overseer/
├── src/
│   ├── VeaMarketplace.Shared/      # Shared models and DTOs
│   │   ├── Models/
│   │   ├── DTOs/
│   │   └── Enums/
│   ├── VeaMarketplace.Server/      # ASP.NET Core Server
│   │   ├── Controllers/
│   │   ├── Hubs/
│   │   ├── Services/
│   │   └── Data/
│   └── VeaMarketplace.Client/      # WPF Client (Overseer)
│       ├── Views/
│       ├── ViewModels/
│       ├── Controls/
│       │   └── MatrixBackground.cs # Animated background
│       ├── Services/
│       ├── Styles/
│       ├── Themes/
│       │   └── OverseerTheme.xaml  # Core theme resources
│       └── Converters/
└── VeaMarketplace.sln
```

---

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

---

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

---

## Technologies

### Backend
- ASP.NET Core 8
- SignalR for real-time communication
- LiteDB for data persistence
- JWT for authentication
- BCrypt for password hashing

### Frontend
- WPF (.NET 8)
- CommunityToolkit.Mvvm
- NAudio for voice processing
- FFmpeg.AutoGen for video/screen sharing
- Custom animations and theme system

---

## Theme System

Overseer uses a comprehensive theme system defined in `OverseerTheme.xaml`:

### Color Palette
| Name | Hex | Usage |
|------|-----|-------|
| Primary | `#00FF88` | Main accent, success states |
| Secondary | `#00CCFF` | Links, secondary actions |
| Accent | `#FF00FF` | Highlights, special elements |
| Warning | `#FFD700` | Warnings, important notices |
| Danger | `#FF4444` | Errors, destructive actions |
| Background | `#050510` | App background |
| Surface | `#101020` | Cards, panels |
| Text | `#E0E0FF` | Primary text |
| TextDim | `#8080A0` | Secondary text |

### Available Styles
- `OverseerCircleButton` - Circular buttons with neon glow
- `OverseerPillButton` - Pill-shaped action buttons
- `OverseerTextBox` - Rounded input fields
- `OverseerPasswordBox` - Secure password inputs
- `OverseerCard` - Content cards with glow
- `OverseerPanel` - Gradient panels
- `OverseerNavItem` - Navigation buttons
- `OverseerAvatar` - User avatar containers
- `OverseerGlowLabel` - Glowing text labels
- `OverseerRainbowLabel` - Rainbow gradient text

---

## Screenshots

*Add your screenshots here to showcase the Overseer interface*

---

## License

MIT License

---

<p align="center">
  <strong>OVERSEER</strong><br>
  <em>Observe • Connect • Control</em>
</p>
