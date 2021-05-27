using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Server
{
    /// <summary>
    /// Class used to create custom chat rooms for private group discussions.
    /// </summary>
    internal class Room
    {
        public Dictionary<User, ConcurrentStreamWriter> ConnectedUsers = new Dictionary<User, ConcurrentStreamWriter>(); // Username mapped to SSLStream
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

        public void KickUser(string user)
        {
            KeyValuePair<User, ConcurrentStreamWriter> usr = ConnectedUsers.First(i => i.Key.Username == user);
            if (usr.Key != null)
                ConnectedUsers.Remove(usr.Key);
        }

        public void UnbanUser(string user)
        {
            if (BannedUsers.Contains(user))
                BannedUsers.Remove(user);
        }

        /// <summary>
        /// Attempt to join a chat room.
        /// If it is public, a user will be allowed to join if they aren't on the banned user list
        /// If it isn't public, a user will only be allowed if they are on the allowed users list
        /// </summary>
        /// <param name="user"></param>
        /// <param name="sslStream"></param>
        /// <returns></returns>
        public bool Join(User user, ConcurrentStreamWriter sslStream)
        {
            if (IsPublic)
            {
                if (!BannedUsers.Contains(user.Username))
                {
                    using (MemoryStream mem = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(mem, Encoding.UTF8, true))
                        {
                            writer.Write((int)Opcode.SystemMessage);
                            writer.Write(Name);
                            writer.Write("You have joined: " + Name + ".\n The administrator for this chat room is: " + Admin);
                        }
                        sslStream.Write(mem.ToArray());
                    }
                    ConnectedUsers.Add(user, sslStream);
                    //ClientHandler client = new ClientHandler(sslStream, user, Name);
                    //client.Start();
                }
                else
                {
                    using (MemoryStream mem = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(mem, Encoding.UTF8, true))
                        {
                            writer.Write((int)Opcode.SystemMessage);
                            writer.Write(Name);
                            writer.Write(string.Format("You are banned from: {0}", Name));
                        }
                        sslStream.Write(mem.ToArray());
                    }
                    return false;
                }
            }
            else
            {
                if (AllowedUsers.Contains(user.Username))
                {
                    using (MemoryStream mem = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(mem, Encoding.UTF8, true))
                        {
                            writer.Write((int)Opcode.SystemMessage);
                            writer.Write(Name);
                            writer.Write("You have joined: " + Name + ".\n The administrator for this chat room is: " + Admin);
                        }
                        sslStream.Write(mem.ToArray());
                    }
                    ConnectedUsers.Add(user, sslStream);
                    //  ClientHandler client = new ClientHandler(sslStream, user, Name);
                    //  client.Start();
                }
                else
                {
                    using (MemoryStream mem = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(mem, Encoding.UTF8, true))
                        {
                            writer.Write((int)Opcode.SystemMessage);
                            writer.Write(Name);
                            writer.Write(string.Format("{0} is not a public chat room, and you are not on the allowed user list.", Name));
                        }
                        sslStream.Write(mem.ToArray());
                    }
                    return false;
                }
            }
            UpdateUserWithConnectedUsersList(sslStream, user.Username);
            UpdateAllConnectedUsersWithNewUser(user, true);
            return true;
        }

        public void Leave(User user)
        {
            if (ConnectedUsers.ContainsKey(user))
            {
                // ConnectedUsers[user].Dispose(); //close the stream
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
        public void SendMessage(byte[] message, int count, string sourceUser, string targetUser)
        {
            foreach (KeyValuePair<User, ConcurrentStreamWriter> Item in ConnectedUsers)
            {
                User user = Item.Key;
                if (user.Username == targetUser)
                {
                    ConcurrentStreamWriter broadcastSocket = Item.Value;
                    using (MemoryStream mem = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(mem, Encoding.UTF8, true))
                        {
                            writer.Write((int)Opcode.UserMessage);
                            writer.Write(Name);
                            writer.Write(sourceUser);
                            writer.Write(count);
                            writer.Write(message);
                        }
                        broadcastSocket.Write(mem.ToArray());
                    }
                    Console.WriteLine("[" + sourceUser + " sending message to " + targetUser + "]:" + Convert.ToBase64String(message)); //base 64 is smallest representation of large text - REMOVE THIS AFTER TESTING
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
            foreach (KeyValuePair<User, ConcurrentStreamWriter> client in ConnectedUsers)
            {
                User user = client.Key;
                if (user.Username == userName)
                    return true;
            }
            return false;
        }

        public enum Opcode
        {
            GetUsers, UpdateUser, UserMessage, SystemMessage
        }

        /// <summary>
        /// Updates user list of a new client with all current users
        /// </summary>
        /// <param name="newClient"></param>
        /// <param name="userName"></param>
        private void UpdateUserWithConnectedUsersList(ConcurrentStreamWriter newClient, string userName)
        {
            foreach (KeyValuePair<User, ConcurrentStreamWriter> client in ConnectedUsers)
            {
                User user = client.Key;
                if (user.Username != userName)
                {
                    Console.WriteLine("Updating user with: " + user.Username);
                    using MemoryStream mem = new MemoryStream();
                    using (BinaryWriter writer = new BinaryWriter(mem, Encoding.UTF8, true))
                    {
                        writer.Write((int)Opcode.GetUsers);
                        writer.Write(user.Username);
                        writer.Write(user.PublicKey);
                        writer.Write(Name);
                    }
                    newClient.Write(mem.ToArray());
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
            foreach (KeyValuePair<User, ConcurrentStreamWriter> client in ConnectedUsers)
            {
                User endUser = client.Key;
                if (endUser.Username != user.Username)
                {
                    Console.WriteLine("Updating " + endUser.Username + "  with: " + user.Username);
                    ConcurrentStreamWriter broadcastStream = client.Value;
                    using MemoryStream mem = new MemoryStream();
                    using (BinaryWriter writer = new BinaryWriter(mem, Encoding.UTF8, true))
                    {
                        writer.Write((int)Opcode.UpdateUser);
                        writer.Write(user.Username);
                        writer.Write(user.PublicKey);
                        writer.Write(status);
                        writer.Write(Name);
                    }
                    broadcastStream.Write(mem.ToArray());
                }
            }
        }
    }
}