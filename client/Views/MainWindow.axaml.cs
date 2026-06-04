using Avalonia.Controls;
using QnEvt.ViewModels;
using System.ComponentModel;

namespace QnEvt.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnOpened(System.EventArgs e)
        {
            base.OnOpened(e);


            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(MainWindowViewModel.TerminalLogs))
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            var scroller = this.FindControl<ScrollViewer>("TerminalLogScroller");
                            scroller?.ScrollToEnd();
                        });
                    }
                };
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {

            if (DataContext is MainWindowViewModel vm)
            {
                vm.SaveSettings();
                if (vm.IsConnected)
                {

                    _ = vm.ToggleConnectionCommand.ExecuteAsync(null);
                }
            }
            base.OnClosing(e);
        }
    }
}
