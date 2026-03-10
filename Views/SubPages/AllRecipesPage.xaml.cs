using Recipe_book.ViewModels;

namespace Recipe_book.Views.SubPages;

public partial class AllRecipesPage : ContentPage
{
    private readonly AllRecipesViewModel _viewModel;

    public AllRecipesPage(AllRecipesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadRecipesCommand.Execute(null);
    }
}