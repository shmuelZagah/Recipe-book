using System.Windows.Input;

namespace Recipe_book.Views.Items;

public partial class RecipeCardControl : ContentView
{
    #region Bindable Properties

    public string RecipeName
    {
        get => (string)GetValue(RecipeNameProperty);
        set => SetValue(RecipeNameProperty, value);
    }
    public static readonly BindableProperty RecipeNameProperty =
        BindableProperty.Create(nameof(RecipeName), typeof(string), typeof(RecipeCardControl), string.Empty);

    public string RecipeImage
    {
        get => (string)GetValue(RecipeImageProperty);
        set => SetValue(RecipeImageProperty, value);
    }
    public static readonly BindableProperty RecipeImageProperty =
        BindableProperty.Create(nameof(RecipeImage), typeof(string), typeof(RecipeCardControl), string.Empty);

    public string RecipeDescription
    {
        get => (string)GetValue(RecipeDescriptionProperty);
        set => SetValue(RecipeDescriptionProperty, value);
    }
    public static readonly BindableProperty RecipeDescriptionProperty =
        BindableProperty.Create(nameof(RecipeDescription), typeof(string), typeof(RecipeCardControl), string.Empty);

    public double RecipeRating
    {
        get => (double)GetValue(RecipeRatingProperty);
        set => SetValue(RecipeRatingProperty, value);
    }
    public static readonly BindableProperty RecipeRatingProperty =
        BindableProperty.Create(nameof(RecipeRating), typeof(double), typeof(RecipeCardControl), 0.0);

    public string RecipePrepTime
    {
        get => (string)GetValue(RecipePrepTimeProperty);
        set => SetValue(RecipePrepTimeProperty, value);
    }
    public static readonly BindableProperty RecipePrepTimeProperty =
        BindableProperty.Create(nameof(RecipePrepTime), typeof(string), typeof(RecipeCardControl), string.Empty);

    public string RecipeServings
    {
        get => (string)GetValue(RecipeServingsProperty);
        set => SetValue(RecipeServingsProperty, value);
    }
    public static readonly BindableProperty RecipeServingsProperty =
        BindableProperty.Create(nameof(RecipeServings), typeof(string), typeof(RecipeCardControl), string.Empty);

    public bool ShowOptions
    {
        get => (bool)GetValue(ShowOptionsProperty);
        set => SetValue(ShowOptionsProperty, value);
    }
    public static readonly BindableProperty ShowOptionsProperty =
        BindableProperty.Create(nameof(ShowOptions), typeof(bool), typeof(RecipeCardControl), false);

    public ICommand OptionsCommand
    {
        get => (ICommand)GetValue(OptionsCommandProperty);
        set => SetValue(OptionsCommandProperty, value);
    }
    public static readonly BindableProperty OptionsCommandProperty =
        BindableProperty.Create(nameof(OptionsCommand), typeof(ICommand), typeof(RecipeCardControl), null);

    #endregion

    public RecipeCardControl()
    {
        InitializeComponent();
    }
}