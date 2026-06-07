using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common.Apis;
using Android.Net;
using Plants.Models;
using Plants.Services.Database;

namespace Plants.Services;

public sealed class AuthService
{
    private const string PreferencesName = "plants.preferences";
    private const string CurrentUserIdKey = "CurrentUserId";
    private const string SignedOutKey = "SignedOut";
    private readonly Context _context;
    private readonly DatabaseService _database;
    private readonly SecureDataService _secureData = new();

    public AuthService(Context context, DatabaseService database)
    {
        _context = context;
        _database = database;
    }

    public Intent GetGoogleSignInIntent()
    {
        if (!HasInternetConnection())
        {
            throw new InvalidOperationException("Нет подключения к интернету. Для входа через Google подключите устройство к сети.");
        }

        return CreateGoogleSignInClient().SignInIntent;
    }

    public async Task<User?> TryRestoreGoogleSessionAsync()
    {
        if (WasSignedOut())
        {
            return null;
        }

        var account = GoogleSignIn.GetLastSignedInAccount(_context);
        if (account is null)
        {
            return null;
        }

        var user = await SaveGoogleUserAsync(account);
        SaveCurrentUserId(user.Id);
        return user;
    }

    public async Task<User> CompleteGoogleSignInAsync(Intent? data)
    {
        try
        {
            var accountResult = GoogleSignIn.GetSignedInAccountFromIntent(data);
            var account = accountResult.GetResult(Java.Lang.Class.FromType(typeof(ApiException))) as GoogleSignInAccount;
            if (account is null)
            {
                throw new InvalidOperationException("Google не вернул данные аккаунта.");
            }

            var user = await SaveGoogleUserAsync(account);
            SaveCurrentUserId(user.Id);
            SetSignedOut(false);
            return user;
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 12502)
            {
                var lastAccount = GoogleSignIn.GetLastSignedInAccount(_context);
                if (lastAccount is not null)
                {
                    var restoredUser = await SaveGoogleUserAsync(lastAccount);
                    SaveCurrentUserId(restoredUser.Id);
                    SetSignedOut(false);
                    return restoredUser;
                }

                throw new InvalidOperationException("Вход через Google уже выполняется. Подождите пару секунд и попробуйте еще раз.", ex);
            }

            if (ex.StatusCode == 12501)
            {
                throw new InvalidOperationException("Вход через Google был отменен.", ex);
            }

            if (ex.StatusCode == 10)
            {
                throw new InvalidOperationException(
                    "Google Sign-In отклонен настройками OAuth. Проверьте Android OAuth client: package name должен быть com.companyname.plants, SHA-1 должен совпадать с debug.keystore, а аккаунт должен быть добавлен в Test users.",
                    ex);
            }

            throw new InvalidOperationException($"Google Sign-In завершился ошибкой: {ex.StatusCode}.", ex);
        }
    }

    public int? GetCurrentUserId()
    {
        var preferences = _context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        if (preferences is null || !preferences.Contains(CurrentUserIdKey))
        {
            return null;
        }
        return preferences.GetInt(CurrentUserIdKey, 0);
    }

    public void SaveCurrentUserId(int userId)
    {
        var preferences = _context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        preferences?.Edit()?.PutInt(CurrentUserIdKey, userId).PutBoolean(SignedOutKey, false).Apply();
    }

    public async Task SignOutAsync()
    {
        await CreateGoogleSignInClient().SignOutAsync();
        var preferences = _context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        preferences?.Edit()?.Remove(CurrentUserIdKey).PutBoolean(SignedOutKey, true).Apply();
    }

    private async Task<User> SaveGoogleUserAsync(GoogleSignInAccount account)
    {
        var stableGoogleId = account.Id ?? account.Email ?? account.IdToken ?? throw new InvalidOperationException("В Google-аккаунте нет идентификатора.");
        var googleIdHash = _secureData.HashStableId(stableGoogleId);
        var users = await _database.GetUsersAsync();
        var user = await _database.GetUserByGoogleIdAsync(googleIdHash);
        var isFirstUser = users.Count == 0;

        if (user is null)
        {
            user = new User
            {
                GoogleId = googleIdHash,
                Email = _secureData.Encrypt(account.Email ?? string.Empty),
                Name = _secureData.Encrypt(account.DisplayName ?? account.GivenName ?? account.Email ?? "Пользователь"),
                AvatarUrl = _secureData.Encrypt(account.PhotoUrl?.ToString() ?? string.Empty),
                IsAdmin = isFirstUser
            };
            await _database.AddUserAsync(user);
        }
        else
        {
            user.Email = _secureData.Encrypt(account.Email ?? _secureData.Decrypt(user.Email));
            user.Name = _secureData.Encrypt(account.DisplayName ?? account.GivenName ?? _secureData.Decrypt(user.Name));
            user.AvatarUrl = _secureData.Encrypt(account.PhotoUrl?.ToString() ?? _secureData.Decrypt(user.AvatarUrl));
            await _database.UpdateUserAsync(user);
        }

        return user;
    }

    private GoogleSignInClient CreateGoogleSignInClient()
    {
        var options = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
            .RequestEmail()
            .RequestProfile()
            .Build();
        return GoogleSignIn.GetClient(_context, options);
    }

    private bool WasSignedOut()
    {
        var preferences = _context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        return preferences?.GetBoolean(SignedOutKey, false) == true;
    }

    private void SetSignedOut(bool value)
    {
        var preferences = _context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        preferences?.Edit()?.PutBoolean(SignedOutKey, value).Apply();
    }

    private bool HasInternetConnection()
    {
        try
        {
            var connectivity = (ConnectivityManager?)_context.GetSystemService(Context.ConnectivityService);
            if (connectivity is null)
            {
                return true;
            }

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
            {
                var network = connectivity.ActiveNetwork;
                var capabilities = connectivity.GetNetworkCapabilities(network);
                return capabilities?.HasCapability(NetCapability.Internet) == true;
            }

            return connectivity.ActiveNetworkInfo?.IsConnected == true;
        }
        catch (Java.Lang.SecurityException)
        {
            return true;
        }
    }
}
