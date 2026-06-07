using Microsoft.Extensions.DependencyInjection;

namespace Plants.Views;

public partial class PestLibraryPage : ContentPage
{
    public PestLibraryPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<ViewModels.PestLibraryViewModel>();
    }
}
