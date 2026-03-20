 namespace Neary.Mobile.Services;

public sealed class LocationTracker
{
    public static LocationTracker Instance { get; } = new();
    private LocationTracker() { }

    public bool IsRunning { get; set; }

    public event Action<string>? StatusChanged;
    public event Action<double, double>? LocationUpdated;

    public void NotifyStatus(string status) => StatusChanged?.Invoke(status);
    public void NotifyLocation(double lat, double lon) => LocationUpdated?.Invoke(lat, lon);
}
