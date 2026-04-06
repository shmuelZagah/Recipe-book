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

        var todayItem = _viewModel.WeekDays.FirstOrDefault(d => d.Date.Date == DateTime.Today);

        if (todayItem != null)
        {
            await Task.Delay(100);
            DaysCollectionView.ScrollTo(todayItem, position: ScrollToPosition.Start, animate: true);
        }
    }
    #endregion

    #region ISwipeAwarePage Implementation
    public SwipeAction GetSwipeAction(double totalX, double startX, double startY)
    {
        // Hit testing: Check if the swipe originated on the horizontal Days bar
        double daysTop = DaysCollectionView.Y;
        double daysBottom = DaysCollectionView.Y + DaysCollectionView.Height;

        if (startY >= daysTop && startY <= daysBottom)
        {
            return SwipeAction.NativeChildScroll;
        }

        // Swiping anywhere else triggers main app tabs navigation
        return SwipeAction.MainPageSwipe;
    }

    public void StartInnerSwipe() { }
    public void RunningInnerSwipe(double deltaX) { }
    public void CompletedInnerSwipe(double deltaX, double screenWidth) { }
    #endregion
}