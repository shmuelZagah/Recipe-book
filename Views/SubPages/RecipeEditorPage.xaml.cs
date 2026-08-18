using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Recipe_book.Models.Recipes;
using Recipe_book.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

#if ANDROID
using Android.Views.InputMethods;
using Android.Widget;
#endif

namespace Recipe_book.Views.SubPages;

public partial class RecipeEditorPage : ContentPage
{
    private bool _isAddingNewRow = false;
    private bool _forceKeyboard = false;
    private Dictionary<object, Entry> _activeEntries = new();

    private bool _isWarmingUp = false;
    private bool _hasWarmedUp = false;

    public RecipeEditorPage(RecipeEditorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        vm.PropertyChanged += Vm_PropertyChanged;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0 && !_hasWarmedUp)
        {
            // Notice the minus sign: Position the steps list off-screen to the LEFT
            StepsSection.TranslationX = -width;

            CarouselContainer.MinimumHeightRequest = Math.Max(0, height - 150);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasWarmedUp && BindingContext is RecipeEditorViewModel vm)
        {
            _isWarmingUp = true;
            await Task.Delay(200);

            await LoadingCover.FadeTo(0, 250);
            LoadingCover.IsVisible = false;

            _isWarmingUp = false;
            _hasWarmedUp = true;
        }
    }

    private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeEditorViewModel.IsIngredientsMode) &&
            BindingContext is RecipeEditorViewModel vm)
        {
            OnModeChanged(vm.IsIngredientsMode);
        }
    }

    #region Carousel Transition Logic

    private async void OnModeChanged(bool isIngredients)
    {
        if (BindingContext is not RecipeEditorViewModel vm) return;

        if (!_isWarmingUp)
        {
            HideKeyboard();
        }

        double screenWidth = this.Width;

        if (isIngredients)
        {
            IngredientsSection.InputTransparent = false;
            StepsSection.InputTransparent = true;

            // Make sure ingredients start from the right before sliding in
            IngredientsSection.TranslationX = screenWidth;

            // Steps slide out to LEFT (-screenWidth), Ingredients slide in from RIGHT to center (0)
            Task t1 = StepsSection.TranslateTo(-screenWidth, 0, 300, Easing.CubicOut);
            Task t2 = IngredientsSection.TranslateTo(0, 0, 300, Easing.CubicOut);
            Task t3 = StepsSection.FadeTo(0, 200);
            Task t4 = IngredientsSection.FadeTo(1, 200);

            vm.ShowIngredientsCommand?.Execute(null);
            await Task.WhenAll(t1, t2, t3, t4);
        }
        else
        {
            StepsSection.InputTransparent = false;
            IngredientsSection.InputTransparent = true;

            // Make sure steps start from the left before sliding in
            StepsSection.TranslationX = -screenWidth;

            // Ingredients slide out to RIGHT (screenWidth), Steps slide in from LEFT to center (0)
            Task t1 = IngredientsSection.TranslateTo(screenWidth, 0, 300, Easing.CubicOut);
            Task t2 = StepsSection.TranslateTo(0, 0, 300, Easing.CubicOut);
            Task t3 = IngredientsSection.FadeTo(0, 200);
            Task t4 = StepsSection.FadeTo(1, 200);

            vm.ShowStepsCommand?.Execute(null);
            await Task.WhenAll(t1, t2, t3, t4);
        }
    }

    #endregion

    #region Floating Description Animation
    private void OnDescriptionLoaded(object sender, EventArgs e)
    {
        if (sender is Editor editor && editor.Parent?.Parent is Grid parentGrid)
        {
            var pill = parentGrid.Children.OfType<Border>().FirstOrDefault(b => b.ClassId == "TitlePill");
            if (pill != null && BindingContext is RecipeEditorViewModel vm)
            {
                if (!string.IsNullOrWhiteSpace(vm.RecipeDescription))
                {
                    pill.Opacity = 1;
                    pill.TranslationY = -12;
                    editor.Placeholder = "";
                }
            }
        }
    }



    private void OnDescriptionFocused(object sender, FocusEventArgs e)
    {
        if (sender is Editor editor && editor.Parent?.Parent is Grid parentGrid)
        {
            var pill = parentGrid.Children.OfType<Border>().FirstOrDefault(b => b.ClassId == "TitlePill");
            if (pill != null)
            {
                pill.FadeTo(1, 200, Easing.CubicOut);
                pill.TranslateTo(pill.TranslationX, -12, 200, Easing.CubicOut);
            }
            editor.Placeholder = "";
        }
    }

    private void OnDescriptionUnfocused(object sender, FocusEventArgs e)
    {
        if (sender is Editor editor && editor.Parent?.Parent is Grid parentGrid)
        {
            var pill = parentGrid.Children.OfType<Border>().FirstOrDefault(b => b.ClassId == "TitlePill");
            if (pill != null && string.IsNullOrWhiteSpace(editor.Text))
            {
                pill.FadeTo(0, 200, Easing.CubicIn);
                pill.TranslateTo(pill.TranslationX, 0, 200, Easing.CubicIn);
                editor.Placeholder = "ספר קצת על המתכון (תיאור)..."; // Not translated as it's a UI string
            }
        }
    }
    #endregion

    #region Smart Focus & Navigation

    private void HideKeyboard()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            this.Unfocus();

