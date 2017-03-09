using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Controls;
namespace Client
{
    /// <summary>
    /// This class creates a message with the user it was sent from and the message settings.
    /// </summary>
    class Message
    {
        public User User { get; set; }
        public DateTime DateTimeStamp { get; set; }
        public string EncryptedMessage { get; set; }
        public string DecryptedMessage { get; set; }
        public FontFamily Font { get; set; }
        public int FontSize { get; set; }
        public Message(User User, DateTime DateTimeStamp, string EncryptedMessage, string DecryptedMessage, FontFamily Font, int FontSize)
        {
            this.User = User;
            this.DateTimeStamp = DateTimeStamp;
            this.EncryptedMessage = EncryptedMessage;
            this.DecryptedMessage = DecryptedMessage;
            this.Font = Font;
            this.FontSize = FontSize;
        }

    }
}
