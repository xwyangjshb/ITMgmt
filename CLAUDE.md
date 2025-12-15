# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IT Device Manager (IT设备管理系统) is an ASP.NET Core 8.0 Web API solution for discovering, managing, and controlling network devices in a local area network. The system automatically discovers devices via network scanning, tracks device relationships (e.g., VMs to ESXi hosts), and provides remote power management through Wake-on-LAN.

### Key Capabilities
- **Network Discovery**: Automated scanning of IP ranges to discover devices via Ping, ARP, and port scanning
- **Device Management**: CRUD operations for devices with MAC/IP tracking and device type identification
- **Power Management**: Wake-on-LAN support for remote device power-on
- **Device Relationships**: Track hierarchical relationships (physical-to-virtual, network dependencies, service dependencies)
- **Background Services**: Automated 30-minute device discovery scans with conflict resolution

## Solution Structure

```
ITDeviceManager.sln
├── ITDeviceManager.API/        # ASP.NET Core Web API project (主应用)
│   ├── Controllers/            # API endpoints (DevicesController, DeviceRelationsController)
│   ├── Data/                   # Entity Framework DbContext
│   ├── Migrations/             # EF Core database migrations
│   ├── Services/               # Background services (DeviceDiscoveryBackgroundService)
│   └── wwwroot/                # Static files for basic web UI
└── ITDeviceManager.Core/       # Class library with domain models and services
    ├── Models/                 # Domain entities (Device, DeviceRelation, PowerOperation)
    └── Services/               # Business logic (NetworkDiscoveryService, WakeOnLanService)
```

## Development Commands

### Build and Run
```bash
# Build the entire solution
dotnet build

# Run the API project
cd ITDeviceManager.API
dotnet run

# Run in watch mode for development
dotnet watch run

# Publish for deployment
dotnet publish -c Release -o ./publish
```

### Database Management
```bash
# Add a new migration (from ITDeviceManager.API directory)
dotnet ef migrations add <MigrationName>

# Update database to latest migration
dotnet ef database update

# Drop database (for development reset)
dotnet ef database drop

# Generate SQL script for migration
dotnet ef migrations script
```

### Testing
```bash
# Note: No test projects currently exist in the solution
# When adding tests, use:
dotnet test
```

## Architecture Notes

### Database Schema
The system uses **SQL Server** with Entity Framework Core. Three main entities:
- **Device**: Core entity with unique constraints on `IPAddress` and `MACAddress`
- **DeviceRelation**: Many-to-many self-referencing relationship with `ParentDevice` and `ChildDevice` (uses Restrict delete behavior to prevent cascading deletes)
- **PowerOperation**: Tracks WOL/shutdown operations as audit log

**Important**: The database uses `EnsureCreated()` in Program.cs startup. For production, use proper migrations instead.

### Network Discovery Flow
1. **DeviceDiscoveryBackgroundService** runs every 30 minutes
2. Scans network ranges derived from existing devices (defaults: 192.168.1.x and 192.168.0.x)
3. For each IP: Ping → ARP lookup → Port scan → Device type identification
4. **Conflict Resolution**:
   - MAC addresses are treated as primary identifiers
   - IP conflicts update existing device's MAC (assumes NIC replacement)
   - Uses `processedMacAddresses` HashSet to prevent duplicates within a scan

### Device Type Identification
Uses multi-strategy approach in `NetworkDiscoveryService.IdentifyDeviceTypeAsync()`:
1. MAC address OUI lookup (extensive vendor prefix dictionary)
2. Hostname pattern matching
3. Port scanning for service identification (SSH, HTTP, RDP, MySQL, etc.)

### API Design Patterns
- **Controllers** follow REST conventions with proper status codes
- **Circular reference prevention**: Controllers use projection (Select) to exclude navigation properties when serializing
- **JSON configuration**: Uses `ReferenceHandler.Preserve` and `MaxDepth = 32` for complex object graphs
- **CORS**: Configured with "AllowAll" policy (⚠️ production concern)

## Code Review & Optimization Recommendations

### 🔴 Critical Issues

1. **Security: CORS Policy Too Permissive** (Program.cs:40-47)
   - Current: `AllowAnyOrigin()` allows all cross-origin requests
   - Risk: Enables CSRF attacks and unauthorized API access
   - Fix: Restrict to specific origins in production:
   ```csharp
   options.AddPolicy("AllowFrontend", policy =>
       policy.WithOrigins("https://yourdomain.com")
             .AllowAnyMethod()
             .AllowAnyHeader());
   ```

