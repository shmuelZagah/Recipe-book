using System.ComponentModel;
using Microsoft.Maui.Controls;
using Recipe_book.ViewModels;

namespace Recipe_book.Views.Items;

public partial class ShoppingListsBottomSheet : ContentView
{
    public ShoppingListsBottomSheet()
    {
        InitializeComponent();
        this.BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object sender, EventArgs e)
    {
        if (BindingContext is ShoppingListViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private async void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShoppingListViewModel.IsListsMenuOpen) && BindingContext is ShoppingListViewModel vm)
        {
            if (vm.IsListsMenuOpen)
                await OpenSheetAsync();
            else
                await CloseSheetAsync();
        }
    }

    private async Task OpenSheetAsync()
    {
        BottomSheetOverlay.IsVisible = true;

        // Ensure starting position is below viewable area
        double screenHeight = Application.Current?.MainPage?.Height ?? 800;
        BottomSheetContainer.TranslationY = screenHeight + 100;

        // Dim background fades in parallel
        _ = BottomSheetDim.FadeTo(1, 400, Easing.Linear);

        // Elegant spring-out animation for a premium "landing" feel (550ms)
        await BottomSheetContainer.TranslateTo(BottomSheetContainer.TranslationX, 0, 550, Easing.SpringOut);
    }

    private async Task CloseSheetAsync()
    {
        double targetHeight = BottomSheetContainer.Height > 0 ? BottomSheetContainer.Height : 800;

        _ = BottomSheetDim.FadeTo(0, 300, Easing.Linear);
        await BottomSheetContainer.TranslateTo(BottomSheetContainer.TranslationX, targetHeight, 400, Easing.CubicInOut);

        BottomSheetOverlay.IsVisible = false;
    }

    private void OnCloseTapped(object sender, EventArgs e)
    {
        if (BindingContext is ShoppingListViewModel vm)
            vm.ToggleListsMenuCommand.Execute(null);
    }

    private void OnBottomSheetPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (BindingContext is ShoppingListViewModel vm && vm.IsListsMenuOpen)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Running:
                    if (e.TotalY > 0)
                        BottomSheetContainer.TranslationY = e.TotalY;
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    // Close if dragged more than 30% of height
                    if (BottomSheetContainer.TranslationY > (BottomSheetContainer.Height * 0.3))
                        vm.ToggleListsMenuCommand.Execute(null);
                    else
                        BottomSheetContainer.TranslateTo(BottomSheetContainer.TranslationX, 0, 250, Easing.CubicOut);
                    break;
            }
        }
    }
}