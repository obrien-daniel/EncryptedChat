using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer
{
    /// <summary>
    /// Class used to create custom chat rooms for private group discussions.
    /// </summary>
    class Room
    {
        public User Admin { get; set; } // Owner/ Creator of chatroom
        public string Name { get; set; } // Name of Chat Room, used to join
        // Initializing all of these Lists here for now, change in future
        public List<User> Moderators = new List<User>();
        public List<User> ConnectedUsers = new List<User>();
        public List<User> BannedUsers = new List<User>();
        public List<User> AllowedUsers = new List<User>(); // Used if chat is private
        public bool IsPublic; // If chatroom is public to anyone
        public Room(User admin, string name, bool isPublic) //Create Initial Room, add another to recreate one with users
        {
            Admin = admin;
            Name = name; //Check if it doesn't already exist, or use admin + name to create so it is unique
            IsPublic = isPublic;
        }
        public void AddUser(User user)
        {
            AllowedUsers.Add(user);
            ConnectedUsers.Add(user);
        }
        public void BanUser(User user)
        {
            if (AllowedUsers.Contains(user))
                AllowedUsers.Remove(user);
            BannedUsers.Add(user);
        }
        public void KickUser(User user)
        {
            ConnectedUsers.Remove(user);
        }
        public void UnbanUser(User user)
        {
            BannedUsers.Remove(user);
        }
        public void Join(User user)
        {
            if (IsPublic)
                if (AllowedUsers.Contains(user))
                    ConnectedUsers.Add(user);
                else
                    ConnectedUsers.Add(user);
        }
        public void Leave(User user)
        {
            if (ConnectedUsers.Contains(user))
                ConnectedUsers.Remove(user);
        }
    }
}
