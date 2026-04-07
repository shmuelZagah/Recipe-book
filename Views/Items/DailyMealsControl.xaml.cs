using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace Recipe_book.Views.Items;

public partial class DailyMealsControl : ContentView
{
    private IDispatcherTimer _snapTimer;
    private bool _isSnapping;
    private bool _isLoaded;

    // Store the time of the last scroll event to implement a debounce mechanism
    private DateTime _lastScrollTime;

    public DailyMealsControl()
    {
        InitializeComponent();
        this.Loaded += OnControlLoaded;
    }

    private void OnControlLoaded(object sender, EventArgs e)
    {
        if (_isLoaded) return;
        _isLoaded = true;

        // Initialize the timer for snapping the closest card to the center
        _snapTimer = Application.Current.Dispatcher.CreateTimer();
        _snapTimer.Interval = TimeSpan.FromMilliseconds(250);
        _snapTimer.Tick += OnSnapTimerTick;

        UpdateCardsEffect();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0)
        {
            double cardWidth = 280; // This must match the XAML WidthRequest="280"

            // Calculate dynamic padding to ensure the first and last cards 
            // can reach the exact center of the screen
            double sidePadding = (width - cardWidth) / 2;
            if (sidePadding < 0) sidePadding = 0;

            CardsContainer.Padding = new Thickness(sidePadding, 20, sidePadding, 30);

            UpdateCardsEffect();
        }
    }

    public async Task ScrollToStartAsync()
    {
        await Task.Delay(150);

        if (CarouselScroll.ContentSize.Width > 0)
        {
            double fullWidth = CarouselScroll.ContentSize.Width;

            // Scroll to the far right (start position for RTL layout) without animation
            await CarouselScroll.ScrollToAsync(fullWidth, 0, animated: false);

            UpdateCardsEffect();
        }
    }

    private void OnScroll(object sender, ScrolledEventArgs e)
    {
        if (!_isLoaded) return;

        // Record the exact time of the scroll to prevent premature snapping
        _lastScrollTime = DateTime.Now;

        UpdateCardsEffect();

        // Restart the timer on every scroll event
        _snapTimer.Stop();
        _snapTimer.Start();
    }

    private void UpdateCardsEffect()
    {
        int count = CardsContainer.Children.Count;

        if (count == 0 || CarouselScroll.ContentSize.Width <= 0)
            return;

        double scrollMax = CarouselScroll.ContentSize.Width - CarouselScroll.Width;

        if (scrollMax <= 0)
            return;

        double scrollX = Math.Clamp(CarouselScroll.ScrollX, 0, scrollMax);

        // Calculate scroll percentage (0.0 to 1.0)
        double scrollPercent = scrollX / scrollMax;

        // Calculate which card index should currently be the active (centered) one
        double activeIndexFloat = (1.0 - scrollPercent) * (count - 1);

        int i = 0;

        foreach (var view in CardsContainer.Children)
        {
            if (view is VisualElement card)
            {
                // Calculate distance from the active center
                double distance = Math.Abs(i - activeIndexFloat);
                double normalizedDistance = Math.Clamp(distance, 0, 1);

                // Apply scale and opacity based on distance (centered is 1.0, sides are smaller/faded)
                double scale = 1.0 - (normalizedDistance * 0.20);
                double opacity = 1.0 - (normalizedDistance * 0.50);

                card.Scale = scale;
                card.Opacity = opacity;

                i++;
            }
            else if (view is Layout layout)
            {
                // If the child is a layout, apply the effect to its children instead of the layout itself.
                foreach (var childView in layout.Children)
                {
                    if (childView is VisualElement cardInLayout)
                    {
                        // This part of the loop seems unused in the current architecture but
                        // is kept from previous versions just in case.
                        double distance = Math.Abs(i - activeIndexFloat);
                        double normalizedDistance = Math.Clamp(distance, 0, 1);
                        double scale = 1.0 - (normalizedDistance * 0.20);
                        double opacity = 1.0 - (normalizedDistance * 0.50);
                        cardInLayout.Scale = scale;
                        cardInLayout.Opacity = opacity;
                    }
                }
                i++;
            }
        }
    }

    private async void OnSnapTimerTick(object sender, EventArgs e)
    {
        _snapTimer.Stop();

        // 🔥 CRITICAL: Do not snap while still scrolling (Momentum/Fling check)
        // Ensure at least 200ms have passed since the last actual scroll movement
        if ((DateTime.Now - _lastScrollTime).TotalMilliseconds < 200)
        {
            return;
        }

        if (_isSnapping) return;

        _isSnapping = true;

        try
        {
            int count = CardsContainer.Children.Count;

            if (count <= 1)
                return;

            double scrollMax = CarouselScroll.ContentSize.Width - CarouselScroll.Width;

            if (scrollMax <= 0)
                return;

            double scrollX = Math.Clamp(CarouselScroll.ScrollX, 0, scrollMax);
            double scrollPercent = scrollX / scrollMax;
            double activeIndexFloat = (1.0 - scrollPercent) * (count - 1);

            // Find the closest whole index to snap to
            int targetIndex = (int)Math.Round(activeIndexFloat);
            targetIndex = Math.Clamp(targetIndex, 0, count - 1);

            // Calculate the exact scroll position needed for the target card
            double targetScrollPercent = 1.0 - (targetIndex / (double)(count - 1));
            double targetScrollX = targetScrollPercent * scrollMax;

            // Only animate if the distance is significant enough (prevents micro-jitters)
            if (Math.Abs(CarouselScroll.ScrollX - targetScrollX) > 5)
            {
                // Scroll to target, keeping the Y axis at its current state
                await CarouselScroll.ScrollToAsync(targetScrollX, CarouselScroll.ScrollY, animated: true);

                // Wait for the animation to complete
                await Task.Delay(250);
            }
        }
        finally
        {
            _isSnapping = false;
        }
    }
}