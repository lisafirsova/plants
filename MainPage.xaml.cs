using Microsoft.Extensions.DependencyInjection;

namespace Plants;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<ViewModels.MainViewModel>();
    }
}
