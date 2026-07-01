using Microsoft.Maui.Controls;
using Recipe_book.Models.Recipes;
using Recipe_book.ViewModels;
using System;
using System.Collections.Generic;
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
    private bool _focusPendingIngredientTab = false;
    private bool _focusPendingStepTab = false;

    // מילונים חכמים לשמירת הרפרנסים של השדות
    private Dictionary<Ingredient, Entry> _ingredientQuantityEntries = new();
    private Dictionary<RecipeStep, Entry> _stepDescriptionEntries = new();

    public RecipeEditorPage(RecipeEditorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

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

    #region Smart Focus & Navigation (Tabs and Buttons)

    private void OnIngredientsTabTapped(object sender, TappedEventArgs e)
    {
        _focusPendingIngredientTab = true;
        if (BindingContext is RecipeEditorViewModel vm)
        {
            vm.ShowIngredientsCommand.Execute(null);
        }
    }

    private void OnStepsTabTapped(object sender, TappedEventArgs e)
    {
        _focusPendingStepTab = true;
        if (BindingContext is RecipeEditorViewModel vm)
        {
            vm.ShowStepsCommand.Execute(null);
        }
    }

    private void OnFooterAddIngredientTapped(object sender, TappedEventArgs e)
    {
        _isAddingNewRow = true;
        if (BindingContext is RecipeEditorViewModel vm)
        {
            vm.AddIngredientCommand.Execute(null);
        }
    }

    private void OnFooterAddStepTapped(object sender, TappedEventArgs e)
    {
        _isAddingNewRow = true;
        if (BindingContext is RecipeEditorViewModel vm)
        {
            vm.AddStepCommand.Execute(null);
        }
    }
    #endregion

    #region Row Flow & Native Keyboard Control

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

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(50);
                    nameEntry.Focus();
                });
            }
        }
    }

    private void OnNameCompleted(object sender, EventArgs e)
    {
        _isAddingNewRow = true;
        if (BindingContext is RecipeEditorViewModel vm) vm.AddIngredientCommand.Execute(null);
    }

    private void OnStepCompleted(object sender, EventArgs e)
    {
        _isAddingNewRow = true;
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
                                vm.AddIngredientCommand.Execute(null);
                            else if (entry.BindingContext is RecipeStep)
                                vm.AddStepCommand.Execute(null);
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
            if (BindingContext is RecipeEditorViewModel vm && ingredient == vm.IngredientsList.LastOrDefault())
            {
                if (_isAddingNewRow || _focusPendingIngredientTab)
                {
                    _isAddingNewRow = false;
                    _focusPendingIngredientTab = false;

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Task.Delay(100);
                        entry.Focus();
                        IngredientsCollection.ScrollTo(ingredient, position: ScrollToPosition.MakeVisible, animate: true);
                    });
                }
            }
        }
    }

    private void OnStepDescriptionLoaded(object sender, EventArgs e)
    {
        // קוראים לפונקציה השלישית כדי להפעיל את האזנת ה-Enter לאנדרואיד במקביל לפוקוס
        OnSeamlessEntryLoaded(sender, e);

        if (sender is Entry entry && entry.BindingContext is RecipeStep step)
        {
            if (BindingContext is RecipeEditorViewModel vm && step == vm.StepsList.LastOrDefault())
            {
                if (_isAddingNewRow || _focusPendingStepTab)
                {
                    _isAddingNewRow = false;
                    _focusPendingStepTab = false;

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Task.Delay(100);
                        entry.Focus();
                        StepsCollection.ScrollTo(step, position: ScrollToPosition.MakeVisible, animate: true);
                    });
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