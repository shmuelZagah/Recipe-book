using Recipe_book.ViewModels;
using Recipe_book.Views.Items.bars;
using Recipe_book.Views.Pages;

namespace Recipe_book;

public partial class MainPage : ContentPage
{
    private readonly ContentView _homePage;
    private readonly ContentView _libraryPage;
    private readonly ContentView _schedulePage;
    private readonly ContentView _shoppingPage;

    private View _currentView;
    private int _currentIndex = 0;
    private bool _isAnimating = false;

    public static Action<int> SwitchTabAction;

    public MainPage(MainViewModel homeVm, LibraryViewModel libVm, WeeklyScheduleViewModel schedVm, ShoppingListViewModel shopVm)
    {
        InitializeComponent();

        _homePage = new HomePage(homeVm);
        _libraryPage = new LibraryPage(libVm);
        _schedulePage = new SchedulePage(schedVm);
        _shoppingPage = new ShoppingListPage(shopVm);

        BottomBar.Items = new List<AnimatedBarItem>
        {
            new() { Title = "בית", IconSource = "home_icon.svg", SelectedIconSource = "home_clicked_icon.svg" },
            new() { Title = "ספרייה", IconSource = "recipe_icon.svg", SelectedIconSource = "recipe_clicked_icon.svg" },
            new() { Title = "לוז", IconSource = "calendar_icon.svg", SelectedIconSource = "calendar_clicked_icon.svg" },
            new() { Title = "קניות", IconSource = "shopping_icon.svg", SelectedIconSource = "shopping_clicked_icon.svg" }
        };

        _currentView = _homePage;
        ContentHost.Children.Add(_currentView);

        SwitchTabAction = async (index) =>
        {

            BottomBar.SelectTab(index);

            await SwitchToTab(index);
        };


    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.Delay(500);

        var pagesToPreload = new View[] { _libraryPage, _schedulePage, _shoppingPage };
        foreach (var page in pagesToPreload)
        {
            if (!ContentHost.Children.Contains(page))
            {
                page.TranslationX = 5000;
                ContentHost.Children.Add(page);
                await Task.Delay(50);
            }
        }
    }

    private void OnTabSelected(object sender, int index)
    {
        SwitchToTab(index);
    }

    private async Task SwitchToTab(int newIndex)
    {
        if (newIndex == _currentIndex || _isAnimating) return;

        View incomingView = newIndex switch
        {
            0 => _homePage,
            1 => _libraryPage,
            2 => _schedulePage,
            3 => _shoppingPage,
            _ => _homePage
        };

        _isAnimating = true;

        double screenWidth = this.Width;
        bool isMovingLeft = newIndex > _currentIndex;

        double startX = isMovingLeft ? -screenWidth : screenWidth;
        double endX = isMovingLeft ? screenWidth : -screenWidth;

        if (!ContentHost.Children.Contains(incomingView))
        {
            incomingView.TranslationX = startX;
            ContentHost.Children.Add(incomingView);
            await Task.Delay(20);
        }
        else
        {
            incomingView.TranslationX = startX;
        }

        var outgoingView = _currentView;

        await Task.WhenAll(
            outgoingView.TranslateTo(endX, 0, 500, Easing.CubicInOut),
            incomingView.TranslateTo(0, 0, 500, Easing.CubicInOut)
        );

        _currentView = incomingView;
        _currentIndex = newIndex;
        _isAnimating = false;

    }

    protected override bool OnBackButtonPressed()
    {

        if (_currentIndex == 0 && _homePage is Recipe_book.Views.Pages.HomePage home)
        {
            if (home.HandleBackPressed())
            {
                return true;
            }
        }

        return base.OnBackButtonPressed();
    }
}