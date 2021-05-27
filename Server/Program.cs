using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Serialization;

namespace Server
{
    internal class Program
    {
        //Hashtable of all client sockets
        //public static Dictionary<User, SslStream> ClientList = new Dictionary<User,SslStream>(); // Global Chat Room
        public static Dictionary<string, Room> Rooms = new Dictionary<string, Room>();

        //Directory of server certificate used in SSL authentication
        private static readonly string _serverCertificateFile = "./Cert/server.pfx";

        private static readonly string _serverCertificatePassword = null;

        private static void Main(string[] args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            TcpListener server = null;
            TcpClient clientSocket = null;
            try
            {
                //read from the file
                X509Certificate2 serverCertificate = new X509Certificate2(_serverCertificateFile, _serverCertificatePassword);
                // Set the TcpListener on port 43594 at localhost.
                int port = 43594;
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");
                server = new TcpListener(localAddr, port);
                //Creates a socket for client communication
                clientSocket = default;
                // Start listening for client requests.
                server.Start();
                Console.WriteLine("TcpListener Started.");
                /*
                 * Read All chatrooms that are serialized or in database here and add them to the list
                 */
                Room globalChat = new Room("Daniel", "Global", true);
                Rooms.Add("Global", globalChat);
                /*
                Enter the listening loop. This will accept a TCP client and then attempt to authenticate the user.
                If the user is authenticated, a thread will be created to handle the client communication of the user,
                while the loop continues listening for new client connections.
                */
                while (true)
                {
                    clientSocket = server.AcceptTcpClient();
                    string clientIP = ((IPEndPoint)clientSocket.Client.RemoteEndPoint).Address.ToString();
                    Console.WriteLine("Incoming client connection from: " + clientIP);
                    SslStream sslStream = new SslStream(clientSocket.GetStream(), false, App_CertificateValidation);
                    Console.WriteLine("Accepted client " + clientSocket.Client.RemoteEndPoint.ToString());

                    sslStream.AuthenticateAsServer(serverCertificate, true, SslProtocols.Tls12, false);
                    Console.WriteLine("SSL authentication completed.");

                    // Read the username and password of incoming connection.
                    BinaryReader reader = new BinaryReader(sslStream);
                    string userName = reader.ReadString();
                    string password = reader.ReadString();
                    string chatRoom = reader.ReadString();
                    bool authResult = false;
                    string authMessage = string.Empty;
                    //create folders if they don't exist
                    Directory.CreateDirectory("./Users");
                    //Directory.CreateDirectory("./PublicKeys"); //Not used
                    Directory.CreateDirectory("./PrivateKeys");
                    User user = new User();
                    if (File.Exists("./Users/" + userName + ".xml")) //load user info if user exists
                    {
                        user = DeSerializeObject<User>("./Users/" + userName + ".xml");
                    }
                    else //if user does not exist, create one
                    {
                        user.Username = userName;
                        string saltedHashPassword = GenerateKeyHash(password);
                        user.Password = saltedHashPassword;
                        user.PublicKey = GenerateKeyPair(userName);
                        SerializeObject<User>(user, "./Users/" + userName + ".xml");
                    }

                    if (!ComparePasswords(user.Password, password))
                    {
                        authResult = false;
                        authMessage = "Your password or username is incorrect.";
                    }
                    else if (Rooms.TryGetValue(chatRoom, out Room r))
                    {
                        if (r.IsUsedLoggedIn(userName))
                        {
                            authResult = false;
                            authMessage = "You are already logged in.";
                        }
                        else
                            authResult = true;
                    }
                    else
                    {
                        authResult = true;
                    }
                    // send the authentication results back to the client if it failed
                    if (!authResult)
                    {
                        BinaryWriter writer = new BinaryWriter(sslStream);
                        writer.Write(authResult);
                        writer.Write(authMessage);
                        writer.Close();
                        sslStream.Close();
                    }
                    else
                    {
                        BinaryWriter writer = new BinaryWriter(sslStream);
                        writer.Write(authResult);
                        writer.Write(authMessage);
                        Console.WriteLine(user.Username + " has connected from: " + clientIP);
                        ClientHandler client = new ClientHandler(sslStream, user);
                        client.Start();
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
                clientSocket.Close();
            }

            // Placed this read input to stop it from closing immedietely due to exceptions, so we can see what the issue is.
            Console.WriteLine("\nHit enter to continue...");
            Console.Read();
        }

        /// <summary>
        /// Validate the server's certificate.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        private static bool App_CertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) { return true; }
            if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors) { return true; } //we don't have a proper certificate tree, as we are using a test certificate
            Console.WriteLine("*** SSL Error: " + sslPolicyErrors.ToString());
            return false;
        }

