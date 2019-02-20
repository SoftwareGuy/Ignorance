using System;

public interface IDissonanceTransport
{
    bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data);
    bool ClientSend(int channelId, ArraySegment<byte> data);
}
