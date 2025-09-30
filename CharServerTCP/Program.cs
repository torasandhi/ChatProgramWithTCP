using System;
using System.Threading.Tasks;

namespace ChatServerTCP
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ChatServer server = new ChatServer();
            await server.Start();
        }
    }
}
