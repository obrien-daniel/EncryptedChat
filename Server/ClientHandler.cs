using System;
using System.Threading;
using System.IO;
using System.Net.Security;

namespace Server
{
    // Class to handle each client request separatly
    public class ClientHandler
    {
        private SslStream ClientSocket { get; set; }
        private User User { get; set; }
        private string RoomName { get; set; }
        public ClientHandler(SslStream client, User user, string roomName)
        {
            ClientSocket = client;
            User = user;
            RoomName = roomName;
        }
        /// <summary>
        /// Creates a thread to run the client listener
        /// </summary>
        public void Start()
        {
            Thread ctThread = new Thread(ClientListener);
            ctThread.Start();
        }
        /// <summary>
        /// Listens to any client communication and forwards any incoming messages to the correct users.
        /// </summary>
        private void ClientListener()
        {
            int requestCount = 0;
            BinaryReader reader = null;
            try
            {
                while (true)
                {
                    requestCount = requestCount + 1;
                    reader = new BinaryReader(ClientSocket);
                    //string message = reader.ReadString();
                    string userName = reader.ReadString();
                    int count = reader.ReadInt32();
                    byte[] encryptedMessage = reader.ReadBytes(count);
                    Program.Rooms[RoomName].SendMessage(encryptedMessage, count, User.Username, userName, false);
                }
            }
            catch (IOException ex)
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
                Program.Rooms[RoomName].Leave(User);
                if(reader != null)
                    reader.Close();
                ClientSocket.Close();

            }
        }
    }
}