#if ANDROID
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var windowToken = activity?.CurrentFocus?.WindowToken ?? activity?.Window?.DecorView?.WindowToken;

            if (windowToken != null)
            {
                activity?.CurrentFocus?.ClearFocus();
                var imm = (InputMethodManager)activity.GetSystemService(Android.Content.Context.InputMethodService);
                imm?.HideSoftInputFromWindow(windowToken, HideSoftInputFlags.None);
            }
#endif
        });
    }

    private void OnFooterAddIngredientTapped(object sender, TappedEventArgs e)
    {
        _isAddingNewRow = true;
        _forceKeyboard = true;
        if (BindingContext is RecipeEditorViewModel vm) vm.AddIngredientCommand.Execute(null);
    }

    private void OnFooterAddStepTapped(object sender, TappedEventArgs e)
    {
        _isAddingNewRow = true;
        _forceKeyboard = true;
        if (BindingContext is RecipeEditorViewModel vm) vm.AddStepCommand.Execute(null);
    }
    #endregion

    #region Row Flow & Native Keyboard Control

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void FocusNatively(VisualElement element, bool forceKeyboard)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
#if ANDROID
            if (element.Handler?.PlatformView is EditText native)
            {
                native.RequestFocus();

                if (forceKeyboard)
                {
                    var imm = (InputMethodManager)native.Context.GetSystemService(Android.Content.Context.InputMethodService);

                    int attempts = 0;
                    while (!native.HasWindowFocus && attempts < 20)
                    {
                        await Task.Delay(10);
                        attempts++;
                    }

                    imm?.ShowSoftInput(native, ShowFlags.Implicit);
                }
                return;
            }
#endif
            element.Focus();
        });
    }

    private void OnQuantityCompleted(object sender, EventArgs e)
    {
        if (sender is Entry entry && entry.Parent is Grid grid)
        {
            var picker = grid.Children.OfType<Picker>().FirstOrDefault();
            picker?.Focus();
        }
    }

    private void OnUnitChanged(object sender, EventArgs e)
    {
        if (_isAddingNewRow) return;

        if (sender is Picker picker && picker.Parent is Grid grid)
        {
            var quantityEntry = grid.Children.OfType<Entry>().FirstOrDefault(c => Grid.GetColumn(c) == 0);
            var nameEntry = grid.Children.OfType<Entry>().FirstOrDefault(c => Grid.GetColumn(c) == 2);

            if (nameEntry != null)
            {
                if (string.IsNullOrEmpty(nameEntry.Text) && string.IsNullOrEmpty(quantityEntry?.Text)) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FocusNatively(nameEntry, forceKeyboard: true);
                });
            }
        }
    }

    private void OnNameCompleted(object sender, EventArgs e)
    {
        _isAddingNewRow = true;
        _forceKeyboard = false;
        if (BindingContext is RecipeEditorViewModel vm) vm.AddIngredientCommand.Execute(null);
    }

    private void OnStepCompleted(object sender, EventArgs e)
    {
        _isAddingNewRow = true;
        _forceKeyboard = true;
        if (BindingContext is RecipeEditorViewModel vm) vm.AddStepCommand.Execute(null);
    }

    private void OnSeamlessEntryLoaded(object sender, EventArgs e)
    {
        if (sender is Entry entry)
        {
#if ANDROID
            if (entry.Handler?.PlatformView is EditText editText)
            {
                editText.SetOnEditorActionListener(new CustomAndroidEditorActionListener(() =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _isAddingNewRow = true;

                        if (BindingContext is RecipeEditorViewModel vm)
                        {
                            if (entry.BindingContext is Ingredient)
                            {
                                _forceKeyboard = false;
                                vm.AddIngredientCommand.Execute(null);
                            }
                            else if (entry.BindingContext is RecipeStep step)
                            {
                                _forceKeyboard = true;
                                vm.AddStepCommand.Execute(null);
                            }
                        }
                    });
                }));
            }
#endif
        }
    }

    private void OnQuantityLoaded(object sender, EventArgs e)
    {
        if (sender is Entry entry && entry.BindingContext is Ingredient ingredient)
        {
            _activeEntries[ingredient] = entry;

            if (_isAddingNewRow && BindingContext is RecipeEditorViewModel vm)
            {
                if (ingredient == vm.IngredientsList.LastOrDefault())
                {
                    _isAddingNewRow = false;
                    FocusNatively(entry, _forceKeyboard);
                }
            }
        }
    }

    private void OnStepDescriptionLoaded(object sender, EventArgs e)
    {
        OnSeamlessEntryLoaded(sender, e);

        if (sender is Entry entry && entry.BindingContext is RecipeStep step)
        {
            _activeEntries[step] = entry;

            if (_isAddingNewRow && BindingContext is RecipeEditorViewModel vm)
            {
                if (step == vm.StepsList.LastOrDefault())
                {
                    _isAddingNewRow = false;
                    FocusNatively(entry, _forceKeyboard);
                }
            }
        }
    }
    #endregion
}

#if ANDROID
public class CustomAndroidEditorActionListener : Java.Lang.Object, TextView.IOnEditorActionListener
{
    private readonly Action _onEnter;

    public CustomAndroidEditorActionListener(Action onEnter)
    {
        _onEnter = onEnter;
    }

    public bool OnEditorAction(TextView v, ImeAction actionId, Android.Views.KeyEvent e)
    {
        if (actionId == ImeAction.Next || actionId == ImeAction.Done ||
           (e != null && e.KeyCode == Android.Views.Keycode.Enter && e.Action == Android.Views.KeyEventActions.Down))
        {
            _onEnter?.Invoke();
            return true;
        }
        return false;
    }
}
#endif