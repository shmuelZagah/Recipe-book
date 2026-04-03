using Microsoft.Maui.Handlers;
using Recipe_book.Views.Layouts;
using UIKit;

namespace Recipe_book.Platforms.iOS;

/// <summary>
/// iOS handler for SwipeInterceptView.
/// Adds a UIPanGestureRecognizer that cooperates with child scroll views:
/// vertical pans are rejected immediately so UIScrollView can take them,
/// horizontal pans are offered to MainPage via ShouldInterceptHorizontal.
/// </summary>
public class SwipeInterceptViewHandler : ContentViewHandler
{
	private UIPanGestureRecognizer _pan;

	// Track whether the current gesture was handed to this recognizer
	private bool _ownsGesture;

	protected override void ConnectHandler(Microsoft.Maui.Platform.ContentView nativeView)
	{
		base.ConnectHandler(nativeView);
		AttachGesture(nativeView);
	}

	protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentView nativeView)
	{
		if (_pan != null)
			nativeView.RemoveGestureRecognizer(_pan);
		base.DisconnectHandler(nativeView);
	}

	// -------------------------------------------------------------------------
	private void AttachGesture(UIView nativeView)
	{
		_pan = new UIPanGestureRecognizer(HandlePan);
		_pan.MaximumNumberOfTouches = 1;

		// Allow child recognizers to run simultaneously while we are still deciding.
		// Once we call Began successfully we own the gesture exclusively.
		_pan.ShouldRecognizeSimultaneously = (_, other) =>
		{
			// Let child pan recognizers (UIScrollView) run until we decide
			return !_ownsGesture;
		};

		nativeView.AddGestureRecognizer(_pan);
	}

	private void HandlePan(UIPanGestureRecognizer r)
	{
		if (VirtualView is not SwipeInterceptView v) return;

		var translation = r.TranslationInView(r.View);
		float totalX = (float)translation.X;
		float totalY = (float)translation.Y;

		switch (r.State)
		{
			case UIGestureRecognizerState.Began:
				_ownsGesture = false;

				// Reject if movement is more vertical than horizontal
				if (Math.Abs(totalY) > Math.Abs(totalX) * 1.2f)
				{
					CancelRecognizer(r);
					return;
				}

				// Ask MainPage
				bool take = v.ShouldInterceptHorizontal?.Invoke(totalX) ?? true;
				if (!take)
				{
					CancelRecognizer(r);
					return;
				}

				_ownsGesture = true;
				v.OnSwipeStarted?.Invoke(totalX);
				break;

			case UIGestureRecognizerState.Changed:
				if (!_ownsGesture) return;
				v.OnSwipeRunning?.Invoke(totalX);
				break;

			case UIGestureRecognizerState.Ended:
				if (!_ownsGesture) return;
				v.OnSwipeCompleted?.Invoke(totalX);
				_ownsGesture = false;
				break;

			case UIGestureRecognizerState.Cancelled:
			case UIGestureRecognizerState.Failed:
				if (_ownsGesture) v.OnSwipeCompleted?.Invoke(0);
				_ownsGesture = false;
				break;
		}
	}

	// Disable + re-enable is the standard iOS trick to reset a recognizer mid-gesture
	private static void CancelRecognizer(UIGestureRecognizer r)
	{
		r.Enabled = false;
		r.Enabled = true;
	}
}