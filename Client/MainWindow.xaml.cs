using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Net.Sockets;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Security.Authentication;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Windows.Media;
using System.Collections.ObjectModel;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Server certificate name
        private static readonly string ServerCertificateName = "MyServer";
        //Directory of client certificate used in SSL authentication
        private static readonly string ClientCertificateFile = "./Cert/client.pfx";
        private static readonly string ClientCertificatePassword = null;
        TcpClient client = new TcpClient();
        SslStream sslStream = null;
        BackgroundWorker backgroundWorker = null;
        private string serverIP = string.Empty;
        private int port = 0;
        private string connectedUser = string.Empty;
        private User currentUser;

        public ObservableCollection<ChatRoomView> Tabs { get; set; }

        public MainWindow()
        {
            Tabs = new ObservableCollection<ChatRoomView>();
            Tabs.Add(new ChatRoomView("Global"));
            Tabs.Add(new ChatRoomView("+"));
            InitializeComponent();
            //listBox.ItemsSource = users;
            tabControl.ItemsSource = Tabs;
            buttonDisconnect.IsEnabled = false;
            // buttonSend.IsEnabled = false;
            //tabControl


        }
        /// <summary>
        /// Listens for any communication from the server.
        /// Three opcodes are used for communication from the server.
        /// OpCode 1 - Update entire User list
        /// OpCode 2 - Add/Remove single user
        /// OpCode 3 - New Message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ServerListener(object sender, DoWorkEventArgs e)
        {
            try
            {
                BinaryReader reader = new BinaryReader(sslStream);
                int OpCode = reader.ReadInt32();

                Console.WriteLine("reading opcode: " + OpCode);
                if (OpCode == 1) //update entire user list, occurs on client/server connection
                {
                    Console.WriteLine("OPCODE 2 HAS BEEN ACCEPTED");
                    string userName = reader.ReadString();
                    string publicKey = reader.ReadString();
                    string chatRoom = reader.ReadString();
                    User user = new User(userName, publicKey);
                    Console.WriteLine("adding user: " + user.UserName + " to : " + chatRoom);
                    var tab = Tabs.FirstOrDefault(i => i.Name == chatRoom);
                    if (tab != null)
                    {
                        lock (tab._syncLock)
                        {
                            tab.Users.Add(user);
                        }
                    }
                    e.Result = null; //no message is passed on to UI
                }
                else if (OpCode == 2) //Update user list, occurs whenever a user connects/disconnects
                {
                    Console.WriteLine("OPCODE 2 HAS BEEN ACCEPTED");
                    string userName = reader.ReadString(); //username of other client
                    string publicKey = reader.ReadString();
                    bool status = reader.ReadBoolean(); //connecting (true) or disconnecting (false)
                    string chatRoom = reader.ReadString();
                    string message = null;
                    User user = new User(userName, publicKey);
                    if (status)
                    {
                        var tab = Tabs.FirstOrDefault(i => i.Name == chatRoom);
                        if (tab != null)
                        {
                            lock (tab._syncLock)
                            {
                                Console.WriteLine("Adding " + user.UserName + " to " + chatRoom);
                                tab.Users.Add(user);
                            }
                        }
                        message = userName + " has joined the chat.";
                    }
                    else
                    {

                        var tab = Tabs.FirstOrDefault(i => i.Name == chatRoom);
                        if (tab != null)
                        {
                            var usersCopy = new List<User>(tab.Users);
                            foreach (var item in usersCopy)
                            {
                                if (item.UserName == userName)
                                {
                                    lock (tab._syncLock)
                                    {
                                        tab.Users.Remove(item);
                                    }
                                }
                            }
                        }
                        message = userName + " has left the chat.";
                    }
                    e.Result = new Tuple<string, string>(chatRoom, message); //pass on message to UI
                }
                else if (OpCode == 3) //new message
                {
                    string chatRoom = reader.ReadString();
                    string username = reader.ReadString();
                    int count = reader.ReadInt32();
                    byte[] encryptedMessage = reader.ReadBytes(count);
                    string privateKey = File.ReadAllText(@"..\..\..\..\Server\bin\Debug\netcoreapp3.1\PrivateKeys\" + connectedUser + "_privateKey.txt"); // dir for when running in visual studio
                    //string privateKey = File.ReadAllText("./PrivateKeys/" + connectedUser + "_privateKey.txt"); //dir for when running release with client and server in same dir
                    Message message = (Message)Deserialize(encryptedMessage);
                    message.Decrypt(privateKey);
                    //string decryptedMessage = RSADecrypt(encryptedMessage, privateKey);
                    //string message = username + ": " + decryptedMessage;
                    e.Result = message; //pass on message to UI
                }
                else if (OpCode == 4) //System message
                {
                    string chatRoom = reader.ReadString();
                    string message = reader.ReadString();
                    e.Result = new Tuple<string, string>(chatRoom, message); //pass on message to UI
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine("ServerListener IOException: " + ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServerListener exception: " + ex);
            }
        }
        /// <summary>
        /// Updates the UI thread whenever the server sends a message or user status update.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bgCheck_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (client.Connected)
            {
                /*listBox.ItemsSource = null;
                lock (_syncLock)
                {
                    listBox.ItemsSource = users;
                }*/
                // Update UI
                if (e.Result is Tuple<string, string>)
                {
                    Tuple<string, string> response = e.Result as Tuple<string, string>; // if it is not string, it assigns null
                    if (response != null)
                    {
                        var tab = Tabs.FirstOrDefault(i => i.Name == response.Item1);
                        if (tab != null)
                        {
                            lock (tab._syncLock2)
                            {
                                tab.Messages.Add(new Message(null, tab.Name, null, DateTime.Now, null, response.Item2, Brushes.Blue.ToString()));
                            }
                        }

                    }
                }
                else if (e.Result is Message)
                {
                    Message message = e.Result as Message;
                    if (message != null)
                    {
                        Console.Write(message.Chatroom);
                        var tab = Tabs.FirstOrDefault(i => i.Name == message.Chatroom);
                        if (tab != null)
                        {
                            lock (tab._syncLock2)
                            {
                                tab.Messages.Add(message);
                            }
                        }
                    }
                }
                // Continue listening on server stream
                backgroundWorker.RunWorkerAsync();
            }
            else
            {
                // SEND TO ALL TABS
                foreach (var tab in Tabs)
                {
                    if (tab != null)
                    {
                        lock (tab._syncLock2)
                        {
                            tab.Messages.Add(new Message(null, tab.Name, null, DateTime.Now, null, "You have been disconnected from the server.", Brushes.Red.ToString()));
                        }
                    }
                }
                DropClient();
            }
        }
        public enum Opcode
        {
            Join, Leave, SendMessage, AddUser, KickUser, BanUser, UnbanUser
        }
        /// <summary>
        /// Sends encrypted message to each user currently connected to the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSend_Click(object sender, RoutedEventArgs e)
        {
            //TabItem ti = (TabItem)tabControl.SelectedItem;
            //var tab = Tabs.FirstOrDefault(i => i.Name == ti.Header.ToString());
            Console.WriteLine("Click: ");
            ChatRoomView tab = tabControl.SelectedItem as ChatRoomView;

            Console.WriteLine("Click: " + tab.Name);
            try
            {
                if (client.Connected)
                {
                    BinaryWriter writer = new BinaryWriter(sslStream);
                    foreach (var user in tab.Users)
                    {
                        Console.WriteLine("Sending message to: " + user.UserName);
                        Message message = new Message(currentUser.UserName, tab.Name, user, DateTime.UtcNow, null, tab.Message, ClrPcker_Background.SelectedColor.Value.ToString());
                        message.Encrypt();
                        byte[] encryptedMessage = Serialize(message);
                        int count = encryptedMessage.Count();
                        writer.Write((int)Opcode.SendMessage);
                        writer.Write(tab.Name); // add chat room
                        writer.Write(user.UserName);
                        writer.Write(count);
                        writer.Write(encryptedMessage);
                        writer.Flush();
                    }

                    tab.Messages.Add(new Message(currentUser.UserName, tab.Name, null, DateTime.UtcNow, null, tab.Message, ClrPcker_Background.SelectedColor.Value.ToString()));
                    tab.Message = string.Empty;
                    // textBox.Clear();
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("SocketException: {0}", ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex);
                tab.Messages.Add(new Message(null, tab.Name, null, DateTime.Now, null, "There was an exception in encrypting and sending the message. Make sure the private key is not corrupt or missing", Brushes.Red.ToString()));

            }

        }
        private void scrollViewer_Changed(object sender, ScrollChangedEventArgs e)
        {
            ScrollViewer scrollViewer = sender as ScrollViewer;
            if (e.ExtentHeightChange != 0)
                scrollViewer.ScrollToVerticalOffset(scrollViewer.ExtentHeight);
        }
        /// <summary>
        /// Connects to the server and authenticates the user. If the user is authenticated, a background worker thread is created 
        /// to listen to any server communication. This is done in a background thread to free up the main UI thread, as to prevent freezing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            /*
            TabItem ti = (TabItem)tabControl.SelectedItem;
            var tab = Tabs.FirstOrDefault(i => i.Name == ti.Header.ToString()); */
            var tab = tabControl.SelectedItem as ChatRoomView;
            try
            {
                tab.Messages.Add(new Message(null, tab.Name, null, DateTime.Now, "Connecting to Server...", "Connecting to Server...", Brushes.Blue.ToString()));
                serverIP = textBoxIP.Text;
                port = Convert.ToInt32(textBoxPort.Text);
                client = new TcpClient(); //causes the handle of the previous TcpClient to be lost
                client.Connect(serverIP, port);

                var clientCertificate = new X509Certificate2(ClientCertificateFile, ClientCertificatePassword);
                var clientCertificateCollection = new X509CertificateCollection(new X509Certificate[] { clientCertificate });

                //creates an SSL Stream that contains the TCP client socket as the underlying stream.
                sslStream = new SslStream(client.GetStream(), false, App_CertificateValidation);
                Console.WriteLine("Client connected.");
                sslStream.AuthenticateAsClient(ServerCertificateName, clientCertificateCollection, SslProtocols.Tls12, false);
                Console.WriteLine("SSL authentication completed.");
                tab.Messages.Add(new Message(null, tab.Name, null, DateTime.Now, null, "Authenticating User...", Brushes.Blue.ToString()));
                // Send username and password to sever for authentication
                BinaryWriter writer = new BinaryWriter(sslStream);
                writer.Write(textBoxUsername.Text);
                writer.Write(passwordBox.Password);
                writer.Write("Global"); // Chat group name sent
                writer.Flush();
                // Receieve response from server about authentication
                BinaryReader reader = new BinaryReader(sslStream);
                bool result = reader.ReadBoolean();
                string response = reader.ReadString();
                Console.WriteLine("Finished reading." + response);
                if (result == false)
                {
                    tab.Messages.Add(new Message(null, tab.Name, null, DateTime.Now, null, response, Brushes.Red.ToString()));
                    writer.Close();
                    sslStream.Close();
                    DropClient();
                }
                else
                {
                    currentUser = new User(textBoxUsername.Text, null);
                    buttonDisconnect.IsEnabled = true;
                    //buttonSend.IsEnabled = true;
                    buttonConnect.IsEnabled = false;
                    connectedUser = textBoxUsername.Text;
                    tab.Messages.Add(new Message(null, tab.Name, null, DateTime.Now, null, "Connected to Server: " + textBoxIP.Text, Brushes.Blue.ToString()));
                    backgroundWorker = new BackgroundWorker();
                    Console.WriteLine("Starting background worker listener");
                    backgroundWorker.DoWork += ServerListener;
                    backgroundWorker.RunWorkerCompleted += bgCheck_RunWorkerCompleted;
                    backgroundWorker.RunWorkerAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                DropClient();
            }
        }
        private void tabDynamic_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ChatRoomView tab = tabControl.SelectedItem as ChatRoomView;

            if (tab != null && tab.Name != null)
            {
                if (tab.Name.Equals("+"))
                {
                    string name = string.Empty;
                    PopupDialog popup = new PopupDialog();
                    popup.ShowDialog();
                    if (popup.Canceled == true)
                        return;
                    else
                    {
                        string result = popup.RoomName;
                        name = result;
                    }
                    if (!(name.Equals("+") || name == string.Empty))
                    {
                        ChatRoomView newTab = new ChatRoomView(name);
                        Tabs.Insert(Tabs.Count - 1, newTab);
                        tabControl.ItemsSource = Tabs;
                        tabControl.SelectedItem = newTab;
                        BinaryWriter writer = new BinaryWriter(sslStream);
                        writer.Write((int)Opcode.Join);
                        writer.Write(name);
                        writer.Flush();

                    }
                }
            }
        }
        /// <summary>
        /// Serialize object into byte array
        /// </summary>
        /// <param name="serializableObject"></param>
        /// <returns>byte array</returns>
        public static byte[] Serialize(object serializableObject)
        {
            using (var memoryStream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, serializableObject);
                return memoryStream.ToArray();
            }
        }
        /// <summary>
        /// Deserializes byte array
        /// </summary>
        /// <param name="serializedObject"></param>
        /// <returns>object</returns>
        public static object Deserialize(byte[] serializedObject)
        {
            using (var memoryStream = new MemoryStream(serializedObject))
                return (new BinaryFormatter()).Deserialize(memoryStream);
        }

        /// <summary>
        /// Authenticates the client's certificate.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        private static bool App_CertificateValidation(Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) { return true; }
            if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors) { return true; } //we don't have a proper certificate tree
            Console.WriteLine("*** SSL Error: " + sslPolicyErrors.ToString());
            return false;
        }
        /// <summary>
        /// Closes all client connections and sets the client to an unconnected state in the GUI.
        /// </summary>
        private void DropClient()
        {
            client.Close();
            sslStream.Close();
            // SEND TO ALL TABS
            foreach (var tab in Tabs)
            {
                if (tab != null)
                {
                    lock (tab._syncLock)
                    {
                        tab.Users.Clear();
                    }
                }
            }
            buttonDisconnect.IsEnabled = false;
            //buttonSend.IsEnabled = false;
            buttonConnect.IsEnabled = true;
        }
        /// <summary>
        /// Disconnects the client and updates the GUI to reflect the disconnection.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            DropClient();
        }
    }

}
