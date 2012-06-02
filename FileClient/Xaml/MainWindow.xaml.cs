using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace FileClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

        }

        /// <summary>
        /// Handles keyboard key press events.
        /// </summary>
        public void KeyEventHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                EnterDirectory();
            }
        }

        /// <summary>
        /// Handles double-click events on list items.
        /// </summary>
        public void DoubleClickHandler(object sender, MouseButtonEventArgs e)
        {
            EnterDirectory();
        }

        private void EnterDirectory()
        {
            var model = ViewModelLocator.FileBrowserModel;
            if (model.LeftPanelActive)
            {
                model.LeftPanel.EnterDirectory();
            }
            else
            {
                model.RightPanel.EnterDirectory();
            }
        }

        private void LeftPanelGotFocus(object sender, RoutedEventArgs e)
        {
            var model = ViewModelLocator.FileBrowserModel;
            model.LeftPanelActive = true;
        }

        private void RightPanelGotFocus(object sender, RoutedEventArgs e)
        {
            var model = ViewModelLocator.FileBrowserModel;
            model.LeftPanelActive = false;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            var model = ViewModelLocator.FileBrowserModel;
            model.Close();
        }
    }
}
