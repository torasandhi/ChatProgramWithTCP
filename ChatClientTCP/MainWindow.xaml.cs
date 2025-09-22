using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ChatClientTCP
{
    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private NetworkStream _stream;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(txtIP.Text, int.Parse(txtPort.Text));
                _stream = _client.GetStream();

                listBoxChat.Items.Add("Connected to server.");
                _ = ReceiveMessages(); // mulai terima pesan async
            }
            catch (Exception ex)
            {
                listBoxChat.Items.Add("Error: " + ex.Message);
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_stream != null && _client.Connected)
            {
                string msg = txtMessage.Text;
                string json = $"{{\"msg\",\"from\":\"{txtUsername.Text}\",\"to\":\"all\":\"{msg}\"}}";

                byte[] data = Encoding.UTF8.GetBytes(json);
                await _stream.WriteAsync(data, 0, data.Length);

                txtMessage.Clear();
            }
        }

        private async Task ReceiveMessages()
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (_client.Connected)
                {
                    int byteCount = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (byteCount == 0) break;

                    string json = Encoding.UTF8.GetString(buffer, 0, byteCount);

                    Dispatcher.Invoke(() =>
                    {
                        listBoxChat.Items.Add(json);
                    });
                }
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    listBoxChat.Items.Add("Disconnected from server.");
                });
            }
        }
    }
}