2. **Security: No Authentication/Authorization** (全局)
   - All API endpoints are publicly accessible
   - Recommendation: Add JWT or API key authentication, especially for power operations
   ```csharp
   [Authorize]
   [ApiController]
   public class DevicesController : ControllerBase { }
   ```

3. **Database: Using EnsureCreated() in Production** (Program.cs:76)
   - `EnsureCreated()` bypasses migrations and can cause data loss
   - Fix: Replace with `context.Database.Migrate()` for production

4. **Resource Leak: Missing Disposal** (NetworkDiscoveryService.cs:426)
   - `TcpClient` instances in `ScanCommonPortsAsync()` may not dispose on timeout
   - Fix: Ensure disposal with try-finally or using declaration

5. **Configuration: Hardcoded Connection String** (Program.cs:10-11)
   - Connection string has inline fallback instead of failing
   - Should read from appsettings.json or environment variables exclusively

### 🟡 Performance Concerns

6. **N+1 Query Problem** (DevicesController.cs:266-325 in DiscoverDevices)
   - Each discovered device triggers individual `FirstOrDefaultAsync` calls
   - Optimization: Batch load existing devices by MAC/IP before loop:
   ```csharp
   var existingMacs = await _context.Set<Device>()
       .Where(d => discoveredDevices.Select(dd => dd.MACAddress).Contains(d.MACAddress))
       .ToDictionaryAsync(d => d.MACAddress);
   ```

7. **Inefficient Port Scanning** (NetworkDiscoveryService.cs:417-443)
   - Scans 19 common ports with 1-second timeout per device
   - Each device discovery takes ~19 seconds minimum
   - Recommendation:
     - Reduce timeout to 200-500ms
     - Limit ports to essential ones (22, 80, 443, 3389)
     - Consider making port scan optional/configurable

8. **Parallel Processing Bottleneck** (NetworkDiscoveryService.cs:17-31)
   - `Task.WhenAll()` for 254 IPs can overwhelm network/system
   - Add throttling: Use `SemaphoreSlim` or `Parallel.ForEachAsync` with MaxDegreeOfParallelism

9. **Database Round-trips in Background Service** (DeviceDiscoveryBackgroundService.cs:86-112)
   - Multiple database queries per device in tight loop
   - Consider bulk operations with `BulkExtensions` package

### 🟢 Code Quality Improvements

10. **Logging: Console.WriteLine Usage** (全局)
    - Multiple places use `Console.WriteLine` instead of ILogger
    - Controllers (DevicesController.cs:61, 108, 162, etc.) should inject and use ILogger
    - Benefits: Structured logging, log levels, filtering

11. **Error Handling: Swallowing Exceptions** (NetworkDiscoveryService.cs:59, 73, 93, etc.)
    - Empty catch blocks hide failures silently
    - Recommendation: Log exceptions or at minimum log failure context

12. **Magic Numbers** (DeviceDiscoveryBackgroundService.cs:11)
    - Hardcoded 30-minute interval, 5-minute retry, 60-minute offline threshold
    - Should be configurable via appsettings.json:
    ```json
    "DeviceDiscovery": {
      "ScanIntervalMinutes": 30,
      "RetryDelayMinutes": 5,
      "OfflineThresholdMinutes": 60
    }
    ```

13. **Data Validation Missing** (DevicesController.cs:116)
    - MAC/IP format validation relies on client input
    - Add validation attributes or FluentValidation:
    ```csharp
    [RegularExpression(@"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$")]
    public string MACAddress { get; set; }
    ```

14. **Code Duplication** (DevicesController.cs:33-53 vs 75-95)
    - GetDevices and GetDevice have identical projection logic
    - Extract to shared method or AutoMapper profile

15. **Unused Code** (Program.cs:81-84)
    - `WeatherForecast` record is scaffolding template code, should be removed

16. **DeviceContext Property Pattern** (DeviceContext.cs:16-26)
    - Unusual getter/setter pattern for `devices` DbSet
    - Standard pattern: `public DbSet<Device> Devices { get; set; }`

### 🔵 Feature Gaps

17. **Remote Shutdown Not Implemented** (WakeOnLanService.cs:72-78)
    - Placeholder returns false
    - Needs implementation via WMI (Windows), SSH (Linux), or IPMI

