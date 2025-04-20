using System.Net;
using System.Net.Sockets;
using System.Text;
using BookHeaven.Domain.Constants;
using BookHeaven.Domain.Shared;
#if ANDROID31_0_OR_GREATER
using Android.App;
using Android.Content;
using Android.Net;
using Java.Net;
#elif ANDROID
using Android.App;
using Android.Net.Wifi;
#endif

namespace BookHeaven.Reader.Services;

public class UdpBroadcastClient
{
    public async Task<Result<string>> StartAsync()
    {
        if(Connectivity.Current.NetworkAccess == NetworkAccess.None)
        {
            return new Error("No internet connection");
        }
        
        IPAddress? ip = null;
#if DEBUG
        ip = IPAddress.Parse("192.168.68.250");
#endif
        
#if ANDROID31_0_OR_GREATER
        var connectivityManager = (ConnectivityManager)Android.App.Application.Context.GetSystemService(Context.ConnectivityService)!;
        var activeNetwork = connectivityManager?.ActiveNetwork;
        var linkProperties = connectivityManager?.GetLinkProperties(activeNetwork);
        if (linkProperties != null)
        {
            foreach (var linkAddress in linkProperties.LinkAddresses)
            {
                if (linkAddress.Address is not Inet4Address) continue;

                ip = IPAddress.Parse(linkAddress.Address.HostAddress!);
                break;
            }
        }
#elif ANDROID
        var wifiManager = (WifiManager)Android.App.Application.Context.GetSystemService(Service.WifiService);
        
        ip = new IPAddress(wifiManager.ConnectionInfo.IpAddress);
#endif
        if (ip is null)
        {
            return new Error("Unable to get local IP address");
        }
        
        using var udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;

        udpClient.Client.Bind(new IPEndPoint(ip, Broadcast.BROADCAST_PORT));

        var message = $"{Broadcast.DISCOVER_MESSAGE_PREFIX}{ip}";

        var broadcastAddress = new IPEndPoint(IPAddress.Broadcast, Broadcast.BROADCAST_PORT);
        var discoverMessage = Encoding.UTF8.GetBytes(message);
        await udpClient.SendAsync(discoverMessage, discoverMessage.Length, broadcastAddress);

        try
        {
            while (true)
            {
                var task = udpClient.ReceiveAsync();

                var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
                if (completedTask != task)
                {
                    return new Error("No response from server");
                }

                var result = task.Result;
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
            return new Error("Unknown error while connecting the to server");
        }
    }
}