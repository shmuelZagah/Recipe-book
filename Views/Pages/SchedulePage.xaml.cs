using Recipe_book.ViewModels;
using Recipe_book.Views.Layouts;

namespace Recipe_book.Views.Pages;

/// <summary>
/// Code-behind for the SchedulePage.
/// Implements ISwipeAwarePage to allow native horizontal scrolling on the days collection.
/// </summary>
public partial class SchedulePage : ContentView, ISwipeAwarePage
{
    #region Fields
    private readonly WeeklyScheduleViewModel _viewModel;
    #endregion

    #region Constructor & Initialization
    public SchedulePage(WeeklyScheduleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        this.Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, EventArgs e)
    {
        await _viewModel.LoadScheduleAsync();
        ScrollToToday();
    }
    #endregion

    #region Helper Methods
    // Centered scrolling helper to focus on today's card element
    private void ScrollToToday()
    {
        var todayItem = _viewModel.WeekDays.FirstOrDefault(d => d.Date.Date == DateTime.Today);
        if (todayItem != null)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(150); // Small layout measurement safety boundary
                DaysCollectionView.ScrollTo(todayItem, position: ScrollToPosition.Center, animate: true);
            });
        }
    }

    // Handles quick "Today" shortcut navigation triggers cleanly
    //To Fix : the bord and the hand use list dont comunicte properly
    private void OnGoToTodayClicked(object sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.SelectedDatePickerDate = DateTime.Today;
            ScrollToToday();
        }
    }
    #endregion

    #region ISwipeAwarePage Implementation
    public SwipeAction GetSwipeAction(double totalX, double startX, double startY)
    {
        double daysTop = DaysCollectionView.Y;
        double daysBottom = DaysCollectionView.Y + DaysCollectionView.Height + 50;

        if (startY >= daysTop && startY <= daysBottom)
        {
            return SwipeAction.NativeChildScroll;
        }

        return SwipeAction.MainPageSwipe;
    }


    public void StartInnerSwipe() { }
    public void RunningInnerSwipe(double deltaX) { }
    public void CompletedInnerSwipe(double deltaX, double screenWidth) { }
    #endregion
}