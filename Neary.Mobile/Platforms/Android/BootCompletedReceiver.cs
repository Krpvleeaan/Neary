using Android.App;
using Android.Content;
using Android.OS;
using Neary.Mobile.Platforms.Android.Services;

namespace Neary.Mobile.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public sealed class BootCompletedReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action != Intent.ActionBootCompleted)
            return;
        if (!TrackingState.IsEnabled(context))
            return;
        var userId = TrackingState.GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            return;

        try
        {
            var i = new Intent(context, typeof(LocationForegroundService));
            i.PutExtra("userId", userId);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(i);
            else
                context.StartService(i);
        }
        catch
        {
            // Ограничения фона после загрузки (Android 12+) — не валим процесс
        }
    }
}
