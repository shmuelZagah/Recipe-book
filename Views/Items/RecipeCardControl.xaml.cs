namespace Recipe_book.Views.Items;

public partial class RecipeCardControl : ContentView
{
    //---------------
    #region Varibals
    //---------------
 
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


    #endregion

    public RecipeCardControl()
    {
        InitializeComponent();
    }
}