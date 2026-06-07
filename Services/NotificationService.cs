using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Plants.Models;

namespace Plants.Services;

public sealed class NotificationService
{
    public const string ChannelId = "plants.care.v2";
    public const string ScheduledAtKey = "scheduledAt";
    public const string PeriodDaysKey = "periodDays";
    private readonly Context _context;

    public NotificationService(Context context)
    {
        _context = context;
        CreateNotificationChannel();
    }

    public bool NotificationsEnabled()
    {
        var manager = (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        var permissionGranted = Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu ||
            _context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Permission.Granted;
        return permissionGranted && manager?.AreNotificationsEnabled() != false;
    }

    public void ScheduleWateringNotification(Plant plant, WateringSchedule schedule)
    {
        if (schedule.PeriodDays <= 0)
        {
            return;
        }

        var triggerAt = BuildTriggerTime(schedule.StartDate, schedule.NotificationTime, schedule.PeriodDays);
        ScheduleAlarm(
            GetNotificationId(plant.Id, schedule.Type),
            CareTitle(schedule.Type),
            plant.Name,
            triggerAt,
            schedule.PeriodDays);
    }

    public void ScheduleNextFromReceiver(Intent sourceIntent)
    {
        var id = sourceIntent.GetIntExtra(NotificationReceiver.NotificationIdKey, 0);
        var periodDays = sourceIntent.GetIntExtra(PeriodDaysKey, 0);
        var previousTrigger = sourceIntent.GetLongExtra(ScheduledAtKey, 0);
        if (id == 0 || periodDays <= 0 || previousTrigger <= 0)
        {
            return;
        }

        var next = DateTimeOffset.FromUnixTimeMilliseconds(previousTrigger).LocalDateTime
            .AddDays(periodDays);
        while (next <= DateTime.Now)
        {
            next = next.AddDays(periodDays);
        }
        ScheduleAlarm(
            id,
            sourceIntent.GetStringExtra(NotificationReceiver.TitleKey) ?? "Plants",
            sourceIntent.GetStringExtra(NotificationReceiver.BodyKey) ?? "Напоминание по уходу",
            new DateTimeOffset(next).ToUnixTimeMilliseconds(),
            periodDays);
    }

    public void ShowTestNotification()
    {
        NotificationReceiver.ShowNotification(
            _context,
            909001,
            "Уведомления Plants работают",
            "Напоминания о поливе и подкормке включены.");
    }

    public void CancelNotification(int plantId)
    {
        CancelNotification(plantId, "Watering");
        CancelNotification(plantId, "Fertilizer");
        CancelNotification(plantId, "Pruning");
        CancelNotification(plantId, "Repotting");
    }

    public void CancelNotification(int plantId, string type)
    {
        var alarm = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
        var intent = new Intent(_context, typeof(NotificationReceiver));
        var pendingIntent = PendingIntent.GetBroadcast(
            _context,
            GetNotificationId(plantId, type),
            intent,
            PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
        if (pendingIntent is null)
        {
            return;
        }
        alarm?.Cancel(pendingIntent);
        pendingIntent.Cancel();
    }

    public static int GetNotificationId(int plantId, string type)
    {
        var suffix = type switch
        {
            "Fertilizer" => 2,
            "Pruning" => 3,
            "Repotting" => 4,
            _ => 1
        };
        return plantId * 10 + suffix;
    }

    private void ScheduleAlarm(int id, string title, string body, long triggerAt, int periodDays)
    {
        var alarm = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
        if (alarm is null)
        {
            return;
        }

        var intent = new Intent(_context, typeof(NotificationReceiver));
        intent.PutExtra(NotificationReceiver.TitleKey, title);
        intent.PutExtra(NotificationReceiver.BodyKey, body);
        intent.PutExtra(NotificationReceiver.NotificationIdKey, id);
        intent.PutExtra(ScheduledAtKey, triggerAt);
        intent.PutExtra(PeriodDaysKey, periodDays);
        var pendingIntent = PendingIntent.GetBroadcast(
            _context,
            id,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        if (pendingIntent is null)
        {
            return;
        }

        try
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.S || alarm.CanScheduleExactAlarms())
            {
                alarm.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pendingIntent);
            }
            else
            {
                alarm.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pendingIntent);
            }
        }
        catch (Java.Lang.SecurityException)
        {
            alarm.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pendingIntent);
        }
    }

    private static long BuildTriggerTime(DateTime startDate, TimeSpan notificationTime, int periodDays)
    {
        var local = startDate.Date.Add(notificationTime);
        while (local <= DateTime.Now)
        {
            local = local.AddDays(Math.Max(1, periodDays));
        }
        return new DateTimeOffset(local).ToUnixTimeMilliseconds();
    }

    private static string CareTitle(string type) => type switch
    {
        "Fertilizer" => "Пора удобрить растение",
        "Pruning" => "Пора осмотреть растение для обрезки",
        "Repotting" => "Пора проверить необходимость пересадки",
        _ => "Пора полить растение"
    };

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }
        var manager = (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        var channel = new NotificationChannel(ChannelId, "Уход за растениями", NotificationImportance.High)
        {
            Description = "Напоминания о поливе, подкормке, обрезке и пересадке"
        };
        channel.EnableVibration(true);
        manager?.CreateNotificationChannel(channel);
    }
}
