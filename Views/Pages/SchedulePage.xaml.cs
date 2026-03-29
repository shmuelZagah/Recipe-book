using Recipe_book.ViewModels;

namespace Recipe_book.Views.Pages;

public partial class SchedulePage : ContentView
{
    private readonly WeeklyScheduleViewModel _viewModel;

    public SchedulePage(WeeklyScheduleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // טעינת הנתונים דרך Loaded
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
}