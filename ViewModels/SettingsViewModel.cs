using System.Windows.Input;
using Plants.Helpers;

namespace Plants.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private bool _pushNotificationsEnabled = true;
    private bool _darkThemeEnabled = true;

    public SettingsViewModel()
    {
        ConnectGoogleCommand = new RelayCommand(() =>
            AlertRequested?.Invoke("Google", "Здесь будет подключение реальной Google-авторизации."));
        ExportReportCommand = new RelayCommand(() =>
            AlertRequested?.Invoke("Экспорт", "Экспорт отчетов пока работает как заглушка."));
    }

    public event Action<string, string>? AlertRequested;

    public bool PushNotificationsEnabled
    {
        get => _pushNotificationsEnabled;
        set => SetProperty(ref _pushNotificationsEnabled, value);
    }

    public bool DarkThemeEnabled
    {
        get => _darkThemeEnabled;
        set => SetProperty(ref _darkThemeEnabled, value);
    }

    public ICommand ConnectGoogleCommand { get; }
    public ICommand ExportReportCommand { get; }
}
