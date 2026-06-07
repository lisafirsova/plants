using Microsoft.Extensions.DependencyInjection;

namespace Plants.Views;

public partial class PlantDetailPage : ContentPage, IQueryAttributable
{
    private readonly ViewModels.PlantDetailViewModel _viewModel;

    public PlantDetailPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<ViewModels.PlantDetailViewModel>();
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(value?.ToString(), out var id))
        {
            _viewModel.LoadPlant(id);
        }
    }
}
