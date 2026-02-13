using System.Windows;

namespace PureDesktop.Views
{
    public partial class InputDialog : Window
    {
        public string InputValue { get; private set; } = "";

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputBox.Text = defaultValue;
            InputBox.Focus();
            InputBox.SelectAll();
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            InputValue = InputBox.Text;
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