18. **No Retry Logic for WOL** (WakeOnLanService.cs:10-51)
    - Single send attempt, no verification of device wake-up
    - Consider: Retry with exponential backoff + ping verification

19. **Device Discovery Limitations**
    - No support for CIDR notation (e.g., 192.168.1.0/24)
    - No exclusion list for IP ranges
    - No custom MAC vendor database integration (hardcoded in code)

20. **Missing API Endpoints**
    - No endpoint for device search/filtering
    - No pagination for large device lists
    - No batch operations (bulk wake, bulk delete)
    - No historical power operation queries

21. **No VMware/ESXi Integration** (README mentions but not implemented)
    - vSphere API integration planned but missing
    - Would enable automatic VM-to-host relationship discovery

### 📋 Suggested Priorities

**Phase 1 - Critical Fixes**
- Implement authentication/authorization
- Fix CORS policy for production
- Replace EnsureCreated with Migrate
- Add connection string validation

**Phase 2 - Performance**
- Optimize port scanning (reduce ports/timeout)
- Add parallelism throttling
- Fix N+1 queries with bulk loading
- Add caching for frequent queries

**Phase 3 - Reliability**
- Replace Console.WriteLine with ILogger
- Add proper error handling
- Implement retry logic for network operations
- Add health checks endpoint

**Phase 4 - Features**
- Complete remote shutdown implementation
- Add API pagination and filtering
- Implement VMware vSphere integration
- Add scheduled power operations (task scheduler)

## Configuration Notes

### Connection String
Located in `appsettings.json` under `ConnectionStrings:DefaultConnection`. Uses LocalDB by default:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ITDeviceManagerDB;Trusted_Connection=true;MultipleActiveResultSets=true"
}
```

For production, use SQL Server instance and store in environment variables or Azure Key Vault.

### API URLs
- Development: `https://localhost:5001` or `http://localhost:5000`
- Swagger UI: `https://localhost:5001/swagger`
- Default frontend: `https://localhost:5001/index.html`

## Dependencies

### ITDeviceManager.API
- `Microsoft.EntityFrameworkCore.SqlServer` (8.0.10)
- `Microsoft.EntityFrameworkCore.Tools` (8.0.10)
- `Swashbuckle.AspNetCore` (6.8.1)
- `Microsoft.AspNetCore.OpenApi` (8.0.10)

### ITDeviceManager.Core
- `Microsoft.Extensions.Logging.Abstractions` (8.0.2)

## Working with Device Types

Device type identification is based on MAC OUI prefixes in `NetworkDiscoveryService.cs:246-415`. To add new device types:
1. Add enum value to `DeviceType` in `Device.cs`
2. Add MAC prefix mapping to `vendorPrefixes` dictionary
3. Optionally add port-based detection in `ScanCommonPortsAsync` result handling

## Common Development Scenarios

### Adding a New API Endpoint
1. Add method to appropriate controller with `[Http___]` attribute
2. Ensure proper status codes (200 OK, 201 Created, 400 Bad Request, 404 Not Found)
3. Use `try-catch` for database operations
4. Return appropriate ActionResult type

### Adding a New Domain Model
1. Create model class in `ITDeviceManager.Core/Models/`
2. Add `DbSet<T>` to `DeviceContext`
3. Configure entity in `OnModelCreating` if needed
4. Run `dotnet ef migrations add <Name>` from API project
5. Update database with `dotnet ef database update`

### Modifying Device Discovery Logic
- Primary logic in `NetworkDiscoveryService.cs`
- Background service orchestration in `DeviceDiscoveryBackgroundService.cs`
- Consider impact on scan duration (254 IPs × port scan time)
- Test with small IP ranges first

## Known Limitations

1. **Single-threaded background service**: Only one discovery scan runs at a time
2. **No device authentication**: Cannot verify device ownership/access
3. **Windows-centric**: MAC discovery via ARP works best on Windows (arp command parsing differs on Linux)
4. **UDP broadcast limitation**: WOL packets may not cross subnets without router configuration
5. **No encrypted communication**: Device credentials/operations sent in plain text (HTTPS provides transport security only)

## Project Status

Based on git commits:
- Initial project structure completed
- Basic device CRUD and discovery functional
- Wake-on-LAN implemented
- Remote shutdown placeholder only
- No frontend framework integrated (basic HTML/JS in wwwroot)
- No unit/integration tests
