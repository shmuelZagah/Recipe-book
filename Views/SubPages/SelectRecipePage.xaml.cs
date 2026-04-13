using Recipe_book.ViewModels;

namespace Recipe_book.Views.SubPages;

public partial class SelectRecipePage : ContentPage
{
    private readonly SelectRecipeViewModel _viewModel;

    public SelectRecipePage(SelectRecipeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataAsync();
    }
}