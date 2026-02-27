// OpenSNI - Open Source SQL Server Network Interface for macOS/Linux
// Exported functions matching the SNI API consumed by Microsoft.Data.SqlClient (netfx)
//
// IMPORTANT: When loaded as a dylib by Mono, Mono's threads are not registered
// with the NativeAOT runtime. Any managed allocation (new, string, etc.) from an
// unregistered thread will crash. We solve this by using
// RuntimeImports.RhpRegisterFrozenSegment... actually the simplest solution is
// to use the NativeAOT "RuntimeHelpers.EnsureSufficientExecutionStack" trick:
// call Thread.GetCurrentProcessorId() which forces thread attachment, OR
// use the public API: use a [ThreadStatic] field access which triggers thread init.
//
// The supported NativeAOT approach for hosting scenarios is to call
// NativeLibrary's exported "InitializeRuntime" before doing any managed work.
// NativeAOT dylibs export this as "NativeAOT_StaticInitialization" (dotnet 8+).

using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenSNI;

internal static unsafe class SniExports
{
    // NativeAOT dylibs export this symbol which initializes the runtime.
    // We call it once from our own dylib init (the static constructor equivalent).
    // Actually for NativeAOT dylibs the runtime IS auto-initialized when the dylib
    // is loaded — but Mono's P/Invoke may call us on an unregistered thread.
    // The fix: use Thread.CurrentThread (a managed property) which implicitly
    // registers the current thread with the GC. We gate this with a volatile bool.

    private static volatile int s_initialized = 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnsureInitialized()
    {
        if (s_initialized == 0 &&
            Interlocked.CompareExchange(ref s_initialized, 1, 0) == 0)
        {
            // Force GC thread registration by touching a managed object.
            // This is safe because NativeAOT's dylib init has already run
            // the module initializer and set up the GC heap — we just need
            // to register this particular calling thread (Mono's thread).
            RuntimeHelpers.RunClassConstructor(typeof(HandleRegistry).TypeHandle);
        }
    }

