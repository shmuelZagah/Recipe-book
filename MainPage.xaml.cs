using Microsoft.Maui.Controls;
using Recipe_book.ViewModels;
using Recipe_book.Views.Layouts;
using Recipe_book.Views.Items.bars;
using Recipe_book.Views.Pages;

namespace Recipe_book;

public partial class MainPage : ContentPage
{
    #region Fields
    private readonly ContentView _homePage;
    private readonly ContentView _libraryPage;
    private readonly ContentView _schedulePage;
    private readonly ContentView _shoppingPage;

    private int _currentIndex = 0;
    private bool _isAnimating = false;
    private bool _isSetup = false;

    private double _currentGlobalOffset = 0;
    private double _panStartOffset;
    private double _swipeStartXOffset;
    private DateTime _swipeStartTime;

    private ISwipeAwarePage _activeInnerPage = null;
    private SwipeAction _currentSwipeAction = SwipeAction.MainPageSwipe;

    public static Action<int> SwitchTabAction;
    #endregion

    #region Constructor & Initialization
    public MainPage(MainViewModel homeVm, LibraryViewModel libVm, WeeklyScheduleViewModel schedVm, ShoppingListViewModel shopVm)
    {
        InitializeComponent();

        _homePage = new HomePage(homeVm) { FlowDirection = FlowDirection.RightToLeft };
        _libraryPage = new LibraryPage(libVm) { FlowDirection = FlowDirection.RightToLeft };
        _schedulePage = new SchedulePage(schedVm) { FlowDirection = FlowDirection.RightToLeft };
        _shoppingPage = new ShoppingListPage(shopVm) { FlowDirection = FlowDirection.RightToLeft };

        PagesContainer.Children.Add(_shoppingPage);
        PagesContainer.Children.Add(_schedulePage);
        PagesContainer.Children.Add(_libraryPage);
        PagesContainer.Children.Add(_homePage);

        BottomBar.Items = new List<AnimatedBarItem>
        {
            new() { Title = "בית",    IconSource = "home_icon.svg",     SelectedIconSource = "home_clicked_icon.svg"     },
            new() { Title = "ספרייה", IconSource = "recipe_icon.svg",   SelectedIconSource = "recipe_clicked_icon.svg"   },
            new() { Title = "לוז",    IconSource = "calendar_icon.svg", SelectedIconSource = "calendar_clicked_icon.svg" },
            new() { Title = "קניות",  IconSource = "shopping_icon.svg", SelectedIconSource = "shopping_clicked_icon.svg" }
        };

        SwitchTabAction = async (index) =>
        {
            BottomBar.UpdateFromSwipe(index);
            await SwitchToTab(index);
        };

        // Attach binding context to the isolated bottom sheet control
        GlobalBottomSheet.BindingContext = shopVm;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        SetupSwipeLayer();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0 && height > 0)
        {
            if (!_isSetup || !_isAnimating)
            {
                _isSetup = true;
                _currentGlobalOffset = GetTargetOffsetForIndex(_currentIndex, width);
                ApplyTranslations(_currentGlobalOffset, width);
            }
        }
    }
    #endregion

    #region Navigation & Layout
    private void ApplyTranslations(double globalOffset, double width)
    {
        _shoppingPage.TranslationX = 0 + globalOffset;
        _schedulePage.TranslationX = width + globalOffset;
        _libraryPage.TranslationX = (width * 2) + globalOffset;
        _homePage.TranslationX = (width * 3) + globalOffset;
    }

    private double GetTargetOffsetForIndex(int index, double width)
    {
        return -((3 - index) * width);
    }

    private void OnTabSelected(object sender, int index)
    {
        _ = SwitchToTab(index);
    }

    private async Task SwitchToTab(int newIndex)
    {
        if (newIndex == _currentIndex || !_isSetup) return;

        try
        {
            _shoppingPage.CancelAnimations();
            _schedulePage.CancelAnimations();
            _libraryPage.CancelAnimations();
            _homePage.CancelAnimations();
        }
        catch { }

        _isAnimating = true;
        _currentIndex = newIndex;

        double targetOffset = GetTargetOffsetForIndex(newIndex, this.Width);
        double w = this.Width;

        try
        {
            await Task.WhenAll(
                _shoppingPage.TranslateTo(0 + targetOffset, 0, 350, Easing.CubicOut),
                _schedulePage.TranslateTo(w + targetOffset, 0, 350, Easing.CubicOut),
                _libraryPage.TranslateTo(w * 2 + targetOffset, 0, 350, Easing.CubicOut),
                _homePage.TranslateTo(w * 3 + targetOffset, 0, 350, Easing.CubicOut)
            );

            _currentGlobalOffset = targetOffset;
        }
        catch { }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task ForceSnapToPage(int targetIndex)
    {
        _isAnimating = true;
        _currentIndex = targetIndex;

        BottomBar.UpdateFromSwipe(targetIndex);

        double targetOffset = GetTargetOffsetForIndex(targetIndex, this.Width);
        double w = this.Width;

        try
        {
            await Task.WhenAll(
                _shoppingPage.TranslateTo(0 + targetOffset, 0, 250, Easing.CubicOut),
                _schedulePage.TranslateTo(w + targetOffset, 0, 250, Easing.CubicOut),
                _libraryPage.TranslateTo(w * 2 + targetOffset, 0, 250, Easing.CubicOut),
                _homePage.TranslateTo(w * 3 + targetOffset, 0, 250, Easing.CubicOut)
            );
            _currentGlobalOffset = targetOffset;
        }
        catch { }
        finally { _isAnimating = false; }
    }
    #endregion

    #region Gesture Routing Core
    private void SetupSwipeLayer()
    {
        SwipeLayer.ShouldInterceptHorizontal = (totalX, startX, startY) =>
        {
            if (!_isSetup) return false;

            // Block page swiping if global bottom sheet menu is active
            if (GlobalBottomSheet.BindingContext is ShoppingListViewModel shopVm && shopVm.IsListsMenuOpen)
                return false;

            _activeInnerPage = null;
            _currentSwipeAction = SwipeAction.MainPageSwipe;

            ISwipeAwarePage currentPage = null;
            if (_currentIndex == 1 && _libraryPage is ISwipeAwarePage lib) currentPage = lib;
            else if (_currentIndex == 2 && _schedulePage is ISwipeAwarePage sched) currentPage = sched;
            else if (_currentIndex == 3 && _shoppingPage is ISwipeAwarePage shop) currentPage = shop;
            else if (_currentIndex == 0 && _homePage is ISwipeAwarePage home) currentPage = home;

            if (currentPage != null)
            {
                _currentSwipeAction = currentPage.GetSwipeAction(totalX, startX, startY);

                if (_currentSwipeAction == SwipeAction.NativeChildScroll)
                    return false;

                if (_currentSwipeAction == SwipeAction.ManualInnerSwipe)
                    _activeInnerPage = currentPage;
            }

            return true;
        };

        SwipeLayer.OnSwipeStarted = (totalX) =>
        {
            _swipeStartXOffset = totalX;
            _swipeStartTime = DateTime.Now;

            if (_currentSwipeAction == SwipeAction.ManualInnerSwipe && _activeInnerPage != null)
            {
                _activeInnerPage.StartInnerSwipe();
                return;
            }

            try
            {
                _shoppingPage.CancelAnimations();
                _schedulePage.CancelAnimations();
                _libraryPage.CancelAnimations();
                _homePage.CancelAnimations();
            }
            catch { }

            _isAnimating = false;
            _panStartOffset = _shoppingPage.TranslationX;
        };

        SwipeLayer.OnSwipeRunning = (totalX) =>
        {
            double deltaX = totalX - _swipeStartXOffset;

            if (_currentSwipeAction == SwipeAction.ManualInnerSwipe && _activeInnerPage != null)
            {
                _activeInnerPage.RunningInnerSwipe(deltaX);
                return;
            }

            double newOffset = _panStartOffset + deltaX;
            double maxRight = 0;
            double maxLeft = -3 * this.Width;

            if (newOffset > maxRight) newOffset = maxRight + (newOffset - maxRight) * 0.2;
            if (newOffset < maxLeft) newOffset = maxLeft + (newOffset - maxLeft) * 0.2;

            ApplyTranslations(newOffset, this.Width);
        };

        SwipeLayer.OnSwipeCompleted = (totalX) =>
        {
            double deltaX = totalX - _swipeStartXOffset;

            if (_currentSwipeAction == SwipeAction.ManualInnerSwipe && _activeInnerPage != null)
            {
                _activeInnerPage.CompletedInnerSwipe(deltaX, this.Width);
                _activeInnerPage = null;
                _currentSwipeAction = SwipeAction.MainPageSwipe;
                return;
            }

            double screenWidth = this.Width;
            double currentX = _shoppingPage.TranslationX;

            double exactIndex = (currentX / screenWidth) + 3;
            int magnetIndex = (int)Math.Round(exactIndex);

            TimeSpan swipeDuration = DateTime.Now - _swipeStartTime;
            if (swipeDuration.TotalMilliseconds < 250 && Math.Abs(deltaX) > screenWidth * 0.1)
            {
                if (deltaX > 0) magnetIndex = _currentIndex + 1;
                else magnetIndex = _currentIndex - 1;
            }
            else
            {
                if (deltaX > screenWidth * 0.35) magnetIndex = (int)Math.Floor(exactIndex) + 1;
                else if (deltaX < -screenWidth * 0.35) magnetIndex = (int)Math.Ceiling(exactIndex) - 1;
            }

            magnetIndex = Math.Clamp(magnetIndex, 0, 3);
            _ = ForceSnapToPage(magnetIndex);
        };
    }
    #endregion

    #region Overrides
    protected override bool OnBackButtonPressed()
    {
        if (_currentIndex == 0 && _homePage is HomePage home)
        {
            if (home.HandleBackPressed()) return true;
        }
        return base.OnBackButtonPressed();
    }
    #endregion
}