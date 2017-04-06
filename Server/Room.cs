using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    /// <summary>
    /// Class used to create custom chat rooms for private group discussions.
    /// </summary>
    class Room
    {
        public Dictionary<User, SslStream> ConnectedUsers = new Dictionary<User, SslStream>(); // Username mapped to SSLStream
        public string Admin { get; set; } // Owner/ Creator of chatroom
        public string Name { get; set; } // Name of Chat Room, used to join
        // Initializing all of these Lists here for now, change in future
        public List<string> Moderators = new List<string>();
        //public List<User> ConnectedUsers = new List<User>();
        public List<string> BannedUsers = new List<string>();
        public List<string> AllowedUsers = new List<string>(); // Used if chat is private
        public bool IsPublic; // If chatroom is public to anyone
        public Room(string admin, string name, bool isPublic)
        {
            Admin = admin;
            AllowedUsers.Add(admin);
            Name = name; //Check if it doesn't already exist, or use admin + name to create so it is unique
            IsPublic = isPublic;
        }
        public void AddUser(string user)
        {
            if (!AllowedUsers.Contains(user))
                AllowedUsers.Add(user);
            //ConnectedUsers.Add(user, sslStream);
        }
        public void BanUser(string user)
        {
            if (AllowedUsers.Contains(user))
                AllowedUsers.Remove(user);
            BannedUsers.Add(user);
        }
        public void KickUser(User user)
        {
            if (ConnectedUsers.ContainsKey(user))
                ConnectedUsers.Remove(user);
        }
        public void UnbanUser(string user)
        {
            if (BannedUsers.Contains(user))
                BannedUsers.Remove(user);
        }
        // Attempt to Join a chat room
        /// <summary>
        /// Attempt to join a chat room.
        /// If it is public, a user will be allowed to join if they aren't on the banned user list
        /// If it isn't public, a user will only be allowed if they are on the allowed users list
        /// </summary>
        /// <param name="user"></param>
        public void Join(User user, SslStream sslStream)
        {
            if (IsPublic)
            {
                if (!BannedUsers.Contains(user.Username))
                {
                    BinaryWriter writer = new BinaryWriter(sslStream, Encoding.UTF8, true);
                    writer.Write(true);
                    writer.Write(string.Empty);
                    writer.Close();
                    ConnectedUsers.Add(user, sslStream);
                    ClientHandler client = new ClientHandler(sslStream, user, Name);
                    client.Start();
                }
                else
                {
                    BinaryWriter writer = new BinaryWriter(sslStream, Encoding.UTF8, false);
                    writer.Write(false);
                    writer.Write(string.Format("You are banned from: {0}", Name));
                    writer.Close();
                    return;
                }
            }
            else
            {
                if (AllowedUsers.Contains(user.Username))
                {
                    BinaryWriter writer = new BinaryWriter(sslStream, Encoding.UTF8, true);
                    writer.Write(true);
                    writer.Write(string.Empty);
                    writer.Close();
                    ConnectedUsers.Add(user, sslStream);
                    ClientHandler client = new ClientHandler(sslStream, user, Name);
                    client.Start();
                }
                else
                {
                    BinaryWriter writer = new BinaryWriter(sslStream, Encoding.UTF8, false);
                    writer.Write(false);
                    writer.Write(string.Format("{0} is not a public chat room, and you are not on the allowed user list.", Name));
                    writer.Close();
                    return;
                }
            }
            UpdateUserWithConnectedUsersList(sslStream, user.Username);
            UpdateAllConnectedUsersWithNewUser(user, true);
        }
        public void Leave(User user)
        {
            if (ConnectedUsers.ContainsKey(user))
            {
                ConnectedUsers[user].Close(); //close the stream
                ConnectedUsers.Remove(user);
            }
            UpdateAllConnectedUsersWithNewUser(user, false);
        }

        /// <summary>
        /// Sendes a byte array message to a user.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="count"></param>
        /// <param name="sourceUser"></param>
        /// <param name="targetUser"></param>
        /// <param name="flag"></param>
        public void SendMessage(byte[] message, int count, string sourceUser, string targetUser, bool flag)
        {
            foreach (var Item in ConnectedUsers)
            {
                User user = (User)Item.Key;
                if (user.Username == targetUser)
                {
                    SslStream broadcastSocket = (SslStream)Item.Value;
                    BinaryWriter writer = new BinaryWriter(broadcastSocket);
                    int OpCode = 3;
                    writer.Write(OpCode);
                    writer.Write(sourceUser);
                    writer.Write(count);
                    writer.Write(message);
                    writer.Flush();
                    Console.WriteLine("[" + sourceUser + " sending message to " + targetUser + "]:" + Convert.ToBase64String(message)); //base 64 is smallest representation of large text
                }
            }
        }

        /// <summary>
        /// Checks if the user is already logged in.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public bool IsUsedLoggedIn(string userName)
        {
            foreach (var client in ConnectedUsers)
            {
                User user = (User)client.Key;
                if (user.Username == userName)
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Updates user list of a new client with all current users
        /// </summary>
        /// <param name="newClient"></param>
        /// <param name="userName"></param>
        private void UpdateUserWithConnectedUsersList(SslStream newClient, string userName)
        {
            foreach (var client in ConnectedUsers)
            {
                User user = (User)client.Key;
                if (user.Username != userName)
                {
                    BinaryWriter writer = new BinaryWriter(newClient);
                    int OpCode = 1;
                    writer.Write(OpCode);
                    writer.Write(user.Username);
                    writer.Write(user.PublicKey);
                    writer.Flush();
                }
            }
        }
        /// <summary>
        /// Update an already connected client's user list about one specific user.
        /// true for add, false for remove
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="status"></param>
        public void UpdateAllConnectedUsersWithNewUser(User user, bool status)
        {
            //if preexisting client,
            foreach (var client in ConnectedUsers)
            {
                User endUser = (User)client.Key;
                if (endUser.Username != user.Username)
                {
                    SslStream broadcastStream = (SslStream)client.Value;
                    BinaryWriter writer = new BinaryWriter(broadcastStream);
                    int OpCode = 2;
                    writer.Write(OpCode);
                    writer.Write(user.Username);
                    writer.Write(user.PublicKey);
                    writer.Write(status);
                    writer.Flush();
                }
            }

        }
    }
}
