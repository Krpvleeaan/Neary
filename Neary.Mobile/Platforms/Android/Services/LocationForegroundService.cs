using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Neary.Mobile.Platforms.Android;
using Neary.Mobile.Services;
using System.Net.Http.Json;

namespace Neary.Mobile.Platforms.Android.Services;

[Service(ForegroundServiceType = ForegroundService.TypeLocation)]
public class LocationForegroundService : Service
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        lock (_sync)
        {
            var userId = intent?.GetStringExtra("userId") ?? TrackingState.GetUserId(this);
            if (string.IsNullOrEmpty(userId))
                return StartCommandResult.NotSticky;

            // Повторный старт, пока цикл уже крутится — не плодим второй Task
            if (_cts is { IsCancellationRequested: false })
                return StartCommandResult.Sticky;

            StartForegroundNotification();

            _cts = new CancellationTokenSource();
            LocationTracker.Instance.IsRunning = true;

            Task.Run(() => TrackingLoop(userId!, _cts.Token));

            return StartCommandResult.Sticky;
        }
    }

    private void StartForegroundNotification()
    {
        const string channelId = "neary_tracking";
        var nm = (NotificationManager)GetSystemService(NotificationService)!;
        nm.CreateNotificationChannel(new NotificationChannel(channelId, "Отслеживание", NotificationImportance.Low));

        var notification = new Notification.Builder(this, channelId)
            .SetContentTitle("Neary")
            .SetContentText("Отслеживание активно")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuMyLocation)
            .SetOngoing(true)
            .Build();

        StartForeground(1001, notification, ForegroundService.TypeLocation);
    }

    /// <summary>Задача убрана из недавних — при включённой трансляции поднимаем сервис снова (стандартный приём для OEM).</summary>
    public override void OnTaskRemoved(Intent? rootIntent)
    {
        if (!TrackingState.IsEnabled(this))
            return;
        var userId = TrackingState.GetUserId(this);
        if (string.IsNullOrEmpty(userId))
            return;

        var i = new Intent(ApplicationContext!, typeof(LocationForegroundService));
        i.PutExtra("userId", userId);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            ApplicationContext!.StartForegroundService(i);
        else
            ApplicationContext!.StartService(i);
    }

    private async Task TrackingLoop(string userId, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            PowerManager.WakeLock? wake = null;
            try
            {
                var pm = (PowerManager?)GetSystemService(PowerService);
                wake = pm?.NewWakeLock(WakeLockFlags.Partial, "Neary:LocSend");
                wake?.SetReferenceCounted(false);
                wake?.Acquire((long)TimeSpan.FromMinutes(2).TotalMilliseconds);

                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(30));
                var location = await Geolocation.Default.GetLocationAsync(request, token);

                if (location != null)
                {
                    var battery = GetBatteryLevel();

                    LocationTracker.Instance.NotifyLocation(location.Latitude, location.Longitude);

                    await _http.PostAsJsonAsync(
                        ApiConfig.LocationPostUrl,
                        new { UserId = userId, Lat = location.Latitude, Lon = location.Longitude, Battery = battery },
                        token);

                    LocationTracker.Instance.NotifyStatus(
                        $"Обновлено в {DateTime.Now:HH:mm} · Батарея {battery}%");
                }
            }
            catch (System.OperationCanceledException) { break; }
            catch (Exception ex)
            {
                LocationTracker.Instance.NotifyStatus($"Ошибка: {ex.Message}");
            }
            finally
            {
                try { wake?.Release(); } catch { /* ignored */ }
            }

            try { await Task.Delay(TimeSpan.FromMinutes(20), token); }
            catch (System.OperationCanceledException) { break; }
        }
    }

    private static double GetBatteryLevel()
    {
        try
        {
            using var filter = new IntentFilter(Intent.ActionBatteryChanged);
            using var bi = global::Android.App.Application.Context.RegisterReceiver(null, filter);
            if (bi is null) return -1;
            var level = bi.GetIntExtra("level", -1);
            var scale = bi.GetIntExtra("scale", -1);
            return scale > 0 ? Math.Round(level * 100.0 / scale) : -1;
        }
        catch { return -1; }
    }

    public override void OnDestroy()
    {
        lock (_sync)
        {
            _cts?.Cancel();
            _cts = null;
            // Остановка только по кнопке (prefs сброшены) — тогда обновляем UI
            if (!TrackingState.IsEnabled(this))
            {
                LocationTracker.Instance.IsRunning = false;
                LocationTracker.Instance.NotifyStatus("Трансляция остановлена");
            }
        }

        base.OnDestroy();
    }
}
