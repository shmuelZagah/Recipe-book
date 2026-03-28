using Microsoft.Maui.Controls;

namespace Recipe_book.Views.Pages;

public partial class AppContainerPage : ContentPage
{
    private readonly ContentView _homeView;
    private readonly ContentView _libraryView;
    private readonly ContentView _scheduleView;
    private readonly ContentView _shoppingView;

    public AppContainerPage()
    {
        InitializeComponent();
    }

    //public AppContainerPage(
    //    MainPage homeView,
    //    LibraryPage libraryView,
    //    SchedulePage scheduleView,
    //    ShoppingListPage shoppingView)
    //{
    //    InitializeComponent();

    //    _homeView = homeView;
    //    _libraryView = libraryView;
    //    _scheduleView = scheduleView;
    //    _shoppingView = shoppingView;

    //    ViewsContainer.Children.Add(_homeView);
    //    ViewsContainer.Children.Add(_libraryView);
    //    ViewsContainer.Children.Add(_scheduleView);
    //    ViewsContainer.Children.Add(_shoppingView);

  
    //    _homeView.IsVisible = true;
    //    _libraryView.IsVisible = false;
    //    _scheduleView.IsVisible = false;
    //    _shoppingView.IsVisible = false;


    //    BottomNav.PropertyChanged += OnBottomNavPropertyChanged;
    //}

    //private void OnBottomNavPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    //{
    //    if (e.PropertyName == nameof(BottomNav.ActiveTab))
    //    {
    //        _homeView.IsVisible = BottomNav.ActiveTab == 0;
    //        _libraryView.IsVisible = BottomNav.ActiveTab == 1;
    //        _scheduleView.IsVisible = BottomNav.ActiveTab == 2;
    //        _shoppingView.IsVisible = BottomNav.ActiveTab == 3;
    //    }

    //    switch (BottomNav.ActiveTab)
    //    {
    //        case 0: (_homeView as MainPage)?.OnViewAppearing(); break;
    //        case 1: (_libraryView as LibraryPage)?.OnViewAppearing(); break;
    //        case 2: (_scheduleView as SchedulePage)?.OnViewAppearing(); break;
    //        case 3: (_shoppingView as ShoppingListPage)?.OnViewAppearing(); break;
    //    }
    //}
}