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


        TestBottomBar.Items = new List<Recipe_book.Views.Items.AnimatedBottomBarItem>
        {
            new() { Title = "Home", IconSource = "home_icon.svg" , SelectedIconSource = "home_clicked_icon.svg"},
            new() { Title = "Library", IconSource = "recipe_icon.svg" , SelectedIconSource = "recipe_clicked_icon.svg"},
            new() { Title = "Schedule", IconSource = "calendar_icon.svg" , SelectedIconSource = "calendar_clicked_icon.svg"},
            new() { Title = "Shopping", IconSource = "shopping_icon.svg", SelectedIconSource = "shopping_clicked_icon.svg" }
        };
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
