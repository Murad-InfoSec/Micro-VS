using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Micro_VS.View;

namespace Micro_VS
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Same MVP wiring as the original Program.Main.
                var view = new MainWindow();
                var presenter = new Presenter.Presenter(view, new Model.Model());
                desktop.MainWindow = view;
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
