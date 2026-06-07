using Microsoft.Extensions.DependencyInjection;

namespace Plants.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<ViewModels.SettingsViewModel>();
    }
}
