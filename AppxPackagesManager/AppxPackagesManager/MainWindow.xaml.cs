using AppxPackagesManager.ViewModels;
using System.Windows;

namespace AppxPackagesManager {
    public partial class MainWindow : Window {
        public MainWindow() {
            DataContext = new MainWindowViewModel();
            InitializeComponent();
        }
    }
}
