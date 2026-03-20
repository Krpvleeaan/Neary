using Neary.Mobile.Services;

namespace Neary.Mobile;

public partial class MainPage : ContentPage
{
    private bool _isActive;

    public MainPage()
    {
        InitializeComponent();

        VersionLabel.Text = $"Сборка {AppInfo.VersionString} ({AppInfo.BuildString}) · интервал 20 мин";

        LocationTracker.Instance.StatusChanged += s =>
            MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = s);

        LocationTracker.Instance.LocationUpdated += (lat, lon) =>
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await MapView.EvaluateJavaScriptAsync(
                        $"updatePosition({lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                        $"{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
                }
                catch { }
            });

        MapView.Source = new HtmlWebViewSource { Html = LoadMapHtml() };

        if (LocationTracker.Instance.IsRunning)
        {
            _isActive = true;
            UserIdEntry.IsEnabled = false;
            ToggleButton.Text = "ОСТАНОВИТЬ";
            ToggleButton.BackgroundColor = Color.FromArgb("#DC2626");
            StatusLabel.Text = "Трансляция активна";
            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
        }
    }

    private string LoadMapHtml()
    {
        using var stream = FileSystem.OpenAppPackageFileAsync("map.html").GetAwaiter().GetResult();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        await ToggleButton.ScaleTo(0.95, 80, Easing.CubicIn);
        await ToggleButton.ScaleTo(1.0, 80, Easing.CubicOut);

        if (_isActive)
            StopTracking();
        else
            await StartTracking();
    }

    private async Task StartTracking()
    {
        var userId = UserIdEntry.Text?.Trim();
        if (string.IsNullOrEmpty(userId))
        {
            await DisplayAlert("Neary", "Введите ваш ID для начала трансляции", "OK");
            return;
        }

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Neary", "Для работы необходим доступ к геолокации", "OK");
            return;
        }

        _isActive = true;
        UserIdEntry.IsEnabled = false;
        ToggleButton.Text = "ОСТАНОВИТЬ";
        ToggleButton.BackgroundColor = Color.FromArgb("#DC2626");
        StatusLabel.Text = "Запуск трансляции...";
        StatusLabel.TextColor = Color.FromArgb("#3B82F6");

#if ANDROID
        var ctx = Android.App.Application.Context;
        Platforms.Android.TrackingState.SetEnabled(ctx, userId);
        var intent = new Android.Content.Intent(ctx, typeof(Platforms.Android.Services.LocationForegroundService));
        intent.PutExtra("userId", userId);
        ctx.StartForegroundService(intent);
#endif
    }

    private void StopTracking()
    {
        _isActive = false;
        UserIdEntry.IsEnabled = true;
        ToggleButton.Text = "ЗАПУСТИТЬ ТРАНСЛЯЦИЮ";
        ToggleButton.BackgroundColor = Color.FromArgb("#3B82F6");
        StatusLabel.Text = "Трансляция не активна";
        StatusLabel.TextColor = Color.FromArgb("#8B949E");

#if ANDROID
        var ctx = Android.App.Application.Context;
        Platforms.Android.TrackingState.Clear(ctx);
        ctx.StopService(new Android.Content.Intent(ctx, typeof(Platforms.Android.Services.LocationForegroundService)));
#endif
    }
}
