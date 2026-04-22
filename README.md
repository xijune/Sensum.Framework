C# Library to easily create botting tool for Growtopia (Works and tested in Windows x64 & Linux x64).

This was used in my old multibot project "Sensum" (latest client releases). And as i no longer have much interest in gt anymore i decided to make this framework public.

Everything should work and world data deserialization should be up to date too has ready to use Actions such as Punch, Placing items to storage box etc.. these can be found in Actions.cs, PathFinder and handles most packets needed bot to handle. Game version and protocol can be updated from App.cs

## Web Interface

A modern web interface is now available for easy bot management!

### Features
- **Dashboard**: View all bots at a glance with real-time status
- **Add/Remove Bots**: Dynamically manage multiple bots through the UI
- **Live Console**: See bot console messages in real-time
- **Controls**: Connect, disconnect, and manage bots with one click

### Running the Web Interface

```bash
cd src/Sensum.Web
dotnet run
```

Then open **http://localhost:5000** in your browser.

### API Endpoints

- `GET /api/bots` - List all bots with status
- `POST /api/bots` - Add a new bot (body: `{ "growid": "...", "password": "..." }`)
- `POST /api/bots/{id}/connect` - Connect a bot
- `POST /api/bots/{id}/disconnect` - Disconnect a bot
- `DELETE /api/bots/{id}` - Remove a bot

## Project Structure

```
src/
├── Sensum.Console/      # Traditional console-based bot client
├── Sensum.Framework/    # Core framework library
└── Sensum.Web/          # Web interface for bot management
```

## Quick Start

### Console Bot
```bash
cd src/Sensum.Console
dotnet run
```
Edit `Program.cs` to configure credentials.

### Web Interface
```bash
cd src/Sensum.Web
dotnet run
```
Open http://localhost:5000 and manage bots through the UI.