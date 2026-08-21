namespace Recipe_book.Views.Items.Popups;

public partial class EditDisplayModalControl : ContentView
{
    public EditDisplayModalControl()
    {
        InitializeComponent();

        // ברגע שהקונטרול נוצר, אנחנו מתחילים להאזין לשינויים בתצוגה שלו
        OverlayGrid.PropertyChanged += OnOverlayGridPropertyChanged;
    }

    private async void OnOverlayGridPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // אנחנו תופסים את הרגע המדויק שבו ה-ViewModel הופך את החלון לגלוי
        if (e.PropertyName == nameof(VisualElement.IsVisible) && OverlayGrid.IsVisible)
        {
            // 1. הכנת השטח (מאתחלים למצב מוסתר וקצת נמוך)
            OverlayGrid.Opacity = 0;
            ModalFrame.Opacity = 0;
            ModalFrame.TranslationY = 40; // מתחיל 40 פיקסלים מתחת למרכז

            // 2. מריצים את האנימציות במקביל (Fade-in + Spring)
            _ = OverlayGrid.FadeTo(1, 200); // הרקע השחור מופיע מהר
            _ = ModalFrame.FadeTo(1, 250); // הפריים נחשף

            // פה קורה הקסם: הוא עולה, עובר קצת את האמצע (Overshoot) ומתיישב בחזרה
            await ModalFrame.TranslateTo(0, 0, 450, Easing.SpringOut);
        }
    }

    private void EditEntry_Completed(object sender, EventArgs e)
    {
        EditEntry.Unfocus();
    }
}