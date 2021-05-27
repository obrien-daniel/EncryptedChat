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
        private string _username;
        public string UserName { get => _username; set => _username = value; }
        public string PublicKey { get; set; }

        public List<User> Contacts;

        public User(string userName, string publicKey)
        {
            UserName = userName;
            PublicKey = publicKey;
        }
    }
}