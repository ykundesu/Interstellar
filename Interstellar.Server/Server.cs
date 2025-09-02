using Interstellar.Server.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp.Server;

namespace Interstellar.Server;

internal class Server
{
    static public void StartServer(string url)
    {
        var server = new WebSocketServer(url);

        server.AddWebSocketService<VCClientService>("/vc");

        server.Start();

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
