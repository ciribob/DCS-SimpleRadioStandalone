using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow
{
    public partial class BrowseFolderControl : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty PathProperty =
            DependencyProperty.Register(nameof(Path), typeof(string), typeof(BrowseFolderControl), new PropertyMetadata(""));

        public static readonly DependencyProperty DialogTitleProperty =
            DependencyProperty.Register(nameof(DialogTitle), typeof(string), typeof(BrowseFolderControl), new PropertyMetadata("Select Directory"));

        public static readonly DependencyProperty DefaultPathProperty =
            DependencyProperty.Register(nameof(DefaultPath), typeof(string), typeof(BrowseFolderControl), new PropertyMetadata(""));

        public string Path
        {
            get => (string)GetValue(PathProperty);
            set
            {
                SetValue(PathProperty, value);
                // Notify UI that DirectoryName changed
                OnPropertyChanged(nameof(DirectoryName));
            }
        }

        public string DialogTitle
        {
            get => (string)GetValue(DialogTitleProperty);
            set => SetValue(DialogTitleProperty, value);
        }

        public string DefaultPath
        {
            get => (string)GetValue(DefaultPathProperty);
            set => SetValue(DefaultPathProperty, value);
        }

        public string DirectoryName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Path))
                    return Directory.GetCurrentDirectory(); //current working directory
                try
                {
                    return System.IO.Path.GetFileName(Path);
                }
                catch
                {
                    return Path;
                }
            }
        }

        public BrowseFolderControl()
        {
            InitializeComponent();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = DialogTitle;

                var initialPath = Path;
                #region Resolve initial path: if not rooted, combine with executable directory
                if (!string.IsNullOrWhiteSpace(initialPath) && !System.IO.Path.IsPathRooted(initialPath))
                {
                    string exeDir = Directory.GetCurrentDirectory();
                    initialPath = System.IO.Path.Combine(exeDir, initialPath);
                }
                #endregion
                dialog.SelectedPath = initialPath;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    Path = dialog.SelectedPath;
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            Path = DefaultPath; //back to default path
        }

        // Add this method to raise property changed notifications
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}