using Recipe_book.ViewModels;

namespace Recipe_book.Views.SubPages;

public partial class ManageMealsPage : ContentPage
{
    public ManageMealsPage(ManageMealsViewModel viewModel)
    {
        InitializeComponent();

        // השורה הכי חשובה - מחברת את העיצוב לנתונים!
        BindingContext = viewModel;
    }
}