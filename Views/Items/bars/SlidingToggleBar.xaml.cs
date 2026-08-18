using Microsoft.Maui.Controls;
using System;

namespace Recipe_book.Views.bars.Items;

public partial class SlidingToggleBar : ContentView
{
    public static readonly BindableProperty IsRightSelectedProperty = BindableProperty.Create(
        nameof(IsRightSelected), typeof(bool), typeof(SlidingToggleBar), true, BindingMode.TwoWay, propertyChanged: OnIsRightSelectedChanged);
    public bool IsRightSelected
    {
        get => (bool)GetValue(IsRightSelectedProperty);
        set => SetValue(IsRightSelectedProperty, value);
    }

    public static readonly BindableProperty RightTextProperty = BindableProperty.Create(nameof(RightText), typeof(string), typeof(SlidingToggleBar), string.Empty);
    public string RightText { get => (string)GetValue(RightTextProperty); set => SetValue(RightTextProperty, value); }

    public static readonly BindableProperty LeftTextProperty = BindableProperty.Create(nameof(LeftText), typeof(string), typeof(SlidingToggleBar), string.Empty);
    public string LeftText { get => (string)GetValue(LeftTextProperty); set => SetValue(LeftTextProperty, value); }

    public static readonly BindableProperty RightIconProperty = BindableProperty.Create(nameof(RightIcon), typeof(string), typeof(SlidingToggleBar), string.Empty);
    public string RightIcon { get => (string)GetValue(RightIconProperty); set => SetValue(RightIconProperty, value); }

    public static readonly BindableProperty LeftIconProperty = BindableProperty.Create(nameof(LeftIcon), typeof(string), typeof(SlidingToggleBar), string.Empty);
    public string LeftIcon { get => (string)GetValue(LeftIconProperty); set => SetValue(LeftIconProperty, value); }

    private bool _initialPositionSet = false;

    public SlidingToggleBar()
    {
        InitializeComponent();
        // עכשיו מאזינים ל-MainGrid ולא ל-Slider, מה שפותר את באג הקפיצה גם בתוך DataTemplate
        MainGrid.SizeChanged += MainGrid_SizeChanged;
    }

    private void MainGrid_SizeChanged(object sender, EventArgs e)
    {
        if (MainGrid.Width <= 0) return;

        if (!_initialPositionSet)
        {
            _initialPositionSet = true;
            // התיקון לאופסט: במקום לזוז לפי הרוחב של הסליידר, זזים בדיוק חצי מהרוחב של הגריד הראשי
            Slider.TranslationX = IsRightSelected ? 0 : -(MainGrid.Width / 2);
            UpdateColors(IsRightSelected);
        }
    }

    private static void OnIsRightSelectedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SlidingToggleBar bar)
        {
            bar.AnimateSlider((bool)newValue);
        }
    }

    private void OnRightTabTapped(object sender, EventArgs e)
    {
        if (!IsRightSelected) IsRightSelected = true;
    }

    private void OnLeftTabTapped(object sender, EventArgs e)
    {
        if (IsRightSelected) IsRightSelected = false;
    }

    private async void AnimateSlider(bool isRight)
    {
        UpdateColors(isRight);

        if (!_initialPositionSet || MainGrid.Width <= 0) return;

        double targetX = isRight ? 0 : -(MainGrid.Width / 2);
        await Slider.TranslateTo(targetX, 0, 250, Easing.CubicOut);
    }

    private void UpdateColors(bool isRight)
    {
        RightLabel.TextColor = isRight ? Colors.White : Color.FromArgb("#555555");
        LeftLabel.TextColor = !isRight ? Colors.White : Color.FromArgb("#555555");
        RightIconImage.Opacity = isRight ? 1 : 0.6;
        LeftIconImage.Opacity = !isRight ? 1 : 0.6;
    }
}