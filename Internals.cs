// OpenSNI - Open Source SQL Server Network Interface for macOS/Linux
// Internal connection and packet management

using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace OpenSNI;

internal class SniConnection
{
    public TcpClient? TcpClient;
    public NetworkStream? Stream;
    public string Host = string.Empty;
    public int Port = 1433;
    public uint PacketSize = 4096;
    public bool IsOpen => TcpClient?.Connected ?? false;
}

internal class SniPacket
{
    public byte[] Data;
    public int DataLength;
    public SniConnection? Connection;

    public SniPacket(int size)
    {
        Data = new byte[size];
        DataLength = 0;
    }
}

internal static class HandleRegistry
{
    private static readonly ConcurrentDictionary<nint, SniConnection> s_connections = new();
    private static readonly ConcurrentDictionary<nint, SniPacket> s_packets = new();
    private static long s_nextHandle = 0x10000;

    public static nint RegisterConnection(SniConnection conn)
    {
        var handle = (nint)Interlocked.Increment(ref s_nextHandle);
        s_connections[handle] = conn;
        return handle;
    }

    public static SniConnection? GetConnection(nint handle) =>
        s_connections.TryGetValue(handle, out var c) ? c : null;

    public static void RemoveConnection(nint handle) =>
        s_connections.TryRemove(handle, out _);

    public static nint RegisterPacket(SniPacket packet)
    {
        var handle = (nint)Interlocked.Increment(ref s_nextHandle);
        s_packets[handle] = packet;
        return handle;
    }

    public static SniPacket? GetPacket(nint handle) =>
        s_packets.TryGetValue(handle, out var p) ? p : null;

    public static void RemovePacket(nint handle) =>
        s_packets.TryRemove(handle, out _);
}