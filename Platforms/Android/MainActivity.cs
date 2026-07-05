using Android.App;
using Android.Content.PM;
using Android.Views;
using Microsoft.Maui.Platform;

namespace com.shmuel.recipebook;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Exported = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
    ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]

[IntentFilter(new[] { Android.Content.Intent.ActionView },
    Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "recipe-book-d9389.web.app",
    DataPathPrefix = "/sharelist",
    AutoVerify = true)]

[IntentFilter(new[] { Android.Content.Intent.ActionView },
    Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "recipe-book-d9389.web.app",
    DataPathPrefix = "/recipe",
    AutoVerify = true)]

[IntentFilter(new[] { Android.Content.Intent.ActionView },
    Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "recipe-book-d9389.web.app",
    DataPathPrefix = "/folder",
    AutoVerify = true)]

public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Android.OS.Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue("SecondaryBack", out var secondaryRes) == true && secondaryRes is Color secondaryColor)
        {
            Window.SetStatusBarColor(secondaryColor.ToPlatform());
        }

        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
        {
            Window.DecorView.SystemUiVisibility = (Android.Views.StatusBarVisibility)Android.Views.SystemUiFlags.LightStatusBar;
        }


        //
        try
        {
            Firebase.FirebaseApp.InitializeApp(this);
            System.Diagnostics.Debug.WriteLine("FIREBASE SUCCESS: App initialized!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FIREBASE CRASH: {ex.Message}");
        }

        Android.Content.Intent currentIntent = Intent;
        if (currentIntent?.Action == Android.Content.Intent.ActionView && !string.IsNullOrWhiteSpace(currentIntent.DataString))
        {
            Recipe_book.App.PendingDeepLinkUrl = currentIntent.DataString;
        }
    }

    protected override void OnNewIntent(Android.Content.Intent intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;

        if (intent.Action == Android.Content.Intent.ActionView && !string.IsNullOrWhiteSpace(intent.DataString))
        {
            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(new Uri(intent.DataString));
        }
    }
}