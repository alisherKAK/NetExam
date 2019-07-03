using System.Collections.Generic;

namespace ExamNet
{
    public class Lobby
    {
        public int Id { get; set; }
        public List<User> Users = new List<User>();
        public User Admin { get; set; }
        public string Number { get; set; }
    }
}
