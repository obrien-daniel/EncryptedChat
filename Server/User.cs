using System.Collections.Generic;

namespace Server
{
    /// <summary>
    /// User class used to create instances of each user connected to the server.
    /// </summary>
    public class User
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string PublicKey { get; set; }
        public List<User> Contacts; // Still needs support, does not currently deserialize
    }
}
