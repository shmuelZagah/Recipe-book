using Android.Content;
using Android.Views;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Recipe_book.Views.Layouts;

namespace Recipe_book.Platforms.Android;

/// <summary>
/// Android handler for SwipeInterceptView.
/// Overrides touch events so child views receive touches normally,
/// unless a horizontal swipe is detected, which is then captured by the MainPage.
/// Includes fixes for touches on empty spaces and state leak prevention.
/// </summary>
public class SwipeInterceptViewHandler : ContentViewHandler
{
    private float _startX, _startY;
    private bool _decided;
    private bool _intercepting;
    private bool _mauiSwipeStarted; // Prevents state leaks and teleportation bugs
    private const float SlopPx = 10f;

    protected override ContentViewGroup CreatePlatformView()
    {
        return new InterceptingLayout(MauiContext!.Context, this);
    }

    private class InterceptingLayout : ContentViewGroup
    {
        private readonly SwipeInterceptViewHandler _h;

        public InterceptingLayout(Context ctx, SwipeInterceptViewHandler h) : base(ctx)
        {
            _h = h;
        }

        public override bool OnInterceptTouchEvent(MotionEvent ev)
        {
            if (_h.VirtualView is not SwipeInterceptView v) return false;

            float density = Context.Resources.DisplayMetrics.Density;

            switch (ev.Action)
            {
                case MotionEventActions.Down:
                    _h._startX = ev.GetX();
                    _h._startY = ev.GetY();
                    _h._decided = false;
                    _h._intercepting = false;
                    _h._mauiSwipeStarted = false;
                    return false;

                case MotionEventActions.Move:
                    if (_h._decided) return _h._intercepting;

                    float dxPx = Math.Abs(ev.GetX() - _h._startX);
                    float dyPx = Math.Abs(ev.GetY() - _h._startY);

                    if (dxPx < SlopPx && dyPx < SlopPx) return false;

                    _h._decided = true;

                    float dxDp = dxPx / density;
                    float dyDp = dyPx / density;

                    if (dyDp > dxDp * 1.2f)
                    {
                        _h._intercepting = false;
                        return false;
                    }

                    float totalXDp = (ev.GetX() - _h._startX) / density;
                    float startXDp = _h._startX / density;
                    float startYDp = _h._startY / density;

                    bool take = v.ShouldInterceptHorizontal?.Invoke(totalXDp, startXDp, startYDp) ?? true;
                    _h._intercepting = take;

                    if (take)
                    {
                        _h._mauiSwipeStarted = true;
                        v.OnSwipeStarted?.Invoke(totalXDp);
                    }
                    return take;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    bool was = _h._intercepting;
                    _h._intercepting = false;
                    _h._decided = false;
                    return was;
            }

            return _h._intercepting;
        }

        public override bool OnTouchEvent(MotionEvent ev)
        {
            if (_h.VirtualView is not SwipeInterceptView v) return false;

            float density = Context.Resources.DisplayMetrics.Density;

            switch (ev.Action)
            {
                case MotionEventActions.Down:
                    _h._startX = ev.GetX();
                    _h._startY = ev.GetY();
                    _h._decided = false;
                    _h._intercepting = false;
                    _h._mauiSwipeStarted = false;
                    return true; // Capture touches on empty or transparent areas

                case MotionEventActions.Move:
                    float totalX = (ev.GetX() - _h._startX) / density;

                    // Validate start condition if gesture bypassed OnInterceptTouchEvent
                    if (!_h._mauiSwipeStarted)
                    {
                        float dxPx = Math.Abs(ev.GetX() - _h._startX);
                        float dyPx = Math.Abs(ev.GetY() - _h._startY);

                        if (dxPx < SlopPx && dyPx < SlopPx) return true;

                        _h._decided = true;

                        float dxDp = dxPx / density;
                        float dyDp = dyPx / density;

                        if (dyDp > dxDp * 1.2f) return true; // Ignore vertical movement

                        float startXDp = _h._startX / density;
                        float startYDp = _h._startY / density;

                        bool take = v.ShouldInterceptHorizontal?.Invoke(totalX, startXDp, startYDp) ?? true;

                        if (take)
                        {
                            _h._mauiSwipeStarted = true;
                            v.OnSwipeStarted?.Invoke(totalX);
                        }
                    }

                    if (_h._mauiSwipeStarted)
                    {
                        v.OnSwipeRunning?.Invoke(totalX);
                    }
                    return true;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    if (_h._mauiSwipeStarted)
                    {
                        float finalX = (ev.Action == MotionEventActions.Cancel) ? 0 : (ev.GetX() - _h._startX) / density;
                        v.OnSwipeCompleted?.Invoke(finalX);
                        _h._mauiSwipeStarted = false;
                    }
                    _h._intercepting = false;
                    _h._decided = false;
                    return true;
            }

            return false;
        }
    }
}