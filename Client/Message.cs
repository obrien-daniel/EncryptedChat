using System;
using System.IO;
using System.Security.Cryptography;

namespace Client
{
    /// <summary>
    /// This class creates a message with the user it was sent from and the message settings.
    /// </summary>
    [Serializable]
    public class Message
    {
        public User User { get; set; }
        public string Chatroom { get; set; }
        public string Sender { get; set; }
        public DateTime DateTimeStamp { get; set; }
        public string EncryptedMessage { get; set; }
        private byte[] _encryptedSymmetrickey;
        private byte[] _encryptedIV;

        [NonSerialized]
        private string _decryptedMessage;

        public string DecryptedMessage { get => _decryptedMessage; set => _decryptedMessage = value; }
        public string Font { get; set; }
        public string FontColor { get; set; }
        public int FontSize { get; set; }

        public Message(string sender, string chatroom, User user, DateTime dateTimeStamp, string encryptedMessage, string decryptedMessage, string fontColor)
        {
            Sender = sender ?? "SYSTEM";
            Chatroom = chatroom;
            User = user;
            DateTimeStamp = dateTimeStamp;
            EncryptedMessage = encryptedMessage;
            DecryptedMessage = decryptedMessage;
            FontColor = fontColor;
        }

        public void Encrypt()
        {
            if (DecryptedMessage == null || User == null) return;

            using Rijndael rijAlg = Rijndael.Create();
            // Create an encryptor to perform the stream transform.
            ICryptoTransform encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV);

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(DecryptedMessage);
                }
                EncryptedMessage = Convert.ToBase64String(msEncrypt.ToArray());
            }
            // Encrypt key and initialization vector
            _encryptedSymmetrickey = RSAEncrypt(rijAlg.Key);
            _encryptedIV = RSAEncrypt(rijAlg.IV);
        }

        public void Decrypt()
        {
            if (EncryptedMessage == null || EncryptedMessage.Length <= 0) return;
            if (_encryptedSymmetrickey == null || _encryptedSymmetrickey.Length <= 0) throw new ArgumentNullException("Key");
            if (_encryptedIV == null || _encryptedIV.Length <= 0) throw new ArgumentNullException("IV");

            byte[] key = RSADecrypt(_encryptedSymmetrickey);
            byte[] IV = RSADecrypt(_encryptedIV);
            // Create an Rijndael object  with the specified key and IV.
            using Rijndael rijAlg = Rijndael.Create();
            rijAlg.Key = key;
            rijAlg.IV = IV;

            // Create a decryptor to perform the stream transform.
            ICryptoTransform decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);

            byte[] cipherTextBytes = Convert.FromBase64String(EncryptedMessage);
            // Create the streams used for decryption.
            using MemoryStream msDecrypt = new MemoryStream(cipherTextBytes);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using StreamReader srDecrypt = new StreamReader(csDecrypt);
            // Read the decrypted bytes from the decrypting stream  and place them in a string.
            DecryptedMessage = srDecrypt.ReadToEnd();
        }

        private byte[] RSAEncrypt(byte[] message)
        {
            try
            {
                Console.WriteLine("Encrypting " + message.Length + " bytes.");
                //Create a new instance of RSACryptoServiceProvider.
                var rsa = new RSACryptoServiceProvider(2048);
                rsa.FromXmlString(User.PublicKey);
                //Encrypt the passed byte array and specify OAEP padding.
                return rsa.Encrypt(message, true);
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
        private byte[] RSADecrypt(byte[] message)
        {
            try
            {
                var parameters = new CspParameters
                {
                    KeyContainerName = $"{User.UserName}_keyContainer"
                };

                using var rsa = new RSACryptoServiceProvider(2048, parameters);

                //Decrypt the passed byte array and specify OAEP padding.
                return rsa.Decrypt(message, true);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }
    }
}