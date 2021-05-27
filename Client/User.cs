using System;
using System.Collections.Generic;

namespace Client
{
    /// <summary>
    /// User class used to create instances of each user connected to the server.
    /// </summary>
    [SerializableAttribute]
    public class User
    {
        private string username;
        public string UserName { get => username; set => username = value; }
        public string PublicKey { get; set; }
        public List<User> Contacts;

        public User(string userName, string publicKey)
        {
            UserName = userName;
            PublicKey = publicKey;
        }
    }
}