using Microsoft.Extensions.DependencyInjection;

namespace Plants;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        Services = services;
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
    }

    public static IServiceProvider Services { get; private set; } = default!;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(Services.GetRequiredService<AppShell>());
    }
}