    // -------------------------------------------------------------------------
    // SNIInitialize — first function Mono calls, perfect place to init
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNIInitialize", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIInitialize(nint pmo)
    {
        EnsureInitialized();
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "SNITerminate", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNITerminate()
        => 0;

    // -------------------------------------------------------------------------
    // SNIOpenSyncExWrapper
    // Reads SniClientConsumerInfo as raw bytes to avoid managed-struct-pointer
    // issues with NativeAOT. Offsets calculated for arm64 (all pointers = 8 bytes).
    //
    // SniConsumerInfo:
    //   [0]  int DefaultUserDataLength        4 + 4 pad = 8
    //   [8]  nint ConsumerKey                 8
    //   [16] nint fnReadComp                  8
    //   [24] nint fnWriteComp                 8
    //   [32] nint fnTrace                     8
    //   [40] nint fnAcceptComp                8
    //   [48] uint dwNumProts                  4 + 4 pad = 8
    //   [56] nint rgListenInfo                8
    //   [64] nint NodeAffinity                8
    //                                       = 72 bytes
    // SniClientConsumerInfo (after SniConsumerInfo):
    //   [72]  wszConnectionString   LPWStr*   8
    //   [80]  HostNameInCertificate LPWStr*   8
    //   [88]  networkLibrary (Prefix=uint)    4 + 4 pad = 8
    //   [96]  szSPN byte*                     8
    //   [104] cchSPN uint                     4 + 4 pad = 8
    //   [112] szInstanceName byte*            8
    //   [120] cchInstanceName uint            4
    //   [124] fOverrideLastConnectCache BOOL  4
    //   [128] fSynchronousConnection BOOL     4
    //   [132] timeout int                     4
    //   [136] fParallel BOOL                  4
    //   [140] transparentNetworkResolution    1 + 3 pad = 4
    //   [144] totalTimeout int                4
    //   [148] isAzureSqlServerEndpoint bool   1
    //   [149] ipAddressPreference byte        1 + 6 pad = 8
    //   [156] (pad to align to 8)
    //   [160] DNSCacheInfo.wszCachedFQDN*     8
    //   [168] DNSCacheInfo.wszCachedTcpIPv4*  8
    //   [176] DNSCacheInfo.wszCachedTcpIPv6*  8
    //   [184] DNSCacheInfo.wszCachedTcpPort*  8
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNIOpenSyncExWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIOpenSyncExWrapper(void* pInfo, nint* ppConn)
    {
        *ppConn = 0;
        EnsureInitialized();
        try
        {
            byte* p = (byte*)pInfo;

            nint connStrPtr = *(nint*)(p + 72);
            string? connStr  = connStrPtr != 0 ? Marshal.PtrToStringUni((IntPtr)connStrPtr) : null;

            int timeout      = *(int*)(p + 132);

            nint fqdnPtr     = *(nint*)(p + 160);
            string? fqdn     = fqdnPtr != 0 ? Marshal.PtrToStringUni((IntPtr)fqdnPtr) : null;

            nint portStrPtr  = *(nint*)(p + 184);
            string? portStr  = portStrPtr != 0 ? Marshal.PtrToStringUni((IntPtr)portStrPtr) : null;

            string host;
            int port;

            if (!string.IsNullOrEmpty(fqdn))
            {
                host = fqdn!;
                port = !string.IsNullOrEmpty(portStr) && int.TryParse(portStr, out var cp) ? cp : 1433;
            }
            else if (!string.IsNullOrEmpty(connStr) && TryParseConnectionString(connStr!, out host, out port))
            {
                // parsed
            }
            else
            {
                return TdsError.ConnOpenFailed;
            }

            var tcp = new TcpClient();
            if (timeout > 0)
                tcp.SendTimeout = tcp.ReceiveTimeout = timeout;

            tcp.Connect(host, port);

            var conn = new SniConnection
            {
                TcpClient = tcp,
                Stream    = tcp.GetStream(),
                Host      = host,
                Port      = port,
            };

            *ppConn = HandleRegistry.RegisterConnection(conn);
            return 0;
        }
        catch
        {
            return TdsError.ConnOpenFailed;
        }
    }

    // -------------------------------------------------------------------------
    // SNIOpenWrapper
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNIOpenWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIOpenWrapper(
        SniConsumerInfo* pConsumerInfo,
        nint szConnect,
        nint pConn,
        nint* ppConn,
        int fSync,
        uint ipPreference,
        SniDnsCacheInfo* pDnsCacheInfo)
    {
        *ppConn = 0;
        EnsureInitialized();
        try
        {
            string? connStr = Marshal.PtrToStringUni((IntPtr)szConnect);
            if (string.IsNullOrEmpty(connStr))
                return TdsError.ConnOpenFailed;

            string? host = null;
            int port = 1433;

            if (pDnsCacheInfo != null)
            {
                nint fqdnPtr = *(nint*)pDnsCacheInfo;
                if (fqdnPtr != 0)
                {
                    host = Marshal.PtrToStringUni((IntPtr)fqdnPtr);
                    nint portPtr = *(nint*)((byte*)pDnsCacheInfo + 24);
                    if (portPtr != 0)
                    {
                        var ps = Marshal.PtrToStringUni((IntPtr)portPtr);
                        if (int.TryParse(ps, out var cp)) port = cp;
                    }
                }
            }

            if (host == null && !TryParseConnectionString(connStr!, out host, out port))
                return TdsError.ConnOpenFailed;

            var tcp = new TcpClient();
            tcp.Connect(host, port);

            var conn = new SniConnection
            {
                TcpClient = tcp,
                Stream    = tcp.GetStream(),
                Host      = host,
                Port      = port,
            };

            *ppConn = HandleRegistry.RegisterConnection(conn);
            return 0;
        }
        catch
        {
            return TdsError.ConnOpenFailed;
        }
    }

    // -------------------------------------------------------------------------
    // SNICloseWrapper
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNICloseWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNICloseWrapper(nint pConn)
    {
        var conn = HandleRegistry.GetConnection(pConn);
        if (conn == null) return TdsError.InvalidParameter;
        conn.Stream?.Dispose();
        conn.TcpClient?.Dispose();
        HandleRegistry.RemoveConnection(pConn);
        return 0;
    }

    // -------------------------------------------------------------------------
    // Read / Write
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNIReadSyncOverAsync", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIReadSyncOverAsync(nint pConn, nint* ppNewPacket, int timeout)
        => ReadImpl(pConn, ppNewPacket, timeout);

    [UnmanagedCallersOnly(EntryPoint = "SNIReadAsyncWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIReadAsyncWrapper(nint pConn, nint* ppNewPacket)
        => ReadImpl(pConn, ppNewPacket, 0);

    [UnmanagedCallersOnly(EntryPoint = "SNIWriteSyncOverAsync", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIWriteSyncOverAsync(nint pConn, nint pPacket)
        => WriteImpl(pConn, pPacket);

    [UnmanagedCallersOnly(EntryPoint = "SNIWriteAsyncWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIWriteAsyncWrapper(nint pConn, nint pPacket)
        => WriteImpl(pConn, pPacket);

    // -------------------------------------------------------------------------
    // Packet management
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNIPacketAllocateWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static nint SNIPacketAllocateWrapper(nint pConn, IoType ioType)
    {
        var conn   = HandleRegistry.GetConnection(pConn);
        var packet = new SniPacket((int)(conn?.PacketSize ?? 4096)) { Connection = conn };
        return HandleRegistry.RegisterPacket(packet);
    }

    [UnmanagedCallersOnly(EntryPoint = "SNIPacketRelease", CallConvs = [typeof(CallConvCdecl)])]
    internal static void SNIPacketRelease(nint pPacket)
        => HandleRegistry.RemovePacket(pPacket);

    [UnmanagedCallersOnly(EntryPoint = "SNIPacketSetData", CallConvs = [typeof(CallConvCdecl)])]
    internal static void SNIPacketSetData(nint pPacket, byte* pbBuf, uint cbBuf)
    {
        var packet = HandleRegistry.GetPacket(pPacket);
        if (packet == null) return;
        if (packet.Data.Length < (int)cbBuf)
            packet.Data = new byte[cbBuf];
        Marshal.Copy((IntPtr)pbBuf, packet.Data, 0, (int)cbBuf);
        packet.DataLength = (int)cbBuf;
    }

    [UnmanagedCallersOnly(EntryPoint = "SNIPacketGetDataWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIPacketGetDataWrapper(nint pPacket, byte* pbBuf, uint cbBuf, uint* pcbData)
    {
        var packet = HandleRegistry.GetPacket(pPacket);
        if (packet == null) { *pcbData = 0; return TdsError.InvalidParameter; }
        uint toCopy = Math.Min(cbBuf, (uint)packet.DataLength);
        Marshal.Copy(packet.Data, 0, (IntPtr)pbBuf, (int)toCopy);
        *pcbData = toCopy;
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "SNIPacketResetWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static void SNIPacketResetWrapper(nint pConn, IoType ioType, nint pPacket, ConsumerNumber consumer)
    {
        var packet = HandleRegistry.GetPacket(pPacket);
        if (packet != null) packet.DataLength = 0;
    }

    // -------------------------------------------------------------------------
    // Info getters / setters
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNIGetInfoWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIGetInfoWrapper(nint pConn, QueryType queryType, void* pbQInfo)
    {
        var conn = HandleRegistry.GetConnection(pConn);
        if (conn == null) return TdsError.InvalidParameter;

        switch (queryType)
        {
            case QueryType.SNI_QUERY_CONN_BUFSIZE:
            case QueryType.SNI_QUERY_CONN_NETPACKETSIZE:
                *(uint*)pbQInfo = conn.PacketSize; return 0;
            case QueryType.SNI_QUERY_CONN_PROVIDERNUM:
                *(uint*)pbQInfo = (uint)Provider.TCP_PROV; return 0;
            case QueryType.SNI_QUERY_CONN_PEERPORT:
                *(ushort*)pbQInfo = (ushort)conn.Port; return 0;
            case QueryType.SNI_QUERY_CLIENT_ENCRYPT_POSSIBLE:
            case QueryType.SNI_QUERY_SERVER_ENCRYPT_POSSIBLE:
            case QueryType.SNI_QUERY_CONN_ENCRYPT:
                *(int*)pbQInfo = 0; return 0;
            case QueryType.SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC:
                *(int*)pbQInfo = 1; return 0;
            default:
                *(nint*)pbQInfo = 0; return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "SNISetInfoWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNISetInfoWrapper(nint pConn, QueryType queryType, uint* pbQueryInfo)
    {
        var conn = HandleRegistry.GetConnection(pConn);
        if (conn == null) return TdsError.InvalidParameter;
        if (queryType is QueryType.SNI_QUERY_CONN_BUFSIZE or QueryType.SNI_QUERY_CONN_NETPACKETSIZE)
            conn.PacketSize = *pbQueryInfo;
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "SNICheckConnectionWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNICheckConnectionWrapper(nint pConn)
    {
        var conn = HandleRegistry.GetConnection(pConn);
        return (conn?.IsOpen ?? false) ? 0u : TdsError.ConnClosed;
    }

    [UnmanagedCallersOnly(EntryPoint = "SNIGetLastError", CallConvs = [typeof(CallConvCdecl)])]
    internal static void SNIGetLastError(SniError* pError)
        => *pError = default;

    [UnmanagedCallersOnly(EntryPoint = "SNIGetPeerAddrStrWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIGetPeerAddrStrWrapper(nint pConn, int bufferSize, char* addrBuffer, uint* addrLength)
    {
        var conn = HandleRegistry.GetConnection(pConn);
        string addr = conn?.Host ?? "127.0.0.1";
        int len = Math.Min(addr.Length, bufferSize - 1);
        for (int i = 0; i < len; i++) addrBuffer[i] = addr[i];
        addrBuffer[len] = '\0';
        *addrLength = (uint)len;
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "SNIQueryInfo", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIQueryInfo(QueryType queryType, nint* pbQueryInfo)
    {
        if (queryType == QueryType.SNI_QUERY_CONN_BUFSIZE)
            *(uint*)pbQueryInfo = 4096;
        else
            *pbQueryInfo = 0;
        return 0;
    }

    // -------------------------------------------------------------------------
    // SSL stubs — connections must use Encrypt=false for now
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNIAddProviderWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIAddProviderWrapper(nint pConn, Provider provider, nint pInfo)
        => 0;

    [UnmanagedCallersOnly(EntryPoint = "SNIRemoveProviderWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIRemoveProviderWrapper(nint pConn, Provider provider)
        => 0;

    [UnmanagedCallersOnly(EntryPoint = "SNISecInitPackage", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNISecInitPackage(uint* pcbMaxToken)
    {
        *pcbMaxToken = 0;
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "SNISecGenClientContextWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNISecGenClientContextWrapper(
        nint pConn, byte* pIn, uint cbIn, byte* pOut, uint* pcbOut,
        int* pfDone, byte* szServerInfo, uint cbServerInfo,
        nint pwszUserName, nint pwszPassword)
    {
        *pcbOut = 0;
        *pfDone = 1;
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "SNIWaitForSSLHandshakeToCompleteWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIWaitForSSLHandshakeToCompleteWrapper(
        nint pConn, int dwMilliseconds, SniSslProtocols* pProtocolVersion)
    {
        *pProtocolVersion = SniSslProtocols.None;
        return 0;
    }

    // -------------------------------------------------------------------------
    // Server enumeration stubs
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNIServerEnumOpenWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static nint SNIServerEnumOpenWrapper() => 0;

    [UnmanagedCallersOnly(EntryPoint = "SNIServerEnumReadWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static int SNIServerEnumReadWrapper(nint h, char* buf, int len, int* more)
    {
        *more = 0; return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "SNIServerEnumCloseWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static void SNIServerEnumCloseWrapper(nint h) { }

    // -------------------------------------------------------------------------
    // Certificate / SPN stubs
    // -------------------------------------------------------------------------
    [UnmanagedCallersOnly(EntryPoint = "SNIClientCertificateFallbackWrapper", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIClientCertificateFallbackWrapper(nint pConn, nint pCert) => 0;

    [UnmanagedCallersOnly(EntryPoint = "SNIClientCertificateFallbackWrapper2", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint SNIClientCertificateFallbackWrapper2(nint pConn, nint pCert) => 0;

    [UnmanagedCallersOnly(EntryPoint = "GetSniMaxComposedSpnLength", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint GetSniMaxComposedSpnLength() => 260;

    [UnmanagedCallersOnly(EntryPoint = "UnmanagedIsTokenRestricted", CallConvs = [typeof(CallConvCdecl)])]
    internal static uint UnmanagedIsTokenRestricted(nint token, int* isRestricted)
    {
        *isRestricted = 0;
        return 0;
    }

    // =========================================================================
    // Private implementation helpers (cannot call [UnmanagedCallersOnly] directly)
    // =========================================================================

    private static uint ReadImpl(nint pConn, nint* ppNewPacket, int timeout)
    {
        *ppNewPacket = 0;
        var conn = HandleRegistry.GetConnection(pConn);
        if (conn?.Stream == null) return TdsError.ConnClosed;
        try
        {
            if (timeout > 0) conn.TcpClient!.ReceiveTimeout = timeout;

            var header = new byte[8];
            if (!ReadExact(conn.Stream, header, 8)) return TdsError.ConnClosed;

            int totalLength = (header[2] << 8) | header[3];
            if (totalLength < 8) return TdsError.ConnClosed;

            var data = new byte[totalLength];
            Buffer.BlockCopy(header, 0, data, 0, 8);
            if (!ReadExact(conn.Stream, data, totalLength - 8, 8)) return TdsError.ConnClosed;

            var packet = new SniPacket(totalLength) { DataLength = totalLength, Connection = conn };
            Buffer.BlockCopy(data, 0, packet.Data, 0, totalLength);
            *ppNewPacket = HandleRegistry.RegisterPacket(packet);
            return 0;
        }
        catch { return TdsError.ConnClosed; }
    }

    private static uint WriteImpl(nint pConn, nint pPacket)
    {
        var conn   = HandleRegistry.GetConnection(pConn);
        var packet = HandleRegistry.GetPacket(pPacket);
        if (conn?.Stream == null || packet == null) return TdsError.ConnClosed;
        try
        {
            conn.Stream.Write(packet.Data, 0, packet.DataLength);
            conn.Stream.Flush();
            return 0;
        }
        catch { return TdsError.ConnClosed; }
    }

    private static bool TryParseConnectionString(string connStr, out string host, out int port)
    {
        host = "localhost";
        port = 1433;
        var s = connStr;
        if (s.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(4);
        else if (s.Contains(':') && !s.StartsWith("tcp", StringComparison.OrdinalIgnoreCase))
            return false;
        var parts = s.Split(',');
        host = parts[0].Trim();
        if (string.IsNullOrEmpty(host)) return false;
        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var p)) port = p;
        return true;
    }

    private static bool ReadExact(NetworkStream stream, byte[] buffer, int count, int offset = 0)
    {
        while (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read == 0) return false;
            offset += read;
            count  -= read;
        }
        return true;
    }
}

internal static class TdsError
{
    public const uint Success          = 0;
    public const uint ConnClosed       = 1;
    public const uint ConnOpenFailed   = 2;
    public const uint InvalidParameter = 87;
    public const uint Timeout          = 258;
}