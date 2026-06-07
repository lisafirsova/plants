using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Text;
using Android.Views;
using Android.Widget;
using AColor = Android.Graphics.Color;
using Plants.Models;
using Plants.Services;
using Plants.Services.Database;

namespace Plants;

[Activity(
    Theme = "@android:style/Theme.Material.NoActionBar",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : Activity
{
    private const int PickPhotoRequest = 4801;
    private const int GoogleSignInRequest = 4802;
    private const int AiPhotoRequest = 4803;
    private const int AdminIssuePhotoRequest = 4804;
    private readonly DatabaseService _database = DatabaseService.Instance;
    private AuthService? _authService;
    private NotificationService? _notificationService;
    private AiCareService? _aiCareService;
    private ReportService? _reportService;
    private User? _currentUser;
    private List<Plant> _plants = [];
    private Plant? _selectedPhotoPlant;
    private DateTime _pendingPhotoDate = DateTime.Today;
    private string? _pendingPhotoFilePath;
    private string _pendingPlantType = "Лиственные";
    private string _activeTab = "watering";
    private bool _googleSignInInProgress;
    private bool _guideSearchVisible;
    private bool _guideFilterVisible;
    private string _guideSearchQuery = string.Empty;
    private int _guidePlantTypeFilter;
    private string _calendarMode = "day";
    private DateTime _calendarAnchorDate = DateTime.Today;
    private string _photoSearchQuery = string.Empty;
    private int _photoSortMode;
    private readonly List<AiChatMessage> _aiChatMessages = [];
    private string? _pendingAiPhotoPath;
    private bool _aiChatBusy;
    private string? _pendingAdminImageData;
    private ImageView? _adminImagePreview;
    private bool _plantCreateInProgress;
    private bool _plantDeleteInProgress;

    private const string Bg = "#1C1C1C";
    private const string CardBg = "#2A2A2A";
    private const string SheetBg = "#2A2A2A";
    private const string InputBg = "#2A2A2A";
    private const string Border = "#3A3A3A";
    private const string SoftBorder = "#3A3A3A";
    private const string Text = "#FFFFFF";
    private const string Secondary = "#AAAAAA";
    private const string Muted = "#6B6B6B";
    private const string Mint = "#A8E6C3";
    private const string DarkText = "#1C1C1C";
    private const string Red = "#E05A5A";

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetStatusBarColor(AColor.ParseColor(Bg));
        Window?.SetNavigationBarColor(AColor.ParseColor(Bg));
        _authService = new AuthService(this, _database);
        _notificationService = new NotificationService(this);
        _aiCareService = new AiCareService(_database);
        _reportService = new ReportService(_database);
        RequestRuntimePermissions();

        try
        {
            await _database.InitializeAsync();
            var userId = _authService.GetCurrentUserId();
            _currentUser = userId is null ? null : await _database.GetUserAsync(userId.Value);
            if (_currentUser is null)
            {
                _currentUser = await _authService.TryRestoreGoogleSessionAsync();
                if (_currentUser is null)
                {
                    ShowWelcome();
                }
                else
                {
                    await PromoteOnlyUserToAdminAsync();
                    await LoadPlantsAsync();
                    if (_currentUser.IsAdmin)
                    {
                        ShowAdmin();
                    }
                    else
                    {
                        await ShowWatering(false);
                    }
                }
            }
            else
            {
                await PromoteOnlyUserToAdminAsync();
                await LoadPlantsAsync();
                if (_currentUser.IsAdmin)
                {
                    ShowAdmin();
                }
                else
                {
                    await ShowWatering(false);
                }
            }
        }
        catch (Exception ex)
        {
            await AlertAsync("Ошибка базы данных", ex.Message);
            ShowWelcome();
        }
    }

    private async Task LoadPlantsAsync()
    {
        if (_currentUser is null)
        {
            _plants = [];
            return;
        }

        _plants = await _database.GetPlantsByUserAsync(_currentUser.Id);
        if (_notificationService is not null)
        {
            foreach (var plant in _plants)
            {
                foreach (var schedule in await _database.GetSchedulesForPlantAsync(plant.Id))
                {
                    _notificationService.ScheduleWateringNotification(plant, schedule);
                }
            }
        }
    }

    private async Task PromoteOnlyUserToAdminAsync()
    {
        if (_currentUser is null || _currentUser.IsAdmin)
        {
            return;
        }

        var users = await _database.GetUsersAsync();
        if (users.Count == 1)
        {
            _currentUser.IsAdmin = true;
            await _database.UpdateUserAsync(_currentUser);
        }
    }

    private void ShowWelcome()
    {
        var root = Vertical();
        root.SetBackgroundColor(AColor.ParseColor(Bg));
        root.SetPadding(Dp(24), StatusBarInset() + Dp(28), Dp(24), Dp(28));

        root.AddView(Spacer(205));
        root.AddView(Icon("icon_potted_plant", 86, Mint), CenterFixed(86, 86));
        root.AddView(Spacer(30));
        AddCentered(root, "Добро пожаловать в Plants!", 22, Text, TypefaceStyle.Bold);
        AddCentered(root, "Наслаждайся уходом за растениями", 14, Secondary, TypefaceStyle.Normal);
        root.AddView(new Space(this), new LinearLayout.LayoutParams(1, 0, 1));
        root.AddView(PrimaryButton("Войти через Google", async () =>
        {
            if (_authService is null)
            {
                return;
            }

            try
            {
                if (_googleSignInInProgress)
                {
                    return;
                }

                _googleSignInInProgress = true;
                StartActivityForResult(_authService.GetGoogleSignInIntent(), GoogleSignInRequest);
            }
            catch (Exception ex)
            {
                _googleSignInInProgress = false;
                await AlertAsync("Ошибка входа", ex.Message);
            }
        }));
        SetContentView(root);
    }

    private async Task ShowWatering(bool photoReport)
    {
        _activeTab = "watering";
        await LoadPlantsAsync();
        var dueItems = photoReport ? [] : await GetDueScheduleRowsAsync();

        BuildScreen("Полив", content =>
        {
            if (_plants.Count == 0)
            {
                content.AddView(Spacer(122));
                content.AddView(Icon("icon_leaf", 90, Mint), CenterFixed(90, 90));
                content.AddView(Spacer(30));
                AddCentered(content, "У вас пока нет растений...", 20, Text, TypefaceStyle.Bold);
                AddCentered(content, "Добавьте растения для напоминаний\nо поливе", 14, Secondary, TypefaceStyle.Normal);
                content.AddView(Spacer(54));
                content.AddView(PrimaryButton("Добавить растение", () => ShowAddPlant("Лиственные")));
                return;
            }

            AddSegmented(content, photoReport);
            content.AddView(Spacer(18));
            if (_notificationService?.NotificationsEnabled() == false)
            {
                content.AddView(Pill("Разрешить уведомления", false, RequestNotificationPermission), FixedMatchHeight(42));
                content.AddView(Spacer(10));
            }

            if (photoReport)
            {
                foreach (var plant in _plants.Take(3))
                {
                    content.AddView(PhotoReportRow(plant));
                }
                return;
            }

            content.AddView(CalendarModeControl());
            content.AddView(CalendarPeriodNavigation());
            content.AddView(Spacer(10));

            if (dueItems.Count == 0)
            {
                AddCentered(content, "На выбранный период задач нет", 15, Secondary, TypefaceStyle.Normal);
                return;
            }

            foreach (var group in dueItems.GroupBy(x => x.OccurrenceDate.Date).OrderBy(x => x.Key))
            {
                AddScheduleGroup(content, CalendarDateTitle(group.Key), group.ToList());
            }
        });
    }

    private void ShowPlants()
    {
        _activeTab = "plants";
        BuildScreen("Растения", content =>
        {
            content.AddView(Spacer(30));
            var firstRow = Horizontal();
            firstRow.AddView(CategoryCard("icon_potted_plant", "Лиственные", "Яркие листья, цветки\nмалозаметны,\nдекоративные", "Добавить", () => ShowAddPlant("Лиственные")), Weight());
            firstRow.AddView(CategoryCard("icon_flower", "Цветущие", "Яркие крупные\nцветы, декоративные\nлистья", "Добавить", () => ShowAddPlant("Цветущие")), Weight());
            content.AddView(firstRow);

            var secondRow = Horizontal();
            secondRow.AddView(CategoryCard("icon_cactus", "Суккуленты", "Толстые сочные\nлистья, запасают\nводу", "Добавить", () => ShowAddPlant("Суккуленты")), Weight());
            secondRow.AddView(CategoryCard("icon_pencil", "Редакция", "Редактировать уже\nсуществующие\nрастения", "Редакция", ShowEditPlant), Weight());
            content.AddView(secondRow);

            content.AddView(Spacer(14));
            content.AddView(PrimaryButton("Отчёты моего сада", () => ShowReports()));
            AddMuted(content, "План ухода, статистика, динамика роста и список активных растений");
        }, rightText: "⎋", rightAction: () => _ = SignOutAsync());
    }

    private async void ShowAddPlant(string plantType = "Лиственные")
    {
        _pendingPlantType = plantType;
        var species = await _database.GetPlantSpeciesAsync(plantType);
        BuildScreen("Полив название", content =>
        {
            AddSection(content, "Имя");
            var nameInput = Input("Введите имя");
            content.AddView(nameInput);
            AddMuted(content, "Вид растения");
            var speciesSpinner = new Spinner(this);
            speciesSpinner.Adapter = new ArrayAdapter<string>(
                this,
                Android.Resource.Layout.SimpleSpinnerDropDownItem,
                new[] { "Не выбран" }.Concat(species.Select(x => x.DisplayName)).ToList());
            content.AddView(speciesSpinner, FixedMatchHeight(52));
            var locationInput = Input("Расположение");
            var notesInput = Input("Заметки");
            var phaseSpinner = SimpleSpinner(["Активный рост", "Покой", "Бутонизация", "Цветение", "Адаптация"]);
            var healthSpinner = SimpleSpinner(["Здорово", "Требует внимания", "На лечении"]);
            content.AddView(TwoInputs("Фаза жизненного цикла", phaseSpinner, "Состояние", healthSpinner));
            content.AddView(locationInput);
            content.AddView(notesInput);
            var wateringToggle = ToggleCheck("Полив", true);
            content.AddView(wateringToggle);
            AddSection(content, "Задайте расписание полива растений");
            var waterDate = DateInput(DateTime.Today, DateTime.Today.AddDays(-3));
            var waterPeriod = PeriodInput();
            content.AddView(TwoInputs("Дата отсчета", waterDate, "Периодичность", waterPeriod.View));
            AddSection(content, "Задайте время уведомления полива");
            AddMuted(content, "Время уведомления");
            var waterTime = TimeInput(new TimeSpan(9, 0, 0));
            content.AddView(waterTime);
            var fertilizerToggle = ToggleCheck("Удобрения", false);
            content.AddView(fertilizerToggle);
            AddSection(content, "Задайте расписание удобрения\nрастений");
            var fertilizerDate = DateInput(DateTime.Today, DateTime.Today.AddDays(-3));
            var fertilizerPeriod = PeriodInput();
            content.AddView(TwoInputs("Дата отсчета", fertilizerDate, "Периодичность", fertilizerPeriod.View));
            AddSection(content, "Задайте время уведомления");
            AddMuted(content, "Время уведомления");
            var fertilizerTime = TimeInput(new TimeSpan(9, 0, 0));
            content.AddView(fertilizerTime);
            var pruningToggle = ToggleCheck("Обрезка", false);
            content.AddView(pruningToggle);
            AddSection(content, "Задайте расписание обрезки");
            var pruningDate = DateInput(DateTime.Today, DateTime.Today.AddDays(-3));
            var pruningPeriod = PeriodInput(3, "month");
            content.AddView(TwoInputs("Дата отсчета", pruningDate, "Периодичность", pruningPeriod.View));
            AddMuted(content, "Время уведомления");
            var pruningTime = TimeInput(new TimeSpan(10, 0, 0));
            content.AddView(pruningTime);
            var repottingToggle = ToggleCheck("Пересадка", false);
            content.AddView(repottingToggle);
            AddSection(content, "Задайте расписание пересадки");
            var repottingDate = DateInput(DateTime.Today, DateTime.Today.AddDays(-3));
            var repottingPeriod = PeriodInput(12, "month");
            content.AddView(TwoInputs("Дата отсчета", repottingDate, "Периодичность", repottingPeriod.View));
            AddMuted(content, "Время уведомления");
            var repottingTime = TimeInput(new TimeSpan(10, 0, 0));
            content.AddView(repottingTime);
            content.AddView(Spacer(16));
            Button? addPlantButton = null;
            addPlantButton = PrimaryButton("Добавить растение", async () =>
            {
                if (_plantCreateInProgress)
                {
                    return;
                }

                _plantCreateInProgress = true;
                addPlantButton.Enabled = false;
                addPlantButton.Text = "Сохраняем...";
                try
                {
                    await AddPlantAsync(
                        nameInput,
                        species.ElementAtOrDefault(speciesSpinner.SelectedItemPosition - 1),
                        phaseSpinner, healthSpinner, locationInput, notesInput,
                        wateringToggle, waterDate, waterPeriod, waterTime,
                        fertilizerToggle, fertilizerDate, fertilizerPeriod, fertilizerTime,
                        pruningToggle, pruningDate, pruningPeriod, pruningTime,
                        repottingToggle, repottingDate, repottingPeriod, repottingTime);
                }
                finally
                {
                    _plantCreateInProgress = false;
                    if (addPlantButton.IsAttachedToWindow)
                    {
                        addPlantButton.Enabled = true;
                        addPlantButton.Text = "Добавить растение";
                    }
                }
            });
            content.AddView(addPlantButton);
        }, showBottomNav: false, back: async () => await ShowWatering(false));
    }

    private async void ShowEditPlant()
    {
        await LoadPlantsAsync();
        BuildScreen("Редакция название", content =>
        {
            var waterBlock = Vertical();
            AddSection(waterBlock, "Задайте расписание полива растений");
            var waterDate = DateInput(DateTime.Today, DateTime.Today.AddDays(-3));
            var waterPeriod = PeriodInput();
            waterBlock.AddView(TwoInputs("Дата отсчета", waterDate, "Периодичность", waterPeriod.View));
            AddSection(waterBlock, "Задайте время уведомления");
            AddMuted(waterBlock, "Время уведомления");
            var waterTime = TimeInput(new TimeSpan(9, 0, 0));
            waterBlock.AddView(waterTime);
            content.AddView(waterBlock);

            AddSection(content, "Имя");
            var plantSpinner = new Spinner(this);
            plantSpinner.Background = Rounded(InputBg, Border, 14, 2);
            plantSpinner.SetPadding(Dp(14), 0, Dp(14), 0);
            plantSpinner.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, _plants.Select(x => x.Name).ToList());
            content.AddView(plantSpinner, FixedMatchHeight(52));
            var plantNameInput = Input("Имя растения");
            var phaseSpinner = SimpleSpinner(["Активный рост", "Покой", "Бутонизация", "Цветение", "Адаптация"]);
            var healthSpinner = SimpleSpinner(["Здорово", "Требует внимания", "На лечении"]);
            var locationInput = Input("Расположение");
            var notesInput = Input("Заметки");
            content.AddView(plantNameInput);
            content.AddView(TwoInputs("Фаза жизненного цикла", phaseSpinner, "Состояние", healthSpinner));
            content.AddView(locationInput);
            content.AddView(notesInput);

            var toggles = Horizontal();
            var wateringToggle = ToggleCheck("Полив", true);
            var fertilizerToggle = ToggleCheck("Удобрения", false);
            toggles.AddView(wateringToggle, Weight());
            toggles.AddView(fertilizerToggle, Weight());
            content.AddView(toggles);

            var extraToggles = Horizontal();
            var pruningToggle = ToggleCheck("Обрезка", false);
            var repottingToggle = ToggleCheck("Пересадка", false);
            extraToggles.AddView(pruningToggle, Weight());
            extraToggles.AddView(repottingToggle, Weight());
            content.AddView(extraToggles);

            var fertilizerBlock = Vertical();
            AddSection(fertilizerBlock, "Задайте расписание удобрения растений");
            var fertilizerDate = DateInput(DateTime.Today, DateTime.Today.AddDays(-3));
            var fertilizerPeriod = PeriodInput();
            fertilizerBlock.AddView(TwoInputs("Дата отсчета", fertilizerDate, "Периодичность", fertilizerPeriod.View));
            AddSection(fertilizerBlock, "Задайте время уведомления удобрения");
            AddMuted(fertilizerBlock, "Время уведомления");
            var fertilizerTime = TimeInput(new TimeSpan(9, 0, 0));
            fertilizerBlock.AddView(fertilizerTime);
            content.AddView(fertilizerBlock);

            var pruningBlock = Vertical();
            AddSection(pruningBlock, "Задайте расписание обрезки");
            var pruningDate = DateInput(DateTime.Today, DateTime.Today.AddDays(-3));
            var pruningPeriod = PeriodInput(3, "month");
            pruningBlock.AddView(TwoInputs("Дата отсчета", pruningDate, "Периодичность", pruningPeriod.View));
            AddMuted(pruningBlock, "Время уведомления");
            var pruningTime = TimeInput(new TimeSpan(10, 0, 0));
            pruningBlock.AddView(pruningTime);
            content.AddView(pruningBlock);

            var repottingBlock = Vertical();
            AddSection(repottingBlock, "Задайте расписание пересадки");
            var repottingDate = DateInput(DateTime.Today, DateTime.Today.AddDays(-3));
            var repottingPeriod = PeriodInput(12, "month");
            repottingBlock.AddView(TwoInputs("Дата отсчета", repottingDate, "Периодичность", repottingPeriod.View));
            AddMuted(repottingBlock, "Время уведомления");
            var repottingTime = TimeInput(new TimeSpan(10, 0, 0));
            repottingBlock.AddView(repottingTime);
            content.AddView(repottingBlock);
            content.AddView(new Space(this), new LinearLayout.LayoutParams(1, Dp(80)));
            void UpdateBlocks()
            {
                waterBlock.Visibility = wateringToggle.Checked ? ViewStates.Visible : ViewStates.Gone;
                fertilizerBlock.Visibility = fertilizerToggle.Checked ? ViewStates.Visible : ViewStates.Gone;
                pruningBlock.Visibility = pruningToggle.Checked ? ViewStates.Visible : ViewStates.Gone;
                repottingBlock.Visibility = repottingToggle.Checked ? ViewStates.Visible : ViewStates.Gone;
            }
            wateringToggle.Click += (_, _) => UpdateBlocks();
            fertilizerToggle.Click += (_, _) => UpdateBlocks();
            pruningToggle.Click += (_, _) => UpdateBlocks();
            repottingToggle.Click += (_, _) => UpdateBlocks();
            plantSpinner.ItemSelected += async (_, args) =>
            {
                var plant = _plants.ElementAtOrDefault(args.Position);
                if (plant is not null)
                {
                    plantNameInput.Text = plant.Name;
                    phaseSpinner.SetSelection(SpinnerIndex(phaseSpinner, plant.LifecyclePhase));
                    healthSpinner.SetSelection(SpinnerIndex(healthSpinner, plant.HealthStatus));
                    locationInput.Text = plant.Location;
                    notesInput.Text = plant.Notes;
                    await PopulateEditFieldsAsync(
                        plant,
                        wateringToggle, waterDate, waterPeriod, waterTime,
                        fertilizerToggle, fertilizerDate, fertilizerPeriod, fertilizerTime,
                        pruningToggle, pruningDate, pruningPeriod, pruningTime,
                        repottingToggle, repottingDate, repottingPeriod, repottingTime);
                    UpdateBlocks();
                }
            };
            UpdateBlocks();
            content.AddView(PrimaryButton("Редактировать растение", async () =>
            {
                var plant = _plants.ElementAtOrDefault(plantSpinner.SelectedItemPosition);
                if (plant is not null)
                {
                    await UpdatePlantAsync(
                        plant,
                        plantNameInput, phaseSpinner, healthSpinner, locationInput, notesInput,
                        wateringToggle, waterDate, waterPeriod, waterTime,
                        fertilizerToggle, fertilizerDate, fertilizerPeriod, fertilizerTime,
                        pruningToggle, pruningDate, pruningPeriod, pruningTime,
                        repottingToggle, repottingDate, repottingPeriod, repottingTime);
                }
            }));
            content.AddView(Spacer(10));
            var deleteButton = Pill("Удалить растение", false, async () =>
            {
                if (_plantDeleteInProgress)
                {
                    return;
                }

                var plant = _plants.ElementAtOrDefault(plantSpinner.SelectedItemPosition);
                if (plant is null)
                {
                    await AlertAsync("Удаление", "Выберите растение.");
                    return;
                }
                if (!await ConfirmAsync(
                        "Удаление растения",
                        $"Удалить «{plant.Name}» вместе с расписаниями, задачами и фотоархивом? Отменить это действие будет нельзя."))
                {
                    return;
                }

                _plantDeleteInProgress = true;
                try
                {
                    await DeleteUserPlantAsync(plant);
                }
                finally
                {
                    _plantDeleteInProgress = false;
                }
            });
            deleteButton.SetTextColor(AColor.ParseColor(Red));
            deleteButton.Background = Rounded(Bg, Red, 26, 2);
            content.AddView(deleteButton, FixedMatchHeight(52));
        }, showBottomNav: false, back: ShowPlants);
    }

    private async Task DeleteUserPlantAsync(Plant plant)
    {
        if (_currentUser is null || plant.UserId != _currentUser.Id)
        {
            await AlertAsync("Нет доступа", "Можно удалять только собственные растения.");
            return;
        }

        try
        {
            var photos = await _database.GetPhotosAsync(plant.Id);
            _notificationService?.CancelNotification(plant.Id);
            var deleted = await _database.DeletePlantAsync(plant.Id);
            if (deleted == 0)
            {
                await AlertAsync("Удаление", "Растение уже было удалено.");
                await LoadPlantsAsync();
                ShowPlants();
                return;
            }

            foreach (var photo in photos)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(photo.FilePath) && File.Exists(photo.FilePath))
                    {
                        File.Delete(photo.FilePath);
                    }
                }
                catch (IOException)
                {
                    // Запись в БД уже удалена; недоступный локальный файл не мешает завершить операцию.
                }
            }

            if (_selectedPhotoPlant?.Id == plant.Id)
            {
                _selectedPhotoPlant = null;
            }
            await LoadPlantsAsync();
            await AlertAsync("Готово", $"Растение «{plant.Name}» удалено.");
            ShowPlants();
        }
        catch (Exception ex)
        {
            await AlertAsync("Ошибка PostgreSQL", ex.Message);
        }
    }

    private async void ShowPhotos(bool addMode)
    {
        _selectedPhotoPlant ??= _plants.FirstOrDefault();
        var photos = _selectedPhotoPlant is null ? [] : await _database.GetPhotosAsync(_selectedPhotoPlant.Id);
        BuildScreen("Фото название", content =>
        {
            content.AddView(Spacer(30));
            content.AddView(PhotoFilterPanel());
            var filteredPhotos = FilterPhotos(photos);
            if (filteredPhotos.Count == 0)
            {
                AddCentered(content, photos.Count == 0 ? "В фотоархиве пока нет фотографий" : "Ничего не найдено", 14, Secondary, TypefaceStyle.Normal);
            }
            foreach (var photo in filteredPhotos)
            {
                content.AddView(PhotoRow(photo));
            }
            content.AddView(new Space(this), new LinearLayout.LayoutParams(1, addMode ? Dp(170) : Dp(260)));

            if (!addMode)
            {
                content.AddView(PrimaryButton("Добавить фото", () => ShowPhotos(true)));
            }
            else
            {
                content.AddView(AddPhotoPanel());
            }
        }, showBottomNav: !addMode, back: async () => await ShowWatering(true));
    }

    private void ShowGuide()
    {
        _activeTab = "guide";
        BuildScreen("Справочник", content =>
        {
            content.AddView(Spacer(30));
            if (_guideSearchVisible)
            {
                var search = Input("Поиск по справочнику");
                search.Text = _guideSearchQuery;
                search.TextChanged += (_, _) =>
                {
                    _guideSearchQuery = search.Text?.Trim() ?? string.Empty;
                };
                content.AddView(search);
            }

            if (_guideFilterVisible)
            {
                content.AddView(GuideFilterPanel());
            }

            var row = Horizontal();
            row.AddView(GuideCard("icon_pest", "Вредители", "Вредители\nразрушают листья и\nстебли растений", () => ShowPestsList("Вредители")), Weight());
            row.AddView(GuideCard("icon_virus", "Болезни", "Болезни вредят\nросту, нужно лечить\nбыстро", () => ShowPestsList("Болезни")), Weight());
            content.AddView(row);
            content.AddView(Spacer(18));
            content.AddView(AiEntryCard());
        }, rightText: "__guide_actions");
    }

    private async void ShowAiChat()
    {
        if (_aiCareService is null)
        {
            return;
        }

        await LoadPlantsAsync();
        BuildScreen("ИИ-консультант", content =>
        {
            content.AddView(Spacer(18));
            if (_aiChatMessages.Count == 0)
            {
                content.AddView(Icon("icon_ai", 58, Mint), CenterFixed(58, 58));
                AddCentered(content, "Спросите о своём растении", 18, Text, TypefaceStyle.Bold);
                AddCentered(content, "Можно приложить фотографию листьев, стебля или грунта", 13, Secondary, TypefaceStyle.Normal);
                content.AddView(Spacer(18));
            }

            foreach (var message in _aiChatMessages)
            {
                content.AddView(AiChatBubble(message));
            }

            if (!string.IsNullOrWhiteSpace(_pendingAiPhotoPath))
            {
                var preview = Horizontal();
                preview.SetGravity(GravityFlags.CenterVertical);
                preview.AddView(PhotoPreview(_pendingAiPhotoPath, 64), Fixed(72, 64));
                preview.AddView(Txt("Фото прикреплено", 13, Mint, TypefaceStyle.Bold), Weight());
                preview.AddView(Pill("Убрать", false, () =>
                {
                    _pendingAiPhotoPath = null;
                    ShowAiChat();
                }), Fixed(104, 36));
                content.AddView(preview);
            }

            var question = Input("Напишите вопрос о растении");
            question.SetMinLines(3);
            question.Gravity = GravityFlags.Top;
            content.AddView(question, FixedMatchHeight(96));

            var actions = Horizontal();
            actions.AddView(Pill("Прикрепить фото", false, PickAiPhoto), Weight());
            actions.AddView(Pill(_aiChatBusy ? "Ожидание..." : "Отправить", true, async () =>
            {
                await SendAiChatMessageAsync(question.Text?.Trim() ?? string.Empty);
            }), Weight());
            content.AddView(actions);
        }, back: ShowGuide, rightText: _aiChatMessages.Count == 0 ? string.Empty : "×", rightAction: () =>
        {
            _aiChatMessages.Clear();
            _pendingAiPhotoPath = null;
            ShowAiChat();
        });
    }

    private async Task SendAiChatMessageAsync(string question)
    {
        if (_aiCareService is null || _aiChatBusy)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(question) && string.IsNullOrWhiteSpace(_pendingAiPhotoPath))
        {
            await AlertAsync("ИИ-консультант", "Введите вопрос или прикрепите фотографию.");
            return;
        }

        question = string.IsNullOrWhiteSpace(question)
            ? "Проанализируй фотографию растения: есть ли признаки болезни, вредителей, необходимости пересадки или обрезки?"
            : question;
        var history = _aiChatMessages.ToList();
        var imagePath = _pendingAiPhotoPath;
        _aiChatMessages.Add(new AiChatMessage { IsUser = true, Text = question, ImagePath = imagePath });
        _pendingAiPhotoPath = null;
        _aiChatBusy = true;
        ShowAiChat();

        try
        {
            var answer = await _aiCareService.AskAsync(_plants, history, question, imagePath);
            _aiChatMessages.Add(new AiChatMessage { IsUser = false, Text = answer });
        }
        catch (Exception ex)
        {
            _aiChatMessages.Add(new AiChatMessage
            {
                IsUser = false,
                Text = $"Не удалось получить ответ: {ex.Message}"
            });
        }
        finally
        {
            _aiChatBusy = false;
            ShowAiChat();
        }
    }

    private void PickAiPhoto()
    {
        var intent = new Intent(Intent.ActionGetContent);
        intent.SetType("image/*");
        intent.AddCategory(Intent.CategoryOpenable);
        StartActivityForResult(Intent.CreateChooser(intent, "Выберите фото растения"), AiPhotoRequest);
    }

    private async void ShowPestsList(string title)
    {
        var items = title == "Вредители"
            ? await _database.GetPestsAsync(true, SelectedGuidePlantType(), _guideSearchQuery)
            : await _database.GetPestsAsync(false, SelectedGuidePlantType(), _guideSearchQuery);
        BuildScreen(title, content =>
        {
            content.AddView(Spacer(30));
            if (items.Count == 0)
            {
                AddCentered(content, "Ничего не найдено", 15, Secondary, TypefaceStyle.Normal);
                return;
            }

            foreach (var item in items)
            {
                content.AddView(PestRow(item));
            }
        }, redHeader: true, back: ShowGuide);
    }

    private void ShowTreatment(Pest issue)
    {
        BuildScreen(issue.Name, content =>
        {
            content.AddView(IssueImage(issue, 220), new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                Dp(220)));
            AddSection(content, "Способ лечения");
            var body = Txt(issue.TreatmentDescription, 14, Secondary, TypefaceStyle.Normal);
            body.Gravity = GravityFlags.Left;
            body.SetLineSpacing(0, 1.25f);
            content.AddView(body);
            if (_currentUser?.IsAdmin != true && _plants.Count > 0)
            {
                content.AddView(Spacer(20));
                content.AddView(PrimaryButton("Отметить у моего растения", async () =>
                {
                    await AddIssueObservationAsync(issue);
                }));
            }
        }, redHeader: true, back: () => ShowPestsList(issue.IsPest ? "Вредители" : "Болезни"));
    }

    private async Task AddIssueObservationAsync(Pest issue)
    {
        var spinner = new Spinner(this);
        spinner.Adapter = new ArrayAdapter<string>(
            this,
            Android.Resource.Layout.SimpleSpinnerDropDownItem,
            _plants.Select(x => x.Name).ToList());
        var note = Input("Заметка (необязательно)");
        var body = Vertical();
        body.SetPadding(Dp(18), 0, Dp(18), 0);
        body.AddView(spinner, FixedMatchHeight(52));
        body.AddView(note);

        var completion = new TaskCompletionSource<bool>();
        new AlertDialog.Builder(this)
            .SetTitle(issue.IsPest ? "Обнаружен вредитель" : "Обнаружена болезнь")
            .SetView(body)
            .SetPositiveButton("Сохранить", (_, _) => completion.TrySetResult(true))
            .SetNegativeButton("Отмена", (_, _) => completion.TrySetResult(false))
            .Show();
        if (!await completion.Task)
        {
            return;
        }

        var plant = _plants.ElementAtOrDefault(spinner.SelectedItemPosition);
        if (plant is null)
        {
            return;
        }
        await _database.AddIssueObservationAsync(new IssueObservation
        {
            PlantId = plant.Id,
            ReferenceItemId = issue.Id,
            ObservedAt = DateTime.Today,
            Note = note.Text?.Trim() ?? string.Empty
        });
        await AlertAsync("Готово", "Наблюдение сохранено и попадёт в статистический отчёт.");
    }

    private LinearLayout GuideFilterPanel()
    {
        var panel = Vertical();
        panel.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
        panel.Background = Rounded(CardBg, Border, 14, 1);
        panel.LayoutParameters = MarginMatch(0, 0, 0, Dp(16));

        AddMuted(panel, "Показывать вредителей и болезни для");
        var plantType = new Spinner(this);
        plantType.Background = Rounded(InputBg, Border, 10, 1);
        plantType.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, new[] { "Все растения", "Лиственные", "Цветущие", "Кактусы и суккуленты" });
        plantType.SetSelection(_guidePlantTypeFilter);
        plantType.ItemSelected += (_, args) => _guidePlantTypeFilter = args.Position;
        panel.AddView(plantType, FixedMatchHeight(48));

        var actions = Horizontal();
        actions.AddView(Pill("Применить", true, ShowGuide), Weight());
        actions.AddView(Pill("Сбросить", false, () =>
        {
            _guideSearchQuery = string.Empty;
            _guidePlantTypeFilter = 0;
            _guideSearchVisible = false;
            _guideFilterVisible = false;
            ShowGuide();
        }), Weight());
        panel.AddView(actions);
        return panel;
    }

    private string SelectedGuidePlantType() => _guidePlantTypeFilter switch
    {
        1 => "Лиственные",
        2 => "Цветущие",
        3 => "Суккуленты",
        _ => string.Empty
    };

    private LinearLayout PhotoFilterPanel()
    {
        var panel = Vertical();
        panel.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
        panel.Background = Rounded(CardBg, Border, 14, 1);
        panel.LayoutParameters = MarginMatch(0, 0, 0, Dp(14));

        var search = Input("Поиск по названию фотографии");
        search.Text = _photoSearchQuery;
        panel.AddView(search);
        var sorting = SimpleSpinner(["Сначала новые", "Сначала старые", "По названию"]);
        sorting.SetSelection(_photoSortMode);
        panel.AddView(sorting, FixedMatchHeight(48));

        var actions = Horizontal();
        actions.AddView(Pill("Применить", true, () =>
        {
            _photoSearchQuery = search.Text?.Trim() ?? string.Empty;
            _photoSortMode = sorting.SelectedItemPosition;
            ShowPhotos(false);
        }), Weight());
        actions.AddView(Pill("Сбросить", false, () =>
        {
            _photoSearchQuery = string.Empty;
            _photoSortMode = 0;
            ShowPhotos(false);
        }), Weight());
        panel.AddView(actions);
        return panel;
    }

    private List<Photo> FilterPhotos(IEnumerable<Photo> photos)
    {
        var query = photos.Where(x =>
            string.IsNullOrWhiteSpace(_photoSearchQuery) ||
            x.Title.Contains(_photoSearchQuery, StringComparison.OrdinalIgnoreCase));
        query = _photoSortMode switch
        {
            1 => query.OrderBy(x => x.DateTaken),
            2 => query.OrderBy(x => x.Title),
            _ => query.OrderByDescending(x => x.DateTaken)
        };
        return query.ToList();
    }

    private async void ShowAdmin(string sheet = "")
    {
        if (_currentUser?.IsAdmin != true)
        {
            _ = AlertAsync("Нет доступа", "Панель администратора доступна только администратору.");
            return;
        }

        List<Pest> records = [];
        List<PlantSpecies> species = [];
        List<ProtectionProduct> protectionProducts = [];
        Dictionary<string, decimal> settings = [];
        if (sheet is "edit" or "delete")
        {
            try
            {
                records = await _database.GetAllPestRecordsAsync();
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка PostgreSQL", ex.Message);
            }
        }
        if (sheet == "species")
        {
            species = await _database.GetPlantSpeciesAsync();
        }
        if (sheet == "settings")
        {
            settings = await _database.GetRecommendationSettingsAsync();
        }
        if (sheet == "protection")
        {
            protectionProducts = await _database.GetProtectionProductsAsync();
        }

        BuildScreen("Панель администратора", content =>
        {
            var row1 = Horizontal();
            row1.AddView(AdminCard("icon_plus_circle", "Добавление", "Добавить новую\nинформацию", "Добавить", () => OpenAdminEditor("add")), Weight());
            row1.AddView(AdminCard("icon_minus_circle", "Удаление", "Удалить новую\nинформацию", "Удалить", () => ShowAdmin("delete")), Weight());
            content.AddView(row1);

            var row2 = Horizontal();
            row2.AddView(AdminCard("icon_pencil", "Редакция", "Редактировать новую\nинформацию", "Редакция", () => OpenAdminEditor("edit")), Weight());
            row2.AddView(AdminCard("icon_pencil", "Отчёты", "Просмотр и экспорт\nаналитической\nотчётности", "Открыть", () => ShowReports()), Weight());
            content.AddView(row2);

            var row3 = Horizontal();
            row3.AddView(AdminCard("icon_potted_plant", "Виды растений", "Добавление и\nредактирование видов", "Открыть", () => ShowAdmin("species")), Weight());
            row3.AddView(AdminCard("icon_ai", "Рекомендации", "Настройка алгоритмов\nухода", "Настроить", () => ShowAdmin("settings")), Weight());
            content.AddView(row3);

            var row4 = Horizontal();
            row4.AddView(AdminCard("icon_shield", "Средства защиты", "Препараты и способы\nбезопасного применения", "Открыть", () => ShowAdmin("protection")), Weight());
            content.AddView(row4);

            if (sheet == "species")
            {
                content.AddView(AdminSpeciesPanel(species));
            }
            else if (sheet == "settings")
            {
                content.AddView(AdminSettingsPanel(settings));
            }
            else if (sheet == "protection")
            {
                content.AddView(AdminProtectionPanel(protectionProducts));
            }
            else if (!string.IsNullOrWhiteSpace(sheet))
            {
                content.AddView(AdminEditorPanel(sheet, records));
            }
        }, showBottomNav: false, rightText: "⎋", rightAction: () => _ = SignOutAsync());
    }

    private sealed record ScheduleRow(Plant Plant, WateringSchedule Schedule, DateTime OccurrenceDate, CareEvent? Event);

    private sealed record PeriodField(EditText CountInput, Spinner UnitSpinner, LinearLayout View);

    private void OpenAdmin() => ShowAdmin();

    private void OpenAdminEditor(string mode)
    {
        _pendingAdminImageData = null;
        _adminImagePreview = null;
        ShowAdmin(mode);
    }

    private void RequestRuntimePermissions()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var permissions = new List<string>
            {
                Android.Manifest.Permission.Camera,
                Android.Manifest.Permission.GetAccounts,
                Android.Manifest.Permission.ReadContacts
            };
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                permissions.Add(Android.Manifest.Permission.PostNotifications);
            }
            RequestPermissions(permissions.ToArray(), 42);
        }
    }

    private void RequestNotificationPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            RequestPermissions([Android.Manifest.Permission.PostNotifications], 43);
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if ((requestCode == 42 || requestCode == 43) &&
            permissions.Contains(Android.Manifest.Permission.PostNotifications) &&
            grantResults.ElementAtOrDefault(Array.IndexOf(permissions, Android.Manifest.Permission.PostNotifications)) == Permission.Granted)
        {
            _ = LoadPlantsAsync();
        }
    }

    private Task AlertAsync(string title, string message)
    {
        var completion = new TaskCompletionSource();
        RunOnUiThread(() =>
        {
            new AlertDialog.Builder(this)
                .SetTitle(title)
                .SetMessage(message)
                .SetPositiveButton("ОК", (_, _) => completion.TrySetResult())
                .Show();
        });
        return completion.Task;
    }

    private Task<bool> ConfirmAsync(string title, string message)
    {
        var completion = new TaskCompletionSource<bool>();
        RunOnUiThread(() =>
        {
            new AlertDialog.Builder(this)
                .SetTitle(title)
                .SetMessage(message)
                .SetPositiveButton("Да", (_, _) => completion.TrySetResult(true))
                .SetNegativeButton("Нет", (_, _) => completion.TrySetResult(false))
                .Show();
        });
        return completion.Task;
    }

    private async Task SignOutAsync()
    {
        if (!await ConfirmAsync("Выход из профиля", "Выйти из текущего профиля?"))
        {
            return;
        }

        if (_authService is not null)
        {
            await _authService.SignOutAsync();
        }
        _currentUser = null;
        _plants = [];
        _selectedPhotoPlant = null;
        ShowWelcome();
    }

    private async void ShowReports(DateTime? selectedMonth = null)
    {
        if (_currentUser is null || _reportService is null)
        {
            await AlertAsync("Нет доступа", "Для просмотра отчётов необходимо войти в аккаунт.");
            return;
        }

        int? reportUserId = _currentUser.IsAdmin ? null : _currentUser.Id;
        var month = selectedMonth ?? DateTime.Today;
        try
        {
            var sheets = await _reportService.GetReportSheetsAsync(reportUserId, month);
            BuildScreen(_currentUser.IsAdmin ? "Сводные отчёты" : "Отчёты моего сада", content =>
            {
                content.AddView(Spacer(18));
                AddSection(content, "Период отчётности");
                var monthInput = DateInput(month);
                content.AddView(monthInput);
                content.AddView(Pill("Показать выбранный месяц", true, () =>
                {
                    ShowReports(ParseDate(monthInput.Text) ?? month);
                }), FixedMatchHeight(44));
                AddMuted(content, $"Сформировано за {month:MMMM yyyy}");
                content.AddView(Spacer(12));
                content.AddView(PrimaryButton("Скачать отчёт в Excel", async () =>
                {
                    await ExportReportsAsync(month, reportUserId);
                }));
                content.AddView(Spacer(14));

                foreach (var sheet in sheets)
                {
                    content.AddView(ReportSheetView(sheet));
                }

                content.AddView(Spacer(16));
                content.AddView(PrimaryButton("Скачать отчёт ещё раз", async () =>
                {
                    await ExportReportsAsync(month, reportUserId);
                }));
            }, showBottomNav: false, back: _currentUser.IsAdmin
                ? () => ShowAdmin()
                : ShowPlants);
        }
        catch (Exception ex)
        {
            await AlertAsync("Ошибка отчётов", ex.Message);
        }
    }

    private LinearLayout ReportSheetView(ReportSheet sheet)
    {
        var section = Vertical();
        section.SetPadding(Dp(14), Dp(14), Dp(14), Dp(14));
        section.Background = Rounded(CardBg, Border, 14, 1);
        section.LayoutParameters = MarginMatch(0, 0, 0, Dp(14));
        section.AddView(Txt(sheet.Name, 17, Text, TypefaceStyle.Bold));
        AddMuted(section, $"Строк: {sheet.Rows.Count}");

        if (sheet.Rows.Count == 0)
        {
            section.AddView(Txt("За выбранный период данных нет.", 14, Secondary, TypefaceStyle.Normal));
            return section;
        }

        foreach (var row in sheet.Rows)
        {
            var rowCard = Vertical();
            rowCard.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            rowCard.Background = Rounded(Bg, SoftBorder, 10, 1);
            rowCard.LayoutParameters = MarginMatch(0, Dp(8), 0, 0);
            for (var index = 0; index < Math.Min(sheet.Headers.Count, row.Count); index++)
            {
                var value = string.IsNullOrWhiteSpace(row[index]) ? "—" : row[index];
                rowCard.AddView(Txt($"{sheet.Headers[index]}: {value}", 13, Secondary, TypefaceStyle.Normal));
            }
            section.AddView(rowCard);
        }
        return section;
    }

    private async Task ExportReportsAsync(DateTime reportMonth, int? userId)
    {
        if (_reportService is null)
        {
            return;
        }

        Android.Net.Uri? uri = null;
        try
        {
            var workbook = await _reportService.GenerateExcelWorkbookAsync(userId, reportMonth);
            var scope = userId.HasValue ? "my_garden" : "all_users";
            var fileName = $"Plants_{scope}_{reportMonth:yyyy_MM}_{DateTime.Now:HHmm}.xls";
            var values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(MediaStore.IMediaColumns.MimeType, "application/vnd.ms-excel");
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                values.Put(MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads + "/Plants");
                values.Put(MediaStore.IMediaColumns.IsPending, 1);
            }

            var collection = Build.VERSION.SdkInt >= BuildVersionCodes.Q
                ? MediaStore.Downloads.ExternalContentUri
                : MediaStore.Files.GetContentUri("external");
            uri = ContentResolver?.Insert(collection, values);
            if (uri is null)
            {
                throw new InvalidOperationException("Не удалось создать файл отчёта.");
            }

            await using (var stream = ContentResolver!.OpenOutputStream(uri))
            {
                if (stream is null)
                {
                    throw new InvalidOperationException("Не удалось открыть файл отчёта.");
                }
                await stream.WriteAsync(workbook);
                await stream.FlushAsync();
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                var completedValues = new ContentValues();
                completedValues.Put(MediaStore.IMediaColumns.IsPending, 0);
                ContentResolver.Update(uri, completedValues, null, null);
            }

            ShowSavedReportDialog(uri, fileName);
        }
        catch (Exception ex)
        {
            if (uri is not null)
            {
                try
                {
                    ContentResolver?.Delete(uri, null, null);
                }
                catch
                {
                    // Частично созданный файл уже недоступен или удалён системой.
                }
            }
            await AlertAsync("Ошибка экспорта", ex.Message);
        }
    }

    private void ShowSavedReportDialog(Android.Net.Uri uri, string fileName)
    {
        RunOnUiThread(() =>
        {
            new AlertDialog.Builder(this)
                .SetTitle("Отчёт скачан")
                .SetMessage($"Файл сохранён в папку Загрузки/Plants:\n{fileName}")
                .SetPositiveButton("Открыть", (_, _) => OpenReport(uri))
                .SetNeutralButton("Поделиться", (_, _) => ShareReport(uri))
                .SetNegativeButton("Закрыть", (_, _) => { })
                .Show();
        });
    }

    private void OpenReport(Android.Net.Uri uri)
    {
        try
        {
            var open = new Intent(Intent.ActionView);
            open.SetDataAndType(uri, "application/vnd.ms-excel");
            open.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
            StartActivity(Intent.CreateChooser(open, "Открыть отчёт"));
        }
        catch (ActivityNotFoundException)
        {
            Toast("Установите Microsoft Excel или Google Таблицы. Файл уже сохранён в Загрузках.");
        }
    }

    private void ShareReport(Android.Net.Uri uri)
    {
        var share = new Intent(Intent.ActionSend);
        share.SetType("application/vnd.ms-excel");
        share.PutExtra(Intent.ExtraStream, uri);
        share.AddFlags(ActivityFlags.GrantReadUriPermission);
        StartActivity(Intent.CreateChooser(share, "Отправить отчёт"));
    }

    private async Task<List<ScheduleRow>> GetDueScheduleRowsAsync()
    {
        var rows = new List<ScheduleRow>();
        var (from, to) = CalendarRange();
        var events = _currentUser is null
            ? []
            : await _database.GetCareEventsAsync(_currentUser.Id, from.AddDays(-365), to);
        var eventMap = events.ToDictionary(x => (x.ScheduleId, x.ScheduledDate.Date));

        foreach (var plant in _plants)
        {
            foreach (var schedule in await _database.GetSchedulesForPlantAsync(plant.Id))
            {
                if (schedule.PeriodDays <= 0)
                {
                    continue;
                }

                var occurrence = schedule.StartDate.Date;
                if (occurrence < from)
                {
                    if (_calendarMode == "day" && from.Date == DateTime.Today && occurrence < DateTime.Today)
                    {
                        eventMap.TryGetValue((schedule.Id, occurrence), out var overdueEvent);
                        rows.Add(new ScheduleRow(plant, schedule, occurrence, overdueEvent));
                    }

                    while (occurrence < from)
                    {
                        occurrence = occurrence.AddDays(schedule.PeriodDays);
                    }
                }

                while (occurrence <= to)
                {
                    eventMap.TryGetValue((schedule.Id, occurrence), out var careEvent);
                    rows.Add(new ScheduleRow(plant, schedule, occurrence, careEvent));
                    occurrence = occurrence.AddDays(schedule.PeriodDays);
                }
            }
        }
        return rows
            .OrderBy(x => x.OccurrenceDate.Date)
            .ThenBy(x => x.Schedule.NotificationTime)
            .ThenBy(x => x.Plant.Name)
            .ToList();
    }

    private void AddScheduleGroup(LinearLayout parent, string title, IReadOnlyList<ScheduleRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        AddSection(parent, title);
        foreach (var row in rows)
        {
            parent.AddView(WateringRow(row));
        }
    }

    private LinearLayout WateringRow(ScheduleRow item)
    {
        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(6), 0, Dp(18));
        row.AddView(Icon(item.Schedule.Type == "Fertilizer" ? "icon_potted_plant" : "icon_flower", 40, Muted), Fixed(56, 48));
        row.AddView(Txt(item.Plant.Name, 16, Text, TypefaceStyle.Normal), Weight());
        Button? actionButton = null;
        var completed = item.Event?.Status == CareTaskStatus.Done;
        var activeText = CareActionText(item.Schedule.Type, false);
        var doneText = CareActionText(item.Schedule.Type, true);
        actionButton = Pill(completed ? doneText : activeText, !completed, async () =>
        {
            if (completed)
            {
                await _database.DeleteCareEventAsync(item.Schedule.Id, item.OccurrenceDate);
                if (item.Schedule.StartDate.Date > item.OccurrenceDate.Date)
                {
                    item.Schedule.StartDate = item.OccurrenceDate.Date;
                    item.Schedule.LastCompletedDate = null;
                    await _database.UpdateScheduleAsync(item.Schedule);
                }
                _notificationService?.ScheduleWateringNotification(item.Plant, item.Schedule);
                completed = false;
                SetPillStyle(actionButton, true, activeText);
                return;
            }

            await _database.SaveCareEventAsync(new CareEvent
            {
                ScheduleId = item.Schedule.Id,
                PlantId = item.Plant.Id,
                ScheduledDate = item.OccurrenceDate.Date,
                CompletedAt = DateTime.Now,
                Status = CareTaskStatus.Done
            });
            if (item.OccurrenceDate.Date <= DateTime.Today && item.Schedule.StartDate.Date <= item.OccurrenceDate.Date)
            {
                item.Schedule.LastCompletedDate = item.OccurrenceDate.Date;
                item.Schedule.StartDate = item.OccurrenceDate.Date.AddDays(item.Schedule.PeriodDays);
                await _database.UpdateScheduleAsync(item.Schedule);
            }
            _notificationService?.ScheduleWateringNotification(item.Plant, item.Schedule);
            completed = true;
            SetPillStyle(actionButton, false, doneText);
        });
        row.AddView(actionButton, Fixed(170, 38));
        return row;
    }

    private string CareActionText(string type, bool completed) => type switch
    {
        "Fertilizer" => completed ? "Удобрено" : "Удобрить",
        "Pruning" => completed ? "Обрезано" : "Обрезать",
        "Repotting" => completed ? "Пересажено" : "Пересадить",
        _ => completed ? "Полито" : "Полить"
    };

    private (DateTime From, DateTime To) CalendarRange()
    {
        if (_calendarMode == "week")
        {
            var offset = ((int)_calendarAnchorDate.DayOfWeek + 6) % 7;
            var monday = _calendarAnchorDate.Date.AddDays(-offset);
            return (monday, monday.AddDays(6));
        }

        if (_calendarMode == "month")
        {
            var first = new DateTime(_calendarAnchorDate.Year, _calendarAnchorDate.Month, 1);
            return (first, first.AddMonths(1).AddDays(-1));
        }

        return (_calendarAnchorDate.Date, _calendarAnchorDate.Date);
    }

    private LinearLayout CalendarModeControl()
    {
        var row = Horizontal();
        row.Background = Rounded(CardBg, Border, 12, 1);
        row.SetPadding(Dp(3), Dp(3), Dp(3), Dp(3));
        row.AddView(CalendarModeButton("День", "day"), Weight());
        row.AddView(CalendarModeButton("Неделя", "week"), Weight());
        row.AddView(CalendarModeButton("Месяц", "month"), Weight());
        return row;
    }

    private Button CalendarModeButton(string title, string mode)
    {
        var selected = _calendarMode == mode;
        var button = Pill(title, selected, async () =>
        {
            _calendarMode = mode;
            await ShowWatering(false);
        });
        if (!selected)
        {
            button.SetTextColor(AColor.ParseColor(Secondary));
            button.Background = Rounded(CardBg, CardBg, 10, 0);
        }
        return button;
    }

    private LinearLayout CalendarPeriodNavigation()
    {
        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        row.AddView(Pill("‹", false, async () =>
        {
            _calendarAnchorDate = ShiftCalendar(-1);
            await ShowWatering(false);
        }), Fixed(54, 40));
        var (from, to) = CalendarRange();
        var title = _calendarMode switch
        {
            "week" => $"{from:dd.MM}–{to:dd.MM.yyyy}",
            "month" => _calendarAnchorDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU")),
            _ => CalendarDateTitle(_calendarAnchorDate)
        };
        var label = Txt(title, 15, Text, TypefaceStyle.Bold);
        label.Gravity = GravityFlags.Center;
        row.AddView(label, Weight());
        row.AddView(Pill("›", false, async () =>
        {
            _calendarAnchorDate = ShiftCalendar(1);
            await ShowWatering(false);
        }), Fixed(54, 40));
        return row;
    }

    private DateTime ShiftCalendar(int direction) => _calendarMode switch
    {
        "week" => _calendarAnchorDate.AddDays(7 * direction),
        "month" => _calendarAnchorDate.AddMonths(direction),
        _ => _calendarAnchorDate.AddDays(direction)
    };

    private static string CalendarDateTitle(DateTime date)
    {
        if (date.Date == DateTime.Today)
        {
            return "Сегодня";
        }
        if (date.Date == DateTime.Today.AddDays(1))
        {
            return "Завтра";
        }
        if (date.Date < DateTime.Today)
        {
            return $"Просрочено · {date:dd.MM.yyyy}";
        }
        return date.ToString("dddd, dd.MM.yyyy", new System.Globalization.CultureInfo("ru-RU"));
    }

    private CheckBox ToggleCheck(string text, bool isChecked)
    {
        var check = new CheckBox(this)
        {
            Text = text,
            Checked = isChecked,
            TextSize = 15
        };
        check.SetTextColor(AColor.ParseColor(Text));
        check.ButtonTintList = Android.Content.Res.ColorStateList.ValueOf(AColor.ParseColor("#E8EBED"));
        return check;
    }

    private EditText DateInput(DateTime value, DateTime? minDate = null)
    {
        var input = Input("Дата");
        input.Text = value.ToString("dd.MM.yyyy");
        input.Focusable = false;
        input.Click += (_, _) =>
        {
            var current = ParseDate(input.Text) ?? DateTime.Today;
            var dialog = new DatePickerDialog(this, (_, args) =>
            {
                input.Text = args.Date.ToString("dd.MM.yyyy");
            }, current.Year, current.Month - 1, current.Day);
            if (minDate is not null)
            {
                dialog.DatePicker.MinDate = new DateTimeOffset(minDate.Value.Date).ToUnixTimeMilliseconds();
            }
            dialog.Show();
        };
        return input;
    }

    private EditText TimeInput(TimeSpan value)
    {
        var input = Input("Выберите время");
        input.Text = value.ToString(@"hh\:mm");
        input.Focusable = false;
        input.Click += (_, _) =>
        {
            var current = ParseTime(input.Text) ?? value;
            new TimePickerDialog(this, (_, args) =>
            {
                input.Text = new TimeSpan(args.HourOfDay, args.Minute, 0).ToString(@"hh\:mm");
            }, current.Hours, current.Minutes, true).Show();
        };
        return input;
    }

    private PeriodField PeriodInput(int count = 1, string unit = "day")
    {
        var row = Horizontal();
        row.Background = Rounded(InputBg, Border, 14, 2);
        row.SetPadding(Dp(10), 0, Dp(10), 0);

        var countInput = new EditText(this);
        countInput.Hint = "Раз в";
        countInput.Text = count.ToString();
        countInput.TextSize = 14;
        countInput.InputType = InputTypes.ClassNumber;
        countInput.SetSingleLine(true);
        countInput.SetTextColor(AColor.ParseColor(Text));
        countInput.SetHintTextColor(AColor.ParseColor(Muted));
        countInput.Background = null;
        row.AddView(countInput, new LinearLayout.LayoutParams(0, Dp(52), 1));

        var unitSpinner = new Spinner(this);
        var units = new[] { "день", "неделя", "месяц" };
        unitSpinner.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, units);
        unitSpinner.SetSelection(UnitIndex(unit));
        unitSpinner.Background = null;
        row.AddView(unitSpinner, new LinearLayout.LayoutParams(Dp(95), Dp(52)));

        return new PeriodField(countInput, unitSpinner, row);
    }

    private Spinner SimpleSpinner(IReadOnlyList<string> items)
    {
        var spinner = new Spinner(this);
        spinner.Background = Rounded(InputBg, Border, 14, 2);
        spinner.SetPadding(Dp(10), 0, Dp(10), 0);
        spinner.Adapter = new ArrayAdapter<string>(
            this,
            Android.Resource.Layout.SimpleSpinnerDropDownItem,
            items.ToList());
        return spinner;
    }

    private static int SpinnerIndex(Spinner spinner, string value)
    {
        for (var index = 0; index < spinner.Count; index++)
        {
            if (string.Equals(spinner.GetItemAtPosition(index)?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return 0;
    }

    private async Task AddPlantAsync(
        EditText nameInput,
        PlantSpecies? species,
        Spinner phaseSpinner, Spinner healthSpinner, EditText locationInput, EditText notesInput,
        CheckBox wateringToggle, EditText waterDate, PeriodField waterPeriod, EditText waterTime,
        CheckBox fertilizerToggle, EditText fertilizerDate, PeriodField fertilizerPeriod, EditText fertilizerTime,
        CheckBox pruningToggle, EditText pruningDate, PeriodField pruningPeriod, EditText pruningTime,
        CheckBox repottingToggle, EditText repottingDate, PeriodField repottingPeriod, EditText repottingTime)
    {
        if (_currentUser is null)
        {
            await AlertAsync("Ошибка", "Сначала войдите в приложение");
            return;
        }

        var name = nameInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            MarkInvalid(nameInput);
            await AlertAsync("Проверка данных", "Введите имя растения");
            return;
        }

        if (!TryReadSchedule(wateringToggle.Checked, waterDate, waterPeriod, waterTime, out var waterSchedule, out var error) ||
            !TryReadSchedule(fertilizerToggle.Checked, fertilizerDate, fertilizerPeriod, fertilizerTime, out var fertilizerSchedule, out error) ||
            !TryReadSchedule(pruningToggle.Checked, pruningDate, pruningPeriod, pruningTime, out var pruningSchedule, out error) ||
            !TryReadSchedule(repottingToggle.Checked, repottingDate, repottingPeriod, repottingTime, out var repottingSchedule, out error))
        {
            await AlertAsync("Проверка данных", error);
            return;
        }

        try
        {
            var plant = new Plant
            {
                UserId = _currentUser.Id,
                SpeciesId = species?.Id,
                Name = name,
                Type = _pendingPlantType,
                WateringEnabled = wateringToggle.Checked,
                FertilizerEnabled = fertilizerToggle.Checked,
                LifecyclePhase = phaseSpinner.SelectedItem?.ToString() ?? "Активный рост",
                HealthStatus = healthSpinner.SelectedItem?.ToString() ?? "Здорово",
                Location = locationInput.Text?.Trim() ?? string.Empty,
                Notes = notesInput.Text?.Trim() ?? string.Empty
            };
            await _database.AddPlantAsync(plant);

            if (waterSchedule is not null)
            {
                waterSchedule.PlantId = plant.Id;
                waterSchedule.Type = "Watering";
                await _database.AddScheduleAsync(waterSchedule);
                _notificationService?.ScheduleWateringNotification(plant, waterSchedule);
            }

            if (fertilizerSchedule is not null)
            {
                fertilizerSchedule.PlantId = plant.Id;
                fertilizerSchedule.Type = "Fertilizer";
                await _database.AddScheduleAsync(fertilizerSchedule);
                _notificationService?.ScheduleWateringNotification(plant, fertilizerSchedule);
            }

            if (pruningSchedule is not null)
            {
                pruningSchedule.PlantId = plant.Id;
                pruningSchedule.Type = "Pruning";
                await _database.AddScheduleAsync(pruningSchedule);
                _notificationService?.ScheduleWateringNotification(plant, pruningSchedule);
            }

            if (repottingSchedule is not null)
            {
                repottingSchedule.PlantId = plant.Id;
                repottingSchedule.Type = "Repotting";
                await _database.AddScheduleAsync(repottingSchedule);
                _notificationService?.ScheduleWateringNotification(plant, repottingSchedule);
            }

            await LoadPlantsAsync();
            await ShowWatering(false);
        }
        catch (Exception ex)
        {
            await AlertAsync("Ошибка PostgreSQL", ex.Message);
        }
    }

    private async Task PopulateEditFieldsAsync(
        Plant plant,
        CheckBox wateringToggle, EditText waterDate, PeriodField waterPeriod, EditText waterTime,
        CheckBox fertilizerToggle, EditText fertilizerDate, PeriodField fertilizerPeriod, EditText fertilizerTime,
        CheckBox pruningToggle, EditText pruningDate, PeriodField pruningPeriod, EditText pruningTime,
        CheckBox repottingToggle, EditText repottingDate, PeriodField repottingPeriod, EditText repottingTime)
    {
        wateringToggle.Checked = plant.WateringEnabled;
        fertilizerToggle.Checked = plant.FertilizerEnabled;
        var waterSchedule = await _database.GetScheduleAsync(plant.Id, "Watering");
        if (waterSchedule is not null)
        {
            waterDate.Text = waterSchedule.StartDate.ToString("dd.MM.yyyy");
            SetPeriodField(waterPeriod, waterSchedule);
            waterTime.Text = waterSchedule.NotificationTime.ToString(@"hh\:mm");
        }

        var fertilizerSchedule = await _database.GetScheduleAsync(plant.Id, "Fertilizer");
        if (fertilizerSchedule is not null)
        {
            fertilizerDate.Text = fertilizerSchedule.StartDate.ToString("dd.MM.yyyy");
            SetPeriodField(fertilizerPeriod, fertilizerSchedule);
            fertilizerTime.Text = fertilizerSchedule.NotificationTime.ToString(@"hh\:mm");
        }

        var pruningSchedule = await _database.GetScheduleAsync(plant.Id, "Pruning");
        pruningToggle.Checked = pruningSchedule is not null;
        if (pruningSchedule is not null)
        {
            pruningDate.Text = pruningSchedule.StartDate.ToString("dd.MM.yyyy");
            SetPeriodField(pruningPeriod, pruningSchedule);
            pruningTime.Text = pruningSchedule.NotificationTime.ToString(@"hh\:mm");
        }

        var repottingSchedule = await _database.GetScheduleAsync(plant.Id, "Repotting");
        repottingToggle.Checked = repottingSchedule is not null;
        if (repottingSchedule is not null)
        {
            repottingDate.Text = repottingSchedule.StartDate.ToString("dd.MM.yyyy");
            SetPeriodField(repottingPeriod, repottingSchedule);
            repottingTime.Text = repottingSchedule.NotificationTime.ToString(@"hh\:mm");
        }
    }

    private async Task UpdatePlantAsync(
        Plant plant,
        EditText nameInput, Spinner phaseSpinner, Spinner healthSpinner, EditText locationInput, EditText notesInput,
        CheckBox wateringToggle, EditText waterDate, PeriodField waterPeriod, EditText waterTime,
        CheckBox fertilizerToggle, EditText fertilizerDate, PeriodField fertilizerPeriod, EditText fertilizerTime,
        CheckBox pruningToggle, EditText pruningDate, PeriodField pruningPeriod, EditText pruningTime,
        CheckBox repottingToggle, EditText repottingDate, PeriodField repottingPeriod, EditText repottingTime)
    {
        var name = nameInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            MarkInvalid(nameInput);
            await AlertAsync("Проверка данных", "Введите имя растения");
            return;
        }

        if (!TryReadSchedule(wateringToggle.Checked, waterDate, waterPeriod, waterTime, out var waterSchedule, out var error) ||
            !TryReadSchedule(fertilizerToggle.Checked, fertilizerDate, fertilizerPeriod, fertilizerTime, out var fertilizerSchedule, out error) ||
            !TryReadSchedule(pruningToggle.Checked, pruningDate, pruningPeriod, pruningTime, out var pruningSchedule, out error) ||
            !TryReadSchedule(repottingToggle.Checked, repottingDate, repottingPeriod, repottingTime, out var repottingSchedule, out error))
        {
            await AlertAsync("Проверка данных", error);
            return;
        }

        try
        {
            plant.Name = name;
            plant.LifecyclePhase = phaseSpinner.SelectedItem?.ToString() ?? "Активный рост";
            plant.HealthStatus = healthSpinner.SelectedItem?.ToString() ?? "Здорово";
            plant.Location = locationInput.Text?.Trim() ?? string.Empty;
            plant.Notes = notesInput.Text?.Trim() ?? string.Empty;
            plant.WateringEnabled = wateringToggle.Checked;
            plant.FertilizerEnabled = fertilizerToggle.Checked;
            await _database.UpdatePlantAsync(plant);
            _notificationService?.CancelNotification(plant.Id);

            await SaveOrDeleteScheduleAsync(plant, "Watering", waterSchedule);
            await SaveOrDeleteScheduleAsync(plant, "Fertilizer", fertilizerSchedule);
            await SaveOrDeleteScheduleAsync(plant, "Pruning", pruningSchedule);
            await SaveOrDeleteScheduleAsync(plant, "Repotting", repottingSchedule);

            await LoadPlantsAsync();
            await ShowWatering(false);
        }
        catch (Exception ex)
        {
            await AlertAsync("Ошибка PostgreSQL", ex.Message);
        }
    }

    private async Task SaveOrDeleteScheduleAsync(Plant plant, string type, WateringSchedule? schedule)
    {
        var existing = await _database.GetScheduleAsync(plant.Id, type);
        if (schedule is not null)
        {
            schedule.PlantId = plant.Id;
            schedule.Type = type;
            if (existing is null)
            {
                await _database.AddScheduleAsync(schedule);
            }
            else
            {
                schedule.Id = existing.Id;
                await _database.UpdateScheduleAsync(schedule);
            }
            _notificationService?.ScheduleWateringNotification(plant, schedule);
        }
        else if (existing is not null)
        {
            await _database.DeleteScheduleAsync(existing.Id);
        }
    }

    private bool TryReadSchedule(bool enabled, EditText dateInput, PeriodField periodInput, EditText timeInput, out WateringSchedule? schedule, out string error)
    {
        schedule = null;
        error = string.Empty;
        if (!enabled)
        {
            return true;
        }

        var date = ParseDate(dateInput.Text);
        if (date is null)
        {
            MarkInvalid(dateInput);
            error = "Выберите дату";
            return false;
        }
        if (date.Value.Date < DateTime.Today.AddDays(-3))
        {
            MarkInvalid(dateInput);
            error = "Дата отсчёта может быть не раньше, чем за 3 дня до сегодняшней даты";
            return false;
        }
        if (!int.TryParse(periodInput.CountInput.Text, out var period) || period <= 0)
        {
            MarkInvalid(periodInput.CountInput);
            error = "Введите периодичность";
            return false;
        }
        var periodUnit = SelectedPeriodUnit(periodInput.UnitSpinner);
        var periodDays = period * PeriodMultiplier(periodUnit);
        var time = ParseTime(timeInput.Text);
        if (time is null)
        {
            MarkInvalid(timeInput);
            error = "Выберите время";
            return false;
        }

        schedule = new WateringSchedule
        {
            StartDate = date.Value.Date,
            PeriodValue = period,
            PeriodUnit = periodUnit,
            PeriodDays = periodDays,
            NotificationTime = time.Value
        };
        return true;
    }

    private static string SelectedPeriodUnit(Spinner spinner) => spinner.SelectedItemPosition switch
    {
        1 => "week",
        2 => "month",
        _ => "day"
    };

    private static int PeriodMultiplier(string unit) => unit switch
    {
        "week" => 7,
        "month" => 30,
        _ => 1
    };

    private static int UnitIndex(string unit) => unit switch
    {
        "week" => 1,
        "month" => 2,
        _ => 0
    };

    private static void SetPeriodField(PeriodField field, WateringSchedule schedule)
    {
        field.CountInput.Text = schedule.PeriodValue > 0 ? schedule.PeriodValue.ToString() : schedule.PeriodDays.ToString();
        field.UnitSpinner.SetSelection(UnitIndex(schedule.PeriodUnit));
    }

    private static DateTime? ParseDate(string? text) =>
        DateTime.TryParseExact(text, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var date) ? date : null;

    private static TimeSpan? ParseTime(string? text) =>
        TimeSpan.TryParseExact(text, @"hh\:mm", null, out var time) ? time : null;

    private void MarkInvalid(EditText input)
    {
        input.Background = Rounded(InputBg, Red, 14, 2);
    }

    private void BuildScreen(string title, Action<LinearLayout> build, bool showBottomNav = true, Action? back = null, bool redHeader = false, string rightText = "", Action? rightAction = null)
    {
        var root = Vertical();
        root.SetBackgroundColor(AColor.ParseColor(Bg));

        var scroll = new ScrollView(this);
        var content = Vertical();
        content.SetPadding(Dp(26), StatusBarInset() + Dp(8), Dp(26), Dp(12));
        content.AddView(Header(title, back, redHeader, rightText, rightAction));
        build(content);
        scroll.AddView(content);
        root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));

        if (showBottomNav)
        {
            root.AddView(BottomNav());
        }

        SetContentView(root);
    }

    private LinearLayout Header(string title, Action? back, bool red, string rightText, Action? rightAction)
    {
        var header = Vertical();
        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, 0, 0, Dp(10));
        var left = Txt(back is null ? "" : "‹", 34, red ? Red : Text, TypefaceStyle.Normal);
        left.Gravity = GravityFlags.Center;
        left.Click += (_, _) => back?.Invoke();
        row.AddView(left, Fixed(36, 42));

        var label = Txt(title, 22, red ? Red : Text, TypefaceStyle.Bold);
        label.Gravity = GravityFlags.Center;
        row.AddView(label, new LinearLayout.LayoutParams(0, Dp(42), 1));

        if (rightText == "__guide_actions")
        {
            var actions = Horizontal();
            actions.SetGravity(GravityFlags.Center);
            var search = Icon("icon_search", 28, _guideSearchVisible ? Mint : Text);
            search.Click += (_, _) =>
            {
                _guideSearchVisible = !_guideSearchVisible;
                if (!_guideSearchVisible)
                {
                    _guideSearchQuery = string.Empty;
                }
                ShowGuide();
            };
            var filter = Icon("icon_filter", 28, _guideFilterVisible ? Mint : Text);
            filter.Click += (_, _) =>
            {
                _guideFilterVisible = !_guideFilterVisible;
                ShowGuide();
            };
            actions.AddView(search, Fixed(28, 42));
            actions.AddView(filter, Fixed(28, 42));
            row.AddView(actions, Fixed(70, 42));
        }
        else if (rightText == "__share")
        {
            var right = Icon("icon_share", 28, Text);
            if (rightAction is not null)
            {
                right.Click += (_, _) => rightAction();
            }
            row.AddView(right, Fixed(54, 42));
        }
        else
        {
            var right = Txt(rightText, 23, Text, TypefaceStyle.Normal);
            right.Gravity = GravityFlags.Center;
            if (rightAction is not null)
            {
                right.Click += (_, _) => rightAction();
            }
            row.AddView(right, Fixed(54, 42));
        }
        header.AddView(row);
        var divider = new View(this);
        divider.SetBackgroundColor(AColor.ParseColor(Border));
        header.AddView(divider, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(1)));
        return header;
    }

    private LinearLayout BottomNav()
    {
        var container = Vertical();
        container.SetBackgroundColor(AColor.ParseColor(Bg));
        var divider = new View(this);
        divider.SetBackgroundColor(AColor.ParseColor(CardBg));
        container.AddView(divider, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(1)));
        var nav = Horizontal();
        nav.SetPadding(Dp(30), Dp(10), Dp(30), Dp(14));
        nav.SetGravity(GravityFlags.Center);
        nav.SetBackgroundColor(AColor.ParseColor(Bg));
        nav.AddView(NavItem("icon_bookmark", "Справочник", "guide", ShowGuide), Weight());
        nav.AddView(NavItem("icon_calendar_12", "Полив", "watering", () => ShowWatering(false)), Weight());
        nav.AddView(NavItem("icon_tulip", "Растения", "plants", ShowPlants), Weight());
        container.AddView(nav);
        return container;
    }

    private LinearLayout NavItem(string iconName, string title, string tab, Action click)
    {
        var item = Vertical();
        item.SetGravity(GravityFlags.Center);
        var color = _activeTab == tab ? Mint : Muted;
        item.AddView(Icon(iconName, 38, color), CenterFixed(38, 38));
        var label = Txt(title, 11, Text, TypefaceStyle.Normal);
        label.Gravity = GravityFlags.Center;
        item.AddView(label);
        item.Click += (_, _) => click();
        return item;
    }

    private void AddSegmented(LinearLayout parent, bool photoSelected)
    {
        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        row.Background = Rounded(CardBg, CardBg, 8, 0);
        row.SetPadding(Dp(3), Dp(3), Dp(3), Dp(3));
        row.AddView(Segment("Уведомления", !photoSelected, () => ShowWatering(false)), SegmentWeight());
        row.AddView(Segment("Фотоотчет", photoSelected, () => ShowWatering(true)), SegmentWeight());
        parent.AddView(row, MarginMatch(Dp(0), Dp(26), Dp(0), Dp(24), Dp(36)));
    }

    private TextView Segment(string text, bool active, Action click)
    {
        var label = Txt(text, 12, active ? Text : Muted, TypefaceStyle.Bold);
        label.Gravity = GravityFlags.Center;
        label.SetIncludeFontPadding(false);
        label.Background = Rounded(active ? "#77787E" : CardBg, active ? "#77787E" : CardBg, 8, 0);
        label.Click += (_, _) => click();
        return label;
    }

    private LinearLayout WateringRow(Plant plant, string buttonText, bool filled)
    {
        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(6), 0, Dp(18));
        row.AddView(Icon("icon_flower", 40, Muted), Fixed(56, 48));
        row.AddView(Txt("Название", 16, Text, TypefaceStyle.Normal), Weight());
        row.AddView(filled ? Pill("Полив", true, () => Toast(plant.Name)) : Pill("Полито", false, () => Toast(plant.Name)), Fixed(170, 38));
        return row;
    }

    private LinearLayout PhotoReportRow(Plant plant)
    {
        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(8), 0, Dp(18));
        row.AddView(Icon("icon_potted_plant", 40, Muted), Fixed(56, 48));
        row.AddView(Txt(plant.Name, 16, Text, TypefaceStyle.Normal), Weight());
        row.AddView(Pill("Сделать фото", true, () =>
        {
            _selectedPhotoPlant = plant;
            ShowPhotos(false);
        }), Fixed(172, 38));
        return row;
    }

    private LinearLayout CategoryCard(string iconName, string title, string description, string button, Action click)
    {
        var card = Card();
        card.SetGravity(GravityFlags.Center);
        card.AddView(Icon(iconName, 42, Muted), CenterFixed(42, 42));
        AddCentered(card, title, 15, Text, TypefaceStyle.Bold);
        AddCentered(card, description, 12, Secondary, TypefaceStyle.Normal);
        card.AddView(Pill(button, true, click), Fixed(128, 36));
        return card;
    }

    private LinearLayout GuideCard(string iconName, string title, string description, Action click)
    {
        var card = Card();
        card.SetGravity(GravityFlags.Center);
        card.AddView(Icon(iconName, 42, Red), CenterFixed(42, 42));
        AddCentered(card, title, 15, Red, TypefaceStyle.Bold);
        AddCentered(card, description, 12, Secondary, TypefaceStyle.Normal);
        card.AddView(Pill("Изучить", true, click), Fixed(126, 36));
        return card;
    }

    private LinearLayout AiEntryCard()
    {
        var card = Horizontal();
        card.SetGravity(GravityFlags.CenterVertical);
        card.SetPadding(Dp(18), Dp(16), Dp(18), Dp(16));
        card.Background = Rounded(CardBg, Border, 18, 2);
        card.LayoutParameters = MarginMatch(0, 0, 0, Dp(8));
        card.AddView(Icon("icon_ai", 44, Mint), Fixed(54, 54));

        var textBlock = Vertical();
        textBlock.AddView(Txt("ИИ-консультант", 16, Text, TypefaceStyle.Bold));
        textBlock.AddView(Txt("Чат с анализом фотографий растений", 13, Secondary, TypefaceStyle.Normal));
        card.AddView(textBlock, Weight());
        card.AddView(Pill("Открыть", true, ShowAiChat), Fixed(118, 38));
        return card;
    }

    private LinearLayout AiChatBubble(AiChatMessage message)
    {
        var row = Horizontal();
        row.SetGravity(message.IsUser ? GravityFlags.Right : GravityFlags.Left);
        row.LayoutParameters = MarginMatch(0, 0, 0, Dp(10));
        var bubble = Vertical();
        bubble.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
        bubble.Background = Rounded(message.IsUser ? "#29483A" : CardBg, message.IsUser ? Mint : Border, 16, 1);
        bubble.AddView(Txt(message.IsUser ? "Вы" : "ИИ-консультант", 12, message.IsUser ? Mint : Secondary, TypefaceStyle.Bold));
        if (!string.IsNullOrWhiteSpace(message.ImagePath))
        {
            bubble.AddView(PhotoPreview(message.ImagePath, 140), Fixed(180, 140));
        }
        var body = Txt(message.Text, 14, Text, TypefaceStyle.Normal);
        body.SetLineSpacing(0, 1.2f);
        bubble.AddView(body);
        row.AddView(bubble, new LinearLayout.LayoutParams(Dp(300), ViewGroup.LayoutParams.WrapContent));
        return row;
    }

    private ImageView PhotoPreview(string? path, int height)
    {
        var image = new ImageView(this);
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            image.SetImageURI(Android.Net.Uri.FromFile(new Java.IO.File(path)));
        }
        image.SetScaleType(ImageView.ScaleType.CenterCrop);
        image.Background = Rounded(InputBg, Border, 10, 1);
        image.LayoutParameters = Fixed(180, height);
        return image;
    }

    private LinearLayout AdminCard(string iconName, string title, string description, string button, Action click)
    {
        var card = Card();
        card.SetGravity(GravityFlags.Center);
        card.AddView(Icon(iconName, 42, Muted), CenterFixed(42, 42));
        AddCentered(card, title, 15, Text, TypefaceStyle.Bold);
        AddCentered(card, description, 12, Secondary, TypefaceStyle.Normal);
        card.AddView(Pill(button, true, click), Fixed(128, 36));
        return card;
    }

    private LinearLayout PestRow(Pest issue)
    {
        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(8), 0, Dp(10));
        row.AddView(IssueImage(issue, 96), Fixed(172, 96));
        var right = Vertical();
        AddTitle(right, issue.Name, 16);
        right.AddView(Txt(issue.PlantTypesLabel, 12, Secondary, TypefaceStyle.Normal));
        right.AddView(Pill("Узнать больше", true, () => ShowTreatment(issue)), Fixed(170, 36));
        row.AddView(right, Weight());
        return row;
    }

    private ImageView IssueImage(Pest issue, int height)
    {
        var image = new ImageView(this);
        image.SetScaleType(ImageView.ScaleType.CenterCrop);
        image.Background = Rounded(InputBg, Border, 8, 1);
        SetIssueImage(image, issue.ImagePath);
        return image;
    }

    private void SetIssueImage(ImageView image, string? imagePath)
    {
        image.SetImageDrawable(null);
        image.SetPadding(0, 0, 0, 0);
        if (imagePath?.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            var comma = imagePath.IndexOf(',');
            if (comma > 0)
            {
                try
                {
                    var bytes = Convert.FromBase64String(imagePath[(comma + 1)..]);
                    using var bitmap = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
                    if (bitmap is not null)
                    {
                        image.SetImageBitmap(bitmap);
                        return;
                    }
                }
                catch (FormatException)
                {
                    // Некорректное пользовательское изображение заменяется стандартной иконкой.
                }
            }
        }

        var resourceName = imagePath?.StartsWith("resource://", StringComparison.OrdinalIgnoreCase) == true
            ? imagePath["resource://".Length..]
            : imagePath;
        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            var resourceId = Resources?.GetIdentifier(resourceName, "drawable", PackageName) ?? 0;
            if (resourceId != 0)
            {
                image.SetImageResource(resourceId);
                return;
            }
            if (File.Exists(resourceName))
            {
                image.SetImageURI(Android.Net.Uri.FromFile(new Java.IO.File(resourceName)));
                return;
            }
        }
        image.SetImageResource(Resource.Drawable.icon_leaf);
        image.SetPadding(Dp(28), Dp(20), Dp(28), Dp(20));
    }

    private LinearLayout PhotoRow(Photo photo)
    {
        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(8), 0, Dp(10));
        if (File.Exists(photo.FilePath))
        {
            var image = new ImageView(this);
            image.SetImageURI(Android.Net.Uri.FromFile(new Java.IO.File(photo.FilePath)));
            image.SetScaleType(ImageView.ScaleType.CenterCrop);
            row.AddView(image, Fixed(172, 96));
        }
        else
        {
            row.AddView(ImagePlaceholder(90, false), Fixed(172, 96));
        }
        var text = Vertical();
        AddTitle(text, string.IsNullOrWhiteSpace(photo.Title) ? "Фотография" : photo.Title, 16);
        var dateLabel = Txt(photo.DateTaken.ToString("dd.MM.yyyy"), 12, Secondary, TypefaceStyle.Normal);
        text.AddView(dateLabel);
        var actions = Horizontal();
        actions.AddView(Pill("Изменить", false, async () => await EditPhotoAsync(photo)), Weight());
        actions.AddView(Pill("Удалить", false, async () =>
        {
            if (!await ConfirmAsync("Удаление фото", $"Удалить фотографию «{photo.Title}»?"))
            {
                return;
            }
            await _database.DeletePhotoAsync(photo.Id);
            if (File.Exists(photo.FilePath))
            {
                File.Delete(photo.FilePath);
            }
            ShowPhotos(false);
        }), Weight());
        text.AddView(actions);
        row.AddView(text, Weight());
        return row;
    }

    private async Task EditPhotoAsync(Photo photo)
    {
        var container = Vertical();
        container.SetPadding(Dp(18), 0, Dp(18), 0);
        var titleInput = Input("Название фото");
        titleInput.Text = photo.Title;
        var dateInput = DateInput(photo.DateTaken);
        container.AddView(titleInput);
        container.AddView(dateInput);

        var completion = new TaskCompletionSource<bool>();
        var dialog = new AlertDialog.Builder(this)
            .SetTitle("Изменить фотографию")
            .SetView(container)
            .SetPositiveButton("Сохранить", (_, _) => completion.TrySetResult(true))
            .SetNegativeButton("Отмена", (_, _) => completion.TrySetResult(false))
            .Create();
        dialog.Show();
        if (!await completion.Task)
        {
            return;
        }

        var title = titleInput.Text?.Trim() ?? string.Empty;
        var date = ParseDate(dateInput.Text);
        if (string.IsNullOrWhiteSpace(title) || date is null)
        {
            await AlertAsync("Проверка данных", "Введите название и дату фотографии");
            return;
        }

        photo.Title = title;
        photo.DateTaken = date.Value.Date;
        await _database.UpdatePhotoAsync(photo);
        ShowPhotos(false);
    }

    private LinearLayout AddPhotoPanel()
    {
        var panel = Vertical();
        panel.SetPadding(Dp(26), Dp(20), Dp(26), Dp(18));
        panel.Background = Rounded(SheetBg, SoftBorder, 22, 1);
        panel.LayoutParameters = MarginMatch(0, Dp(16), 0, Dp(12));

        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        var dateInput = DateInput(_pendingPhotoDate);
        var titleInput = Input("Название фото");
        var left = Vertical();
        left.AddView(titleInput);
        left.AddView(dateInput);
        left.AddView(Pill("Создать", true, async () =>
        {
            var title = titleInput.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                MarkInvalid(titleInput);
                await AlertAsync("Проверка данных", "Введите название фотографии");
                return;
            }

            if (_selectedPhotoPlant is null)
            {
                await AlertAsync("Ошибка", "Сначала выберите растение");
                return;
            }

            if (_pendingPhotoFilePath is null)
            {
                await AlertAsync("Проверка данных", "Выберите фотографию");
                return;
            }

            var date = ParseDate(dateInput.Text);
            if (date is null)
            {
                MarkInvalid(dateInput);
                await AlertAsync("Проверка данных", "Выберите дату фото");
                return;
            }

            try
            {
                await _database.AddPhotoAsync(new Photo
                {
                    PlantId = _selectedPhotoPlant.Id,
                    Title = title,
                    FilePath = _pendingPhotoFilePath,
                    DateTaken = date.Value.Date
                });
                _pendingPhotoFilePath = null;
                _pendingPhotoDate = DateTime.Today;
                ShowPhotos(false);
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка PostgreSQL", ex.Message);
            }
        }), FixedMatchHeight(54));
        row.AddView(left, Weight());

        var preview = ImagePlaceholder(122, false);
        preview.Text = _pendingPhotoFilePath is null ? "△○" : "OK";
        preview.Click += (_, _) =>
        {
            var intent = new Intent(Intent.ActionGetContent);
            intent.SetType("image/*");
            intent.AddCategory(Intent.CategoryOpenable);
            StartActivityForResult(Intent.CreateChooser(intent, "Выберите фото"), PickPhotoRequest);
        };
        var previewLp = new LinearLayout.LayoutParams(0, Dp(122), 1);
        previewLp.SetMargins(Dp(4), 0, Dp(4), 0);
        row.AddView(preview, previewLp);
        panel.AddView(row);

        return panel;
    }

    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == GoogleSignInRequest)
        {
            _googleSignInInProgress = false;
            if (_authService is null)
            {
                return;
            }

            try
            {
                _currentUser = await _authService.CompleteGoogleSignInAsync(data);
                await PromoteOnlyUserToAdminAsync();
                await LoadPlantsAsync();
                if (_currentUser.IsAdmin)
                {
                    ShowAdmin();
                }
                else
                {
                    await ShowWatering(false);
                }
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка входа", ex.Message);
            }
            return;
        }

        if (requestCode == AiPhotoRequest && resultCode == Result.Ok && data?.Data is not null)
        {
            try
            {
                var photosDir = System.IO.Path.Combine(FilesDir!.AbsolutePath, "ai-chat");
                Directory.CreateDirectory(photosDir);
                var destination = System.IO.Path.Combine(photosDir, $"ai_photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                await using var input = ContentResolver!.OpenInputStream(data.Data);
                if (input is null)
                {
                    await AlertAsync("Ошибка фото", "Не удалось открыть выбранное изображение");
                    return;
                }
                await using var output = File.Create(destination);
                await input.CopyToAsync(output);
                _pendingAiPhotoPath = destination;
                ShowAiChat();
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка фото", ex.Message);
            }
            return;
        }

        if (requestCode == AdminIssuePhotoRequest && resultCode == Result.Ok && data?.Data is not null)
        {
            try
            {
                _pendingAdminImageData = await CreateCompressedImageDataUriAsync(data.Data);
                if (_adminImagePreview is not null)
                {
                    SetIssueImage(_adminImagePreview, _pendingAdminImageData);
                }
                Toast("Изображение готово к сохранению");
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка изображения", ex.Message);
            }
            return;
        }

        if (requestCode != PickPhotoRequest || resultCode != Result.Ok || data?.Data is null)
        {
            return;
        }

        try
        {
            var uri = data.Data;
            var photosDir = System.IO.Path.Combine(FilesDir!.AbsolutePath, "photos");
            Directory.CreateDirectory(photosDir);
            var destination = System.IO.Path.Combine(photosDir, $"plant_photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");

            await using var input = ContentResolver!.OpenInputStream(uri);
            if (input is null)
            {
                await AlertAsync("Ошибка фото", "Не удалось открыть выбранное изображение");
                return;
            }

            await using var output = File.Create(destination);
            await input.CopyToAsync(output);
            _pendingPhotoFilePath = destination;
            Toast("Фото выбрано");
            ShowPhotos(true);
        }
        catch (Exception ex)
        {
            await AlertAsync("Ошибка фото", ex.Message);
        }
    }

    private LinearLayout AdminEditorPanel(string mode, List<Pest> records)
    {
        var panel = Vertical();
        panel.SetPadding(Dp(18), Dp(20), Dp(18), Dp(18));
        panel.Background = Rounded(SheetBg, SoftBorder, 22, 1);
        panel.LayoutParameters = MarginMatch(0, Dp(16), 0, Dp(12));

        if (mode == "delete")
        {
            if (records.Count == 0)
            {
                AddCentered(panel, "В справочнике пока нет записей", 15, Text, TypefaceStyle.Normal);
                return panel;
            }

            var spinner = AdminRecordSpinner(records);
            var preview = Txt(AdminRecordDescription(records[0]), 13, Secondary, TypefaceStyle.Normal);
            panel.AddView(spinner, FixedMatchHeight(52));
            panel.AddView(preview);
            spinner.ItemSelected += (_, args) =>
            {
                var selected = records.ElementAtOrDefault(args.Position);
                if (selected is not null)
                {
                    preview.Text = AdminRecordDescription(selected);
                }
            };
            panel.AddView(Pill("Удалить", true, async () =>
            {
                var selected = records.ElementAtOrDefault(spinner.SelectedItemPosition);
                if (selected is null)
                {
                    return;
                }

                if (!await ConfirmAsync("Удаление", $"Удалить запись \"{selected.Name}\"?"))
                {
                    return;
                }

                try
                {
                    await _database.DeletePestAsync(selected.Id);
                    await AlertAsync("Готово", "Запись удалена");
                    ShowAdmin();
                }
                catch (Exception ex)
                {
                    await AlertAsync("Ошибка PostgreSQL", ex.Message);
                }
            }), FixedMatchHeight(54));
            return panel;
        }

        Spinner? recordSpinner = null;
        var nameInput = Input("Название", mode == "edit");
        var treatmentInput = Input("Лечение");
        var typeRow = AdminTypeToggles(true);
        var plantTypeRow = AdminPlantTypeToggles(null);

        if (mode == "edit")
        {
            if (records.Count == 0)
            {
                AddCentered(panel, "В справочнике пока нет записей", 15, Text, TypefaceStyle.Normal);
                return panel;
            }

            recordSpinner = AdminRecordSpinner(records);
            panel.AddView(recordSpinner, FixedMatchHeight(52));
            FillAdminInputs(records[0], nameInput, treatmentInput, typeRow.Pest, typeRow.Disease, plantTypeRow);
            _pendingAdminImageData = records[0].ImagePath;
            recordSpinner.ItemSelected += (_, args) =>
            {
                var selected = records.ElementAtOrDefault(args.Position);
                if (selected is not null)
                {
                    FillAdminInputs(selected, nameInput, treatmentInput, typeRow.Pest, typeRow.Disease, plantTypeRow);
                    _pendingAdminImageData = selected.ImagePath;
                    if (_adminImagePreview is not null)
                    {
                        SetIssueImage(_adminImagePreview, _pendingAdminImageData);
                    }
                }
            };
        }

        panel.AddView(nameInput);
        panel.AddView(treatmentInput);
        AddMuted(panel, "Изображение признаков болезни или вредителя");
        _adminImagePreview = new ImageView(this);
        _adminImagePreview.SetScaleType(ImageView.ScaleType.CenterCrop);
        _adminImagePreview.Background = Rounded(InputBg, Border, 12, 1);
        SetIssueImage(_adminImagePreview, _pendingAdminImageData);
        _adminImagePreview.Click += (_, _) => PickAdminIssuePhoto();
        panel.AddView(_adminImagePreview, FixedMatchHeight(180));
        panel.AddView(Pill(
            string.IsNullOrWhiteSpace(_pendingAdminImageData) ? "Выбрать изображение" : "Заменить изображение",
            false,
            PickAdminIssuePhoto), FixedMatchHeight(42));
        AddMuted(panel, "Фото будет сжато и сохранено в общей PostgreSQL. Оно появится на всех телефонах.");
        panel.AddView(typeRow.Row);
        AddMuted(panel, "Для каких растений подходит запись");
        panel.AddView(plantTypeRow.Row);
        panel.AddView(Pill(mode == "edit" ? "Редактировать" : "Создать", true, async () =>
        {
            var name = nameInput.Text?.Trim() ?? string.Empty;
            var treatment = treatmentInput.Text?.Trim() ?? string.Empty;
            var selectedPlantTypes = SelectedAdminPlantTypes(plantTypeRow);
            if (string.IsNullOrWhiteSpace(name))
            {
                MarkInvalid(nameInput);
                await AlertAsync("Проверка данных", "Введите название");
                return;
            }

            if (selectedPlantTypes.Count == 0)
            {
                await AlertAsync("Проверка данных", "Выберите хотя бы один тип растений");
                return;
            }

            try
            {
                var record = mode == "edit"
                    ? records.ElementAtOrDefault(recordSpinner?.SelectedItemPosition ?? 0) ?? new Pest()
                    : new Pest();
                record.Name = name;
                record.TreatmentDescription = string.IsNullOrWhiteSpace(treatment) ? "Описание лечения не указано" : treatment;
                record.ImagePath = _pendingAdminImageData ?? record.ImagePath ?? string.Empty;
                record.IsPest = typeRow.Pest.Checked || !typeRow.Disease.Checked;
                record.PlantTypes = selectedPlantTypes;

                if (mode == "edit")
                {
                    await _database.UpdatePestAsync(record);
                    await AlertAsync("Готово", "Запись обновлена");
                }
                else
                {
                    await _database.AddPestAsync(record);
                    await AlertAsync("Готово", "Запись добавлена");
                }

                ShowAdmin();
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка PostgreSQL", ex.Message);
            }
        }), FixedMatchHeight(54));
        return panel;
    }

    private void PickAdminIssuePhoto()
    {
        var intent = new Intent(Intent.ActionGetContent);
        intent.SetType("image/*");
        intent.AddCategory(Intent.CategoryOpenable);
        StartActivityForResult(
            Intent.CreateChooser(intent, "Выберите изображение для справочника"),
            AdminIssuePhotoRequest);
    }

    private async Task<string> CreateCompressedImageDataUriAsync(Android.Net.Uri uri)
    {
        await using var input = ContentResolver?.OpenInputStream(uri)
            ?? throw new InvalidOperationException("Не удалось открыть выбранное изображение.");
        using var original = await BitmapFactory.DecodeStreamAsync(input)
            ?? throw new InvalidOperationException("Формат изображения не поддерживается.");

        const int maxSide = 1024;
        var scale = Math.Min(1d, maxSide / (double)Math.Max(original.Width, original.Height));
        var width = Math.Max(1, (int)Math.Round(original.Width * scale));
        var height = Math.Max(1, (int)Math.Round(original.Height * scale));
        using var resized = scale < 1
            ? Bitmap.CreateScaledBitmap(original, width, height, true)
            : original.Copy(original.GetConfig() ?? Bitmap.Config.Argb8888, false);
        using var output = new MemoryStream();
        if (!resized.Compress(Bitmap.CompressFormat.Jpeg, 82, output))
        {
            throw new InvalidOperationException("Не удалось подготовить изображение.");
        }
        if (output.Length > 1_500_000)
        {
            throw new InvalidOperationException("Изображение получилось слишком большим. Выберите другое фото.");
        }
        return $"data:image/jpeg;base64,{Convert.ToBase64String(output.ToArray())}";
    }

    private LinearLayout AdminSpeciesPanel(List<PlantSpecies> species)
    {
        var panel = Vertical();
        panel.SetPadding(Dp(18), Dp(20), Dp(18), Dp(18));
        panel.Background = Rounded(SheetBg, SoftBorder, 22, 1);
        panel.LayoutParameters = MarginMatch(0, Dp(16), 0, Dp(12));

        var existing = new Spinner(this);
        existing.Adapter = new ArrayAdapter<string>(
            this,
            Android.Resource.Layout.SimpleSpinnerDropDownItem,
            new[] { "Новый вид" }.Concat(species.Select(x => x.DisplayName)).ToList());
        panel.AddView(existing, FixedMatchHeight(52));

        var category = new Spinner(this);
        category.Adapter = new ArrayAdapter<string>(
            this,
            Android.Resource.Layout.SimpleSpinnerDropDownItem,
            new[] { "Лиственные", "Цветущие", "Суккуленты" });
        panel.AddView(category, FixedMatchHeight(52));
        var name = Input("Название вида");
        var latin = Input("Латинское название");
        var care = Input("Базовые рекомендации по уходу");
        panel.AddView(name);
        panel.AddView(latin);
        panel.AddView(care);

        existing.ItemSelected += (_, args) =>
        {
            var selected = args.Position == 0 ? null : species.ElementAtOrDefault(args.Position - 1);
            name.Text = selected?.Name ?? string.Empty;
            latin.Text = selected?.LatinName ?? string.Empty;
            care.Text = selected?.CareDescription ?? string.Empty;
            category.SetSelection(selected?.PlantTypeName switch
            {
                "Цветущие" => 1,
                "Суккуленты" => 2,
                _ => 0
            });
        };

        var actions = Horizontal();
        actions.AddView(Pill("Сохранить", true, async () =>
        {
            var speciesName = name.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(speciesName))
            {
                MarkInvalid(name);
                await AlertAsync("Проверка данных", "Введите название вида растения");
                return;
            }

            var item = existing.SelectedItemPosition == 0
                ? new PlantSpecies()
                : species.ElementAtOrDefault(existing.SelectedItemPosition - 1) ?? new PlantSpecies();
            item.Name = speciesName;
            item.LatinName = latin.Text?.Trim() ?? string.Empty;
            item.CareDescription = care.Text?.Trim() ?? string.Empty;
            item.PlantTypeName = category.SelectedItemPosition switch
            {
                1 => "Цветущие",
                2 => "Суккуленты",
                _ => "Лиственные"
            };
            if (item.Id == 0)
            {
                await _database.AddPlantSpeciesAsync(item);
            }
            else
            {
                await _database.UpdatePlantSpeciesAsync(item);
            }
            await AlertAsync("Готово", "Вид растения сохранён");
            ShowAdmin("species");
        }), Weight());
        actions.AddView(Pill("Удалить", false, async () =>
        {
            var selected = species.ElementAtOrDefault(existing.SelectedItemPosition - 1);
            if (selected is null)
            {
                await AlertAsync("Удаление", "Выберите существующий вид");
                return;
            }
            if (!await ConfirmAsync("Удаление вида", $"Удалить вид «{selected.Name}»? У растений останутся карточки без выбранного вида."))
            {
                return;
            }
            await _database.DeletePlantSpeciesAsync(selected.Id);
            ShowAdmin("species");
        }), Weight());
        panel.AddView(actions);
        return panel;
    }

    private LinearLayout AdminSettingsPanel(Dictionary<string, decimal> settings)
    {
        var panel = Vertical();
        panel.SetPadding(Dp(18), Dp(20), Dp(18), Dp(18));
        panel.Background = Rounded(SheetBg, SoftBorder, 22, 1);
        panel.LayoutParameters = MarginMatch(0, Dp(16), 0, Dp(12));

        AddSection(panel, "Как работают рекомендации");
        AddMuted(panel,
            "Укажите, через сколько дней система должна напоминать о повторной проверке. " +
            "Чем меньше число, тем чаще пользователь будет получать советы.");
        panel.AddView(Spacer(12));

        AddSection(panel, "Анализ фотоархива");
        AddMuted(panel,
            "Как часто ИИ должен сравнивать новые фотографии растения. Например: 14 — проверять изменения каждые две недели.");
        var photoDays = Input("Например: 14 дней");
        photoDays.InputType = InputTypes.ClassNumber;
        photoDays.Text = settings.GetValueOrDefault("photo_check_days", 14).ToString("0");
        panel.AddView(photoDays);
        panel.AddView(Spacer(12));

        AddSection(panel, "Рекомендация об обрезке");
        AddMuted(panel,
            "Через сколько дней после предыдущей проверки снова оценивать необходимость обрезки. Например: 90 — раз в три месяца.");
        var pruningDays = Input("Например: 90 дней");
        pruningDays.InputType = InputTypes.ClassNumber;
        pruningDays.Text = settings.GetValueOrDefault("pruning_check_days", 90).ToString("0");
        panel.AddView(pruningDays);
        panel.AddView(Spacer(12));

        AddSection(panel, "Рекомендация о пересадке");
        AddMuted(panel,
            "Через сколько дней проверять, не пора ли пересадить растение. Например: 365 — один раз в год.");
        var repotDays = Input("Например: 365 дней");
        repotDays.InputType = InputTypes.ClassNumber;
        repotDays.Text = settings.GetValueOrDefault("repot_check_days", 365).ToString("0");
        panel.AddView(repotDays);
        AddMuted(panel,
            "Значения применяются ко всем пользователям. ИИ также учитывает фотографию, вид растения и вопрос пользователя.");
        panel.AddView(Spacer(14));
        panel.AddView(PrimaryButton("Сохранить настройки", async () =>
        {
            if (!decimal.TryParse(photoDays.Text, out var photo) || photo <= 0 ||
                !decimal.TryParse(pruningDays.Text, out var pruning) || pruning <= 0 ||
                !decimal.TryParse(repotDays.Text, out var repot) || repot <= 0)
            {
                await AlertAsync("Проверка данных", "Все интервалы должны быть положительными числами");
                return;
            }
            await _database.SaveRecommendationSettingAsync("photo_check_days", photo);
            await _database.SaveRecommendationSettingAsync("pruning_check_days", pruning);
            await _database.SaveRecommendationSettingAsync("repot_check_days", repot);
            await AlertAsync("Готово", "Алгоритмы рекомендаций обновлены");
            ShowAdmin();
        }));
        return panel;
    }

    private LinearLayout AdminProtectionPanel(List<ProtectionProduct> products)
    {
        var panel = Vertical();
        panel.SetPadding(Dp(18), Dp(20), Dp(18), Dp(18));
        panel.Background = Rounded(SheetBg, SoftBorder, 22, 1);
        panel.LayoutParameters = MarginMatch(0, Dp(16), 0, Dp(12));

        var existing = SimpleSpinner(new[] { "Новое средство" }.Concat(products.Select(x => x.Name)).ToArray());
        var name = Input("Название средства");
        var ingredient = Input("Действующее вещество");
        var application = Input("Способ безопасного применения");
        var hazard = SimpleSpinner(["Низкая", "Средняя", "Высокая"]);
        panel.AddView(existing, FixedMatchHeight(52));
        panel.AddView(name);
        panel.AddView(ingredient);
        panel.AddView(application);
        panel.AddView(TwoInputs("Класс опасности", hazard, string.Empty, new Space(this)));

        existing.ItemSelected += (_, args) =>
        {
            var selected = args.Position == 0 ? null : products.ElementAtOrDefault(args.Position - 1);
            name.Text = selected?.Name ?? string.Empty;
            ingredient.Text = selected?.ActiveIngredient ?? string.Empty;
            application.Text = selected?.ApplicationDescription ?? string.Empty;
            hazard.SetSelection(selected?.HazardClass switch
            {
                "Средняя" => 1,
                "Высокая" => 2,
                _ => 0
            });
        };

        var actions = Horizontal();
        actions.AddView(Pill("Сохранить", true, async () =>
        {
            var productName = name.Text?.Trim() ?? string.Empty;
            var applicationText = application.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(applicationText))
            {
                await AlertAsync("Проверка данных", "Введите название и способ применения средства");
                return;
            }

            var item = existing.SelectedItemPosition == 0
                ? new ProtectionProduct()
                : products.ElementAtOrDefault(existing.SelectedItemPosition - 1) ?? new ProtectionProduct();
            item.Name = productName;
            item.ActiveIngredient = ingredient.Text?.Trim() ?? string.Empty;
            item.ApplicationDescription = applicationText;
            item.HazardClass = hazard.SelectedItem?.ToString() ?? "Низкая";
            if (item.Id == 0)
            {
                await _database.AddProtectionProductAsync(item);
            }
            else
            {
                await _database.UpdateProtectionProductAsync(item);
            }
            await AlertAsync("Готово", "Средство защиты сохранено");
            ShowAdmin("protection");
        }), Weight());
        actions.AddView(Pill("Удалить", false, async () =>
        {
            var selected = products.ElementAtOrDefault(existing.SelectedItemPosition - 1);
            if (selected is null)
            {
                await AlertAsync("Удаление", "Выберите существующее средство");
                return;
            }
            if (await ConfirmAsync("Удаление средства", $"Удалить «{selected.Name}»?"))
            {
                await _database.DeleteProtectionProductAsync(selected.Id);
                ShowAdmin("protection");
            }
        }), Weight());
        panel.AddView(actions);
        return panel;
    }

    private Spinner AdminRecordSpinner(List<Pest> records)
    {
        var spinner = new Spinner(this);
        spinner.Background = Rounded(InputBg, Border, 14, 2);
        spinner.SetPadding(Dp(14), 0, Dp(14), 0);
        spinner.Adapter = new ArrayAdapter<string>(
            this,
            Android.Resource.Layout.SimpleSpinnerDropDownItem,
            records.Select(x => $"{x.Name} ({(x.IsPest ? "Вредитель" : "Болезнь")}, {x.PlantTypesLabel})").ToList());
        return spinner;
    }

    private (LinearLayout Row, CheckBox Pest, CheckBox Disease) AdminTypeToggles(bool isPest)
    {
        var row = Horizontal();
        var pest = ToggleCheck("Вредитель", isPest);
        var disease = ToggleCheck("Болезнь", !isPest);
        pest.Click += (_, _) =>
        {
            pest.Checked = true;
            disease.Checked = false;
        };
        disease.Click += (_, _) =>
        {
            disease.Checked = true;
            pest.Checked = false;
        };
        row.AddView(pest, Weight());
        row.AddView(disease, Weight());
        return (row, pest, disease);
    }

    private string AdminRecordDescription(Pest record)
    {
        var category = record.IsPest ? "Вредитель" : "Болезнь";
        return $"{category}. Типы растений: {record.PlantTypesLabel}";
    }

    private (LinearLayout Row, CheckBox Foliage, CheckBox Flowering, CheckBox Succulents) AdminPlantTypeToggles(IEnumerable<string>? selected)
    {
        var values = selected?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var all = values.Count == 0;
        var row = Vertical();
        var first = Horizontal();
        var second = Horizontal();
        var foliage = ToggleCheck("Лиственные", all || values.Contains("Лиственные"));
        var flowering = ToggleCheck("Цветущие", all || values.Contains("Цветущие"));
        var succulents = ToggleCheck("Суккуленты", all || values.Contains("Суккуленты") || values.Contains("Кактусы и суккуленты"));
        first.AddView(foliage, Weight());
        first.AddView(flowering, Weight());
        second.AddView(succulents, Weight());
        row.AddView(first);
        row.AddView(second);
        return (row, foliage, flowering, succulents);
    }

    private static List<string> SelectedAdminPlantTypes((LinearLayout Row, CheckBox Foliage, CheckBox Flowering, CheckBox Succulents) controls)
    {
        var result = new List<string>();
        if (controls.Foliage.Checked)
        {
            result.Add("Лиственные");
        }

        if (controls.Flowering.Checked)
        {
            result.Add("Цветущие");
        }

        if (controls.Succulents.Checked)
        {
            result.Add("Суккуленты");
        }

        return result;
    }

    private static void FillAdminInputs(Pest record, EditText nameInput, EditText treatmentInput, CheckBox pest, CheckBox disease, (LinearLayout Row, CheckBox Foliage, CheckBox Flowering, CheckBox Succulents) plantTypes)
    {
        nameInput.Text = record.Name;
        treatmentInput.Text = record.TreatmentDescription;
        pest.Checked = record.IsPest;
        disease.Checked = !record.IsPest;
        var values = record.PlantTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var all = values.Count == 0;
        plantTypes.Foliage.Checked = all || values.Contains("Лиственные");
        plantTypes.Flowering.Checked = all || values.Contains("Цветущие");
        plantTypes.Succulents.Checked = all || values.Contains("Суккуленты") || values.Contains("Кактусы и суккуленты");
    }

    private void AddBottomSheet(string mode)
    {
        var old = FindViewById<LinearLayout>(Android.Resource.Id.Content);
        var overlay = Vertical();
        overlay.SetGravity(GravityFlags.Bottom);
        overlay.SetBackgroundColor(AColor.Transparent);

        var sheet = Vertical();
        sheet.SetPadding(Dp(26), Dp(26), Dp(26), Dp(26));
        sheet.Background = TopRounded(SheetBg, 26);

        if (mode == "photo")
        {
            var row = Horizontal();
            row.AddView(Input("Дата"), Weight());
            row.AddView(ImagePlaceholder(122, false), Weight());
            sheet.AddView(row);
            sheet.AddView(Pill("Создать", true, () => ShowPhotos(false)), FixedMatchHeight(54));
        }
        else if (mode == "delete")
        {
            sheet.AddView(Input("Название", true));
            var toggles = Horizontal();
            toggles.AddView(ToggleLine("Вредитель"), Weight());
            toggles.AddView(ToggleLine("Болезни"), Weight());
            sheet.AddView(toggles);
            sheet.AddView(Pill("Удалить", true, () => ShowAdmin()), FixedMatchHeight(54));
        }
        else
        {
            var row = Horizontal();
            row.AddView(Input("Название", mode == "edit"), Weight());
            row.AddView(ImagePlaceholder(122, false), Weight());
            sheet.AddView(row);
            sheet.AddView(Input("Лечение"));
            var toggles = Horizontal();
            toggles.AddView(ToggleLine("Вредитель"), Weight());
            toggles.AddView(ToggleLine("Болезни"), Weight());
            sheet.AddView(toggles);
            sheet.AddView(Pill(mode == "edit" ? "Редактировать" : "Создать", true, () => ShowAdmin()), FixedMatchHeight(54));
        }

        overlay.AddView(sheet, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        AddContentView(overlay, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
    }

    private LinearLayout Card()
    {
        var card = Vertical();
        card.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
        card.Background = Rounded(CardBg, Border, 24, 2);
        var lp = new LinearLayout.LayoutParams(0, Dp(238), 1);
        lp.SetMargins(Dp(6), Dp(6), Dp(6), Dp(8));
        card.LayoutParameters = lp;
        return card;
    }

    private Button PrimaryButton(string text, Action click)
    {
        var button = Pill(text, true, click);
        button.TextSize = 16;
        button.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        button.LayoutParameters = FixedMatchHeight(52);
        return button;
    }

    private Button Pill(string text, bool filled, Action click)
    {
        var button = new Button(this) { Text = text };
        button.SetAllCaps(false);
        button.TextSize = 15;
        button.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        SetPillStyle(button, filled, text);
        button.SetPadding(Dp(10), 0, Dp(10), 0);
        button.Click += (_, _) => click();
        return button;
    }

    private void SetPillStyle(Button? button, bool filled, string text)
    {
        if (button is null)
        {
            return;
        }

        button.Text = text;
        button.SetTextColor(AColor.ParseColor(filled ? DarkText : Mint));
        button.Background = Rounded(filled ? Mint : Bg, Mint, 26, filled ? 0 : 2);
    }

    private EditText Input(string hint, bool chevron = false)
    {
        var input = new EditText(this);
        input.Hint = chevron ? $"{hint}       ˅" : hint;
        input.TextSize = 14;
        input.SetSingleLine(true);
        input.SetTextColor(AColor.ParseColor(Text));
        input.SetHintTextColor(AColor.ParseColor(Muted));
        input.SetPadding(Dp(14), 0, Dp(14), 0);
        input.Background = Rounded(InputBg, Border, 10, 2);
        input.LayoutParameters = FixedMatchHeight(48);
        return input;
    }

    private void AddFieldBlock(LinearLayout parent, string label, string hint, string? subLabel = null, bool chevron = false)
    {
        AddSection(parent, label);
        if (!string.IsNullOrWhiteSpace(subLabel))
        {
            AddMuted(parent, subLabel);
        }
        parent.AddView(Input(hint, chevron));
    }

    private LinearLayout TwoInputs(string label1, string hint1, string label2, string hint2)
    {
        var row = Horizontal();
        var one = Vertical();
        AddMuted(one, label1);
        one.AddView(Input(hint1));
        var two = Vertical();
        AddMuted(two, label2);
        two.AddView(Input(hint2));
        row.AddView(one, Weight());
        row.AddView(two, Weight());
        return row;
    }

    private LinearLayout TwoInputs(string label1, View input1, string label2, View input2)
    {
        var row = Horizontal();
        var one = Vertical();
        AddMuted(one, label1);
        one.AddView(input1);
        var two = Vertical();
        AddMuted(two, label2);
        two.AddView(input2);
        row.AddView(one, Weight());
        row.AddView(two, Weight());
        return row;
    }

    private LinearLayout ToggleLine(string text)
    {
        var row = Horizontal();
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(0, Dp(8), 0, Dp(8));
        var dot = new View(this);
        dot.Background = Rounded("#E8EBED", "#E8EBED", 18, 0);
        row.AddView(dot, Fixed(30, 30));
        row.AddView(Txt(text, 13, Text, TypefaceStyle.Normal));
        return row;
    }

    private TextView ImagePlaceholder(int height, bool fullWidth)
    {
        var image = Txt("△○", 58, Muted, TypefaceStyle.Normal);
        image.Gravity = GravityFlags.Center;
        image.Background = Rounded(Bg, Border, 0, 2);
        if (fullWidth)
        {
            image.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(height));
        }
        return image;
    }

    private void AddSection(LinearLayout parent, string text)
    {
        parent.AddView(Txt(text, 16, Text, TypefaceStyle.Bold), MarginMatch(0, Dp(16), 0, Dp(8)));
    }

    private void AddTitle(LinearLayout parent, string text, int size)
    {
        parent.AddView(Txt(text, size, Text, TypefaceStyle.Normal));
    }

    private void AddMuted(LinearLayout parent, string text)
    {
        parent.AddView(Txt(text, 12, Muted, TypefaceStyle.Normal));
    }

    private void AddCentered(LinearLayout parent, string text, int size, string color, TypefaceStyle style)
    {
        var label = Txt(text, size, color, style);
        label.Gravity = GravityFlags.Center;
        parent.AddView(label, MatchWrap());
    }

    private static string PriorityColor(string priority) => priority switch
    {
        "Высокий" => Red,
        "Средний" => Mint,
        _ => Secondary
    };

    private TextView Txt(string text, int size, string color, TypefaceStyle style)
    {
        var label = new TextView(this) { Text = text, TextSize = size };
        label.SetTextColor(AColor.ParseColor(color));
        label.SetTypeface(Typeface.Default, style);
        label.SetIncludeFontPadding(true);
        return label;
    }

    private ImageView Icon(string resourceName, int size, string color)
    {
        var image = new ImageView(this);
        var id = Resources.GetIdentifier(resourceName, "drawable", PackageName);
        if (id != 0)
        {
            image.SetImageResource(id);
            image.SetColorFilter(AColor.ParseColor(color), PorterDuff.Mode.SrcIn);
        }
        image.SetAdjustViewBounds(true);
        image.SetScaleType(ImageView.ScaleType.CenterInside);
        image.SetPadding(Dp(2), Dp(2), Dp(2), Dp(2));
        image.LayoutParameters = CenterFixed(size, size);
        return image;
    }

    private View Spacer(int height)
    {
        return new Space(this) { LayoutParameters = new LinearLayout.LayoutParams(1, Dp(height)) };
    }

    private LinearLayout Vertical() => new(this) { Orientation = Orientation.Vertical };

    private LinearLayout Horizontal() => new(this) { Orientation = Orientation.Horizontal };

    private LinearLayout.LayoutParams Weight()
    {
        var lp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        lp.SetMargins(Dp(4), 0, Dp(4), 0);
        return lp;
    }

    private LinearLayout.LayoutParams SegmentWeight()
    {
        return new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1);
    }

    private LinearLayout.LayoutParams Fixed(int width, int height)
    {
        var lp = new LinearLayout.LayoutParams(Dp(width), Dp(height));
        lp.SetMargins(Dp(4), Dp(2), Dp(8), Dp(2));
        return lp;
    }

    private LinearLayout.LayoutParams CenterFixed(int width, int height)
    {
        var lp = new LinearLayout.LayoutParams(Dp(width), Dp(height));
        lp.Gravity = GravityFlags.CenterHorizontal;
        lp.SetMargins(0, Dp(2), 0, Dp(6));
        return lp;
    }

    private LinearLayout.LayoutParams FixedMatchHeight(int height)
    {
        var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(height));
        lp.SetMargins(0, Dp(8), 0, Dp(8));
        return lp;
    }

    private LinearLayout.LayoutParams MatchWrap()
    {
        return new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
    }

    private LinearLayout.LayoutParams MarginMatch(int left, int top, int right, int bottom, int height = 0)
    {
        var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, height == 0 ? ViewGroup.LayoutParams.WrapContent : height);
        lp.SetMargins(left, top, right, bottom);
        return lp;
    }

    private GradientDrawable Rounded(string fill, string stroke, int radius, int strokeWidth)
    {
        var drawable = new GradientDrawable();
        drawable.SetColor(AColor.ParseColor(fill));
        drawable.SetCornerRadius(Dp(radius));
        if (strokeWidth > 0)
        {
            drawable.SetStroke(Dp(strokeWidth), AColor.ParseColor(stroke));
        }
        return drawable;
    }

    private GradientDrawable TopRounded(string fill, int radius)
    {
        var drawable = new GradientDrawable();
        drawable.SetColor(AColor.ParseColor(fill));
        var r = Dp(radius);
        drawable.SetCornerRadii([r, r, r, r, 0, 0, 0, 0]);
        return drawable;
    }

    private void Toast(string message)
    {
        Android.Widget.Toast.MakeText(this, message, ToastLength.Short)?.Show();
    }

    private int Dp(int value)
    {
        return (int)(value * Resources.DisplayMetrics!.Density);
    }

    private int StatusBarInset()
    {
        var id = Resources.GetIdentifier("status_bar_height", "dimen", "android");
        return id > 0 ? Resources.GetDimensionPixelSize(id) : Dp(30);
    }
}


