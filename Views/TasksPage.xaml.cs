using Microsoft.Extensions.DependencyInjection;

namespace Plants.Views;

public partial class TasksPage : ContentPage
{
    public TasksPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<ViewModels.TasksViewModel>();
    }
}
