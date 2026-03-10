using Microsoft.Maui.Controls;
using System.Linq;

namespace Recipe_book.Helpers.Behaviors; 

public class NumericValidationBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry entry)
    {
        entry.TextChanged += OnEntryTextChanged;
        base.OnAttachedTo(entry);
    }

    protected override void OnDetachingFrom(Entry entry)
    {
        entry.TextChanged -= OnEntryTextChanged;
        base.OnDetachingFrom(entry);
    }

    private void OnEntryTextChanged(object sender, TextChangedEventArgs args)
    {
  
        if (string.IsNullOrWhiteSpace(args.NewTextValue))
            return;

 
        bool isValid = double.TryParse(args.NewTextValue, out double result);


        if (!isValid)
        {
            ((Entry)sender).Text = args.OldTextValue;
        }
    }
}