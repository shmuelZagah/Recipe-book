// NavBarDrawable.cs
using Microsoft.Maui.Graphics;

namespace Recipe_book.Views.Items;

public class NavBarDrawable : IDrawable
{
    public float HumpCenterX { get; set; } = 0.125f; 
    public float HumpHeight { get; set; } = 35f;
    public float BarHeight { get; set; } = 60f;
    public Color BarColor { get; set; } = Color.FromArgb("#E87A5D");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float width = dirtyRect.Width;
        float height = dirtyRect.Height;
        float barTop = height - BarHeight;

   
        float humpX = width * HumpCenterX;
        float humpWidth = 70f;
        float humpRadius = humpWidth / 2;

     
        PathF path = new PathF();

     
        path.MoveTo(0, height);
        path.LineTo(0, barTop);

      
        float humpStart = humpX - humpRadius - 15;
        path.LineTo(Math.Max(0, humpStart), barTop);

        if (humpStart > 0)
        {
          
            float controlPoint1X = humpX - humpRadius;
            float humpTop = barTop - HumpHeight;

           
            path.QuadTo(
                humpX - humpRadius, barTop,
                humpX - humpRadius + 10, humpTop + 15
            );

         
            path.CurveTo(
                humpX - 20, humpTop,
                humpX + 20, humpTop,
                humpX + humpRadius - 10, humpTop + 15
            );

            
            path.QuadTo(
                humpX + humpRadius, barTop,
                humpX + humpRadius + 15, barTop
            );
        }

       
        path.LineTo(width, barTop);
        path.LineTo(width, height);
        path.Close();

     
        canvas.SetShadow(new SizeF(0, -2), 5, Colors.Black.WithAlpha(0.2f));
        canvas.FillColor = BarColor;
        canvas.FillPath(path);
    }
}
