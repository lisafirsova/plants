using Android.App;
using Android.Runtime;

namespace Plants;

[Application(
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher_round",
    Label = "Plants")]
public class MainApplication : Application
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }
}
