using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;

namespace Client
{
    public class ChatRoomView : INotifyPropertyChanged
    {
        private string name;
        public string Name { get => name; set { name = value; OnPropertyChanged(); } }
        private string message;
        public string Message { get => message; set { message = value; OnPropertyChanged(); } }
        public BindingList<Message> Messages { get; set; }
        //Binding list containing all connected users on the server
        public BindingList<User> Users { get; set; }
        //lock object for synchronization;
        public object _syncLock = new object();
        public object _syncLock2 = new object();

        public ChatRoomView(string name)
        {
            Name = name;
            Messages = new BindingList<Message>();
            Users = new BindingList<User>();
            BindingOperations.EnableCollectionSynchronization(Messages, _syncLock2);
            BindingOperations.EnableCollectionSynchronization(Users, _syncLock);
            //listBoxMessages.ItemsSource = messages;
            OnPropertyChanged();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
