# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BaseAppLib is a Blazor component library that provides shared UI components and view models for the BCProjects suite of multi-tenant SaaS applications. Built using:
- .NET 9.0 with Blazor WebAssembly and .NET MAUI
- ReactiveUI for MVVM pattern with Fody for property change notifications
- Blazorise component library (v1.7.5) with Bootstrap 5
- LazyMagic framework integration
- Central package management with automated local package generation

## Key Components

### Projects in Solution
- **BaseApp.BlazorUI**: Core Blazor components with Blazorise integration (packaged library)
- **BaseApp.ViewModels**: ViewModels using ReactiveUI for shared business logic (packaged library)
- **BlazorUI**: Application-specific Blazor components that reference BaseApp.BlazorUI
- **ViewModels**: Application-specific ViewModels that reference BaseApp.ViewModels
- **WASMApp**: Blazor WebAssembly application for web deployment
- **MAUIApp**: .NET MAUI application for mobile and desktop platforms
- **Blazorise.Icons.FontAwesome**: Custom FontAwesome icon provider (v6 webfonts)

### Project Relationships
- **BaseApp.*** projects are packaged libraries distributed via NuGet
- **Non-BaseApp** projects are application-specific implementations that consume the BaseApp packages
- BlazorUI → references → BaseApp.BlazorUI → references → BaseApp.ViewModels
- ViewModels → references → BaseApp.ViewModels

### Important Files
- `Solution.Build.props`: Version management (currently 1.2.3)
- `Directory.Packages.props`: Central package version management
- `MakePackage.targets`: Automated NuGet package creation for BaseApp.* projects
- `GetSystemConfig.props`: Reads environment from systemconfig.yaml
- `Import-LzAws.ps1`: Finds and loads LzAws PowerShell module

## Build and Development Commands

```bash
# Build entire solution (excludes MAUIApp on non-Windows CI)
dotnet build BaseAppLib.sln

# Build individual projects
dotnet build BlazorUI/BlazorUI.csproj
dotnet build BaseApp.BlazorUI/BaseApp.BlazorUI.csproj
dotnet build ViewModels/ViewModels.csproj
dotnet build BaseApp.ViewModels/BaseApp.ViewModels.csproj

# Build and run WASM app
cd WASMApp
dotnet run

# Build MAUI app for specific platforms
cd MAUIApp
dotnet build -t:Run -f net9.0-maccatalyst     # macOS
dotnet build -t:Run -f net9.0-android         # Android
dotnet build -t:Run -f net9.0-ios             # iOS
dotnet build -t:Run -f net9.0-windows10.0.19041.0  # Windows

# Package Generation (automatic on build for BaseApp.* projects)
# Packages output to ../Packages/ directory
# Package format: {AssemblyName}.{Version}-dev-{timestamp}.nupkg
# Local NuGet cache is cleared automatically for each package

# Clean build artifacts
# Use the DeleteObjAndBin.ps1 script from parent directory if available
```

## Development Launch Profiles

### WASMApp Launch Profiles (launchSettings.json)
- **https Cloud**: Standard development profile using cloud APIs
- **https Local**: Uses local API services (ASPNETCORE_ENVIRONMENT=Localhost)
- **IIS Express Cloud**: IIS Express with cloud APIs
- **IIS Express Local**: IIS Express with local APIs

When running with local profiles, ensure the local API service is running on https://localhost:5001

### MAUIApp Configuration
- **Windows Machine**: Uses MSIX packaging for Windows deployment
- Supports Android, iOS, macOS (Catalyst), and Windows platforms
- Platform-specific minimum versions:
  - iOS/macOS: 15.0
  - Android: 24.0 (API level 24)
  - Windows: 10.0.17763.0

## Testing

No formal test projects are currently included in this solution. Testing is performed through:
- Manual testing via WASMApp and MAUIApp development profiles
- Component testing through the main BCProjects applications
- Integration testing with local API services

## Development Workflow

### Local Package Development
- Packages are automatically built with timestamp suffix (e.g., 1.2.3-dev-20250102123456)
- Output to `../Packages/` directory
- Local NuGet cache is cleared automatically to ensure latest version
- MakePackage.targets handles package creation for BaseApp.* projects

### JavaScript Integration
- Browser fingerprinting service for client identification using ClientJS
- Custom scripts loaded via `baseindexbody.js` and `baseindexhead.js`
- Blazorise.Animate integration for UI animations
- JavaScript modules inherit from `LzBaseJSModule` for proper lifecycle management
- Implements safe async disposal pattern with JSDisconnectedException handling
- Service worker implementation for PWA support (service-worker.published.js)

### Dependency Injection
Services implementing ITransient, ISingleton, or IScoped are auto-registered by LazyMagic framework via reflection.

### MAUI-Specific Configuration
- Uses embedded appConfig.js for configuration
- Separate HttpClient instances for app assets and API calls
- Platform-specific build targets and configurations
- Debugger-aware local/remote API switching

## CI/CD Pipeline

### GitHub Actions
- Triggered on push to main branch
- Publishes NuGet packages to GitHub Packages registry
- Configured for both Banquet-Consulting and LazyMagicOrg package sources

### Package Sources
```xml
<add key="github" value="https://nuget.pkg.github.com/Banquet-Consulting/index.json" />
<add key="LazyMagic" value="https://nuget.pkg.github.com/LazyMagicOrg/index.json" />
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
```

## Architecture Patterns

### MVVM with ReactiveUI
- ViewModels in BaseApp.ViewModels project
- Fody for automatic INotifyPropertyChanged implementation
- Reactive properties and commands

### Component Structure
- Blazor components with code-behind patterns
- Service-based architecture with dependency injection
- JavaScript interop for browser-specific features

### Multi-Tenancy Support
- Tenant configuration DTOs in ViewModels
- Session management interfaces
- Environment-specific configuration via systemconfig.yaml

## Recent Changes

Based on git status, recent work includes:
- Browser fingerprinting service implementation with ClientJS integration
- Consolidation of JavaScript services (removed individual JS files, consolidated to indexbody.js and indexhead.js)
- Configuration updates for BaseApp integration
- Added new Pet-related components (Pet.razor, PetStatusSelect.razor, CategorySelect.razor, TagsSelect.razor)
- Session management refactoring with BaseAppSessionViewModel implementations

## Important Notes

- This is a library solution containing both packaged libraries (BaseApp.*) and consuming applications (WASMApp, MAUIApp)
- Part of larger MagicPets/BCProjects ecosystem (see parent CLAUDE.md)
- Uses LazyMagic code generation - don't edit .g.* files
- Environment configuration read from ../systemconfig.yaml
- Package versions must be kept in sync between BaseApp.* projects via Solution.Build.props
- When developing locally, ensure local API services are running before testing with "Local" launch profiles
- MAUI apps require platform-specific SDKs to be installed
- Current git branch: dev (main branch for PRs: main)