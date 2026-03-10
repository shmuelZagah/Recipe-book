using Recipe_book.ViewModels;

namespace Recipe_book.Views.SubPages;

public partial class FolderSelectionPage : ContentPage
{
    public FolderSelectionPage(FolderSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}