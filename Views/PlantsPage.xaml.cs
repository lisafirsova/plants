using Microsoft.Extensions.DependencyInjection;

namespace Plants.Views;

public partial class PlantsPage : ContentPage
{
    public PlantsPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<ViewModels.PlantsViewModel>();
    }
}
