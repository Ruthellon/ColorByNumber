using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ColorByNumber.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public IFormFile FormFile { get; set; }

        [BindProperty]
        public bool Normalize { get; set; } = false;
        [BindProperty]
        public bool Soften { get; set; } = false;
        [BindProperty]
        public bool Clean { get; set; } = false;
        [BindProperty]
        public double SimilarityDistance { get; set; } = 28.0f;
        [BindProperty]
        public int NormalizeFactor { get; set; } = 50;

        public byte[] Original { get; set; }
        public byte[] NormalizedBytes { get; set; }
        public byte[] SoftenedBytes { get; set; }
        public byte[] PBCBytes { get; set; }
        public byte[] CleanedBytes { get; set; }
        public byte[] OutlineBytes { get; set; }

        public List<CIELab> TopColors { get; set; } = new List<CIELab>();

        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostUpload()
        {
            try
            {
                if (FormFile != null && FormFile.Length > 0)
                {
                    var filePath = Path.GetFullPath(FormFile.FileName);

                    using (var memoryStream = new MemoryStream())
                    {
                        await FormFile.CopyToAsync(memoryStream);

                        Original = memoryStream.ToArray();

                        using (var img = Image<Rgba32>.Load<Rgba32>(memoryStream.ToArray()))
                        {
                            Image<Rgba32> image = img;
                            if (Normalize)
                            {
                                image = NormalizeImage(image, NormalizeFactor);

                                using (var normalStream = new MemoryStream())
                                {
                                    image.SaveAsPng(normalStream);

                                    NormalizedBytes = normalStream.ToArray();
                                }
                            }

                            if (Soften)
                            {
                                image = SoftenImage(image);

                                using (var softenedStream = new MemoryStream())
                                {
                                    image.SaveAsPng(softenedStream);

                                    SoftenedBytes = softenedStream.ToArray();
                                }
                            }

                            image = ProcessImage(image);

                            using (var processedStream = new MemoryStream())
                            {
                                image.SaveAsPng(processedStream);

                                PBCBytes = processedStream.ToArray();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }
            return Page();
        }

        //                using (var img = Image.FromStream(memoryStream))
        //                {
        //                    Original = (byte[])new ImageConverter().ConvertTo(img, typeof(byte[]));

        //                    //Bitmap image = new Bitmap(img, new Size(img.Width / 2, img.Height / 2));
        //                    WorkingImage = new Bitmap(img); //new Bitmap(img, new Size(img.Width / 2, img.Height / 2));

        //                    if (Normalize)
        //                    {
        //                        NormalizeImage();
        //                        NormalizedBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));
        //                    }

        //                    if (Soften)
        //                    {
        //                        SoftenImage();
        //                        SoftenedBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));
        //                    }

        //                    ProcessImage();
        //                    PBCBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));

        //                    if (Clean)
        //                    {
        //                        CleanImage();
        //                        CleanedBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));
        //                    }

        //                    GetOutlines();
        //                    //OutlineBytes = (byte[])new ImageConverter().ConvertTo(WorkingImage, typeof(byte[]));

        //                    //WorkingImage = new Bitmap(WorkingImage, new Size(WorkingImage.Width * 2, WorkingImage.Height * 2));

        //                    Bitmap bmp = new Bitmap(WorkingImage.Width, WorkingImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        //                    using (Graphics g = Graphics.FromImage(bmp))
        //                    {
        //                        g.Clear(Color.White);
        //                        g.DrawImage(WorkingImage, new Rectangle(new Point(), WorkingImage.Size), new Rectangle(new Point(), WorkingImage.Size), GraphicsUnit.Pixel);
        //                    }
        //                    OutlineBytes = (byte[])new ImageConverter().ConvertTo(bmp, typeof(byte[]));
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //    }

        //    return Page();
        //}

        private Image<Rgba32> SoftenImage(Image<Rgba32> image)
        {
            Image<Rgba32> softened = image;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    List<Rgba32> colors = new List<Rgba32>();

                    for (int b = Math.Max(y - 1, 0); b <= Math.Min(y + 1, image.Height - 1); b++)
                    {
                        for (int a = Math.Max(x - 1, 0); a <= Math.Min(x + 1, image.Width - 1); a++)
                        {
                            colors.Add(image[a, b]);
                        }
                    }

                    int red = Convert.ToInt32(Math.Round((double)colors.Sum(x => x.R) / (double)colors.Count));
                    int green = Convert.ToInt32(Math.Round((double)colors.Sum(y => y.G) / (double)colors.Count));
                    int blue = Convert.ToInt32(Math.Round((double)colors.Sum(z => z.B) / (double)colors.Count));


                    softened[x, y] = new Rgba32((byte)red, (byte)green, (byte)blue);
                }
            }
            return softened;
        }

        private Image<Rgba32> NormalizeImage(Image<Rgba32> image, int factor)
        {
            Image<Rgba32> normalized = image;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    normalized[x, y] = NormalizeColor(image[x, y], factor);
                }
            }

            return normalized;
        }

        //private void CleanImage()
        //{
        //    for (int y = 0; y < WorkingImage.Height; y++)
        //    {
        //        for (int x = 0; x < WorkingImage.Width; x++)
        //        {
        //            Dictionary<Color, int> colors = new Dictionary<Color, int>();

        //            for (int b = Math.Max(y - 1, 0); b <= Math.Min(y + 1, WorkingImage.Height - 1); b++)
        //            {
        //                for (int a = Math.Max(x - 1, 0); a <= Math.Min(x + 1, WorkingImage.Width - 1); a++)
        //                {
        //                    if (colors.ContainsKey(WorkingImage.GetPixel(a, b)))
        //                        colors[WorkingImage.GetPixel(a, b)]++;
        //                    else
        //                        colors.Add(WorkingImage.GetPixel(a, b), 1);
        //                }
        //            }
        //            int currentColorCount = colors.Where(z => z.Key == WorkingImage.GetPixel(x, y)).FirstOrDefault().Value;
        //            if (currentColorCount < 4)
        //            {
        //                WorkingImage.SetPixel(x, y, colors.OrderByDescending(z => z.Value).FirstOrDefault().Key);
        //            }
        //            else if (currentColorCount < 9)
        //            {
        //                colors.Clear();
        //                for (int b = Math.Max(y - 2, 0); b <= Math.Min(y + 2, WorkingImage.Height - 1); b++)
        //                {
        //                    for (int a = Math.Max(x - 2, 0); a <= Math.Min(x + 2, WorkingImage.Width - 1); a++)
        //                    {
        //                        if (colors.ContainsKey(WorkingImage.GetPixel(a, b)))
        //                            colors[WorkingImage.GetPixel(a, b)]++;
        //                        else
        //                            colors.Add(WorkingImage.GetPixel(a, b), 1);
        //                    }
        //                }

        //                currentColorCount = colors.Where(z => z.Key == WorkingImage.GetPixel(x, y)).FirstOrDefault().Value;
        //                if (currentColorCount < 9)
        //                {
        //                    WorkingImage.SetPixel(x, y, colors.OrderByDescending(z => z.Value).Where(zz => zz.Key != WorkingImage.GetPixel(x, y)).FirstOrDefault().Key);
        //                }
        //            }
        //        }
        //    }
        //}

        private Image<Rgba32> ProcessImage(Image<Rgba32> image)
        {
            Image<Rgba32> processedImage = image;
            Dictionary<Rgba32, int> colorCount = new Dictionary<Rgba32, int>();
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixelColor = image[x, y];

                    if (colorCount.ContainsKey(pixelColor))
                        colorCount[pixelColor]++;
                    else
                        colorCount.Add(pixelColor, 1);
                }
            }

            var top = colorCount.OrderByDescending(a => a.Value).Select(b => b.Key).ToList();

            foreach (var color in top)
            {
                bool found = false;
                CIELab labColor = new CIELab(color);
                for (int i = 0; i < TopColors.Count; i++)
                {
                    double distance = DistanceBetweenColors(labColor, TopColors[i]);
                    if (distance < SimilarityDistance)
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

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixelColor = image[x, y];

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

                    processedImage[x, y] = TopColors[index].StoredColor;
                }
            }

            return processedImage;
        }

        //private void GetOutlines()
        //{
        //    Bitmap outline = new Bitmap(WorkingImage);

        //    for (int y = 0; y < WorkingImage.Height; y++)
        //    {
        //        for (int x = 1; x < WorkingImage.Width; x++)
        //        {
        //            Color color = WorkingImage.GetPixel(x, y);
        //            outline.SetPixel(x, y, Color.FromArgb(32, color.R, color.G, color.B));
        //        }
        //    }

        //    for (int y = 0; y < WorkingImage.Height; y++)
        //    {
        //        for (int x = 1; x < WorkingImage.Width; x++)
        //        {
        //            if (WorkingImage.GetPixel(x, y) != WorkingImage.GetPixel(x - 1, y))
        //            {
        //                outline.SetPixel(x, y, Color.Black);
        //                x++;
        //                continue;
        //            }
        //        }
        //    }

        //    for (int x = 0; x < WorkingImage.Width; x++)
        //    {
        //        for (int y = 1; y < WorkingImage.Height; y++)
        //        {
        //            if (WorkingImage.GetPixel(x, y) != WorkingImage.GetPixel(x, y - 1))
        //            {
        //                outline.SetPixel(x, y, Color.Black);
        //                y++;
        //                continue;
        //            }
        //        }
        //    }

        //    WorkingImage = outline;
        //}

        private Rgba32 NormalizeColor(Rgba32 pixel, double factor)
        {
            int red = pixel.R;
            int green = pixel.G;
            int blue = pixel.B;

            red = Convert.ToInt32(Math.Round(((double)red / factor)) * factor);
            green = Convert.ToInt32(Math.Round(((double)green / factor)) * factor);
            blue = Convert.ToInt32(Math.Round(((double)blue / factor)) * factor);

            Rgba32 normalizedColor = new Rgba32((byte)Math.Min(255, red), (byte)Math.Min(255, green), (byte)Math.Min(255, blue));

            return normalizedColor;
        }

        private double DistanceBetweenColors(CIELab pixel1, CIELab pixel2)
        {
            return Math.Sqrt(Math.Pow(pixel1.L - pixel2.L, 2.0f) + Math.Pow(pixel1.A - pixel2.A, 2.0f) + Math.Pow(pixel1.B - pixel2.B, 2.0f));
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
            public Rgba32 StoredColor { get; set; }
            public XYZ StoredXYZ { get; set; }
            private double l;
            public double L { get { return l; } }
            private double a;
            public double A { get { return a; } }
            private double b;
            public double B { get { return b; } }

            public CIELab(Rgba32 color)
            {
                StoredColor = color;
                StoredXYZ = new XYZ(color.R, color.G, color.B);

                double var_X = StoredXYZ.X / 95.047f;
                double var_Y = StoredXYZ.Y / 100.000f;
                double var_Z = StoredXYZ.Z / 108.883;

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