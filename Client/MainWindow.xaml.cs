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
        //Binding list containing all connected users on the server
        public BindingList<User> users = new BindingList<User>();
        //lock object for synchronization;
        private static object _syncLock = new object();
        TcpClient client = new TcpClient();
        SslStream sslStream = null;
        BackgroundWorker backgroundWorker = null;
        private string serverIP = string.Empty;
        private int port = 0;
        private string connectedUser = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            //Enable the cross access to this collection elsewhere
            BindingOperations.EnableCollectionSynchronization(users, _syncLock);
            listBox.ItemsSource = users;
            buttonDisconnect.IsEnabled = false;
            buttonSend.IsEnabled = false;

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
                    string userName = reader.ReadString();
                    string publicKey = reader.ReadString();
                    User user = new User(userName, publicKey);
                    Console.WriteLine("adding user: " + user.UserName);
                    lock (_syncLock)
                    {
                        users.Add(user);
                    }
                    e.Result = null; //no message is passed on to UI
                }
                else if (OpCode == 2) //Update user list, occurs whenever a user connects/disconnects
                {
                    string userName = reader.ReadString(); //username of other client
                    string publicKey = reader.ReadString();
                    bool status = reader.ReadBoolean(); //connecting (true) or disconnecting (false)
                    string message = null;
                    User user = new User(userName, publicKey);
                    if (status)
                    {
                        lock (_syncLock)
                        {
                            users.Add(user);
                        }
                        message = userName + " has joined the chat.";
                    }
                    else
                    {
                        var usersCopy = new List<User>(users);
                        foreach (var item in usersCopy)
                        {
                            if (item.UserName == userName)
                            {
                                lock (_syncLock)
                                {
                                    users.Remove(item);
                                }
                            }
                        }
                        message = userName + " has left the chat.";
                    }
                    e.Result = message; //pass on message to UI
                }
                else if (OpCode == 3) //new message
                {
                    string username = reader.ReadString();
                    int count = reader.ReadInt32();
                    byte[] encryptedMessage = reader.ReadBytes(count);
                    //string privateKey = File.ReadAllText(@"..\..\..\Server\bin\Debug\PrivateKeys\" + connectedUser + "_privateKey.txt"); // dir for when running in visual studio
                    string privateKey = File.ReadAllText("./PrivateKeys/" + connectedUser + "_privateKey.txt"); //dir for when running release with client and server in same dir
                    string decryptedMessage = RSADecrypt(encryptedMessage, privateKey);
                    string message = username + ": " + decryptedMessage;
                    e.Result = message; //pass on message to UI
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
                listBox.ItemsSource = null;
                lock (_syncLock)
                {
                    listBox.ItemsSource = users;
                }
                // Update UI
                string response = e.Result as string;
                if (response != null)
                    textBlock.Text += Environment.NewLine + response;
                // Continue listening on server stream
                backgroundWorker.RunWorkerAsync();
            }
            else
            {
                textBlock.Text += Environment.NewLine + "You have been disconnected from the server.";
                DropClient();
            }
        }

        /// <summary>
        /// Sends encrypted message to each user currently connected to the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (client.Connected)
                {
                    BinaryWriter writer = new BinaryWriter(sslStream);
                    foreach (var user in users)
                    {
                        Console.WriteLine("Sending message to: " + user.UserName);
                        byte[] encryptedMessage = RSAEncrypt(textBox.Text, user.PublicKey);
                        int count = encryptedMessage.Count();
                        writer.Write(user.UserName);
                        writer.Write(count);
                        writer.Write(encryptedMessage); //Instead of just the encrypted message, send a serialized Message object that contains this
                        writer.Flush();
                    }

                    textBlock.Text += Environment.NewLine + connectedUser + ": " + textBox.Text;
                    textBox.Clear();
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("SocketException: {0}", ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex);
                textBlock.Text += Environment.NewLine + "There was an exception in encrypting and sending the message. Make sure the private key is not corrupt or missing";

            }

        }
        /// <summary>
        /// Encrypts a string with a public key using the RSA algorithm.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="publicKey"></param>
        /// <returns></returns>
        public static byte[] RSAEncrypt(string message, string publicKey)
        {
            try
            {
                byte[] dataToEncrypt = Encoding.ASCII.GetBytes(message); //using ASCII encoding because 1 char = 1 byte
                Console.WriteLine("Encrypting " + dataToEncrypt.Length + " bytes.");
                //Create a new instance of RSACryptoServiceProvider.
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(publicKey);
                //Encrypt the passed byte array and specify OAEP padding.  
                return rsa.Encrypt(dataToEncrypt, true);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
        /// <summary>
        /// Decrypts byte array messages with a private key using the RSA algorithm.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="privateKey"></param>
        /// <returns></returns>
        public static string RSADecrypt(byte[] message, string privateKey)
        {
            try
            {
                //Create a new instance of RSACryptoServiceProvider.
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(privateKey);
                //Decrypt the passed byte array and specify OAEP padding.  
                return Encoding.ASCII.GetString(rsa.Decrypt(message, true));
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.ToString());
                return "Error Decrypting Message. The private or public key may be corrupt or incorrect.";
            }
        }
        private void scrollViewer_Changed(object sender, ScrollChangedEventArgs e)
        {
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
            try
            {
                textBlock.Text = string.Empty;
                textBlock.Text += "Connecting to Server...";
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
                textBlock.Text += Environment.NewLine + "Authenticating User...";
                // Send username and password to sever for authentication
                BinaryWriter writer = new BinaryWriter(sslStream);
                writer.Write(textBoxUsername.Text);
                writer.Write(passwordBox.Password);
                writer.Write("Global Chat"); // Chat group name sent
                writer.Flush();
                // Receieve response from server about authentication
                BinaryReader reader = new BinaryReader(sslStream);
                bool result = reader.ReadBoolean();
                string response = reader.ReadString();
                if (result == false)
                {
                    textBlock.Text += Environment.NewLine + response;
                    writer.Close();
                    sslStream.Close();
                    DropClient();
                }
                else
                {
                    buttonDisconnect.IsEnabled = true;
                    buttonSend.IsEnabled = true;
                    buttonConnect.IsEnabled = false;
                    connectedUser = textBoxUsername.Text;
                    textBlock.Text += Environment.NewLine + "Connected to Server: " + textBoxIP.Text;
                    backgroundWorker = new BackgroundWorker();
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
            lock (_syncLock)
            {
                users.Clear();
            }
            buttonDisconnect.IsEnabled = false;
            buttonSend.IsEnabled = false;
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
