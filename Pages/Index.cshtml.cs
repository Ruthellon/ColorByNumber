using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

namespace ColorByNumber.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public IFormFile FormFile { get; set; }

        [BindProperty]
        public bool Normalize { get; set; } = true;
        [BindProperty]
        public bool Soften { get; set; } = true;
        [BindProperty]
        public bool Clean { get; set; } = true;

        public byte[] Original { get; set; }
        public byte[] NormalizedBytes { get; set; }
        public byte[] SoftenedBytes { get; set; }
        public byte[] PBCBytes { get; set; }
        public byte[] CleanedBytes { get; set; }
        public byte[] OutlineBytes { get; set; }

        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public Bitmap WorkingImage { get; set; }
        public async Task<IActionResult> OnPostUpload()
        {
            try
            {
                if (FormFile.Length > 0)
                {
                    var filePath = Path.GetFullPath(FormFile.FileName);

                    using (var memoryStream = new MemoryStream())
                    {
                        await FormFile.CopyToAsync(memoryStream);
                        using (var img = System.Drawing.Image.FromStream(memoryStream))
                        {
                            Original = (byte[])new ImageConverter().ConvertTo(img, typeof(byte[]));

                            //Bitmap image = new Bitmap(img, new Size(img.Width / 2, img.Height / 2));
                            WorkingImage = new Bitmap(img);

                            if (Normalize)
                            {
                                NormalizeImage();
                                NormalizedBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));
                            }

                            if (Soften)
                            {
                                SoftenImage();
                                SoftenedBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));
                            }

                            ProcessImage();
                            PBCBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));

                            if (Clean)
                            {
                                CleanImage();
                                CleanedBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));
                            }

                            GetOutlines();
                            //OutlineBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));

                            Bitmap bmp = new Bitmap(WorkingImage.Width, WorkingImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.Clear(Color.White);
                                g.DrawImage(WorkingImage, new Rectangle(new Point(), WorkingImage.Size), new Rectangle(new Point(), WorkingImage.Size), GraphicsUnit.Pixel);
                            }
                            OutlineBytes = (byte[])new ImageConverter().ConvertTo(bmp, typeof(byte[]));
                        }
                    }
                }
            }
            catch (Exception e)
            {
            }

            return Page();
        }

        private void SoftenImage()
        {
            for (int y = 0; y < WorkingImage.Height; y++)
            {
                for (int x = 0; x < WorkingImage.Width; x++)
                {
                    List<Color> colors = new List<Color>();

                    for (int b = Math.Max(y - 1, 0); b <= Math.Min(y + 1, WorkingImage.Height - 1); b++)
                    {
                        for (int a = Math.Max(x - 1, 0); a <= Math.Min(x + 1, WorkingImage.Width - 1); a++)
                        {
                            colors.Add(WorkingImage.GetPixel(a, b));
                        }
                    }

                    int red = Convert.ToInt32(Math.Round((double)colors.Sum(x => x.R) / (double)colors.Count));
                    int green = Convert.ToInt32(Math.Round((double)colors.Sum(y => y.G) / (double)colors.Count));
                    int blue = Convert.ToInt32(Math.Round((double)colors.Sum(z => z.B) / (double)colors.Count));

                    WorkingImage.SetPixel(x, y, Color.FromArgb(red, green, blue));
                }
            }
        }

        private void NormalizeImage()
        {
            for (int y = 0; y < WorkingImage.Height; y++)
            {
                for (int x = 0; x < WorkingImage.Width; x++)
                {
                    WorkingImage.SetPixel(x, y, NormalizeColor(WorkingImage.GetPixel(x, y)));
                }
            }
        }

        private void CleanImage()
        {
            for (int y = 0; y < WorkingImage.Height; y++)
            {
                for (int x = 0; x < WorkingImage.Width; x++)
                {
                    Dictionary<Color, int> colors = new Dictionary<Color, int>();

                    for (int b = Math.Max(y - 3, 0); b <= Math.Min(y + 3, WorkingImage.Height - 1); b++)
                    {
                        for (int a = Math.Max(x - 3, 0); a <= Math.Min(x + 3, WorkingImage.Width - 1); a++)
                        {
                            if (colors.ContainsKey(WorkingImage.GetPixel(a, b)))
                                colors[WorkingImage.GetPixel(a, b)]++;
                            else
                                colors.Add(WorkingImage.GetPixel(a, b), 1);
                        }
                    }



                    if (colors.Where(z => z.Key == WorkingImage.GetPixel(x, y)).FirstOrDefault().Value < 9)
                    {
                        double distance = Double.MaxValue;
                        Color? newColor = null;
                        var current = new CIELab(WorkingImage.GetPixel(x, y));
                        foreach (var color in colors.OrderByDescending(z => z.Value))
                        {
                            if (newColor == null)
                                newColor = color.Key;

                            if (color.Value >= 9)
                            {
                                double d = DistanceBetweenColors(new CIELab(color.Key), current);

                                if (d < distance)
                                {
                                    distance = d;
                                    newColor = color.Key;
                                }
                            }
                        }

                        WorkingImage.SetPixel(x, y, newColor.Value);
                    }
                }
            }
        }

        private void ProcessImage()
        {
            Dictionary<Color, int> colorCount = new Dictionary<Color, int>();
            for (int y = 0; y < WorkingImage.Height; y++)
            {
                for (int x = 0; x < WorkingImage.Width; x++)
                {
                    Color pixelColor = WorkingImage.GetPixel(x, y);

                    if (colorCount.ContainsKey(pixelColor))
                        colorCount[pixelColor]++;
                    else
                        colorCount.Add(pixelColor, 1);
                }
            }

            var top = colorCount.OrderByDescending(a => a.Value).Select(b => b.Key).ToList();

            List<CIELab> TopColors = new List<CIELab>();

            foreach (var color in top)
            {
                bool found = false;
                CIELab labColor = new CIELab(color);
                for (int i = 0; i < TopColors.Count; i++)
                {
                    if (DistanceBetweenColors(labColor, TopColors[i]) < 28.0f)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    TopColors.Add(labColor);
            }

            if (TopColors.Count > 25)
                TopColors.RemoveRange(25, TopColors.Count - 25);

            for (int y = 0; y < WorkingImage.Height; y++)
            {
                for (int x = 0; x < WorkingImage.Width; x++)
                {
                    Color pixelColor = WorkingImage.GetPixel(x, y);

                    double smallestDistance = Double.MaxValue;
                    int index = 0;
                    for (int j = 0; j < TopColors.Count; j++)
                    {
                        double distance = DistanceBetweenColors(new CIELab(pixelColor), TopColors[j]);
                        if (distance < smallestDistance)
                        {
                            smallestDistance = distance;
                            index = j;
                        }
                    }

                    WorkingImage.SetPixel(x, y, TopColors[index].StoredColor);
                }
            }
        }

        private void GetOutlines()
        {
            Bitmap outline = new Bitmap(WorkingImage);

            for (int y = 0; y < WorkingImage.Height; y++)
            {
                for (int x = 1; x < WorkingImage.Width; x++)
                {
                    Color color = WorkingImage.GetPixel(x, y);
                    outline.SetPixel(x, y, Color.FromArgb(32, color.R, color.G, color.B));
                }
            }

            for (int y = 0; y < WorkingImage.Height; y++)
            {
                for (int x = 1; x < WorkingImage.Width; x++)
                {
                    if (WorkingImage.GetPixel(x, y) != WorkingImage.GetPixel(x - 1, y))
                    {
                        outline.SetPixel(x, y, Color.Black);
                        x++;
                        continue;
                    }
                }
            }

            for (int x = 0; x < WorkingImage.Width; x++)
            {
                for (int y = 1; y < WorkingImage.Height; y++)
                {
                    if (WorkingImage.GetPixel(x, y) != WorkingImage.GetPixel(x, y - 1))
                    {
                        outline.SetPixel(x, y, Color.Black);
                        y++;
                        continue;
                    }
                }
            }

            WorkingImage = outline;
        }

        private Color NormalizeColor(Color pixel)
        {
            int red = pixel.R;
            int green = pixel.G;
            int blue = pixel.B;

            red = Convert.ToInt32(Math.Round(((double)red / 10.0f)) * 10);
            green = Convert.ToInt32(Math.Round(((double)green / 10.0f)) * 10);
            blue = Convert.ToInt32(Math.Round(((double)blue / 10.0f)) * 10);

            return Color.FromArgb(Math.Min(255, red), Math.Min(255, green), Math.Min(255, blue));
        }

        private double DistanceBetweenColors(CIELab pixel1, CIELab pixel2)
        {
            return Math.Sqrt(Math.Pow(pixel1.L - pixel2.L, 2) + Math.Pow(pixel1.A - pixel2.A, 2) + Math.Pow(pixel1.B - pixel2.B, 2));
        }

        //public class Point
        //{
        //    public int X { get; set; }
        //    public int Y { get; set; }
        //}

        public class XYZ
        {
            private double x;
            public double X { get { return x; } }
            private double y;
            public double Y { get { return y; } }
            private double z;
            public double Z { get { return z; } }

            public XYZ(int r, int g, int b)
            {
                double var_R = (float)r / 255.0f;
                double var_G = (float)g / 255.0f;
                double var_B = (float)b / 255.0f;

                var_R = (var_R > 0.04045) ? Math.Pow((var_R + 0.055) / 1.055, 2.4)
                                          : var_R / 12.92;
                var_G = (var_G > 0.04045) ? Math.Pow((var_G + 0.055) / 1.055, 2.4)
                                          : var_G / 12.92;
                var_B = (var_B > 0.04045) ? Math.Pow((var_B + 0.055) / 1.055, 2.4)
                                          : var_B / 12.92;

                var_R *= 100;
                var_G *= 100;
                var_B *= 100;

                x = var_R * 0.4124 + var_G * 0.3576 + var_B * 0.1805;
                y = var_R * 0.2126 + var_G * 0.7152 + var_B * 0.0722;
                z = var_R * 0.0193 + var_G * 0.1192 + var_B * 0.9505;
            }
        }

        public class CIELab
        {
            public Color StoredColor { get; set; }
            private double l;
            public double L { get { return l; } }
            private double a;
            public double A { get { return a; } }
            private double b;
            public double B { get { return b; } }

            public CIELab(Color color)
            {
                StoredColor = color;
                XYZ xyz = new XYZ(color.R, color.G, color.B);

                double var_X = xyz.X / 95.047f;
                double var_Y = xyz.Y / 100.000f;
                double var_Z = xyz.Z / 108.883;

                var_X = (var_X > 0.008856f) ? Math.Pow(var_X, (1.0f / 3.0f))
                                          : (var_X * 7.787f) + (16.0f / 116.0f);
                var_Y = (var_Y > 0.008856f) ? Math.Pow(var_Y, (1.0f / 3.0f))
                                          : (var_Y * 7.787f) + (16.0f / 116.0f);
                var_Z = (var_Z > 0.008856f) ? Math.Pow(var_Z, (1.0f / 3.0f))
                                          : (var_Z * 7.787f) + (16.0f / 116.0f);

                l = (116.0f * var_Y) - 16.0f;
                a = (500.0f * (var_X - var_Y));
                b = (200.0f * (var_Y - var_Z));
            }
        }
    }
}