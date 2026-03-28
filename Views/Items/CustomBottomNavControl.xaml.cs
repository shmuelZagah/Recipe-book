// CustomBottomNavControl.xaml.cs
namespace Recipe_book.Views.Items;

public partial class CustomBottomNavControl : ContentView
{
    public static readonly BindableProperty ActiveTabProperty =
    BindableProperty.Create(nameof(ActiveTab), typeof(int), typeof(CustomBottomNavControl), 0);

    public int ActiveTab
    {
        get => (int)GetValue(ActiveTabProperty);
        private set => SetValue(ActiveTabProperty, value);
    }

    private int _currentTab = 0;
    private Image[] _tabIcons;
    private readonly float[] _tabPositions = { 0.875f, 0.625f, 0.375f, 0.125f };

    public NavBarDrawable NavDrawable { get; } = new NavBarDrawable();

    public event EventHandler<int> TabSelected;

    public CustomBottomNavControl()
    {
        InitializeComponent();

        _tabIcons = new Image[4];
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

   
        _tabIcons[0] = Tab0Icon;
        _tabIcons[1] = Tab1Icon;
        _tabIcons[2] = Tab2Icon;
        _tabIcons[3] = Tab3Icon;


        SetInitialState();
    }

    private void SetInitialState()
    {
        NavDrawable.HumpCenterX = _tabPositions[_currentTab];
        NavBarGraphics.Invalidate();

        _tabIcons[_currentTab].Scale = 1.3;
        _tabIcons[_currentTab].TranslationY = -25;
    }

    private async void OnTabTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string paramStr && int.TryParse(paramStr, out int newTab))
        {
            if (newTab == _currentTab) return;

            await AnimateTabChange(_currentTab, newTab);

            _currentTab = newTab;
            ActiveTab = newTab;
            TabSelected?.Invoke(this, newTab);
        }
    }

    private async Task AnimateTabChange(int fromTab, int toTab)
    {
        var fromIcon = _tabIcons[fromTab];
        var toIcon = _tabIcons[toTab];

        float startX = _tabPositions[fromTab];
        float endX = _tabPositions[toTab];

        uint duration = 300;


        var tasks = new List<Task>
        {
          
            fromIcon.ScaleTo(1.0, duration, Easing.CubicInOut),
            fromIcon.TranslateTo(0, 0, duration, Easing.CubicInOut),
            
     
            toIcon.ScaleTo(1.3, duration, Easing.CubicInOut),
            toIcon.TranslateTo(0, -25, duration, Easing.CubicInOut),
            

            AnimateHump(startX, endX, duration)
        };

        await Task.WhenAll(tasks);
    }

    private Task AnimateHump(float from, float to, uint duration)
    {
        var tcs = new TaskCompletionSource<bool>();

        var animation = new Animation(v =>
        {
            NavDrawable.HumpCenterX = (float)v;
            NavBarGraphics.Invalidate();
        }, from, to, Easing.CubicInOut);

        animation.Commit(this, "HumpAnimation", length: duration, finished: (v, c) =>
        {
            tcs.SetResult(true);
        });

        return tcs.Task;
    }

    public async Task SelectTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= 4 || tabIndex == _currentTab) return;

        await AnimateTabChange(_currentTab, tabIndex);
        _currentTab = tabIndex;
        ActiveTab = tabIndex;
    }
}
