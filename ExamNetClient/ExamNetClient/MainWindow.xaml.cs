using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Net;
using System.Threading;

namespace ExamNetClient
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TcpClient client;
        Thread clientThread;
        string clientName;
        bool isHasLobby = false;
        bool isAdmin = false;

        public MainWindow()
        {
            InitializeComponent();

            serverIpTextBox.Text = "10.1.4.87";
            serverPortTextBox.Text = "12345";
            connectButton.Tag = false;
            numberSendButton.IsEnabled = false;
            leaveButton.IsEnabled = false;
            startGameButton.IsEnabled = false;
            createButton.IsEnabled = false;
            joinButton.IsEnabled = false;
        }

        private void ConnectButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(bool)connectButton.Tag && !string.IsNullOrEmpty(serverPortTextBox.Text) && !string.IsNullOrEmpty(serverIpTextBox.Text))
                {
                    client = new TcpClient();
                    clientName = userNameTextBox.Text;
                    client.BeginConnect(IPAddress.Parse(serverIpTextBox.Text), int.Parse(serverPortTextBox.Text), ClientWork, client);

                    connectButton.Tag = true;
                    connectButton.Content = "Disconnect";
                    createButton.IsEnabled = true;
                    joinButton.IsEnabled = true;
                }
                else
                {
                    client.Client.Send(Encoding.UTF8.GetBytes("close"));
                    client.Client.Shutdown(SocketShutdown.Both);
                    client.Close();

                    connectButton.Tag = false;
                    connectButton.Content = "Connect";
                    createButton.IsEnabled = false;
                    joinButton.IsEnabled = false;
                    leaveButton.IsEnabled = false;
                    startGameButton.IsEnabled = false;
                    numberSendButton.IsEnabled = false;
                }
            }
            catch(SocketException)
            {
                connectButton.Tag = false;
                connectButton.Content = "Connect";
                createButton.IsEnabled = false;
                joinButton.IsEnabled = false;
                leaveButton.IsEnabled = false;
                startGameButton.IsEnabled = false;
                numberSendButton.IsEnabled = false;
            }
            catch(NullReferenceException)
            {
                connectButton.Tag = false;
                connectButton.Content = "Connect";
                createButton.IsEnabled = false;
                joinButton.IsEnabled = false;
                leaveButton.IsEnabled = false;
                startGameButton.IsEnabled = false;
                numberSendButton.IsEnabled = false;
            }
        }

        private void ClientWork(object state)
        {
            TcpClient client = (state as IAsyncResult).AsyncState as TcpClient;

            if (!string.IsNullOrEmpty(clientName))
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        client.Client.Send(Encoding.UTF8.GetBytes(userNameTextBox.Text.Trim()));
                        clientThread = new Thread(ClientListen);
                        clientThread.IsBackground = true;
                        clientThread.Start(client);
                        MessageBox.Show("Connected");
                    }
                    catch(SocketException)
                    {
                        MessageBox.Show("Сервер не запущен");
                        client.Dispose();
                        connectButton.Tag = false;
                        connectButton.Content = "Connect";
                        createButton.IsEnabled = false;
                        joinButton.IsEnabled = false;
                        leaveButton.IsEnabled = false;
                        startGameButton.IsEnabled = false;
                        numberSendButton.IsEnabled = false;
                    }
                });
            }
            else
            {
                MessageBox.Show("Напишите свое имя");

                client.Client.Shutdown(SocketShutdown.Both);
                client.Close();

                Dispatcher.Invoke(() =>
                {
                    connectButton.Tag = false;
                    connectButton.Content = "Connect";
                });
            }
        }

        private void ClientListen(object state)
        {
            TcpClient client = state as TcpClient;
            byte[] buf = new byte[4 * 1024];
            int recSize;

            try
            {
                while (client.Connected)
                {
                    recSize = client.Client.Receive(buf);
                    string command = Encoding.UTF8.GetString(buf, 0, recSize);

                    if (command.ToLower() == "start")
                    {
                        Dispatcher.Invoke(() =>
                        {
                            numberSendButton.IsEnabled = true;
                            leaveButton.IsEnabled = false;
                            createButton.IsEnabled = false;
                            joinButton.IsEnabled = false;
                            startGameButton.IsEnabled = false;
                        });

                        if (!isAdmin)
                        {
                            client.Client.Send(Encoding.UTF8.GetBytes("start"));
                        }

                        while(true)
                        {
                            recSize = client.Client.Receive(buf);

                            if (Encoding.UTF8.GetString(buf, 0, recSize).ToLower() == "stop")
                            {
                                client.Client.Send(Encoding.UTF8.GetBytes("stop"));
                                Dispatcher.Invoke(() => { lobbyIdTextBlock.Text = ""; });
                                isAdmin = false;
                                break;
                            }
                            else
                            {
                                Dispatcher.Invoke(() => { logTextBox.Text += Encoding.UTF8.GetString(buf, 0, recSize); });
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            createButton.IsEnabled = true;
                            joinButton.IsEnabled = true;
                            numberSendButton.IsEnabled = false;
                        });
                    }
                    else if (command.ToLower().Split(' ')[0] == "create")
                    {
                        Dispatcher.Invoke(() => { lobbyIdTextBlock.Text = Encoding.UTF8.GetString(buf, 0, recSize).ToLower().Split(' ')[1]; });
                    }
                    else if(command.ToLower().Split(' ')[0] == "join")
                    {
                        Dispatcher.Invoke(() => {
                            lobbyIdTextBlock.Text = Encoding.UTF8.GetString(buf, 0, recSize).ToLower().Split(' ')[1];
                            createButton.IsEnabled = false;
                            joinButton.IsEnabled = false;
                            leaveButton.IsEnabled = true;
                        });
                        isAdmin = false;
                    }
                    else if(command == "leave")
                    {
                        Dispatcher.Invoke(() => { lobbyIdTextBlock.Text = ""; });
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { logTextBox.Text += Encoding.UTF8.GetString(buf, 0, recSize); });
                    }
                }
            }
            catch (NullReferenceException) { }
            catch (SocketException) { }
        }

        private void CreateButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((bool)connectButton.Tag)
                {
                    client.Client.Send(Encoding.UTF8.GetBytes("create"));
                    isHasLobby = true;
                    isAdmin = true;
                    createButton.IsEnabled = false;
                    joinButton.IsEnabled = false;
                    leaveButton.IsEnabled = true;
                    startGameButton.IsEnabled = true;
                }
            }
            catch (SocketException)
            {
                client.Dispose();
                connectButton.Tag = false;
                connectButton.Content = "Connect";
                createButton.IsEnabled = false;
                joinButton.IsEnabled = false;
                leaveButton.IsEnabled = false;
                startGameButton.IsEnabled = false;
                numberSendButton.IsEnabled = false;
            }
            catch (ArgumentNullException) { }
            catch (ObjectDisposedException) { }
        }

        private void JoinButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((bool)connectButton.Tag)
                {
                    client.Client.Send(Encoding.UTF8.GetBytes("join"));
                    Thread.Sleep(10);
                    client.Client.Send(Encoding.UTF8.GetBytes(lobbyIdTextBox.Text));
                    isHasLobby = true;
                }
            }
            catch (SocketException) {
                client.Dispose();
                connectButton.Tag = false;
                connectButton.Content = "Connect";
                createButton.IsEnabled = false;
                joinButton.IsEnabled = false;
                leaveButton.IsEnabled = true;
                startGameButton.IsEnabled = false;
                numberSendButton.IsEnabled = false;
            }
            catch (ArgumentNullException) { }
            catch (ObjectDisposedException) { }
        }

        private void LeaveButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((bool)connectButton.Tag && isHasLobby)
                {
                    client.Client.Send(Encoding.UTF8.GetBytes("leave"));
                    isHasLobby = false;
                    lobbyIdTextBlock.Text = "";
                    leaveButton.IsEnabled = false;
                    createButton.IsEnabled = true;
                    joinButton.IsEnabled = true;
                    numberSendButton.IsEnabled = false;
                    startGameButton.IsEnabled = false;
                }
            }
            catch (SocketException) {
                client.Dispose();
                connectButton.Tag = false;
                connectButton.Content = "Connect";
                createButton.IsEnabled = false;
                joinButton.IsEnabled = false;
                leaveButton.IsEnabled = false;
                startGameButton.IsEnabled = false;
                numberSendButton.IsEnabled = false;
            }
            catch (ObjectDisposedException) { }
            catch (ArgumentNullException) { }
        }

        private void NumberSendButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((bool)connectButton.Tag && isHasLobby && sendNumberTextBox.Text.Length == 4)
                {
                    client.Client.Send(Encoding.UTF8.GetBytes(sendNumberTextBox.Text));
                }
                else
                {
                    MessageBox.Show("Зайдите в лобби или создайте его. Если вы зашли в лобби то ввели меньше 4 знаков");
                }
            }
            catch (SocketException) {
                client.Dispose();
                connectButton.Tag = false;
                connectButton.Content = "Connect";
                createButton.IsEnabled = false;
                joinButton.IsEnabled = false;
                leaveButton.IsEnabled = false;
                startGameButton.IsEnabled = false;
                numberSendButton.IsEnabled = false;
            }
            catch (ArgumentNullException) { }
            catch (ObjectDisposedException) { }
        }

        private void StartGameButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((bool)connectButton.Tag)
                {
                    client.Client.Send(Encoding.UTF8.GetBytes("start"));
                }
            }
            catch(SocketException)
            {
                client.Dispose();
                connectButton.Tag = false;
                connectButton.Content = "Connect";
                createButton.IsEnabled = false;
                joinButton.IsEnabled = false;
                leaveButton.IsEnabled = false;
                startGameButton.IsEnabled = false;
                numberSendButton.IsEnabled = false;
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                client.Client.Send(Encoding.UTF8.GetBytes("close"));
                client.Client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
            catch (NullReferenceException) { }
            catch (SocketException) {
                client.Dispose();
                connectButton.Tag = false;
                connectButton.Content = "Connect";
                createButton.IsEnabled = false;
                joinButton.IsEnabled = false;
                leaveButton.IsEnabled = false;
                startGameButton.IsEnabled = false;
                numberSendButton.IsEnabled = false;
            }
            catch (ObjectDisposedException) { }
        }

        private void SendNumberTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if( (e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) )
            {
                if (sendNumberTextBox.Text.Length < 4)
                {
                    e.Handled = false;
                }
                else
                {
                    e.Handled = true;
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        private void ServerPortTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                if (serverPortTextBox.Text.Length < 5)
                {
                    e.Handled = false;
                }
                else
                {
                    e.Handled = true;
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        private void ServerIpTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) || e.Key == Key.Decimal || e.Key == Key.OemPeriod)
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }
    }
}
