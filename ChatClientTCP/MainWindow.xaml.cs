using System;
using System.ComponentModel.Design;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ChatClientTCP
{
    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private bool isDark = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_client == null || !_client.Connected) // belum connect → CONNECT
            {
                try
                {
                    _client = new TcpClient();
                    await _client.ConnectAsync(txtIP.Text, int.Parse(txtPort.Text));
                    _stream = _client.GetStream();

                    string loginJson = $"{{\"type\":\"login\",\"from\":\"{txtUsername.Text}\"}}";
                    byte[] loginData = Encoding.UTF8.GetBytes(loginJson);
                    await _stream.WriteAsync(loginData, 0, loginData.Length);

                    listBoxChat.Items.Add("Connected to server.");
                    btnConnect.Content = "Disconnect"; // ganti label tombol
                    _ = ReceiveMessages();
                }
                catch (Exception ex)
                {
                    listBoxChat.Items.Add("Error: " + ex.Message);
                }
            }
            else // sudah connect → DISCONNECT
            {
                try
                {
                    string logoutJson = $"{{\"type\":\"logout\",\"from\":\"{txtUsername.Text}\"}}";
                    byte[] logoutData = Encoding.UTF8.GetBytes(logoutJson);
                    await _stream.WriteAsync(logoutData, 0, logoutData.Length);

                    _client.Close();
                    _client = null;

                    listBoxChat.Items.Add("Disconnected from server.");
                    btnConnect.Content = "Connect"; // reset label tombol
                }
                catch (Exception ex)
                {
                    listBoxChat.Items.Add("Error while disconnecting: " + ex.Message);
                }
            }
        }


        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_stream != null && _client.Connected)
            {
                string msg = txtMessage.Text;
                string json;

                if (msg.StartsWith("/w "))
                {
                    var parts = msg.Split(new[] { ' ' }, 3);
                    if (parts.Length >= 3)
                    {
                        string targetUser = parts[1];
                        string privateMsg = parts[2];
                        json = $"{{\"type\":\"pm\",\"from\":\"{txtUsername.Text}\",\"to\":\"{targetUser}\",\"text\":\"{privateMsg}\"}}";
                    }
                    else
                    {
                        listBoxChat.Items.Add("Invalid PM format. Use: /w <username> <message>");
                        return;
                    }
                }
                else
                {
                    json = $"{{\"type\":\"msg\",\"from\":\"{txtUsername.Text}\",\"to\":\"all\",\"text\":\"{msg}\"}}";
                }

                byte[] data = Encoding.UTF8.GetBytes(json);
                await _stream.WriteAsync(data, 0, data.Length);

                txtMessage.Clear();
            }
        }

        private void listViewUsers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (listViewUsers.SelectedItem != null)
            {
                string targetUser = listViewUsers.SelectedItem.ToString();
                txtMessage.Text = $"/w {targetUser} ";
                txtMessage.Focus();
                txtMessage.CaretIndex = txtMessage.Text.Length;
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
                    string displayMessage = FormatMessage(json);

                    Dispatcher.Invoke(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(displayMessage))
                            listBoxChat.Items.Add(displayMessage);
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
            finally
            {
                await CloseClientWindow();
            }
        }

        private async Task CloseClientWindow()
        {
            await Task.Delay(2000);
            Close();
        }

        private string FormatMessage(string json)
        {
            string type = ExtractValue(json, "type");
            string from = ExtractValue(json, "from");
            string to = ExtractValue(json, "to");
            string text = ExtractValue(json, "text");

            switch (type)
            {
                case "login":
                    return $">> {from} joined";
                case "logout":
                    return $">> {from} left";
                case "msg":
                    return $"{from} to {to}: {text}";
                case "pm":
                    return $"{from} to {to}: {text}";
                case "userlist":
                    UpdateUserList(json);
                    return "";
                case "setname":
                    txtUsername.Text = from;
                    return $">> Your username is now '{from}'";

                default:
                    return json;
            }
        }

        private void UpdateUserList(string json)
        {
            int start = json.IndexOf("[");
            int end = json.IndexOf("]");
            if (start == -1 || end == -1) return;

            string content = json.Substring(start + 1, end - start - 1);
            string[] users = content.Replace("\"", "")
                                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            Dispatcher.Invoke(() =>
            {
                listViewUsers.Items.Clear();
                foreach (var u in users)
                {
                    listViewUsers.Items.Add(u.Trim());
                }
            });
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

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_stream != null && _client != null && _client.Connected)
            {
                string logoutJson = $"{{\"type\":\"logout\",\"from\":\"{txtUsername.Text}\"}}";
                byte[] logoutData = Encoding.UTF8.GetBytes(logoutJson);
                await _stream.WriteAsync(logoutData, 0, logoutData.Length);

                _client.Close();
            }
        }

        #region Theme

        private void btnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            isDark = !isDark;

            if (isDark)
                SetDarkTheme();
            else
                SetLightTheme();
        }


        private void SetDarkTheme()
        {
            var darkBg = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            var darkCtrl = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            var darkBtn = new SolidColorBrush(Color.FromRgb(70, 70, 70));
            var white = Brushes.White;

            this.Background = darkBg;

            listBoxChat.Background = darkCtrl;
            listBoxChat.Foreground = white;

            listViewUsers.Background = darkCtrl;
            listViewUsers.Foreground = white;

            txtMessage.Background = darkCtrl;
            txtMessage.Foreground = white;

            txtIP.Background = darkCtrl;
            txtIP.Foreground = white;

            txtPort.Background = darkCtrl;
            txtPort.Foreground = white;

            txtUsername.Background = darkCtrl;
            txtUsername.Foreground = white;

            btnConnect.Background = darkBtn;
            btnConnect.Foreground = white;

            btnSend.Background = darkBtn;
            btnSend.Foreground = white;

            btnToggleTheme.Background = darkBtn;
            btnToggleTheme.Foreground = white;
            btnToggleTheme.Content = "🌞";
        }

        private void SetLightTheme()
        {
            var white = Brushes.White;
            var black = Brushes.Black;
            var lightGray = Brushes.LightGray;

            this.Background = white;

            listBoxChat.Background = white;
            listBoxChat.Foreground = black;

            listViewUsers.Background = white;
            listViewUsers.Foreground = black;

            txtMessage.Background = white;
            txtMessage.Foreground = black;

            txtIP.Background = white;
            txtIP.Foreground = black;

            txtPort.Background = white;
            txtPort.Foreground = black;

            txtUsername.Background = white;
            txtUsername.Foreground = black;

            btnConnect.Background = lightGray;
            btnConnect.Foreground = black;

            btnSend.Background = lightGray;
            btnSend.Foreground = black;

            btnToggleTheme.Background = lightGray;
            btnToggleTheme.Foreground = black;
            btnToggleTheme.Content = "🌙";
        }

        #endregion
    }
}
