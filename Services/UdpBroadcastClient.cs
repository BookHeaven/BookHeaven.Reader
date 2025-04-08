using System.Net;
using System.Net.Sockets;
using System.Text;
using BookHeaven.Domain.Constants;
#if ANDROID31_0_OR_GREATER
using Android.App;
using Android.Content;
using Android.Net;
using System.Net;
using Java.Net;
#elif ANDROID
using Android.App;
using Android.Net.Wifi;
#endif

namespace BookHeaven.Reader.Services;

public class UdpBroadcastClient(AppStateService appStateService)
{
    public async Task<string> StartAsync()
    {
        using var udpClient = new UdpClient
        {
            EnableBroadcast = true
        };
        var ip = IPAddress.Parse("192.168.68.250");
#if ANDROID31_0_OR_GREATER
        var connectivityManager = (ConnectivityManager)Android.App.Application.Context.GetSystemService(Context.ConnectivityService)!;
        var activeNetwork = connectivityManager?.ActiveNetwork;
        var linkProperties = connectivityManager?.GetLinkProperties(activeNetwork);
        if (linkProperties != null)
        {
            foreach (var linkAddress in linkProperties.LinkAddresses)
            {
                if (linkAddress?.Address is Inet4Address)
                {
                    ip = IPAddress.Parse(linkAddress.Address.HostAddress!);
                    break;
                }
            }
        }
#elif ANDROID
        var wifiManager = (WifiManager)Android.App.Application.Context.GetSystemService(Service.WifiService);
        
        ip = new IPAddress(wifiManager.ConnectionInfo.IpAddress);
#endif
        
        udpClient.Client.Bind(new IPEndPoint(ip, Broadcast.BROADCAST_PORT));

        var message = $"{Broadcast.DISCOVER_MESSAGE_PREFIX}{ip}";
        
        var broadcastAddress = new IPEndPoint(IPAddress.Broadcast, Broadcast.BROADCAST_PORT);
        var discoverMessage = Encoding.UTF8.GetBytes(message);
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

                var serverUrl = responseMessage[Broadcast.SERVER_URL_MESSAGE_PREFIX.Length..];

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