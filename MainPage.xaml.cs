using Recipe_book.ViewModels;


namespace Recipe_book;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _vm.LoadRecipesCommand.ExecuteAsync(null);

        await Task.Delay(150);

        double fullWidth = MealsScrollView.ContentSize.Width;
        await MealsScrollView.ScrollToAsync(fullWidth, 0, animated: false);
    }

}
