namespace Recipe_book.Views.Layouts;

/// <summary>
/// Defines the routing action for a horizontal swipe gesture.
/// </summary>
public enum SwipeAction
{
    /// <summary>
    /// The main page captures the gesture and navigates between primary app tabs.
    /// </summary>
    MainPageSwipe,

    /// <summary>
    /// The main page captures the gesture but forwards the translation data to the inner page (remote control).
    /// </summary>
    ManualInnerSwipe,

    /// <summary>
    /// The main page releases the gesture entirely, allowing native child controls (e.g., tabs, scroll views) to handle it.
    /// </summary>
    NativeChildScroll
}

/// <summary>
/// Contract for pages that need to coordinate touch events with the main page's gesture router.
/// </summary>
public interface ISwipeAwarePage
{
    /// <summary>
    /// Asks the page how the router should handle a gesture starting at the specific coordinates.
    /// </summary>
    SwipeAction GetSwipeAction(double totalX, double startX, double startY);

    void StartInnerSwipe();
    void RunningInnerSwipe(double deltaX);
    void CompletedInnerSwipe(double deltaX, double screenWidth);
}