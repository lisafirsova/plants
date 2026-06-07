using Microsoft.Extensions.Logging;
using Plants.Services;
using Plants.Services.Mocks;
using Plants.ViewModels;
using Plants.Views;

namespace Plants;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddSingleton<IPlantService, MockPlantService>();
        builder.Services.AddSingleton<IPestService, MockPestService>();
        builder.Services.AddSingleton<IPhotoService, MockPhotoService>();
        builder.Services.AddSingleton<ITaskService, MockTaskService>();

        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<PlantsViewModel>();
        builder.Services.AddTransient<PlantDetailViewModel>();
        builder.Services.AddTransient<PestLibraryViewModel>();
        builder.Services.AddTransient<IssueDetailViewModel>();
        builder.Services.AddTransient<TasksViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<PlantsPage>();
        builder.Services.AddTransient<PlantDetailPage>();
        builder.Services.AddTransient<PestLibraryPage>();
        builder.Services.AddTransient<IssueDetailPage>();
        builder.Services.AddTransient<TasksPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}
