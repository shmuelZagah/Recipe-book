using Microsoft.Maui.Controls;
using Recipe_book.Models.Recipes;
using Recipe_book.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

#if ANDROID
using Android.Views.InputMethods;
using Android.Widget;
#endif

namespace Recipe_book.Views.SubPages;

public partial class RecipeEditorPage : ContentPage
{
    private bool _isAddingNewRow = false;
    private bool _focusPendingIngredientTab = false;
    private bool _focusPendingStepTab = false;
    private bool _isIngredientsAnimatorDisabled = false;
    private bool _isStepsAnimatorDisabled = false;
    private bool _forceKeyboard = false;
    private Dictionary<object, Entry> _activeEntries = new();

    // משתנים למנגנון חימום המנועים (Pre-warming)
    private bool _isWarmingUp = false;
    private bool _hasWarmedUp = false;

    public RecipeEditorPage(RecipeEditorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        vm.PropertyChanged += Vm_PropertyChanged;
    }

    // הפעלת החימום המזוייף ברגע שהעמוד עולה
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasWarmedUp && BindingContext is RecipeEditorViewModel vm)
        {
            _isWarmingUp = true;

            // 1. מעבירים לשלבים כדי ש-MAUI יקמפל את הרשימה השנייה
            vm.IsIngredientsMode = false;
            await Task.Delay(120); // נותנים לו זמן לצייר

            // 2. מחזירים למצרכים (המצב הדיפולטי)
            vm.IsIngredientsMode = true;
            await Task.Delay(120);

            // 3. מעלימים את הוילון ברכות - המשתמש לא הרגיש כלום!
            await LoadingCover.FadeTo(0, 200);
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

    #region CollectionView Native Animator Kill

    private bool TryDisableAnimation(CollectionView cv, string logTag)
    {
        bool success = false;
#if ANDROID
        if (cv?.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView rv)
        {
            rv.SetItemAnimator(null);
            success = true;
            Debug.WriteLine($"[StepDebug] {logTag}: Android ItemAnimator disabled successfully.");
        }
#endif
        return success;
    }

    private void OnCollectionViewLoaded(object sender, EventArgs e)
    {
        if (sender is CollectionView cv)
        {
            if (cv == IngredientsCollection)
                _isIngredientsAnimatorDisabled = TryDisableAnimation(cv, "Ingredients_Loaded");
            else if (cv == StepsCollection)
                _isStepsAnimatorDisabled = TryDisableAnimation(cv, "Steps_Loaded");
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
                editor.Placeholder = "ספר קצת על המתכון (תיאור)...";
            }
        }
    }
    #endregion

    #region Smart Focus & Navigation (Sliding Toggle)

    private void OnModeChanged(bool isIngredients)
    {
        if (BindingContext is not RecipeEditorViewModel vm) return;

        if (isIngredients)
        {
            vm.ShowIngredientsCommand?.Execute(null);

            // חסימה: אם אנחנו בחימום מנועים, לא מקפיצים מקלדת!
            if (_isWarmingUp) return;

            var lastItem = vm.IngredientsList?.LastOrDefault();
            if (lastItem != null && _activeEntries.TryGetValue(lastItem, out var entry))
            {
                FocusNatively(entry, forceKeyboard: true, "IngredientsToggle");
            }
        }
        else
        {
            vm.ShowStepsCommand?.Execute(null);

            // חסימה: אם אנחנו בחימום מנועים, לא מקפיצים מקלדת!
            if (_isWarmingUp) return;

            var lastItem = vm.StepsList?.LastOrDefault();
            if (lastItem != null && _activeEntries.TryGetValue(lastItem, out var entry))
            {
                FocusNatively(entry, forceKeyboard: true, "StepsToggle");
            }
        }
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

    private void FocusNatively(VisualElement element, bool forceKeyboard, string logContext = "")
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
#if ANDROID
            if (element.Handler?.PlatformView is Android.Widget.EditText native)
            {
                native.RequestFocus();

                if (forceKeyboard)
                {
                    var imm = (Android.Views.InputMethods.InputMethodManager)native.Context.GetSystemService(Android.Content.Context.InputMethodService);

                    int attempts = 0;
                    while (!native.HasWindowFocus && attempts < 20)
                    {
                        await Task.Delay(10);
                        attempts++;
                    }

                    imm?.ShowSoftInput(native, Android.Views.InputMethods.ShowFlags.Implicit);
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
                    FocusNatively(nameEntry, forceKeyboard: true, "OnUnitChanged");
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

            if ((_isAddingNewRow || _focusPendingIngredientTab) && BindingContext is RecipeEditorViewModel vm)
            {
                if (ingredient == vm.IngredientsList.LastOrDefault())
                {
                    _isAddingNewRow = false;
                    _focusPendingIngredientTab = false;

                    FocusNatively(entry, _forceKeyboard, "OnQuantityLoaded");
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

            if ((_isAddingNewRow || _focusPendingStepTab) && BindingContext is RecipeEditorViewModel vm)
            {
                if (step == vm.StepsList.LastOrDefault())
                {
                    _isAddingNewRow = false;
                    _focusPendingStepTab = false;

                    FocusNatively(entry, _forceKeyboard, "OnStepDescriptionLoaded");
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