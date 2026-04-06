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
            await vm.InitializeAutoLoadAsync();
        }
    }
    #endregion

    #region ISwipeAwarePage Implementation
    public SwipeAction GetSwipeAction(double totalX, double startX, double startY)
    {
        // Shopping list only requires vertical scrolling. 
        // Horizontal swipes are captured by the main gesture router.
        return SwipeAction.MainPageSwipe;
    }

    public void StartInnerSwipe() { }
    public void RunningInnerSwipe(double deltaX) { }
    public void CompletedInnerSwipe(double deltaX, double screenWidth) { }
    #endregion
}