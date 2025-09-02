namespace Interstellar.Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Server.StartServer("ws://localhost:8000");
        }
    }
}
