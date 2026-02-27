// OpenSNI - Open Source SQL Server Network Interface for macOS/Linux
// Struct definitions matching Interop.Windows.Sni in Microsoft.Data.SqlClient

using System;
using System.Runtime.InteropServices;

namespace OpenSNI;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct SniError
{
    internal Provider provider;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
    internal string errorMessage;
    internal int nativeError;
    internal uint sniError;
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? fileName;
    [MarshalAs(UnmanagedType.LPWStr)]
    internal string? function;
    internal uint lineNumber;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SniConsumerInfo
{
    public int DefaultUserDataLength;
    public nint ConsumerKey;
    public nint fnReadComp;
    public nint fnWriteComp;
    public nint fnTrace;
    public nint fnAcceptComp;
    public uint dwNumProts;
    public nint rgListenInfo;
    public nint NodeAffinity;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct AuthProviderInfo
{
    public uint flags;
    [MarshalAs(UnmanagedType.Bool)]
    public bool tlsFirst;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? certId;
    [MarshalAs(UnmanagedType.Bool)]
    public bool certHash;
    public object? cert;
    [MarshalAs(UnmanagedType.Bool)]
    public bool enforceRevocation;
    [MarshalAs(UnmanagedType.Bool)]
    public bool ignoreSniEndpointName;
    public uint tlsCompressionType;
}

// Exact match of SniClientConsumerInfo.cs from SqlClient source
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SniClientConsumerInfo
{
    public SniConsumerInfo ConsumerInfo;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? wszConnectionString;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? HostNameInCertificate;
    public Prefix networkLibrary;
    public byte* szSPN;
    public uint cchSPN;
    public byte* szInstanceName;
    public uint cchInstanceName;
    [MarshalAs(UnmanagedType.Bool)]
    public bool fOverrideLastConnectCache;
    [MarshalAs(UnmanagedType.Bool)]
    public bool fSynchronousConnection;
    public int timeout;
    [MarshalAs(UnmanagedType.Bool)]
    public bool fParallel;
    public TransparentNetworkResolutionMode transparentNetworkResolution;
    public int totalTimeout;
    public bool isAzureSqlServerEndpoint;
    public SqlConnectionIPAddressPreference ipAddressPreference;
    public SniDnsCacheInfo DNSCacheInfo;  // embedded, not a pointer!
}

// Exact match of SniDnsCacheInfo.cs from SqlClient source
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct SniDnsCacheInfo
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? wszCachedFQDN;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? wszCachedTcpIPv4;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? wszCachedTcpIPv6;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? wszCachedTcpPort;
}