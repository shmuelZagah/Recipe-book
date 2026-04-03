using Android.Content;
using Android.Views;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Recipe_book.Views.Layouts;

namespace Recipe_book.Platforms.Android;

/// <summary>
/// Android handler for SwipeInterceptView.
/// Overrides OnInterceptTouchEvent so child views (ScrollView, CollectionView, buttons)
/// receive touches normally — unless we detect a horizontal swipe that MainPage wants.
/// </summary>
public class SwipeInterceptViewHandler : ContentViewHandler
{
	private float _startX, _startY;
	private bool _decided;      // true once we know horizontal vs vertical
	private bool _intercepting; // true when this view has captured the gesture
	private const float SlopPx = 10f;

	protected override ContentViewGroup CreatePlatformView()
	{
		// MauiContext.Context is the correct way to get Android Context from a Handler
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
					_h._startX = ev.RawX;
					_h._startY = ev.RawY;
					_h._decided = false;
					_h._intercepting = false;
					// Always let children see Down so buttons/taps can arm themselves
					return false;

				case MotionEventActions.Move:
					if (_h._decided) return _h._intercepting;

					float dxPx = Math.Abs(ev.RawX - _h._startX);
					float dyPx = Math.Abs(ev.RawY - _h._startY);

					// Not moved enough yet — keep waiting
					if (dxPx < SlopPx && dyPx < SlopPx) return false;

					_h._decided = true;

					// Vertical scroll → always give to children
					float dxDp = dxPx / density;
					float dyDp = dyPx / density;
					if (dyDp > dxDp * 1.2f)
					{
						_h._intercepting = false;
						return false;
					}

					// Horizontal swipe → ask MainPage if it wants this
					float totalXDp = (ev.RawX - _h._startX) / density;
					bool take = v.ShouldInterceptHorizontal?.Invoke(totalXDp) ?? true;
					_h._intercepting = take;

					if (take) v.OnSwipeStarted?.Invoke(totalXDp);
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
			float totalX = (ev.RawX - _h._startX) / density;

			switch (ev.Action)
			{
				case MotionEventActions.Move:
					v.OnSwipeRunning?.Invoke(totalX);
					return true;
				case MotionEventActions.Up:
					v.OnSwipeCompleted?.Invoke(totalX);
					return true;
				case MotionEventActions.Cancel:
					v.OnSwipeCompleted?.Invoke(0);
					return true;
			}

			return false;
		}
	}
}