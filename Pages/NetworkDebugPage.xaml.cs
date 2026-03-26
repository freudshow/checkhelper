using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CheckHelper.Pages
{
    public partial class NetworkDebugPage : ContentPage
    {
        ObservableCollection<string> Logs = new();

        CancellationTokenSource? _cts;

        TcpListener? _tcpListener;
        readonly List<TcpClient> _clients = new();

        TcpClient? _tcpClient;
        UdpClient? _udpClient;

        public NetworkDebugPage()
        {
            InitializeComponent();

            LogListView.ItemsSource = Logs;

            ProtocolPicker.SelectedIndex = 0;
            EncodingPicker.SelectedIndex = 0;

            OpenButton.Clicked += OpenButton_Clicked;
            CloseButton.Clicked += CloseButton_Clicked;
            SendButton.Clicked += SendButton_Clicked;
            ClearLogButton.Clicked += (_, __) => { Logs.Clear(); };
        }

        private void AddLog(string text)
        {
            var msg = $"[{DateTime.Now:HH:mm:ss}] {text}";
            Dispatcher.Dispatch(() =>
            {
                Logs.Add(msg);
                if (AutoScrollCheck.IsChecked && Logs.Count > 0)
                {
                    LogListView.ScrollTo(Logs[^1], ScrollToPosition.End, true);
                }
            });
        }

        private async void OpenButton_Clicked(object? sender, EventArgs e)
        {
            var proto = ProtocolPicker.SelectedItem?.ToString();
            var ipText = string.IsNullOrWhiteSpace(IpEntry.Text) ? "0.0.0.0" : IpEntry.Text.Trim();
            if (!int.TryParse(PortEntry.Text, out var port))
            {
                await DisplayAlert("端口错误", "请输入有效端口", "OK");
                return;
            }

            _cts = new CancellationTokenSource();

            try
            {
                if (proto == "TCP Server")
                {
                    IPAddress ip = IPAddress.Parse(ipText);
                    _tcpListener = new TcpListener(ip, port);
                    _tcpListener.Start();
                    AddLog($"TCP Server 已在 {ip}:{port} 启动");
                    _ = AcceptLoopAsync(_cts.Token);
                    StatusLabel.Text = "TCP Server: running";
                }
                else if (proto == "TCP Client")
                {
                    _tcpClient = new TcpClient();
                    await _tcpClient.ConnectAsync(ipText, port);
                    AddLog($"TCP Client 已连接到 {ipText}:{port}");
                    _ = ReceiveFromTcpClientAsync(_tcpClient, _cts.Token);
                    StatusLabel.Text = "TCP Client: connected";
                }
                else // UDP
                {
                    _udpClient = new UdpClient(port);
                    AddLog($"UDP 已在端口 {port} 开始接收");
                    _ = ReceiveUdpAsync(_udpClient, _cts.Token);
                    StatusLabel.Text = "UDP: listening";
                }
            }
            catch (Exception ex)
            {
                AddLog("打开失败: " + ex.Message);
                _cts?.Cancel();
            }
        }

        private async void CloseButton_Clicked(object? sender, EventArgs e)
        {
            await CloseAllAsync();
        }

        private async Task CloseAllAsync()
        {
            try
            {
                _cts?.Cancel();

                if (_tcpListener != null)
                {
                    _tcpListener.Stop();
                    _tcpListener = null;
                }

                lock (_clients)
                {
                    foreach (var c in _clients)
                    {
                        try { c.Close(); } catch { }
                    }
                    _clients.Clear();
                }

                if (_tcpClient != null)
                {
                    try { _tcpClient.Close(); } catch { }
                    _tcpClient = null;
                }

                if (_udpClient != null)
                {
                    try { _udpClient.Close(); } catch { }
                    _udpClient = null;
                }

                AddLog("所有连接已关闭");
                StatusLabel.Text = "Closed";
            }
            catch (Exception ex)
            {
                AddLog("关闭时错误: " + ex.Message);
            }
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _tcpListener != null)
                {
                    var client = await _tcpListener.AcceptTcpClientAsync(ct);
                    lock (_clients) { _clients.Add(client); }
                    AddLog($"Accept 客户端 {((IPEndPoint)client.Client.RemoteEndPoint).ToString()}");
                    _ = ReceiveFromTcpClientAsync(client, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AddLog("AcceptLoop 错误: " + ex.Message);
            }
        }

        private async Task ReceiveFromTcpClientAsync(TcpClient client, CancellationToken ct)
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (read == 0) break;
                    var data = new byte[read];
                    Array.Copy(buffer, data, read);
                    AddLog(FormatReceived(data));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AddLog("TCP 接收错误: " + ex.Message);
            }
            finally
            {
                try { client.Close(); } catch { }
                lock (_clients) { _clients.Remove(client); }
                AddLog("客户端断开");
            }
        }

        private async Task ReceiveUdpAsync(UdpClient udp, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var res = await udp.ReceiveAsync(ct);
                    AddLog($"{res.RemoteEndPoint}: " + FormatReceived(res.Buffer));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AddLog("UDP 接收错误: " + ex.Message);
            }
        }

        private string FormatReceived(byte[] data)
        {
            var enc = EncodingPicker.SelectedItem?.ToString() ?? "ASCII";
            if (enc == "HEX")
            {
                var sb = new StringBuilder();
                foreach (var b in data) sb.Append(b.ToString("X2")).Append(' ');
                return sb.ToString().TrimEnd();
            }
            else
            {
                try { return Encoding.ASCII.GetString(data); }
                catch { return BitConverter.ToString(data); }
            }
        }

        private async void SendButton_Clicked(object? sender, EventArgs e)
        {
            await SendDataAsync(SendEntry.Text ?? string.Empty);
        }

        private async Task SendDataAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var enc = EncodingPicker.SelectedItem?.ToString() ?? "ASCII";
            byte[] data;
            if (enc == "HEX")
            {
                var parts = text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                var list = new List<byte>();
                foreach (var p in parts)
                {
                    if (byte.TryParse(p, System.Globalization.NumberStyles.HexNumber, null, out var b)) list.Add(b);
                }
                data = list.ToArray();
            }
            else
            {
                data = Encoding.ASCII.GetBytes(text);
            }

            var proto = ProtocolPicker.SelectedItem?.ToString();
            try
            {
                if (proto == "TCP Server")
                {
                    lock (_clients)
                    {
                        foreach (var c in _clients)
                        {
                            try
                            {
                                var s = c.GetStream();
                                s.Write(data, 0, data.Length);
                            }
                            catch { }
                        }
                    }
                    AddLog($"发送到所有客户端: {text}");
                }
                else if (proto == "TCP Client")
                {
                    if (_tcpClient?.Connected == true)
                    {
                        var s = _tcpClient.GetStream();
                        await s.WriteAsync(data, 0, data.Length);
                        AddLog($"TCP Client 发送: {text}");
                    }
                    else AddLog("TCP Client 未连接");
                }
                else // UDP
                {
                    if (_udpClient != null)
                    {
                        if (!int.TryParse(PortEntry.Text, out var port)) { AddLog("无效端口"); return; }
                        var ip = string.IsNullOrWhiteSpace(IpEntry.Text) ? "127.0.0.1" : IpEntry.Text.Trim();
                        await _udpClient.SendAsync(data, data.Length, ip, port);
                        AddLog($"UDP 发送到 {ip}:{port} -> {text}");
                    }
                    else AddLog("UDP 未启动接收，请先打开监听或创建 UdpClient");
                }
            }
            catch (Exception ex)
            {
                AddLog("发送错误: " + ex.Message);
            }
        }
    }
}
