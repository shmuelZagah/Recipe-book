using Recipe_book.ViewModels;

namespace Recipe_book.Views.Pages;

public partial class HomePage : ContentView
{
    private readonly MainViewModel _vm;

    public HomePage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        this.Loaded += OnHomePageLoaded;
    }

    private async void OnHomePageLoaded(object sender, EventArgs e)
    {
        await _vm.LoadRecipesCommand.ExecuteAsync(null);

        await Task.Delay(150);

        double fullWidth = MealsScrollView.ContentSize.Width;
        await MealsScrollView.ScrollToAsync(fullWidth, 0, animated: false);
    }
}