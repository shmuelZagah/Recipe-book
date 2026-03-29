using Recipe_book.ViewModels;

namespace Recipe_book.Views.Pages;

public partial class ShoppingListPage : ContentView
{
    public ShoppingListPage(ShoppingListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        this.Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, EventArgs e)
    {
        if (BindingContext is ShoppingListViewModel vm)
        {
            await vm.InitializeAutoLoadAsync();
        }
    }
}