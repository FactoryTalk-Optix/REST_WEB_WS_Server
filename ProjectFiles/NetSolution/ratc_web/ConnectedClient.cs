using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ratc_web
{
 

    public class ConnectedClient
    {

        public ConnectedClient(int socketId, WebSocket socket, Uri requestUri = null)
        {
            SocketId = socketId;
            Socket = socket;
            RequestUri = requestUri;
            Channel = requestUri.AbsolutePath;
            int idx = Channel.IndexOf(@"/");
            if (idx == 0)
                Channel = Channel.Remove(idx, 1);
           
        }


        public int SocketId { get; private set; }

        public Uri RequestUri { get; private set; }

        public WebSocket Socket { get; private set; }

        public string Channel { get; private set; }

        public BlockingCollection<string> BroadcastQueue { get; } = new BlockingCollection<string>();

        public BlockingCollection<byte[]> BroadcastQueueByte { get; } = new BlockingCollection<byte[]>();

        public CancellationTokenSource BroadcastLoopTokenSource { get; set; } = new CancellationTokenSource();

        public async Task BroadcastLoopAsync()
        {
            var cancellationToken = BroadcastLoopTokenSource.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    bool wait = false;
                    await Task.Delay(WebSocketServer.BROADCAST_TRANSMIT_INTERVAL_MS, cancellationToken);
                    if (!cancellationToken.IsCancellationRequested && Socket.State == WebSocketState.Open && BroadcastQueue.TryTake(out var message))
                    {
                        wait = true;
                        //await Task.Delay(WebSocketServer.BROADCAST_TRANSMIT_INTERVAL_MS, cancellationToken);
                        Console.WriteLine($"Socket {SocketId}: Sending from queue.");
                        var msgbuf = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                        await Socket.SendAsync(msgbuf, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
                    }

                    if (!cancellationToken.IsCancellationRequested && Socket.State == WebSocketState.Open && BroadcastQueueByte.TryTake(out byte[] data))
                    {
                        if (wait)
                            await Task.Delay(WebSocketServer.BROADCAST_TRANSMIT_INTERVAL_MS, cancellationToken);
                        Console.WriteLine($"Socket {SocketId}: Sending from queue.");
                        var msgbuf = new ArraySegment<byte>(data, 0, data.Length);
                    }


                }
                catch (OperationCanceledException)
                {
                    // normal upon task/token cancellation, disregard
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: {0}", ex);
                }
            }
        }

    }

}
