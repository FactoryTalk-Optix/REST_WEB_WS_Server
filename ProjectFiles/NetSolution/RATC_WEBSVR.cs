#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.UI;
using FTOptix.Retentivity;
using FTOptix.NativeUI;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.NetLogic;
using FTOptix.CommunicationDriver;
using FTOptix.RAEtherNetIP;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using FTOptix.Alarm;
using FTOptix.EventLogger;
using FTOptix.Store;
using FTOptix.SQLiteStore;
#endregion

public class RATC_WEBSVR : BaseNetLogic
{
    //private ratc_web.webserver websocketServer = null;
    private ratc_web.WebSocketServer websocketServer = null;
    //string startUpPath = @"C:\web";
    public override void Start()
    {
        bool startAtRuntimeStart = LogicObject.GetVariable("StartAtRuntimeStart").Value;
        if (startAtRuntimeStart)
            this.startServer();
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
        stopServer();
    }



    [ExportMethod]
    public void sendDataMsg(string msg, string channel)
    {
        try
        {
            websocketServer.Broadcast(msg, channel);
        }
        catch (Exception ex)
        {
            Log.Error("sendData---" + ex.Message);
        }

    }

    [ExportMethod]
    public void sendDataBytes(byte[] data, string channel = "")
    {
        try
        {
            websocketServer.Broadcast(data, channel);
        }
        catch (Exception ex)
        {
            Log.Error("sendData---" + ex.Message);
        }

    }


    [ExportMethod]
    public async void stopServer()
    {

        Log.Info("stopServer");
        try
        {
            if (websocketServer != null)
                await websocketServer.StopAsync();
        }
        catch (Exception ex)
        {
            Log.Error("stopServer---" + ex.Message);
        }

    }

    [ExportMethod]
    public void startServer()
    {

        Log.Info("startServer");
        string url = "";
        try
        {

            url = LogicObject.GetVariable("URL").Value;
            string publicFolder = LogicObject.GetVariable("PublicFolderWeb").Value;
            publicFolder = publicFolder != "" ? publicFolder : ResourceUri.FromProjectRelativePath("").Uri + "\\htdocs";
            var port = LogicObject.GetVariable("Port").Value;

            if (url == string.Empty || port == string.Empty)
            {
                Log.Error("Error: Start Server URL or Port are empty");
                return;
            }

            //
            //add user everyone to port
            //netsh http add urlacl url="http://192.168.1.201:8080/" user=everyone
            //netsh http add urlacl url="http://localhost:8080/" user=everyone

            if (!url.Contains("http://"))
                url = "http://" + url;

            websocketServer = new ratc_web.WebSocketServer(app);
            websocketServer.PublicFolder = publicFolder;
            websocketServer.PropertyChanged += WebsocketServer_PropertyChanged;
            websocketServer.NewClientAvailableEvent += WebsocketServer_NewClientAvailableEvent;
            websocketServer.MsgRecivedEvent += WebsocketServer_MsgRecivedEvent;
            websocketServer.Start(url + ":" + port + @"/");

        }
        catch (Exception ex)
        {
            Log.Error("startServer--" + url + "-" + ex.Message);
        }

    }

