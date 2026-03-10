using Recipe_book.ViewModels;

namespace Recipe_book.Views.Pages; // או התיקייה שבה המסך נמצא

public partial class ShoppingListPage : ContentPage
{
    public ShoppingListPage(ShoppingListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel; // מחבר את ה"מוח" למסך
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // ברגע שהמסך מופיע, הוא טוען את הרשימה אוטומטית לפי ההעדפה השמורה!
        if (BindingContext is ShoppingListViewModel vm)
        {
            await vm.InitializeAutoLoadAsync();
        }
    }
}