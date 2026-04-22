# Sensum Web - Bot Management System

A modern, secure web interface for managing Growtopia bots with real-time updates and Google authentication.

## 🚀 Improvements Implemented

### Authentication & Security
- **Google OAuth 2.0**: Sign in with Google account support
- **Cookie-based Authentication**: Secure session management with 24-hour expiration
- **Password Encryption**: BCrypt hashing for legacy username/password authentication
- **Input Validation**: Comprehensive validation for all user inputs (GrowID, password length checks)
- **XSS Protection**: HTML escaping in frontend to prevent cross-site scripting
- **Secure Random Generation**: Cryptographically secure random string generation for secrets

### Thread Safety & Concurrency
- **ConcurrentDictionary**: Replaced List with ConcurrentDictionary for thread-safe bot storage
- **ConcurrentQueue**: Thread-safe console message queue
- **SemaphoreSlim**: Added for synchronization where needed
- **Channel<T>**: Used for async console message streaming

### Architecture Improvements
- **Dependency Injection**: Proper DI setup through ASP.NET Core's built-in container
- **Disposable Pattern**: BotManager implements IDisposable for proper cleanup
- **Unique Bot IDs**: Each bot gets a unique GUID-based ID instead of using GrowID
- **Service Classes**: Separated concerns with AuthService, PasswordHasher classes

### Real-time Communication
- **SignalR Integration**: Real-time bot status updates instead of polling
- **Automatic Reconnection**: SignalR client handles reconnection automatically
- **Fallback Polling**: Graceful fallback to polling if SignalR fails
- **Connection Status UI**: Visual indicator for real-time connection status

### Logging & Monitoring
- **Serilog Integration**: Structured logging with console and file outputs
- **Request Logging**: HTTP request/response logging with timing
- **Log Rotation**: Daily log files with 7-day retention
- **Health Check Endpoint**: `/api/health` and `/health` for monitoring

### API Improvements
- **Consistent Response Format**: ApiResponse<T> wrapper for all responses
- **Proper HTTP Status Codes**: Appropriate status codes for different scenarios
- **Error Handling**: Try-catch blocks with proper error responses
- **Input Validation**: Server-side validation with meaningful error messages
- **Health Check**: Public health endpoint for monitoring

### Frontend Enhancements
- **Google Sign-In Button**: Prominent Google authentication button with logo
- **User Session Display**: Shows logged-in user email/name with logout option
- **Toast Notifications**: User-friendly feedback for actions
- **Loading States**: Button disabled states during async operations
- **Responsive Design**: Mobile-friendly layout
- **Real-time Status**: Connection status indicator
- **Better Error Messages**: User-friendly error displays
- **Auto-scroll Console**: Console automatically scrolls to latest messages

### Configuration
- **Environment Variables**: Configurable port via `SENSUM_PORT`, Google credentials via `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET`
- **appsettings.json**: Google OAuth configuration and other settings
- **CORS Configuration**: Configurable CORS policies
- **User Secrets**: Support for sensitive configuration via User Secrets

### Code Quality
- **Nullable Reference Types**: Enabled for better null safety
- **Async/Await**: Proper async patterns throughout
- **Structured Logging**: Consistent logging with context
- **Error Recovery**: Graceful error handling with recovery options

## 📁 File Structure

```
src/Sensum.Web/
├── Program.cs              # Application entry point with Serilog, CORS, SignalR
├── BotManager.cs           # Thread-safe bot management with encryption
├── ApiEndpoints.cs         # REST API endpoints with SignalR hub
├── appsettings.json        # Application configuration
├── Sensum.Web.csproj       # Project file with dependencies
├── logs/                   # Log files directory
└── wwwroot/
    └── index.html          # Modern responsive frontend with SignalR
```

## 🔧 Configuration

### Environment Variables
- `SENSUM_PORT`: Server port (default: 5000)
- `SENSUM_JWT_SECRET`: JWT signing key (auto-generated if not set)

### appsettings.json
Configure logging levels, CORS origins, and other settings.

## 🚦 API Endpoints

### Public Endpoints
- `GET /api/health` - Health check with bot count
- `GET /health` - ASP.NET health check
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login and get JWT token

### Bot Management (Protected)
- `GET /api/bots` - List all bots
- `POST /api/bots` - Add new bot
- `POST /api/bots/{id}/connect` - Connect bot
- `POST /api/bots/{id}/disconnect` - Disconnect bot
- `DELETE /api/bots/{id}` - Remove bot

### SignalR
- `/hubs/bots` - Real-time updates hub

## 🛡️ Security Notes

1. **Enable Authentication**: Uncomment `.RequireAuthorization()` in ApiEndpoints.cs when ready
2. **Set JWT Secret**: Always set `SENSUM_JWT_SECRET` in production
3. **HTTPS**: Use HTTPS in production environments
4. **Rate Limiting**: Consider adding rate limiting for auth endpoints
5. **Password Policy**: Enforce strong password requirements

## 📦 Dependencies

- BCrypt.Net-Next - Password hashing
- Serilog.AspNetCore - Structured logging
- Serilog.Sinks.File - File logging
- Microsoft.AspNetCore.Authentication.JwtBearer - JWT authentication
- System.Threading.Channels - Async messaging
- Microsoft.AspNetCore.SignalR - Real-time communication

## 🏃 Running the Application

```bash
# Development
dotnet run --project src/Sensum.Web

# With custom port
SENSUM_PORT=8080 dotnet run --project src/Sensum.Web

# Production
dotnet publish -c Release
dotnet src/Sensum.Web/bin/Release/net9.0/publish/Sensum.Web.dll
```

## 📝 Usage

1. Open http://localhost:5000
2. Add bots using GrowID and password
3. Connect/disconnect bots as needed
4. Monitor console output in real-time
5. View bot status, world, and ping information
