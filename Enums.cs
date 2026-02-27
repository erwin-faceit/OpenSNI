// OpenSNI - Open Source SQL Server Network Interface for macOS/Linux
// Enum definitions matching Interop.Windows.Sni in Microsoft.Data.SqlClient

using System;

namespace OpenSNI;

internal enum Provider : uint
{
    HTTP_PROV    = 0,
    NP_PROV      = 1,
    SESSION_PROV = 2,
    SIGN_PROV    = 3,
    SM_PROV      = 4,
    SMUX_PROV    = 5,
    SSL_PROV     = 6,
    TCP_PROV     = 7,
    VIA_PROV     = 8,
    CTAIP_PROV   = 9,  // NETFRAMEWORK only
    MAX_PROVS    = 10,
    INVALID_PROV = 11,
}

internal enum QueryType : uint
{
    SNI_QUERY_CONN_INFO                              = 0,
    SNI_QUERY_CONN_BUFSIZE                           = 1,
    SNI_QUERY_CONN_KEY                               = 2,
    SNI_QUERY_CLIENT_ENCRYPT_POSSIBLE                = 3,
    SNI_QUERY_SERVER_ENCRYPT_POSSIBLE                = 4,
    SNI_QUERY_CERTIFICATE                            = 5,
    SNI_QUERY_LOCALDB_HMODULE                        = 6,
    SNI_QUERY_CONN_ENCRYPT                           = 7,
    SNI_QUERY_CONN_PROVIDERNUM                       = 8,
    SNI_QUERY_CONN_CONNID                            = 9,
    SNI_QUERY_CONN_PARENTCONNID                      = 10,
    SNI_QUERY_CONN_SECPKG                            = 11,
    SNI_QUERY_CONN_NETPACKETSIZE                     = 12,
    SNI_QUERY_CONN_NODENUM                           = 13,
    SNI_QUERY_CONN_PACKETSRECD                       = 14,
    SNI_QUERY_CONN_PACKETSSENT                       = 15,
    SNI_QUERY_CONN_PEERADDR                          = 16,
    SNI_QUERY_CONN_PEERPORT                          = 17,
    SNI_QUERY_CONN_LASTREADTIME                      = 18,
    SNI_QUERY_CONN_LASTWRITETIME                     = 19,
    SNI_QUERY_CONN_CONSUMER_ID                       = 20,
    SNI_QUERY_CONN_CONNECTTIME                       = 21,
    SNI_QUERY_CONN_HTTPENDPOINT                      = 22,
    SNI_QUERY_CONN_LOCALADDR                         = 23,
    SNI_QUERY_CONN_LOCALPORT                         = 24,
    SNI_QUERY_CONN_SSLHANDSHAKESTATE                 = 25,
    SNI_QUERY_CONN_SOBUFAUTOTUNING                   = 26,
    SNI_QUERY_CONN_SECPKGNAME                        = 27,
    SNI_QUERY_CONN_SECPKGMUTUALAUTH                  = 28,
    SNI_QUERY_CONN_CONSUMERCONNID                    = 29,
    SNI_QUERY_CONN_SNIUCI                            = 30,
    SNI_QUERY_CONN_SUPPORTS_EXTENDED_PROTECTION      = 31,
    SNI_QUERY_CONN_CHANNEL_PROVIDES_AUTHENTICATION_CONTEXT = 32,
    SNI_QUERY_CONN_PEERID                            = 33,
    SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC          = 34,
    SNI_QUERY_CONN_SSL_SECCTXTHANDLE                 = 35,  // NETFRAMEWORK only
}

internal enum IoType : uint
{
    READ  = 0,
    WRITE = 1,
}

internal enum ConsumerNumber : uint
{
    SNI_Consumer_SNI      = 0,
    SNI_Consumer_SSB      = 1,
    SNI_Consumer_PacketIsReleased = 2,
    SNI_Consumer_Invalid  = 3,
}

internal enum Prefix : uint
{
    UNKNOWN_PREFIX = 0,
    SM_PREFIX      = 1,
    TCP_PREFIX     = 2,
    NP_PREFIX      = 3,
    VIA_PREFIX     = 4,
    INVALID_PREFIX = 5,
}

internal enum TransparentNetworkResolutionMode : byte
{
    DisabledMode = 0,
    SequentialMode,
    ParallelMode,
}

internal enum SqlConnectionIPAddressPreference : byte
{
    IPv4First = 0,
    IPv6First = 1,
    UsePlatformDefault = 2,
}

internal enum SniSslProtocols : uint
{
    None   = 0,
    Ssl2   = 12,
    Ssl3   = 48,
    Tls10  = 192,
    Tls11  = 768,
    Tls12  = 3072,
    Tls13  = 12288,
}