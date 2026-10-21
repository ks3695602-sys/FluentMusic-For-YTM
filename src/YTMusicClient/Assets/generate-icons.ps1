$csharpSource = @"
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class IconGenerator
{
    public static void Generate(int width, int height, string path, bool isWide)
    {
        using (var bmp = new Bitmap(width, height))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            int iconSize = Math.Min(width, height);
            int pad = Math.Max(2, (int)(iconSize * 0.08));
            int drawSize = iconSize - (2 * pad);
            int xOffset = (width - drawSize) / 2;
            int yOffset = (height - drawSize) / 2;

            if (isWide)
            {
                xOffset = pad * 2;
            }

            var rect = new Rectangle(xOffset, yOffset, drawSize, drawSize);

            // Red gradient circle
            using (var brush = new LinearGradientBrush(rect, 
                Color.FromArgb(255, 245, 34, 45), 
                Color.FromArgb(255, 196, 29, 39), 
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillEllipse(brush, rect);
            }

            // Inner subtle ring
            int ringPad = Math.Max(2, (int)(drawSize * 0.18));
            var innerRect = new Rectangle(xOffset + ringPad, yOffset + ringPad, drawSize - (2 * ringPad), drawSize - (2 * ringPad));
            using (var ringPen = new Pen(Color.FromArgb(160, 255, 255, 255), Math.Max(1.5f, drawSize * 0.04f)))
            {
                g.DrawEllipse(ringPen, innerRect);
            }

            // Play triangle in center
            float triWidth = drawSize * 0.28f;
            float triHeight = drawSize * 0.32f;
            float triX = xOffset + (drawSize * 0.40f);
            float triY = yOffset + ((drawSize - triHeight) / 2f);

            PointF[] triangle = new PointF[]
            {
                new PointF(triX, triY),
                new PointF(triX + triWidth, triY + (triHeight / 2f)),
                new PointF(triX, triY + triHeight)
            };

            using (var whiteBrush = new SolidBrush(Color.White))
            {
                g.FillPolygon(whiteBrush, triangle);
            }

            if (isWide)
            {
                using (var font = new Font("Segoe UI", height * 0.22f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    float textX = xOffset + drawSize + (height * 0.15f);
                    float textY = (height - font.Height) / 2f;
                    g.DrawString("FluentMusic", font, textBrush, textX, textY);
                }
            }

            if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                IntPtr hIcon = bmp.GetHicon();
                using (Icon icon = Icon.FromHandle(hIcon))
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    icon.Save(fs);
                }
            }
            else
            {
                bmp.Save(path, ImageFormat.Png);
            }
            Console.WriteLine("Saved: " + path + " (" + width + "x" + height + ")");
        }
    }
}
"@

Add-Type -TypeDefinition $csharpSource -ReferencedAssemblies "System.Drawing"

$assetDir = $PSScriptRoot
[IconGenerator]::Generate(44, 44, "$assetDir\Square44x44Logo.png", $false)
[IconGenerator]::Generate(24, 24, "$assetDir\Square44x44Logo.targetsize-24_altform-unplated.png", $false)
[IconGenerator]::Generate(150, 150, "$assetDir\Square150x150Logo.png", $false)
[IconGenerator]::Generate(310, 150, "$assetDir\Wide310x150Logo.png", $true)
[IconGenerator]::Generate(50, 50, "$assetDir\StoreLogo.png", $false)
[IconGenerator]::Generate(620, 300, "$assetDir\SplashScreen.png", $true)
[IconGenerator]::Generate(24, 24, "$assetDir\LockScreenLogo.png", $false)
[IconGenerator]::Generate(128, 128, "$assetDir\WindowIcon.ico", $false)
