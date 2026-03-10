using Microsoft.Maui.Controls;
using Recipe_book.ViewModels;
using System.ComponentModel;

namespace Recipe_book.Views.SubPages;

public partial class RecipeViewerPage : ContentPage
{
    private readonly RecipeViewerViewModel _vm;


    private double _ingInnerY = 0;
    private double _stepsInnerY = 0;

    private bool _isProgrammaticScroll = false;

    public RecipeViewerPage(RecipeViewerViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        OuterScroll.SizeChanged += (s, e) => UpdateInnerHeights();
        TabsSection.SizeChanged += (s, e) => UpdateInnerHeights();

        await _vm.RefreshRecipeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
    }


    private void UpdateInnerHeights()
    {
        if (OuterScroll.Height <= 0 || TabsSection.Height <= 0)
            return;


        var innerHeight = OuterScroll.Height - TabsSection.Height;

        if (innerHeight > 0)
        {
            IngredientsInnerScroll.HeightRequest = innerHeight;
            StepsInnerScroll.HeightRequest = innerHeight;
        }
    }

 
    private void OnInnerScroll(object sender, ScrolledEventArgs e)
    {
        if (_isProgrammaticScroll) return;

        if (sender == IngredientsInnerScroll && IngredientsInnerScroll.IsVisible)
            _ingInnerY = e.ScrollY;

        if (sender == StepsInnerScroll && StepsInnerScroll.IsVisible)
            _stepsInnerY = e.ScrollY;
    }


    private async void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RecipeViewerViewModel.IsIngredientsMode))
            return;

        var targetScroll = _vm.IsIngredientsMode ? IngredientsInnerScroll : StepsInnerScroll;
        var targetY = _vm.IsIngredientsMode ? _ingInnerY : _stepsInnerY;


        _isProgrammaticScroll = true;
        await targetScroll.ScrollToAsync(0, targetY, false);
        _isProgrammaticScroll = false;
    }


    private void OnOuterScroll(object sender, ScrolledEventArgs e)
    {

    }

}