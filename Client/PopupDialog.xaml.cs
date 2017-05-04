using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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

        private void buttonCancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Canceled = true;
            RoomName = String.Empty;
            Close();
        }

        private void buttnOk_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(InputTextBox.Text))
            {
                RoomName = InputTextBox.Text;
                Canceled = false;
                this.Close();
            }
            else
                MessageBox.Show("Must provide a user name in the textbox.");
          //  Close();
        }
    }
}