        /// <summary>
        /// Creates a salted hash of a password. This is used for as it is more secure than storing plaintext.
        /// </summary>
        /// <param name="Password"></param>
        /// <returns></returns>
        public static string GenerateKeyHash(string Password)
        {
            if (string.IsNullOrEmpty(Password)) return null;
            if (Password.Length < 1) return null;
            byte[] salt = new byte[20];
            byte[] key = new byte[20];
            byte[] ret = new byte[40];

            try
            {
                using (RNGCryptoServiceProvider randomBytes = new RNGCryptoServiceProvider())
                {
                    //generate random salt by using cryptographic random number generator
                    randomBytes.GetBytes(salt);
                    using Rfc2898DeriveBytes hashBytes = new Rfc2898DeriveBytes(Password, salt, 10000);
                    key = hashBytes.GetBytes(20);
                    Buffer.BlockCopy(salt, 0, ret, 0, 20);
                    Buffer.BlockCopy(key, 0, ret, 20, 20);
                }
                // returns salt/key pair
                return Convert.ToBase64String(ret);
            }
            finally
            {
                if (salt != null)
                    Array.Clear(salt, 0, salt.Length);
                if (key != null)
                    Array.Clear(key, 0, key.Length);
                if (ret != null)
                    Array.Clear(ret, 0, ret.Length);
            }
        }

        /// <summary>
        /// Compares a password against the stored salted password hash.
        /// </summary>
        /// <param name="PasswordHash"></param>
        /// <param name="Password"></param>
        /// <returns></returns>
        public static bool ComparePasswords(string PasswordHash, string Password)
        {
            if (string.IsNullOrEmpty(PasswordHash) || string.IsNullOrEmpty(Password)) return false;
            if (PasswordHash.Length < 40 || Password.Length < 1) return false;

            byte[] salt = new byte[20];
            byte[] key = new byte[20];
            byte[] hash = Convert.FromBase64String(PasswordHash);

            try
            {
                Buffer.BlockCopy(hash, 0, salt, 0, 20);
                Buffer.BlockCopy(hash, 20, key, 0, 20);

                using (Rfc2898DeriveBytes hashBytes = new Rfc2898DeriveBytes(Password, salt, 10000))
                {
                    byte[] newKey = hashBytes.GetBytes(20);
                    if (newKey != null)
                        if (newKey.SequenceEqual(key))
                            return true;
                }
                return false;
            }
            finally
            {
                if (salt != null)
                    Array.Clear(salt, 0, salt.Length);
                if (key != null)
                    Array.Clear(key, 0, key.Length);
                if (hash != null)
                    Array.Clear(hash, 0, hash.Length);
            }
        }

        /// <summary>
        /// Generates a private and public key pair. This is used just to create test keys.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public static string GenerateKeyPair(string userName)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048)
            {
                PersistKeyInCsp = false
            };
            string publicPrivateKeyXML = rsa.ToXmlString(true);
            string publicOnlyKeyXML = rsa.ToXmlString(false);
            //File.WriteAllText("./PublicKeys/" + userName + "_publicKey.txt", publicOnlyKeyXML); // not used
            File.WriteAllText("./PrivateKeys/" + userName + "_privateKey.txt", publicPrivateKeyXML);
            return publicOnlyKeyXML;
        }

        /// <summary>
        /// Serializes an object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializableObject"></param>
        /// <param name="fileName"></param>
        public static void SerializeObject<T>(T serializableObject, string fileName)
        {
            if (serializableObject == null) { return; }

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlSerializer serializer = new XmlSerializer(serializableObject.GetType());
                using MemoryStream stream = new MemoryStream();
                serializer.Serialize(stream, serializableObject);
                stream.Position = 0;
                xmlDocument.Load(stream);
                xmlDocument.Save(fileName);
                stream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("SerializeObject exception: " + ex);
            }
        }

        /// <summary>
        /// Deserializes an xml file into an object list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static T DeSerializeObject<T>(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) { return default; }

            T objectOut = default;

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(fileName);
                string xmlString = xmlDocument.OuterXml;

                using StringReader read = new StringReader(xmlString);
                Type outType = typeof(T);

                XmlSerializer serializer = new XmlSerializer(outType);
                using (XmlReader reader = new XmlTextReader(read))
                {
                    objectOut = (T)serializer.Deserialize(reader);
                    reader.Close();
                }

                read.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("DeSerializeObject exception: " + ex);
            }

            return objectOut;
        }
    }
}