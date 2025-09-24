
using System.Net;
using System.Net.Sockets;
using System.Text;

class ChatServer
{
    private TcpListener _listener;
    private List<TcpClient> _clients = new List<TcpClient>();
    private const int PORT = 5000;

    public async Task Start()
    {
        _listener = new TcpListener(IPAddress.Any, PORT);
        _listener.Start();
        Console.WriteLine("Server started on port " + PORT);

        while (true)
        {
            var client = await _listener.AcceptTcpClientAsync();
            _clients.Add(client);
            Console.WriteLine("Client connected!");

            _ = HandleClient(client); // fire & forget
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        var stream = client.GetStream();
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int byteCount = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (byteCount == 0) break; // client disconnect

                string json = Encoding.UTF8.GetString(buffer, 0, byteCount);
                Console.WriteLine("Received: " + json);

                // Broadcast ke semua client
                foreach (var c in _clients)
                {
                    if (c.Connected)
                    {
                        var s = c.GetStream();
                        await s.WriteAsync(buffer, 0, byteCount);
                    }
                }
            }
        }
        catch
        {
            Console.WriteLine("Client disconnected.");
        }
        finally
        {
            _clients.Remove(client);
            client.Close();
        }
    }
}
