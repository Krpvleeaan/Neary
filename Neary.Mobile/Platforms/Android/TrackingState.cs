using Android.Content;

namespace Neary.Mobile.Platforms.Android;

/// <summary>Сохраняет ID и флаг «трансляция включена» — чтобы сервис переживал закрытие задачи и перезагрузку.</summary>
internal static class TrackingState
{
    private const string Name = "neary_tracking";
    private const string KeyUser = "user_id";
    private const string KeyOn = "on";

    public static void SetEnabled(Context ctx, string userId)
    {
        ctx.GetSharedPreferences(Name, FileCreationMode.Private)!.Edit()!
            .PutString(KeyUser, userId)!
            .PutBoolean(KeyOn, true)!
            .Commit();
    }

    public static void Clear(Context ctx)
    {
        ctx.GetSharedPreferences(Name, FileCreationMode.Private)!.Edit()!
            .PutBoolean(KeyOn, false)!
            .Remove(KeyUser)!
            .Commit();
    }

    public static bool IsEnabled(Context ctx) =>
        ctx.GetSharedPreferences(Name, FileCreationMode.Private)!.GetBoolean(KeyOn, false);

    public static string? GetUserId(Context ctx) =>
        ctx.GetSharedPreferences(Name, FileCreationMode.Private)!.GetString(KeyUser, null);
}
