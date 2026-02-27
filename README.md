# OpenSNI

A Native AOT .NET 10 library that implements the Windows-only SQL Server SNI (SQL Server Network Interface) API on macOS and Linux, allowing `Microsoft.Data.SqlClient` (.NET Framework / Mono) to connect to SQL Server via TCP.

## Background

`Microsoft.Data.SqlClient` for .NET Framework unconditionally P/Invokes into `Microsoft.Data.SqlClient.SNI.arm64.dll` (on arm64) for all network I/O. This library is Windows-only — there is no Unix fallback in the .NET Framework build. On .NET Core/.NET 5+, cross-platform support is built in, but when running .NET Framework apps on Mono, the SNI dependency must be satisfied manually.

OpenSNI solves this by compiling to a native dylib (via Native AOT) that exports exactly the same entry points `Microsoft.Data.SqlClient` expects, implemented using `System.Net.Sockets.TcpClient` — which compiles to native code with no runtime dependency.

```
Mono (.NET Framework 4.8.1)
  → P/Invoke → Microsoft.Data.SqlClient.SNI.arm64.dylib  (OpenSNI, Native AOT)
                  → System.Net.Sockets (compiled to native)
                      → TCP → SQL Server
```

## Requirements

- macOS arm64 (Apple Silicon)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build

```bash
git clone https://github.com/your-org/OpenSNI.git
cd OpenSNI
dotnet publish -r osx-arm64 -c Release
```

Output:
```
bin/Release/net10.0/osx-arm64/publish/Microsoft.Data.SqlClient.SNI.arm64.dylib
```

## Installation

Tell Mono where to find the dylib by adding a `dllmap` entry to Mono's global config:

```bash
sudo nano /opt/homebrew/etc/mono/config
```

Add inside the `<configuration>` element:

```xml
<dllmap dll="Microsoft.Data.SqlClient.SNI.arm64"
        target="/path/to/OpenSNI/bin/Release/net10.0/osx-arm64/publish/Microsoft.Data.SqlClient.SNI.arm64.dylib"
        os="osx"/>
```

## Connection String

OpenSNI implements TCP connectivity only and does not support TLS/SSL encryption. Add the following to your SQL Server connection string when running on macOS:

```
Encrypt=false;TrustServerCertificate=true;
```

## Architecture

OpenSNI exports all 35 entry points that `Microsoft.Data.SqlClient` (netfx, arm64) P/Invokes via `[DllImport]`. The implementation:

- Parses the connection string passed via `SniClientConsumerInfo` (read as raw bytes to avoid managed struct pointer issues with Native AOT)
- Uses cached DNS info from the embedded `SniDnsCacheInfo` when available
- Manages connections and packets via a thread-safe handle registry (`ConcurrentDictionary`)
- Reads TDS packets by parsing the 8-byte header (bytes 2–3 = big-endian total length)
- Stubs out all TLS/SSL, Windows Authentication, Named Pipes, and server enumeration entry points

### Files

| File | Purpose |
|------|---------|
| `OpenSNI.csproj` | Native AOT project configuration |
| `Enums.cs` | All SNI enums matching `Microsoft.Data.SqlClient` source |
| `Structs.cs` | All SNI structs with exact memory layouts for arm64 |
| `Internals.cs` | `SniConnection`, `SniPacket`, `HandleRegistry` |
| `SNI.cs` | All 35 exported functions |

## Limitations

| Feature | Status |
|---------|--------|
| TCP connectivity | ✅ Supported |
| TLS/SSL encryption | ❌ Not supported (`Encrypt=false` required) |
| Windows Authentication (Kerberos/SSPI) | ❌ Not supported (SQL auth only) |
| Named Pipes | ❌ Not supported |
| Shared Memory | ❌ Not supported |
| Server enumeration | ❌ Stubbed (not needed for client connections) |

## Technical Notes

**Why Native AOT?**
Native AOT compiles to a standalone native dylib with no .NET runtime dependency. Mono sees it as a regular native library — there is no runtime conflict between Mono and the .NET 10 runtime because there is no .NET 10 runtime at all in the output.

**Why read `SniClientConsumerInfo` as `void*`?**
The struct contains managed string fields (`[MarshalAs(UnmanagedType.LPWStr)]`). Taking a pointer to a managed struct in a `[UnmanagedCallersOnly]` method causes a Native AOT crash. Instead, field values are read at known byte offsets calculated for arm64 (all pointers = 8 bytes).

**Why can't `[UnmanagedCallersOnly]` methods call each other?**
The C# compiler prohibits direct calls between `[UnmanagedCallersOnly]` methods. Private helper methods (`ReadImpl`, `WriteImpl`) contain the shared implementation, and both the sync and async exports delegate to those.

**Thread registration**
When Mono loads the dylib and calls an exported function, the calling thread is a Mono-managed thread not registered with the Native AOT runtime. The `<NativeLib>Shared</NativeLib>` project property enables hosting/embedding support in the dylib, which handles external thread attachment automatically.
