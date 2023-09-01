using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using UAManagedCore;

namespace ratc_web
{

    internal class ExtensionInfo
    {
        public string ContentType { get; set; }
        public Func<string, string, ExtensionInfo, ResponsePacket> Loader { get; set; }
    }

    public class ResponsePacket
    {
        public byte[] Data { get; set; }
        public string ContentType { get; set; }
        public Encoding Encoding { get; set; }
    }


    public class Router
    {
        public ResponsePacket ResponsePacket { get; set; }

        private Dictionary<string, ExtensionInfo> extFolderMap;

        public Router()
        {
            extFolderMap = new Dictionary<string, ExtensionInfo>()
            {                
              {"ico", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/ico"}},
              {"png", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/png"}},
              {"svg", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/svg+xml"}},
              {"jpg", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/jpg"}},
              {"gif", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/gif"}},
              {"bmp", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/bmp"}},
              {"html", new ExtensionInfo() {Loader=FileLoader, ContentType="text/html"}},
              {"css", new ExtensionInfo() {Loader=FileLoader, ContentType="text/css"}},
              {"js", new ExtensionInfo() {Loader=FileLoader, ContentType="text/javascript"}},
              {"", new ExtensionInfo() {Loader=FileLoader, ContentType="text/html"}},
            };
        }

        public Router(string path):this()
        { 
            var extension = Path.GetExtension(path).Replace(".","");
            ExtensionInfo ext = extFolderMap[extension];
            if (ext != null)
                ResponsePacket = ext.Loader(path, extension, ext);

        }



        private ResponsePacket ImageLoader(string fullPath, string ext, ExtensionInfo extInfo)
        {
            FileStream fStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fStream);
            ResponsePacket ret = new ResponsePacket() { Data = br.ReadBytes((int)fStream.Length), ContentType = extInfo.ContentType };
            br.Close();
            fStream.Close();

            return ret;
        }

        private ResponsePacket FileLoader(string fullPath, string ext, ExtensionInfo extInfo)
        {
            string text = File.ReadAllText(fullPath);
            ResponsePacket ret = new ResponsePacket() { Data = Encoding.UTF8.GetBytes(text), ContentType = extInfo.ContentType, Encoding = Encoding.UTF8 };

            return ret;
        }
    }
    public class HttpResponse{

        public string ContentType { get; set; } //= "text/html; charset=utf-8"
        public byte[] Content { get; private set; }
        public int length { get { return isContent ? Content.Length : 0; } }

        public bool isContent = false;
        public HttpResponse(): this(@"*/*")
        {
            
        }
        public HttpResponse(string contentType)
        {
            ContentType = contentType;
            Content = null;
        }
        public void setContent(string cont)
        {
            try
            {
                setContent(Encoding.UTF8.GetBytes(cont));
            }
            catch (Exception)
            {
                throw;
            }
        }
        public void setContent(Stream cont)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                cont.CopyTo(ms);
                setContent(ms.ToArray());
            }
            catch (Exception)
            {
                throw;
            }
        }      
        public void setContent(byte[] cont, string contentType = "*/*")
        {
            try
            {
                ContentType = contentType;
                Content = cont;
                isContent = true;
            }
            catch (Exception)
            {
                throw;
            }
        }

    }
    
    
    public class RestRequestEventArgs : EventArgs
    {
        public object Data { get; private set; }
        public string ContentType { get; private set; }
        public HttpListenerRequest req { get; private set; }
        public HttpResponse res { get; private set; }
        public string Filename { get; private set; }
        public bool isFile { get; private set; }
        public string Path { get; private set; }
        public string PathFile { get; private set; }
        private RestRequestEventArgs() { }
        public RestRequestEventArgs(object data, HttpListenerRequest _req, HttpResponse _res, string path)
        {
            try
            {
                isFile = false;
                PathFile = "";
                Filename = "";
                Data = data;
                ContentType = _req.ContentType;
                req = _req;
                res = _res;
                int idx = path.IndexOf(@"/");
                if (idx == 0)
                    path = path.Remove(idx, 1);
                idx = path.LastIndexOf(@"/");
                if (idx != -1 && idx == path.Length - 1)
                    path = path.Remove(idx, 1);

                Filename = System.IO.Path.GetFileName(path);
                Filename = Filename.Contains(".") ? Filename : "";
                Filename = Filename == "" &&  _req.AcceptTypes != null && _req.AcceptTypes.Contains("text/html") ? "index.html" : Filename;

                Path = path;
                if (Filename != "")
                {
                    Path = path.Replace(Filename, "");
                    isFile = true;
                    PathFile = System.IO.Path.Combine(Path, Filename);
                }
 

            }
            catch (Exception ex)
            {

                throw ex;
            }

            

              
        }
    }

    public class MsgRecivedEventArgs : EventArgs
    {
        public ArraySegment<byte> SourceData { get; private set; }
        public object Data { get; private set; }
        public WebSocketMessageType MessageType { get; private set; }
        public string Message { get; private set; }
        public ConnectedClient Client { get; private set; }
        public WebSocketReceiveResult ReceiveResult { get; private set; }
        private MsgRecivedEventArgs() { }
        public MsgRecivedEventArgs(ArraySegment<byte> data, WebSocketReceiveResult receiveResult, ConnectedClient client)
        {
            SourceData = data;
            ReceiveResult = receiveResult;
            Client = client;
            MessageType = receiveResult.MessageType;

            Message = receiveResult.MessageType == WebSocketMessageType.Text ? Encoding.UTF8.GetString(data.Array, 0, receiveResult.Count) : "";
            //Data = receiveResult.MessageType == WebSocketMessageType.Binary ? data.Array.Take(receiveResult.Count) : null;
            Data = data.Array.Take(receiveResult.Count);

        }
    }




    public delegate void PostRequestEventHandler(object sender, RestRequestEventArgs e);

    public delegate void MsgRecivedEventHandler(object sender, MsgRecivedEventArgs e);

    public delegate void NewClientAvailableEventHandler(object sender, ConnectedClient e);



    public class WebSocketServer
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public event PostRequestEventHandler RestRequestEventArgs;
        public event MsgRecivedEventHandler MsgRecivedEvent;
        public event NewClientAvailableEventHandler NewClientAvailableEvent;

        public const int TIMESTAMP_INTERVAL_SEC = 15;
        public const int BROADCAST_TRANSMIT_INTERVAL_MS = 250;
        public const int CLOSE_SOCKET_TIMEOUT_MS = 2500;
        // note that Microsoft plans to deprecate HttpListener,
        // and for .NET Core they don't even support SSL/TLS
        private  HttpListener Listener;

        private CancellationTokenSource SocketLoopTokenSource;
        private CancellationTokenSource ListenerLoopTokenSource;

        private int SocketCounter = 0;

        private bool serverIsRunning = false;
        public bool ServerIsRunning { 
            get { return serverIsRunning; } 
            set { 
                serverIsRunning = value;
                if (this.PropertyChanged != null)
                    PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("ServerIsRunning"));
            }
        }

        private string publicFolder = @"C:\web";
        public string PublicFolder { 
            get { return publicFolder; } 
            set { 
                publicFolder = value;
                if (this.PropertyChanged != null)
                    PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("PublicFolder"));
            }
        }
        

