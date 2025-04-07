using System.Net;
using System.Net.Sockets;
using System.Text;
using BookHeaven.Domain.Constants;

namespace BookHeaven.Reader.Services;

public class UdpBroadcastClient(AppStateService appStateService)
{
    public async Task<string> StartAsync()
    {
        using var udpClient = new UdpClient
        {
            EnableBroadcast = true
        };
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, Broadcast.BROADCAST_PORT));

        var broadcastAddress = new IPEndPoint(IPAddress.Broadcast, Broadcast.BROADCAST_PORT);
        var discoverMessage = Encoding.UTF8.GetBytes(Broadcast.DISCOVER_MESSAGE);
        await udpClient.SendAsync(discoverMessage, discoverMessage.Length, broadcastAddress);

        try
        {
            while (true)
            {
                var result = await udpClient.ReceiveAsync();
                var responseMessage = Encoding.UTF8.GetString(result.Buffer);

                if (!responseMessage.StartsWith(Broadcast.SERVER_URL_MESSAGE_PREFIX))
                {
                    continue;
                }

                var parts = responseMessage.Split(':');

                var serverUrl = parts[1];

                // Enviar ACK al servidor
                var ackMessage = Encoding.UTF8.GetBytes(Broadcast.ACK_MESSAGE);
                await udpClient.SendAsync(ackMessage, ackMessage.Length, broadcastAddress);
                return serverUrl;
            }
        }
        catch (Exception ex)
        {
            return string.Empty;
        }
        
        
    }
}