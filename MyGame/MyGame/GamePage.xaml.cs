using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyGame
{
    /// <summary>
    ///     Логика взаимодействия для GamePage.xaml
    /// </summary>
    public partial class GamePage : Page
    {
        private readonly IPAddress _hostIpAddress;
        private readonly int _hostPort;
        private readonly string _nickName = "ggwp_jopa";
        private readonly ObservableCollection<Player> cards = new();
        private readonly Socket _connectedSocket;

        public GamePage(IPAddress hostIpAddress, int hostPort, string nickName, Socket connectedSocket)
        {
            InitializeComponent();
            

            _hostIpAddress = hostIpAddress;
            _hostPort = hostPort;
            _nickName = nickName;
            _connectedSocket = connectedSocket;
            WaitForStart();
            ShowPlayerCards();
        }

        private void ChatBox_KeyDown(object sender, KeyEventArgs e)
        {
            var text = ((TextBox) sender).Text.Trim('{', '}', '[', ']', '"', ':','\'');
            if (e.Key == Key.Return && !string.IsNullOrWhiteSpace(text)&& !text.Any(c=>"{}[]\"\':".Contains(c)))
            {
                SendChatMessage(text);
                ((TextBox) sender).Text = "";
            }
        }

        private async void SendChatMessage(string message)
        {
            var socket = new Socket(_hostIpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(_hostIpAddress, _hostPort);
                var chatMessage = new ChatMessage(_nickName, message);
                var json = JsonSerializer.Serialize(chatMessage, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                socket.Send(Encoding.UTF8.GetBytes("chat.send"+json), SocketFlags.None);
                var receivedBytes = new byte[1024];
                await socket.ReceiveAsync(receivedBytes, SocketFlags.None);
                StringBuilder sb = new StringBuilder();
                sb.Append(Encoding.UTF8.GetString(receivedBytes));
                var chat = JsonSerializer.Deserialize<ChatMessage[]>(sb.ToString().Replace("chat.messages", "").TrimEnd('\0'));
                ChatView.ItemsSource = chat;
                ChatView.ScrollIntoView((chat ?? Array.Empty<ChatMessage>()).Last());
            }
            catch
            {
                socket.Shutdown(SocketShutdown.Both);
            }
        }

        private void ShowPlayerCards()
        {
            //TODO: Получать данные от хоста и записывать в массив
            
            PlayerCardsListView.ItemsSource = cards;
        }

        private async void Update()
        {
            var socket = new Socket(_hostIpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(_hostIpAddress, _hostPort);
                socket.Send(Encoding.UTF8.GetBytes("wait"), SocketFlags.None);
                var receivedBytes = new byte[8192];
                string response;
                do
                {
                    await socket.ReceiveAsync(receivedBytes, SocketFlags.None);
                    response = Encoding.UTF8.GetString(receivedBytes).TrimEnd('\0');

                } while (response != "stop");

                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                socket.Shutdown(SocketShutdown.Both);

            }
        }

        private async void WaitForStart()
        {
            WaitingOverlay.Visibility = Visibility.Visible;
            var socket = new Socket(_hostIpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(_hostIpAddress, _hostPort);
                socket.Send(Encoding.UTF8.GetBytes("wait"), SocketFlags.None);
                var receivedBytes = new byte[1024];
                string response;
                do
                {
                    await socket.ReceiveAsync(receivedBytes, SocketFlags.None);
                    response = Encoding.UTF8.GetString(receivedBytes).TrimEnd('\0');
                } while (response!="start");

                WaitingOverlay.Visibility = Visibility.Collapsed;
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                socket.Shutdown(SocketShutdown.Both);

            }
        }
    }
}