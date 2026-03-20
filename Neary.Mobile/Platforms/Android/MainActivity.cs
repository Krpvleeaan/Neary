using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace Neary.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int LocationPermissionCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestLocationPermissions();
    }

    private void RequestLocationPermissions()
    {
        var permissions = new[]
        {
            Manifest.Permission.AccessFineLocation,
            Manifest.Permission.AccessCoarseLocation
        };

        var needed = permissions
            .Where(p => ContextCompat.CheckSelfPermission(this, p) != Permission.Granted)
            .ToArray();

        if (needed.Length > 0)
        {
            ActivityCompat.RequestPermissions(this, needed, LocationPermissionCode);
        }
        else
        {
            RequestBackgroundLocationIfNeeded();
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == LocationPermissionCode && grantResults.All(r => r == Permission.Granted))
        {
            RequestBackgroundLocationIfNeeded();
        }
    }

    private void RequestBackgroundLocationIfNeeded()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q &&
            ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessBackgroundLocation) != Permission.Granted)
        {
            ActivityCompat.RequestPermissions(
                this,
                [Manifest.Permission.AccessBackgroundLocation],
                LocationPermissionCode + 1);
        }
    }
}
