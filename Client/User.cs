using System.Collections.Generic;

namespace Client
{
    /// <summary>
    /// User class used to create instances of each user connected to the server.
    /// </summary>
    public class User
    {
        public string UserName { get; set; }
        public string PublicKey { get; set; }
        public List<User> Contacts; // Still needs support, initialize list on connection
        public User(string userName, string publicKey)
        {
            UserName = userName;
            PublicKey = publicKey;
        }
    }
}
