using Recipe_book.ViewModels;

namespace Recipe_book.Views.SubPages;

public partial class RecipeEditorPage : ContentPage
{
	public RecipeEditorPage(RecipeEditorViewModel vm)
	{
		InitializeComponent();
        BindingContext = vm;
    }
}