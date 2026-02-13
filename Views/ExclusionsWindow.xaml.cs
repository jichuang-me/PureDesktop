using System.Linq;
using System.Windows;
using System.Windows.Input;
using PureDesktop.Core;

namespace PureDesktop.Views
{
    public partial class ExclusionsWindow : Window
    {
        private readonly FenceManager _fenceManager;

        public ExclusionsWindow(FenceManager fm)
        {
            InitializeComponent();
            _fenceManager = fm;
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            ExcludeExtBox.Text = string.Join(", ", _fenceManager.Settings.BlacklistExtensions);
            ExcludeFileBox.Text = string.Join("\n", _fenceManager.Settings.BlacklistFiles);
        }

        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            // Parse Extensions
            var exts = ExcludeExtBox.Text.Split(new[] { ',', ';', ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().StartsWith(".") ? s.Trim() : "." + s.Trim())
                .Distinct()
                .ToList();
            _fenceManager.Settings.BlacklistExtensions = exts;

            // Parse Files/Folders
            var lines = ExcludeFileBox.Text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            _fenceManager.Settings.BlacklistFiles = lines;

            _fenceManager.Save();
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
