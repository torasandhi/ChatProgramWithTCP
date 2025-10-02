using System.Net;
using System.Net.Sockets;
using System.Text;

class ChatServer
{
    private TcpListener _listener;
    private List<TcpClient> _clients = new List<TcpClient>();
    private Dictionary<TcpClient, string> _usernames = new Dictionary<TcpClient, string>();
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

            _ = HandleClient(client);
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
                if (byteCount == 0) break;

                string json = Encoding.UTF8.GetString(buffer, 0, byteCount);
                Console.WriteLine("Received: " + json);

                if (json.Contains("\"type\":\"login\""))
                {
                    string username = ExtractValue(json, "from");

                    // username wong unik
                    if (_usernames.Values.Contains(username))
                    {
                        Random rnd = new Random();
                        string newUsername;
                        do
                        {
                            newUsername = username + rnd.Next(100, 999); // tambah 3 random digits
                        } while (_usernames.Values.Contains(newUsername));

                        username = newUsername;
                    }

                    _usernames[client] = username;

                    // send username yang baru ke client
                    string confirmMsg = $"{{\"type\":\"setname\",\"from\":\"{username}\"}}";
                    await SendMessageToClient(client, confirmMsg);

                    // send userlist
                    await SendUserListToClient(client);

                    // broadcast join 
                    string joinMsg = $"{{\"type\":\"login\",\"from\":\"{username}\"}}";
                    await Broadcast(joinMsg);

                    // update userlist ke semua client
                    await BroadcastUserList();
                }


                else if (json.Contains("\"type\":\"logout\""))
                {
                    string username = ExtractValue(json, "from");

                    string leaveMsg = $"{{\"type\":\"logout\",\"from\":\"{username}\"}}";
                    await Broadcast(leaveMsg);

                    _usernames.Remove(client);
                    await BroadcastUserList();
                }
                else if (json.Contains("\"type\":\"pm\""))
                {
                    string from = ExtractValue(json, "from");
                    string to = ExtractValue(json, "to");

                    // kirim hanya ke target user
                    foreach (var kvp in _usernames)
                    {
                        if (kvp.Value == to)
                        {
                            var s = kvp.Key.GetStream();
                            byte[] data = Encoding.UTF8.GetBytes(json);
                            await s.WriteAsync(data, 0, data.Length);
                        }
                    }

                    // kirim balik ke pengirim supaya dia lihat PM-nya sendiri
                    if (_usernames.TryGetValue(client, out string senderName))
                    {
                        var s = client.GetStream();
                        byte[] data = Encoding.UTF8.GetBytes(json);
                        await s.WriteAsync(data, 0, data.Length);
                    }
                }
                else
                {
                    // normal broadcast ke semua
                    await Broadcast(json);
                }
            }
        }
        catch
        {
            Console.WriteLine("Client disconnected.");
        }
        finally
        {
            if (_usernames.ContainsKey(client))
            {
                string username = _usernames[client];
                string leaveMsg = $"{{\"type\":\"logout\",\"from\":\"{username}\"}}";
                await Broadcast(leaveMsg);

                _usernames.Remove(client);
                await BroadcastUserList();
            }

            _clients.Remove(client);
            client.Close();
        }
    }

    private async Task Broadcast(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        foreach (var c in _clients)
        {
            if (c.Connected)
            {
                try
                {
                    var s = c.GetStream();
                    await s.WriteAsync(data, 0, data.Length);
                }
                catch { }
            }
        }
    }

    private async Task SendMessageToClient(TcpClient client, string message)
    {
        try
        {
            var s = client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(message);
            await s.WriteAsync(data, 0, data.Length);
        }
        catch { }
    }


    private async Task BroadcastUserList()
    {
        string users = string.Join("\",\"", _usernames.Values);
        string json = $"{{\"type\":\"userlist\",\"users\":[\"{users}\"]}}";
        await Broadcast(json);
    }

    private async Task SendUserListToClient(TcpClient client)
    {
        string users = string.Join("\",\"", _usernames.Values);
        string json = $"{{\"type\":\"userlist\",\"users\":[\"{users}\"]}}";

        try
        {
            var s = client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(json);
            await s.WriteAsync(data, 0, data.Length);
        }
        catch { }
    }

    private string ExtractValue(string json, string key)
    {
        string pattern = $"\"{key}\":\"";
        int start = json.IndexOf(pattern);
        if (start == -1) return "";
        start += pattern.Length;
        int end = json.IndexOf("\"", start);
        if (end == -1) return "";
        return json.Substring(start, end - start);
    }
}
