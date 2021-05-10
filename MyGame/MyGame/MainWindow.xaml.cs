using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MyGame
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int WM_SYSCOMMAND = 0x112;
        private HwndSource hwndSource;

        public MainWindow()
        {
            SourceInitialized += Window1_SourceInitialized;
            InitializeComponent();
            //WindowState = WindowState.Maximized;
            //mainWindowFrame.Navigate(new GamePage());
        }

        private void HostBtn_OnClicktn_OnClick(object sender, RoutedEventArgs e)
        {
            MainGrid.Visibility = Visibility.Collapsed;
            var host = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .First(i => i.AddressFamily == AddressFamily.InterNetwork).MapToIPv4();
            MainWindowFrame.Navigate(new HostPage(host, 12222));
        }

        private async void PlayerBtn_OnClickrBtn_OnClick(object sender, RoutedEventArgs e)
        {
            MainGrid.Visibility = Visibility.Collapsed;
            var host = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .First(i => i.AddressFamily == AddressFamily.InterNetwork).MapToIPv4();
            string response = null;
            var nickName = "ggwp";
            var hostPort = 12222;
            var socket = new Socket(host.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var builder = new StringBuilder();
            var buffer = new byte[1024];
            try
            {
                var player = new Player(nickName, 0);
                await socket.ConnectAsync(host, hostPort);
                var json = JsonSerializer.Serialize(player, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await socket.SendAsync(Encoding.UTF8.GetBytes("connect" + json), SocketFlags.None);

                var receivedBytes = socket.Receive(buffer, SocketFlags.None);
                Debug.Print($"Accept receiving from:{socket.RemoteEndPoint}");
                Debug.Print($"Received {receivedBytes} bytes:{Encoding.UTF8.GetString(buffer)}");
                builder.Append(Encoding.UTF8.GetString(buffer));


                response = builder.ToString()?.TrimEnd('\0');

            }
            catch
            {
                socket.Shutdown(SocketShutdown.Both);
            }

            if (response != "ok")
            {
                MessageBox.Show(response);
                //TODO: Show error message
                return;
            }


            MainWindowFrame.Navigate(new GamePage(host, hostPort, nickName, socket));
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private void Window1_SourceInitialized(object sender, EventArgs e)
        {
            hwndSource = PresentationSource.FromVisual((Visual) sender) as HwndSource;
        }

        private void ResizeWindow(ResizeDirection direction)
        {
            SendMessage(hwndSource.Handle, WM_SYSCOMMAND, (IntPtr) direction, IntPtr.Zero);
        }

        private void ResetCursor(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed) Cursor = Cursors.Arrow;
        }

        private void Resize(object sender, MouseButtonEventArgs e)
        {
            var clickedShape = sender as Shape;

            switch (clickedShape.Name)
            {
                case "ResizeN":
                    Cursor = Cursors.SizeNS;
                    ResizeWindow(ResizeDirection.Top);
                    break;
                case "ResizeE":
                    Cursor = Cursors.SizeWE;
                    ResizeWindow(ResizeDirection.Right);
                    break;
                case "ResizeS":
                    Cursor = Cursors.SizeNS;
                    ResizeWindow(ResizeDirection.Bottom);
                    break;
                case "ResizeW":
                    Cursor = Cursors.SizeWE;
                    ResizeWindow(ResizeDirection.Left);
                    break;
                case "ResizeNW":
                    Cursor = Cursors.SizeNWSE;
                    ResizeWindow(ResizeDirection.TopLeft);
                    break;
                case "ResizeNE":
                    Cursor = Cursors.SizeNESW;
                    ResizeWindow(ResizeDirection.TopRight);
                    break;
                case "ResizeSE":
                    Cursor = Cursors.SizeNWSE;
                    ResizeWindow(ResizeDirection.BottomRight);
                    break;
                case "ResizeSW":
                    Cursor = Cursors.SizeNESW;
                    ResizeWindow(ResizeDirection.BottomLeft);
                    break;
            }
        }

        protected void DisplayResizeCursor(object sender, MouseEventArgs e)
        {
            var clickedShape = sender as Shape;

            switch (clickedShape.Name)
            {
                case "ResizeN":
                case "ResizeS":
                    Cursor = Cursors.SizeNS;
                    break;
                case "ResizeE":
                case "ResizeW":
                    Cursor = Cursors.SizeWE;
                    break;
                case "ResizeNW":
                case "ResizeSE":
                    Cursor = Cursors.SizeNWSE;
                    break;
                case "ResizeNE":
                case "ResizeSW":
                    Cursor = Cursors.SizeNESW;
                    break;
            }
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount==2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            DragMove();
        }

        private void MinimizeWindowButton_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeToggleWindowButton_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseWindowButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private enum ResizeDirection
        {
            Left = 61441,
            Right = 61442,
            Top = 61443,
            TopLeft = 61444,
            TopRight = 61445,
            Bottom = 61446,
            BottomLeft = 61447,
            BottomRight = 61448
        }
    }
}