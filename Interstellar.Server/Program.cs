namespace Interstellar.Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string urlPrefix = "ws://";
            if(args.Length > 1)
            {
                for(int i = 1; i < args.Length; i++)
                {
                    switch(args[i])
                    {
                        case "-secure":
                            urlPrefix = "wss://";
                            break;
                    }
                }
            }
            string url = urlPrefix + "localhost:8000";
            if (args.Length >= 1) url = urlPrefix + args[0];
            

            Console.WriteLine("Starting server at " + url);
            Server.StartServer(url);
        }
    }
}
