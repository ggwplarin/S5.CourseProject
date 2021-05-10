using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MyGame
{
    /// <summary>
    ///     Логика взаимодействия для HostPage.xaml
    /// </summary>
    internal class Client
    {
        public Player Player { get; set; }
        public IPAddress ClientIpAddress { get; set; }
        public int ClientPort { get; set; }

        public Socket ConnectedSocket { get; set; }
    }

    public class GameState
    {
        public string State { get; set; }
        public Question SelectedQuestion { get; set; }
        public Player[] PlayersState { get; set; }
        public Round CurrentRound { get; set; }
    }

    public class Round
    {
        public string title { get; set; }
        public Category[] Categories { get; set; }
    }

    public class Category
    {
        public string title { get; set; }
        public Question[] questions { get; set; }
    }

    public class Question
    {
        public string title { get; set; }
        public string answer { get; set; }
        public int reward { get; set; }
        public bool available { get; set; }
    }

    public partial class HostPage : Page
    {
        public const int MessageBufferSize = 2048;

        private readonly GameState _currentGameState = new();
        private readonly IPAddress _hostIpAddress;
        private readonly int _hostPort;
        private readonly List<ChatMessage> chat;

        private readonly List<Client> clients = new();
        private readonly bool gameStarted = false;

        private bool _active;

        private ManualResetEvent allDone = new(false);
        public List<Category> categories = new();

        private bool flag;
        public List<Socket> waiters = new();

        public HostPage(IPAddress hostIpAddress, int hostPort)
        {
            _hostIpAddress = hostIpAddress;
            _hostPort = hostPort;
            InitializeComponent();
            chat = new List<ChatMessage>();
            ListenLoopStart();
            LoadGame();
        }

        public async void ListenLoopStart()
        {
            Socket socket = new(_hostIpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                var acceptBuffer = new byte[1024];
                socket.Bind(new IPEndPoint(_hostIpAddress, _hostPort));
                socket.Listen(100);
                _active = true;
                while (_active)
                {
                    HostIPPortTextBlock.Text = _hostIpAddress + ":" + _hostPort;
                    Debug.Print("Wait for connections...");
                    var acceptSocket = await socket.AcceptAsync();
                    Debug.Print("New connection");
                    StringBuilder builder = new();

                    do
                    {
                        var receivedBytes = await acceptSocket.ReceiveAsync(acceptBuffer, SocketFlags.None);
                        Debug.Print($"Accept receiving from:{acceptSocket.RemoteEndPoint}");
                        Debug.Print($"Received {receivedBytes} bytes:{Encoding.UTF8.GetString(acceptBuffer)}");
                        builder.Append(Encoding.UTF8.GetString(acceptBuffer));
                    } while (acceptSocket.Available > 1);

                    Debug.Print($"Received message:{builder}");

                    string json;

                    if (builder.ToString().StartsWith("connect"))
                    {
                        if (clients.Count < 6 && !gameStarted)
                        {
                            json = builder.ToString().Replace("connect", "").TrimEnd('\0');
                            var player = JsonSerializer.Deserialize<Player>(json, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });
                            if (player.NickName.Equals("host", StringComparison.CurrentCultureIgnoreCase))
                                player.NickName = "lox";
                            while (clients.Select(c => c.Player.NickName).Contains(player.NickName))
                                player.NickName += "_";
                            Debug.Assert(player != null, nameof(player) + " != null");

                            Debug.Print($"New player:{player.NickName}");
                            var response = Encoding.UTF8.GetBytes("ok");

                            Debug.Print(
                                $"Sending {response.Length} bytes:{response} to:{((IPEndPoint) acceptSocket.RemoteEndPoint)?.Address}");
                            await acceptSocket.SendAsync(response, SocketFlags.None);
                            var client = new Client
                            {
                                Player = player,
                                ClientIpAddress = ((IPEndPoint) acceptSocket.RemoteEndPoint).Address,
                                ClientPort = ((IPEndPoint) acceptSocket.RemoteEndPoint).Port
                            };
                            clients.Add(client);
                        }
                        else
                        {
                            var responce = Encoding.UTF8.GetBytes("no");
                            Debug.Print(
                                $"Sending {responce.Length} bytes:{Encoding.UTF8.GetString(responce)} to:{((IPEndPoint) acceptSocket.RemoteEndPoint)?.Address}");
                            await acceptSocket.SendAsync(responce, SocketFlags.None);
                        }
                    }

                    if (builder.ToString().StartsWith("chat.send"))
                        try
                        {
                            json = builder.ToString().Replace("chat.send", "").TrimEnd('\0');
                            var message = JsonSerializer.Deserialize<ChatMessage>(json, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });
                            chat.Add(message);
                            await acceptSocket.SendAsync(
                                Encoding.UTF8.GetBytes("chat.messages" + JsonSerializer.Serialize(chat.ToArray())),
                                SocketFlags.None);
                            ChatView.ItemsSource = chat;
                            Debug.Print(chat.ToString());
                        }
                        catch (Exception e)
                        {
                            Debug.Print(e.Message);
                        }

                    if (builder.ToString().StartsWith("wait")) waiters.Add(acceptSocket);
                }
            }
            finally
            {
                socket.Shutdown(SocketShutdown.Both);
            }
        }

        public void ListenLoopStop()
        {
            _active = false;
        }

        private async void StartGameButton_OnClick(object sender, RoutedEventArgs e)
        {
            GameLoopStart();
            foreach (var waiter in waiters)
                await waiter.SendAsync(Encoding.UTF8.GetBytes("start"),
                    SocketFlags.None);
            StartLayout.Visibility = Visibility.Collapsed;
            UpdateQuestions();
        }

        public async void LoadGame()
        {
            var ofd = new OpenFileDialog();
            string path;
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == true)
            {
                path = ofd.FileName;
                var json = (await File.ReadAllTextAsync("C:\\Users\\ggwplarin\\Downloads\\Questions.json"))
                    .Replace("\n", string.Empty).Trim();
                Console.WriteLine(json);
                var round = JsonSerializer.Deserialize<Round>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _currentGameState.CurrentRound = round;
            }
        }

        private async void UpdateQuestions()
        {
            QuestionsBox.ItemsSource = _currentGameState.CurrentRound.Categories;
        }

        private async void GameLoopStart()
        {
            var gamePort = _hostPort + 1;
            Socket socket = new(_hostIpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                var acceptBuffer = new byte[8192];
                socket.Bind(new IPEndPoint(_hostIpAddress, gamePort));
                socket.Listen(100);
                while (true)
                {
                    Debug.Print("Wait for update requests...");
                    var acceptSocket = await socket.AcceptAsync();
                    Debug.Print("New connection");
                    StringBuilder builder = new();

                    do
                    {
                        var receivedBytes = await acceptSocket.ReceiveAsync(acceptBuffer, SocketFlags.None);
                        Debug.Print($"Accept receiving from:{acceptSocket.RemoteEndPoint}");
                        Debug.Print($"Received {receivedBytes} bytes:{Encoding.UTF8.GetString(acceptBuffer)}");
                        builder.Append(Encoding.UTF8.GetString(acceptBuffer));
                    } while (acceptSocket.Available > 1);

                    Debug.Print($"Received message:{builder}");

                    if (builder.ToString().StartsWith("round"))
                    {
                        var json = JsonSerializer.Serialize(_currentGameState.CurrentRound);
                        var response = Encoding.UTF8.GetBytes(json);
                        Debug.Print(
                            $"Sending {response.Length} bytes:{response} to:{((IPEndPoint) acceptSocket.RemoteEndPoint)?.Address}");
                        await acceptSocket.SendAsync(response, SocketFlags.None);
                    }

                    if (builder.ToString().StartsWith("stat"))
                    {
                        var json = JsonSerializer.Serialize(_currentGameState.PlayersState);
                        var response = Encoding.UTF8.GetBytes(json);
                        Debug.Print(
                            $"Sending {response.Length} bytes:{response} to:{((IPEndPoint) acceptSocket.RemoteEndPoint)?.Address}");
                        await acceptSocket.SendAsync(response, SocketFlags.None);
                    }

                    if (builder.ToString().StartsWith("flag"))
                    {
                        if (!flag)
                        {
                            flag = true;
                            await acceptSocket.SendAsync(Encoding.UTF8.GetBytes("ok"), SocketFlags.None);
                        }
                        else
                        {
                            await acceptSocket.SendAsync(Encoding.UTF8.GetBytes("no"), SocketFlags.None);
                        }
                    }
                }
            }
            finally
            {
                socket.Shutdown(SocketShutdown.Both);
            }
        }

    }
}