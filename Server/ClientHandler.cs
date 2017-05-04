using System;
using System.Threading;
using System.IO;
using System.Net.Security;
using System.Collections.Generic;

namespace Server
{
    // Class to handle each client request separatly
    public class ClientHandler
    {
        private SslStream ClientSocket { get; set; }
        private ConcurrentStreamWriter ClientSocketWriter { get; set; }
        private User User { get; set; }
        private List<string> connectedRooms { get; set; }
        public ClientHandler(SslStream client, User user)//, string roomName)
        {
            ClientSocket = client;
            ClientSocketWriter = new ConcurrentStreamWriter(client);
            User = user;
            connectedRooms = new List<string>();
            //RoomName = roomName;
        }
        /// <summary>
        /// Creates a thread to run the client listener
        /// </summary>
        public void Start()
        {
            Thread ctThread = new Thread(ClientListener);
            ctThread.Start();
            string chatRoom = "Global";
            if (Program.Rooms.ContainsKey(chatRoom))
            {
                Console.WriteLine("Joining Global");
                Program.Rooms[chatRoom].Join(User, ClientSocketWriter);
            }
            else
            {
                Console.WriteLine("Creating Global");
                Room room = new Room(User.Username, chatRoom, true); // create by default public chat room
                Program.Rooms.Add(chatRoom, room);
                room.Join(User, ClientSocketWriter);
            }
            connectedRooms.Add(chatRoom);
        }
        public enum Opcode
        {
            Join, Leave, SendMessage, AddUser, KickUser, BanUser, UnbanUser
        }

        /// <summary>
        /// Listens to any client communication and forwards any incoming messages to the correct users.
        /// </summary>
        private void ClientListener()
        {
            BinaryReader reader = null;
            try
            {
                while (true)
                {
                    //requestCount = requestCount + 1;
                    reader = new BinaryReader(ClientSocket);
                    //string message = reader.ReadString();
                    int opcode = reader.ReadInt32();
                    Console.WriteLine("opcode " + opcode);
                    switch ((Opcode)opcode)
                    {
                        case Opcode.Join:
                            string chatRoom = reader.ReadString();
                            Console.WriteLine("Attempting to Join: " + chatRoom);
                            if (Program.Rooms.ContainsKey(chatRoom))
                            {
                                bool hasJoined = Program.Rooms[chatRoom].Join(User, ClientSocketWriter);
                                if (hasJoined)
                                    connectedRooms.Add(chatRoom);
                            }
                            else
                            {
                                Room room = new Server.Room(User.Username, chatRoom, true); // create by default public chat room
                                Program.Rooms.Add(chatRoom, room);
                                bool hasJoined = room.Join(User, ClientSocketWriter);
                                if (hasJoined)
                                    connectedRooms.Add(chatRoom);
                            }
                            break;
                        case Opcode.Leave:
                            break;
                        case Opcode.SendMessage:
                            string roomName = reader.ReadString();
                            string userName = reader.ReadString();
                            int count = reader.ReadInt32();
                            byte[] encryptedMessage = reader.ReadBytes(count);
                            Program.Rooms[roomName].SendMessage(encryptedMessage, count, User.Username, userName, false);
                            break;
                        case Opcode.AddUser:
                            string user = reader.ReadString();
                            chatRoom = reader.ReadString();
                            if (Program.Rooms[chatRoom].Moderators.Contains(User.Username) || Program.Rooms[chatRoom].Admin.Equals(User.Username)) 
                                Program.Rooms[chatRoom].AllowedUsers.Add(user);
                            break;
                        case Opcode.KickUser:
                            user = reader.ReadString();
                            chatRoom = reader.ReadString();
                            if (Program.Rooms[chatRoom].Moderators.Contains(User.Username) || Program.Rooms[chatRoom].Admin.Equals(User.Username))
                                Program.Rooms[chatRoom].KickUser(user);
                            break;
                        case Opcode.BanUser:
                            user = reader.ReadString();
                            chatRoom = reader.ReadString();
                            if (Program.Rooms[chatRoom].Moderators.Contains(User.Username) || Program.Rooms[chatRoom].Admin.Equals(User.Username))
                                Program.Rooms[chatRoom].BanUser(user);
                            break;
                        case Opcode.UnbanUser:
                            user = reader.ReadString();
                            chatRoom = reader.ReadString();
                            if (Program.Rooms[chatRoom].Moderators.Contains(User.Username) || Program.Rooms[chatRoom].Admin.Equals(User.Username))
                                Program.Rooms[chatRoom].UnbanUser(user);
                            break;
                    }
                }
            }
            catch (IOException)
            {
                Console.WriteLine("{0} has disconnected", User.Username);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally //finally is used because we always want the connections to be closed after the infinite while loops exits.
            {
               // Program.UpdateAllConnectedUsersWithNewUser(User, false);
               if(connectedRooms != null)
                   foreach(string roomName in connectedRooms)
                        Program.Rooms[roomName].Leave(User);
                if(reader != null)
                    reader.Close();
                ClientSocketWriter.Dispose();
                ClientSocket.Close();
            }
        }
    }
}