    private int app(ratc_web.RestRequestEventArgs e)
    {
        try
        {
            int res = 0;
           // Log.Info("APP HttpMethod:", e.req.HttpMethod);
           // Log.Info("APP Path:", e.Path);
            switch (e.req.HttpMethod)
            {
                case "GET":
                    switch (e.Path)
                    {
                        case "test":
                            e.res.ContentType = "text/plain";
                            var paramTest = e.req.QueryString["test"] ?? "N/A";
                            e.res.setContent("Hallo ich bins ein parameter test: " + paramTest);
                            res = 1;
                            break;
                        case "test/parameter":
                            e.res.ContentType = "text/plain";
                            string str = "";

                            foreach (var item in e.req.QueryString)
                            {
                                str += " " + item.ToString() + ":" + e.req.QueryString[item.ToString()];
                            }
                            //var paramTest = e.req.QueryString["test"] ?? "N/A";
                            e.res.setContent("Hallo ich bins ein parameter test: " + str);
                            res = 1;
                            break;

                        case "test/app/json":
                            e.res.ContentType = "application/json; charset=UTF-8";
                            string json = "{\"Id\":\"123\",\"DateOfRegistration\":\"2012-10-21T00:00:00+05:30\",\"Status\":0}";
                            e.res.setContent(json);
                            res = 1;
                            break;
                        case "test/app/string":
                            e.res.ContentType = "text/plain";
                            e.res.setContent("Hallo ich bins");
                            res = 1;
                            break;
                        case "test/app/bin":
                            byte[] b = { 1, 4, 5, 7, 8, 9 };
                            e.res.ContentType = "application/x-binary";
                            e.res.setContent(b);
                            res = 1;
                            break;
                        case "test/error":
                            res = -1;
                            break;
                        default:
                            break;
                    }
                    break;
                case "PUT":
                    switch (e.Path)
                    {
                        case "pick":
                            byte[] payloadraw = (byte[])e.Data;
                            string payload = System.Text.Encoding.UTF8.GetString(payloadraw, 0, payloadraw.Length);
                            Log.Error(payload);
                            LogicObject.GetVariable("Message").Value = payload;
                            Log.Error("message processsed");
                            res = 1;
                            break;
                        default:
                            break;
                    }
                    break;
                case "POST":
                    switch (e.Path)
                    {
                        case "csv":
                            var save2csv = LogicObject.GetVariable("SaveToCSV").Value;
                            //var i = 0;
                            System.Text.StringBuilder line = new System.Text.StringBuilder();
                            string outPath = LogicObject.GetVariable("CSVDataPath").Value;
                            byte[] bytes = (byte[])e.Data;
                            var filePath = outPath + @"\\" + e.req.QueryString["file"] + ".csv";
                            int nval = int.Parse(e.req.QueryString["nval"]);
                            int nmov = int.Parse(e.req.QueryString["nmov"]);
                            var datasize = nval * 4;


                            //if (bytes.Length < nmov * datasize + 8 || bytes.Length % (nmov * datasize + 8) != 0)
                            //{
                            //    res = 409;
                            //}
                            //else
                            //{
                            //    if ((bool)save2csv.Value)
                            //    {

                            //If filename changed we have an additional URL-Paramenter. 
                            //Move the file to excahnge folder
                            //if (!string.IsNullOrEmpty(e.req.QueryString["lastfile"]))
                            //{
                            //    File.Move(outPath + @"\\" + e.req.QueryString["lastfile"] + ".csv", outPath + @"\\arch\\" + e.req.QueryString["lastfile"] + ".csv", true);
                            //}
                            //        while (i < bytes.Length)
                            //        {
                            //            Int64 timeval = BitConverter.ToInt64(bytes, i);
                            //            //var timeStamp = new Date(Number(timeval/1000n));
                            //            i += 8;

                            //            Single[] linevals = new Single[nval * nmov];

                            //            for (int m = 0; m < nmov; m++)
                            //            {
                            //                for (int v = 0; v < nval; v++)
                            //                {
                            //                    linevals[v + (m * nval)] = BitConverter.ToSingle(bytes, i + (v * 4));
                            //                }
                            //                i += datasize;
                            //            }
                            //            line.Append(timeval.ToString());
                            //            line.Append(", ");
                            //            line.AppendLine(string.Join(", ", linevals));
                            //        }

                            //        File.AppendAllText(filePath, line.ToString());
                            //    }

                            websocketServer.Broadcast(bytes, "");

                                res = 1;
                            //}

                            break;
                        case "test":
                            //e.res.ContentLength64 = b.Length;
                            //e.res.OutputStream.Write(b);
                            res = 1;
                            break;
                        case "test/app/string":
                            e.res.ContentType = "text/plain ";
                            e.res.setContent("Hallo ich bins");
                            res = 1;
                            break;
                        case "test/app/bin":
                            byte[] b = { 1, 4, 5, 7, 8, 9 };
                            e.res.ContentType = "application/x-binary";
                            e.res.setContent(b);
                            res = 1;
                            break;
                        case "test/error":
                            res = -1;
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    res = 0;
                    break;
            }

            return res;


        }
        catch (Exception ex)
        {
            Log.Error("app---" + ex.Message);
            return -1;
        }





    }

    private void WebsocketServer_MsgRecivedEvent(object sender, ratc_web.MsgRecivedEventArgs e)
    {
        Log.Info("Message received Client ID: " + e.Client.SocketId);
        Log.Info("Message received Client Channel: " + e.Client.Channel);

        if (e.MessageType == WebSocketMessageType.Text)
        {
            Log.Info("Message received Client Msg: " + e.Message);
            LogicObject.GetVariable("Message").Value = e.Message;
        }
    }


    private void WebsocketServer_NewClientAvailableEvent(object sender, ratc_web.ConnectedClient e)
    {
        Log.Info("New client connceted ID: " + e.SocketId);
        Log.Info("New client connceted Channel: " + e.Channel);
    }



    private void WebsocketServer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case "ServerIsRunning":
                LogicObject.GetVariable("ServerRunning").Value = ((ratc_web.WebSocketServer)sender).ServerIsRunning;
                break;
            case "ClientCount":
                LogicObject.GetVariable("ClientCount").Value = ((ratc_web.WebSocketServer)sender).ClientCount;
                break;
            default:
                break;
        }
    }


}
