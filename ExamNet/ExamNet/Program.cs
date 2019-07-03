using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ExamNet
{
    class Program
    {
        static string serverIp = "0.0.0.0";
        static int serverPort = 12345;

        static List<Lobby> lobbies = new List<Lobby>();
        static List<User> Users = new List<User>();
        static int lobbyAddId = 1;

        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Parse(serverIp), serverPort);
            Thread serverThread;
            server.Start(100);
            serverThread = new Thread(ServerListen);
            serverThread.IsBackground = true;
            serverThread.Start(server);

            while (Console.ReadKey().Key != ConsoleKey.Escape){}
        }

        static void ServerListen(object state)
        {
            TcpListener server = state as TcpListener;
            byte[] buf = new byte[64 * 1024];
            int recSize;

            while(true)
            {
                User user = new User();
                user.Client = server.AcceptTcpClient();

                recSize = user.Client.Client.Receive(buf);
                user.Name = Encoding.UTF8.GetString(buf, 0, recSize);
                user.Id = Users.Count + 1;
                Users.Add(user);

                Console.WriteLine($"User {user.Name} reged");

                ThreadPool.QueueUserWorkItem(ClientThreadRoutine, user);
            }
        }

        static void ClientThreadRoutine(object state)
        {
            User user = state as User;
            byte[] buf = new byte[64 * 1024];
            int recSize;

            try
            {
                while (user.Client.Connected)
                {
                    recSize = user.Client.Client.Receive(buf);
                    string command = Encoding.UTF8.GetString(buf, 0, recSize);

                    if (command.ToLower() == "create")
                    {
                        Lobby newLobby = new Lobby();
                        newLobby.Id = lobbyAddId;
                        lobbyAddId++;
                        newLobby.Admin = user;
                        newLobby.Number = GenerateSecretNumber();
                        user.Lobby = newLobby;
                        lobbies.Add(newLobby);
                        Console.WriteLine($"User {user.Name} created lobby");

                        user.Client.Client.Send(Encoding.UTF8.GetBytes($"create {newLobby.Id.ToString()}"));
                        recSize = user.Client.Client.Receive(buf);

                        string createCommand = Encoding.UTF8.GetString(buf, 0, recSize);

                        if (createCommand.ToLower() == "start")
                        {
                            Console.WriteLine($"User {user.Name} started game");
                            for (int i = 0; i < lobbies.Count; i++)
                            {
                                if (lobbies[i].Equals(newLobby))
                                {
                                    lobbies[i].Admin.Client.Client.Send(Encoding.UTF8.GetBytes("start"));
                                    lobbies[i].Admin.Client.Client.Send(Encoding.UTF8.GetBytes("Game Started\n"));
                                    foreach (User sUser in lobbies[i].Users)
                                    {
                                        sUser.Client.Client.Send(Encoding.UTF8.GetBytes("Game started\n"));
                                        Thread.Sleep(10);
                                        sUser.Client.Client.Send(Encoding.UTF8.GetBytes("start"));
                                    }
                                    break;
                                }
                            }

                            if (GameProcess(user))
                            {
                                Console.WriteLine($"User {user.Name} won");
                                for (int i = 0; i < lobbies.Count; i++)
                                {
                                    if (lobbies[i].Equals(newLobby))
                                    {
                                        lobbies[i].Admin.Client.Client.Send(Encoding.UTF8.GetBytes("stop"));
                                        lobbies[i].Admin.Client.Client.Send(Encoding.UTF8.GetBytes($"Win {user.Name}\n"));
                                        foreach (User sUser in lobbies[i].Users)
                                        {
                                            sUser.Client.Client.Send(Encoding.UTF8.GetBytes("stop"));
                                            Thread.Sleep(10);
                                            sUser.Client.Client.Send(Encoding.UTF8.GetBytes($"Win {user.Name}\n"));
                                        }
                                        break;
                                    }
                                }
                                lobbies.Remove(user.Lobby);
                            }
                            user.Lobby = null;
                        }
                        else if(createCommand.ToLower() == "leave")
                        {
                            if (user.Lobby != null)
                            {
                                Console.WriteLine($"User {user.Name} left from lobby");
                                user.Lobby = null;
                                user.Client.Client.Send(Encoding.UTF8.GetBytes("leave"));
                                for (int i = 0; i < lobbies.Count; i++)
                                {
                                    if (lobbies[i].Admin.Equals(user))
                                    {
                                        lobbies.RemoveAt(i);
                                    }
                                    break;
                                }
                                user.Client.Client.Send(Encoding.UTF8.GetBytes("You left from lobby\n"));
                            }
                        }
                        else if(createCommand.ToLower() == "close")
                        {
                            throw new ArgumentException();
                        }
                    }
                    else if (command.ToLower() == "join")
                    {
                        recSize = user.Client.Client.Receive(buf);
                        int lobbyId = int.Parse(Encoding.UTF8.GetString(buf, 0, recSize));

                        if (lobbies.Any(lobby => lobby.Id == lobbyId))
                        {
                            for(int i = 0; i < lobbies.Count; i++)
                            {
                                if(lobbies[i].Id == lobbyId)
                                {
                                    lobbies[i].Users.Add(user);
                                    user.Lobby = lobbies[i];
                                    user.Client.Client.Send(Encoding.UTF8.GetBytes($"join {lobbies[i].Id.ToString()}"));
                                }
                                break;
                            }
                            Console.WriteLine($"User {user.Name} joined to lobby");
                            recSize = user.Client.Client.Receive(buf);

                            string joinCommand = Encoding.UTF8.GetString(buf, 0, recSize);

                            if (joinCommand == "start")
                            {
                                if (GameProcess(user))
                                {
                                    for (int i = 0; i < lobbies.Count; i++)
                                    {
                                        if (lobbies[i].Id == lobbyId)
                                        {
                                            lobbies[i].Admin.Client.Client.Send(Encoding.UTF8.GetBytes("stop"));
                                            Thread.Sleep(10);
                                            lobbies[i].Admin.Client.Client.Send(Encoding.UTF8.GetBytes($"Win {user.Name}\n"));
                                            foreach (User sUser in lobbies[i].Users)
                                            {
                                                sUser.Client.Client.Send(Encoding.UTF8.GetBytes("stop"));
                                                Thread.Sleep(10);
                                                sUser.Client.Client.Send(Encoding.UTF8.GetBytes($"Win {user.Name}\n"));
                                            }
                                        }
                                        break;
                                    }
                                    lobbies.Remove(user.Lobby);
                                }
                                user.Lobby = null;
                            }
                            else if (joinCommand.ToLower() == "leave")
                            {
                                if (user.Lobby != null)
                                {
                                    Console.WriteLine($"User {user.Name} left from lobby");
                                    user.Lobby = null;
                                    user.Client.Client.Send(Encoding.UTF8.GetBytes("leave"));
                                    for (int i = 0; i < lobbies.Count; i++)
                                    {
                                        if (lobbies[i].Users.Contains(user))
                                        {
                                            lobbies[i].Users.Remove(user);
                                        }
                                        break;
                                    }
                                    user.Client.Client.Send(Encoding.UTF8.GetBytes("You left from lobby\n"));
                                }
                            }
                            else if (joinCommand.ToLower() == "close")
                            {
                                throw new ArgumentException();
                            }
                        }
                        else
                        {
                            user.Client.Client.Send(Encoding.UTF8.GetBytes("Lobby with this is not"));
                        }
                    }
                    else if(command.ToLower() == "close")
                    {
                        break;
                    }
                }

                Users.Remove(user);
                user.Client.Client.Shutdown(SocketShutdown.Both);
                user.Client.Close();
                Console.WriteLine($"User {user.Name} disconnected");
            }
            catch (SocketException) { }
            catch (ArgumentException) {
                Users.Remove(user);
                user.Client.Client.Shutdown(SocketShutdown.Both);
                user.Client.Close();
                Console.WriteLine($"User {user.Name} disconnected");
            }
            catch (IndexOutOfRangeException) { }
        }

        static string GenerateSecretNumber()
        {
            string number = "";

            while(number.Length < 4)
            {
                int randNumber = new Random().Next(0, 9);

                if(!number.Contains(randNumber.ToString()))
                {
                    number += randNumber.ToString();
                }
            }

            return number;
        }

        static bool GameProcess(object state)
        {
            User user = state as User;
            byte[] buf = new byte[4 * 1024];
            int recSize;

            try
            {
                while (true)
                {
                    recSize = user.Client.Client.Receive(buf);
                    string sentNumber = Encoding.UTF8.GetString(buf, 0, recSize);

                    Console.WriteLine($"User {user.Name} sent number: {sentNumber}");

                    if (sentNumber.ToLower() == "stop")
                    {
                        return false;
                    }

                    if(sentNumber.ToLower() == "close")
                    {
                        throw new ArgumentException();
                    }

                    if (sentNumber == user.Lobby.Number)
                    {
                        break;
                    }
                    else
                    {
                        int numCount = 0;
                        int positionCount = 0;
                        for (int i = 0; i < sentNumber.Length; i++)
                        {
                            if (user.Lobby.Number.Contains(sentNumber[i]))
                            {
                                numCount++;
                            }
                            if (user.Lobby.Number[i] == sentNumber[i])
                            {
                                positionCount++;
                            }
                        }

                        user.Client.Client.Send(Encoding.UTF8.GetBytes($"Угадыно чисел: {numCount}\nУгадыно по позиций: {positionCount}\n"));
                    }
                }

                return true;
            }
            catch(Exception)
            {
                throw;
            }
        }

        static void ClearLobby(Lobby lobby)
        {
            lobbies.Remove(lobby);
        }
    }
}
