using Microsoft.Maui.Controls;
using Recipe_book.Models.Recipes;
using Recipe_book.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics; // הוספנו בשביל הלוגים

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

    public RecipeEditorPage(RecipeEditorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
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
        else
        {
            Debug.WriteLine($"[StepDebug] {logTag}: Failed to disable animator (PlatformView is null or wrong type).");
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

    #region Floating Description Animation (ללא שינוי)
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

    private async void OnIngredientsTabTapped(object sender, TappedEventArgs e)
    {
        _focusPendingIngredientTab = true;
        _forceKeyboard = true;
        bool newlyFocused = false;

        if (BindingContext is RecipeEditorViewModel vm)
        {
            vm.ShowIngredientsCommand.Execute(null);

            // מחפש את השורה האחרונה ומתפקס עליה מיד (מה שגונב את הפוקוס מהטאב המוסתר)
            var lastItem = vm.IngredientsList.LastOrDefault();
            if (lastItem != null && _activeEntries.TryGetValue(lastItem, out var entry))
            {
                _focusPendingIngredientTab = false;
                FocusNatively(entry, true, "IngredientsTab_ImmediateFocus");
                newlyFocused = true;
            }
        }

        // אם הרשימה הייתה ריקה ולא מצאנו שדה להתפקס עליו, ננקה את הפוקוס הישן בכוח
        if (!newlyFocused)
        {
            this.Unfocus();
        }

        if (!_isIngredientsAnimatorDisabled)
        {
            await Task.Delay(50);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _isIngredientsAnimatorDisabled = TryDisableAnimation(IngredientsCollection, "Ingredients_TabTapped");
            });
        }
    }

    private async void OnStepsTabTapped(object sender, TappedEventArgs e)
    {
        Debug.WriteLine("[StepDebug] Steps tab tapped.");
        _focusPendingStepTab = true;
        _forceKeyboard = true;
        bool newlyFocused = false;

        if (BindingContext is RecipeEditorViewModel vm)
        {
            vm.ShowStepsCommand.Execute(null);

            // מחפש את השלב האחרון ומתפקס עליו מיד
            var lastItem = vm.StepsList.LastOrDefault();
            if (lastItem != null && _activeEntries.TryGetValue(lastItem, out var entry))
            {
                _focusPendingStepTab = false;
                FocusNatively(entry, true, "StepsTab_ImmediateFocus");
                newlyFocused = true;
            }
        }

        if (!newlyFocused)
        {
            this.Unfocus();
        }

        if (!_isStepsAnimatorDisabled)
        {
            await Task.Delay(50);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _isStepsAnimatorDisabled = TryDisableAnimation(StepsCollection, "Steps_TabTapped");
            });
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
        Debug.WriteLine("[StepDebug] Footer Add Step tapped.");
        _isAddingNewRow = true;
        _forceKeyboard = true;
        if (BindingContext is RecipeEditorViewModel vm) vm.AddStepCommand.Execute(null);
    }
    #endregion

    #region Row Flow & Native Keyboard Control

    private void FocusNatively(VisualElement element, bool forceKeyboard, string logContext = "")
    {
        Debug.WriteLine($"[StepDebug] FocusNatively triggered from: {logContext}. ForceKeyboard: {forceKeyboard}");

        MainThread.BeginInvokeOnMainThread(async () =>
        {
#if ANDROID
            if (element.Handler?.PlatformView is Android.Widget.EditText native)
            {
                Debug.WriteLine($"[StepDebug] Requesting native focus on EditText.");
                native.RequestFocus();

                if (forceKeyboard)
                {
                    var imm = (Android.Views.InputMethods.InputMethodManager)native.Context.GetSystemService(Android.Content.Context.InputMethodService);

                    // האינטואיציה שלך בפעולה: 
                    // דוגמים בעדינות את אנדרואיד כדי לגלות מתי חלון הפופ-אפ (כמו הפיקר)
                    // שחרר את המסך, והשדה שלנו הפך רשמית ל-"Served View".
                    int attempts = 0;
                    while (!native.HasWindowFocus && attempts < 20) // הגנת גיבוי למקרה חירום
                    {
                        await Task.Delay(10); // ממתינים לשבריר שנייה ובודקים שוב
                        attempts++;
                    }

                    Debug.WriteLine($"[StepDebug] Window focus verified after {attempts} checks. Forcing keyboard.");
                    imm?.ShowSoftInput(native, Android.Views.InputMethods.ShowFlags.Implicit);
                }
                return;
            }
#endif
            Debug.WriteLine($"[StepDebug] Fallback to standard MAUI Focus.");
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
        Debug.WriteLine("[StepDebug] OnStepCompleted fired (Standard MAUI Event).");
        _isAddingNewRow = true;
        _forceKeyboard = true; // שינינו ל-true
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
                        Debug.WriteLine("[StepDebug] CustomAndroidEditorActionListener fired (Native Enter).");
                        _isAddingNewRow = true;

                        if (BindingContext is RecipeEditorViewModel vm)
                        {
                            if (entry.BindingContext is Ingredient)
                            {
                                _forceKeyboard = false; // במצרכים המקלדת נשארת טבעית
                                vm.AddIngredientCommand.Execute(null);
                            }
                            else if (entry.BindingContext is RecipeStep step)
                            {
                                _forceKeyboard = true; // בשלבים אנחנו דורשים מקלדת בכוח!
                                Debug.WriteLine($"[StepDebug] Executing AddStepCommand for step: {step.StepNumber}");
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
            _activeEntries[ingredient] = entry; // שומרים את הרפרנס לשדה

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
        Debug.WriteLine("[StepDebug] OnStepDescriptionLoaded fired for a step row.");
        OnSeamlessEntryLoaded(sender, e);

        if (sender is Entry entry && entry.BindingContext is RecipeStep step)
        {
            _activeEntries[step] = entry; // שומרים את הרפרנס לשדה

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