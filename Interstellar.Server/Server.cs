using Interstellar.Server.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp.Server;

namespace Interstellar.Server;

internal class Server
{
    static public void StartServer(string url)
    {
        var http = new HttpServer(url);

        // --- HTTP ハンドラ ---
        http.OnGet += (sender, e) =>
        {
            Console.WriteLine("HTTP request: " + e.Request.Url.AbsolutePath);
            var req = e.Request;
            var res = e.Response;
            var path = req.Url.AbsolutePath;

            string body;
            string contentType = "text/plain; charset=utf-8";
            int status = 200;

            if (path == "/" )
            {
                contentType = "text/html; charset=utf-8";
                body = "<!doctype html><meta charset='utf-8'><h1>Interstellar is working.</h1>";
            }
            else if (path == "/health")
            {
                contentType = "application/json; charset=utf-8";
                body = "{\"status\":\"ok\"}";
            }
            else if (path == "/vc")
            {
                // WebSocket専用エンドポイントにHTTPで来たときの案内
                status = 426; // Upgrade Required
                res.Headers.Add("Upgrade", "websocket");
                body = "This endpoint is WebSocket only. Use wss://.../vc";
            }
            else
            {
                status = 404;
                body = "Not Found";
            }

            res.StatusCode = status;
            res.ContentType = contentType;
            res.ContentEncoding = Encoding.UTF8;
            var buf = Encoding.UTF8.GetBytes(body);
            res.ContentLength64 = buf.LongLength;
            using (var os = res.OutputStream) { os.Write(buf, 0, buf.Length); }
        };

        // --- WebSocket /vc を登録 ---
        http.AddWebSocketService<VCClientService>("/vc");

        // 任意の調整
        http.KeepClean = true;
        http.WaitTime = TimeSpan.FromSeconds(30);

        http.Start();

        ManualResetEvent exitEvent = new(false);
        Console.WriteLine("Press ctrl-c to exit.");
        Console.CancelKeyPress += (_, e)=>
        {
            e.Cancel = true;
            exitEvent.Set();
        };
        exitEvent.WaitOne();
    }
}
