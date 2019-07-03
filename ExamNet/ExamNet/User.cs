using System.Net.Sockets;

namespace ExamNet
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public TcpClient Client = new TcpClient();
        public Lobby Lobby = null;
    }
}
