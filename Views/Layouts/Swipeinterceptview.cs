namespace Recipe_book.Views.Layouts;

/// <summary>
/// A transparent ContentView that sits on top of the pages container.
/// Its platform-specific Handler decides whether touch events go to children (scroll, tap)
/// or are captured here for horizontal page swiping.
/// </summary>
public class SwipeInterceptView : ContentView
{
    /// <summary>
    /// Called once when a horizontal swipe begins and this view wins the gesture.
    /// Parameter: totalX in dp at the moment of capture.
    /// </summary>
    public Action<double> OnSwipeStarted { get; set; }

    /// <summary>
    /// Called every frame while the finger is moving and this view owns the gesture.
    /// Parameter: totalX in dp from the original touch-down point.
    /// </summary>
    public Action<double> OnSwipeRunning { get; set; }

    /// <summary>
    /// Called when the finger lifts or the gesture is cancelled.
    /// Parameter: final totalX in dp.
    /// </summary>
    public Action<double> OnSwipeCompleted { get; set; }

    /// <summary>
    /// MainPage sets this delegate to decide, at gesture-start time, whether this view
    /// should capture the horizontal swipe or let a child (e.g. LibraryPage tabs) handle it.
    /// Return true  → this view captures the swipe (outer page scroll).
    /// Return false → child handles it (inner tab scroll).
    /// Parameter: totalX in dp (positive = finger moved right).
    /// </summary>
    public Func<double, bool> ShouldInterceptHorizontal { get; set; }
}