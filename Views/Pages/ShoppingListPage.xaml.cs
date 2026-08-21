using System.ComponentModel;
using Recipe_book.ViewModels;
using Recipe_book.Views.Layouts;

namespace Recipe_book.Views.Pages;

/// <summary>
/// Code-behind for the ShoppingListPage.
/// Implements ISwipeAwarePage to enforce main page navigation for any horizontal swipe.
/// </summary>
public partial class ShoppingListPage : ContentView, ISwipeAwarePage
{
    #region Constructor & Initialization
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
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;

            await vm.InitializeAutoLoadAsync();
        }
    }

    // Handles the caret icon rotation only
    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShoppingListViewModel.IsListsMenuOpen))
        {
            if (BindingContext is ShoppingListViewModel vm)
            {
                var caretIcon = this.FindByName<Image>("CaretIcon");
                if (caretIcon != null)
                {
                    if (vm.IsListsMenuOpen)
                        caretIcon.RotateTo(180, 250, Easing.CubicOut);
                    else
                        caretIcon.RotateTo(0, 250, Easing.CubicIn);
                }
            }
        }
    }

    private void FreeTextInputEntry_Completed(object sender, EventArgs e)
    {
        FreeTextInputEntry.Unfocus();
    }
    #endregion

    #region ISwipeAwarePage Implementation
    public SwipeAction GetSwipeAction(double totalX, double startX, double startY)
    {
        return SwipeAction.MainPageSwipe;
    }

    public void StartInnerSwipe() { }
    public void RunningInnerSwipe(double deltaX) { }
    public void CompletedInnerSwipe(double deltaX, double screenWidth) { }
    #endregion
}