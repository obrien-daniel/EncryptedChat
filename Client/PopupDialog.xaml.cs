using System.Windows;

namespace Client
{
    /// <summary>
    /// Interaction logic for PopupDialog.xaml
    /// </summary>
    public partial class PopupDialog : Window
    {
        //  internal bool canceled;
        //  internal string roomName;

        public PopupDialog()
        {
            InitializeComponent();
        }

        public bool Canceled { get; set; }
        public string RoomName { get; set; }

        private void ButtonCancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Canceled = true;
            RoomName = string.Empty;
            Close();
        }

        private void ButtnOk_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(InputTextBox.Text))
            {
                RoomName = InputTextBox.Text;
                Canceled = false;
                Close();
            }
            else
            {
                MessageBox.Show("Must provide a user name in the textbox.");
            }
        }
    }
}