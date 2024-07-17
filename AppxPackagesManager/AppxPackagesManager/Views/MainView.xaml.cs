using AppxPackagesManager.ViewModels;
using System.Windows;

namespace AppxPackagesManager {
    public partial class MainView : Window {
        public MainView() {
            DataContext = new MainViewViewModel();
            InitializeComponent();
        }
    }
}
