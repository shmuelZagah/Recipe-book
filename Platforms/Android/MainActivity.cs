using Android.App;
using Android.Content.PM;
using Android.Views;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Exported = true,
    //WindowSoftInputMode = SoftInput.AdjustPan,

    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | 
    ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]

// ==========================================
// Listen for ://recipebook.app/sharelist
// ==========================================
[IntentFilter(new[] { Android.Content.Intent.ActionView },
    Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
    DataScheme = "http",
    DataHost = "recipebook.app",
    DataPathPrefix = "/sharelist")]
// ==========================================
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Android.OS.Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        try
        {
            Firebase.FirebaseApp.InitializeApp(this);
            System.Diagnostics.Debug.WriteLine("FIREBASE SUCCESS: App initialized!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FIREBASE CRASH: {ex.Message}");
        }
    }
}