        public int ClientCount {
            get { return Clients.Count; }
        }

        private Func<RestRequestEventArgs, int> app = null;
        

        // The key is a socket id
        private static ConcurrentDictionary<int, ConnectedClient> Clients = new ConcurrentDictionary<int, ConnectedClient>();
        public WebSocketServer(Func<RestRequestEventArgs, int> _app = null)
        {
            this.app = _app;
        }

        public void Start(string uriPrefix)
        {
            SocketLoopTokenSource = new CancellationTokenSource();
            ListenerLoopTokenSource = new CancellationTokenSource();
           
            Listener = new HttpListener();
            Listener.Prefixes.Add(uriPrefix);
            Listener.Start();
            
            if (Listener.IsListening)
            {
                ServerIsRunning = true;
                LogInfo("Connect browser for a basic web page.");
                LogInfo($"Server listening: {uriPrefix}");
                // listen on a separate thread so that Listener.Stop can interrupt GetContextAsync
                Task.Run(() => ListenerProcessingLoopAsync().ConfigureAwait(false));
            }
            else
            {
                ServerIsRunning = false;
                LogError("Server failed to start.");
            }
        }
        public async Task StopAsync()
        {
            if (Listener?.IsListening ?? false && ServerIsRunning)
            {
                LogInfo("\nServer is stopping.");

                ServerIsRunning = false;            // prevent new connections during shutdown
                await CloseAllSocketsAsync();            // also cancels processing loop tokens (abort ReceiveAsync)
                ListenerLoopTokenSource.Cancel();   // safe to stop now that sockets are closed
                Listener.Stop();
                Listener.Close();
            }
        }
        public void Broadcast(string message, string channel = "")
        {
            if (!ServerIsRunning)
            {
                LogError($"Error Server not running Broadcast: {message}");
                return;
            }

            LogInfo($"Broadcast: {message}");
            foreach (var item in Clients)
            {
                if(channel == string.Empty || @"/" + channel == item.Value.RequestUri.AbsolutePath)
                item.Value.BroadcastQueue.Add(message);
            }
        }
        public void Broadcast(byte[] bytes, string channel = "")
        {
            if (!ServerIsRunning)
            {
                LogError($"Error Server not running Broadcast:");
                return;
            }

            //LogInfo($"Broadcast: Bytes");
            foreach (var item in Clients)
            {
                if (channel == string.Empty || @"/" + channel == item.Value.RequestUri.AbsolutePath)
                    item.Value.BroadcastQueueByte.Add(bytes);
            }
        }
        public void Unicast(int id,string message, string channel = "")
        {
            if (!ServerIsRunning)
            {
                LogError($"Error Server not running Unicast: {message}");
                return;
            }

            //LogInfo($"Unicast: {message}");
            var client = Clients[id];
            if (client == null)
                return;

            if(channel == string.Empty || @"/" + channel == client.RequestUri.AbsolutePath)
                client.BroadcastQueue.Add(message);
        }
        public void Unicast(int id,byte[] bytes, string channel = "")
        {
            if (!ServerIsRunning)
            {
                LogError($"Error Server not running Unicast");
                return;
            }

            //LogInfo($"Unicast: Bytes");
            var client = Clients[id];
            if (client == null)
                return;

            if (channel == string.Empty || @"/" + channel == client.RequestUri.AbsolutePath)
                client.BroadcastQueueByte.Add(bytes);
        }
        public void BroadcastWithoutSender(int id, string message, string channel = "")
        {
            if (!ServerIsRunning)
            {
                LogError($"Error Server not running BroadcastWithoutSender: {message}");
                return;
            }

            //LogInfo($"BroadcastWithoutSender: {message}");
            var client = Clients[id];
            foreach (var item in Clients)
            {
                if(item.Value.SocketId != id && (channel == string.Empty || @"/" + channel == item.Value.RequestUri.AbsolutePath))
                    item.Value.BroadcastQueue.Add(message);
            }
        }
        public void BroadcastWithoutSender(int id, byte[] bytes, string channel = "")
        {
            if (!ServerIsRunning)
            {
                LogError($"Error Server not running BroadcastWithoutSender");
                return;
            }

            //LogInfo($"Broadcast: Bytes");
            foreach (var item in Clients)
            {
                if (item.Value.SocketId != id && (channel == string.Empty || @"/" + channel == item.Value.RequestUri.AbsolutePath))
                    item.Value.BroadcastQueueByte.Add(bytes);
            }
        }
        private async Task ListenerProcessingLoopAsync()
        {
            var cancellationToken = ListenerLoopTokenSource.Token;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    HttpListenerContext context = await Listener.GetContextAsync();
                    if (ServerIsRunning)
                    {
                        if (context.Request.IsWebSocketRequest)
                        {
                            // HTTP is only the initial connection; upgrade to a client-specific websocket
                            HttpListenerWebSocketContext wsContext = null;
                            try
                            {
                                wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                                int socketId = Interlocked.Increment(ref SocketCounter);
                                var client = new ConnectedClient(socketId, wsContext.WebSocket, wsContext.RequestUri);
                                Clients.TryAdd(socketId, client);
                                if (this.PropertyChanged != null)
                                    PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("ClientCount"));

                                if (this.NewClientAvailableEvent != null)
                                    NewClientAvailableEvent(this, client);

                                LogInfo($"Socket {socketId}: New connection.");
                                _ = Task.Run(() => SocketProcessingLoopAsync(client).ConfigureAwait(false));
                            }
                            catch (Exception)
                            {
                                // server error if upgrade from HTTP to WebSocket fails
                                context.Response.StatusCode = 500;
                                context.Response.StatusDescription = "WebSocket upgrade failed";
                                context.Response.Close();
                                return;
                            }
                        }
                        else
                        {
                            if(context.Request.HttpMethod != "")
                            {
                                object data = null;
                                var query = context.Request.QueryString;
                                
                                MemoryStream ms = new MemoryStream();
                                context.Request.InputStream.CopyTo(ms);
                                data = ms.ToArray();                               
                                string[] subs = context.Request.RawUrl.Split('?');

                                context.Response.StatusCode = 200;
                                context.Response.StatusDescription = "OK";
                                var appResult = 0;
                                HttpResponse res = new HttpResponse();
                                var RestRequest = new RestRequestEventArgs(data, context.Request, res, subs[0]);
                                if (app != null)
                                    appResult = app(RestRequest);


                                //only if no data in response
                                if (appResult == 0)
                                {
                                    string file = PublicFolder + @"/" + RestRequest.PathFile;
                                    if (RestRequest.isFile && !File.Exists(file))
                                    {
                                        if (context.Request.AcceptTypes.Contains("text/html"))
                                        {
                                            if (RestRequest.Filename == "index.html")
                                                res.setContent(Encoding.UTF8.GetBytes(StartPageHtml.HTML));
                                            else
                                                res.setContent(Encoding.UTF8.GetBytes("<HTML><BODY>Error find path! " + subs[0] + "</BODY></HTML>"));
                                            appResult = 1;
                                        }
                                    }
                                    else
                                    {
                                        Router router = new Router(file);
                                        //res.setContent(File.ReadAllBytes(file));
                                        res.setContent(router.ResponsePacket.Data, router.ResponsePacket.ContentType);
                                        appResult = 1;
                                    }

                                }


                                bool close = false;
                                switch (appResult)
                                {
                                    case -1:
                                        context.Response.StatusCode = 500;
                                        context.Response.StatusDescription = "Internal Server Error";
                                        close = true;
                                        break;
                                    case 0:
                                        context.Response.StatusCode = 404;
                                        context.Response.StatusDescription =  "Not Found";
                                        close = true;
                                        break;
                                        
                                    case 409:
                                        context.Response.StatusCode = 409;
                                        context.Response.StatusDescription = "The request could not be completed due to a conflict with the current state of the resource.";
                                        break;
                                    default:
                                        
                                        break;
                                }

                                //byte[] b; //= { 0 };
                                context.Response.ContentType = res.ContentType;
                                context.Response.ContentLength64 = res.length;// b.Length;
                                context.Response.OutputStream.Write(res.Content);
                                if (!context.Request.KeepAlive || close)
                                    context.Response.Close();

                            }
 
                            else
                            {
                                context.Response.StatusCode = 400;
                                context.Response.Close();
                            }
                            
                        }
                    }
                    else
                    {
                        // HTTP 409 Conflict (with server's current state)
                        context.Response.StatusCode = 409;
                        context.Response.StatusDescription = "Server is shutting down";
                        context.Response.Close();
                        return;
                    }
                }
            }
            catch (HttpListenerException ex) when (ServerIsRunning)
            {
                LogError("Exception: " +  ex.Message);
            }
        }
        private async Task SocketProcessingLoopAsync(ConnectedClient client)
        {
            _ = Task.Run(() => client.BroadcastLoopAsync().ConfigureAwait(false));

            var socket = client.Socket;
            var loopToken = SocketLoopTokenSource.Token;
            var broadcastTokenSource = client.BroadcastLoopTokenSource; // store a copy for use in finally block
            try
            {
                var buffer = WebSocket.CreateServerBuffer(4096);
                while (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted && !loopToken.IsCancellationRequested)
                {
                    var receiveResult = await client.Socket.ReceiveAsync(buffer, loopToken);
                    // if the token is cancelled while ReceiveAsync is blocking, the socket state changes to aborted and it can't be used
                    if (!loopToken.IsCancellationRequested)
                    {
                        // the client is notifying us that the connection will close; send acknowledgement
                        if (client.Socket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            LogInfo($"Socket {client.SocketId}: Acknowledging Close frame received from client");
                            broadcastTokenSource.Cancel();
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
                            // the socket state changes to closed at this point
                        }

                        // echo text or binary data to the broadcast queue
                        if (client.Socket.State == WebSocketState.Open)
                        {
                            LogInfo($"Socket {client.SocketId}: Received {receiveResult.MessageType} frame ({receiveResult.Count} bytes).");
                            LogInfo($"Socket {client.SocketId}: Echoing data to queue.");
                            string message = Encoding.UTF8.GetString(buffer.Array, 0, receiveResult.Count);
                            if (this.MsgRecivedEvent != null)
                                MsgRecivedEvent(this, new MsgRecivedEventArgs(buffer,receiveResult,client));
                            //client.BroadcastQueue.Add(message);
                            //BroadcastWithoutSender(client.SocketId, message);

                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal upon task/token cancellation, disregard
            }
            catch (Exception ex)
            {
                LogError($"Socket {client.SocketId}:");
                LogError("Exception: " + ex.Message);
            }
            finally
            {
                broadcastTokenSource.Cancel();

                LogInfo($"Socket {client.SocketId}: Ended processing loop in state {socket.State}");

                // don't leave the socket in any potentially connected state
                if (client.Socket.State != WebSocketState.Closed)
                    client.Socket.Abort();

                // by this point the socket is closed or aborted, the ConnectedClient object is useless
                if (Clients.TryRemove(client.SocketId, out _))
                {

                    if (this.PropertyChanged != null)
                        PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("ClientCount"));
                    socket.Dispose();
                }
            }
        }
        private async Task CloseAllSocketsAsync()
        {
            // We can't dispose the sockets until the processing loops are terminated,
            // but terminating the loops will abort the sockets, preventing graceful closing.
            var disposeQueue = new List<WebSocket>(Clients.Count);

            while (Clients.Count > 0)
            {
                var client = Clients.ElementAt(0).Value;
                LogInfo($"Closing Socket {client.SocketId}");

                LogInfo("... ending broadcast loop");
                client.BroadcastLoopTokenSource.Cancel();

                if (client.Socket.State != WebSocketState.Open)
                {
                    LogInfo($"... socket not open, state = {client.Socket.State}");
                }
                else
                {
                    var timeout = new CancellationTokenSource(CLOSE_SOCKET_TIMEOUT_MS);
                    try
                    {
                        LogInfo("... starting close handshake");
                        await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
                    }
                    catch (OperationCanceledException ex)
                    {
                        LogError("Exception: " + ex.Message);
                        // normal upon task/token cancellation, disregard
                    }
                }

                if (Clients.TryRemove(client.SocketId, out _))
                {

                    if (this.PropertyChanged != null)
                        PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("ClientCount"));
                    // only safe to Dispose once, so only add it if this loop can't process it again
                    disposeQueue.Add(client.Socket);
                }

                LogInfo("... done");
            }

            // now that they're all closed, terminate the blocking ReceiveAsync calls in the SocketProcessingLoop threads
            SocketLoopTokenSource.Cancel();

            // dispose all resources
            foreach (var socket in disposeQueue)
                socket.Dispose();
        }
        private void LogInfo(string msg)
        {
            Log.Info(msg);
        }
        private void LogError(string msg)
        {
            Log.Error(msg);
        }

    }
}
