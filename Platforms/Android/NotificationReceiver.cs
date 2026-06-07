using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Plants.Services;

namespace Plants;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class NotificationReceiver : BroadcastReceiver
{
    public const string TitleKey = "title";
    public const string BodyKey = "body";
    public const string NotificationIdKey = "notificationId";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        ShowNotification(
            context,
            intent.GetIntExtra(NotificationIdKey, 1001),
            intent.GetStringExtra(TitleKey) ?? "Plants",
            intent.GetStringExtra(BodyKey) ?? "Напоминание по уходу за растением");
        new NotificationService(context).ScheduleNextFromReceiver(intent);
    }

    public static void ShowNotification(Context context, int id, string title, string body)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            return;
        }

        var openIntent = new Intent(context, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        var contentIntent = PendingIntent.GetActivity(
            context,
            id,
            openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var notification = new Notification.Builder(context, NotificationService.ChannelId)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetContentIntent(contentIntent)
            .SetAutoCancel(true)
            .SetCategory(Notification.CategoryReminder)
            .Build();
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.Notify(id, notification);
    }
}
