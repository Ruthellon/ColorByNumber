using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Org.BouncyCastle.Math.EC.Multiplier;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
        public bool Clean { get; set; } = true;
        [BindProperty]
        public bool Resize { get; set; } = true;
        [BindProperty]
        public bool PdfOnly { get; set; } = false;
        [BindProperty]
        public double SimilarityDistance { get; set; } = 28.0f;
        [BindProperty]
        public int NormalizeFactor { get; set; } = 50;
        [BindProperty]
        public bool ShowDebug { get; set; } = false;
        [BindProperty]
        public string EmailAddress { get; set; } = String.Empty;
        public string ErrorMessage { get; set; } = String.Empty;

        public byte[] Original { get; set; }
        public byte[] NormalizedBytes { get; set; }
        public byte[] SoftenedBytes { get; set; }
        public byte[] PBCBytes { get; set; }
        public byte[] CleanedBytes { get; set; }
        public byte[] OutlineBytes { get; set; }
        [BindProperty]
        public byte[] PdfBytes { get; set; }

        public List<CIELab> TopColors { get; set; } = new List<CIELab>();

        private readonly ILogger<IndexModel> _logger;

        private readonly List<List<Point>> Numbers = new List<List<Point>>()
        {
            //0
            new List<Point>()
            {
                new Point(2, 0),
                new Point(2, -1),
                new Point(1, -2),
                new Point(0, -2),
                new Point(-1, -1),
                new Point(-1, 0),
                new Point(0, 1),
                new Point(1, 1)
            },
            //1
            new List<Point>()
            {
                new Point(0,0),
                new Point(0, -1),
                new Point(0, -2),
                new Point(-1, -1),
                new Point(0, 1),
                new Point(0, 2),
                new Point(1, 2),
                new Point(-1, 2)
            },
            //2
            new List<Point>()
            {
                new Point(0,0),
                new Point(1, -1),
                new Point(1, -2),
                new Point(0, -2),
                new Point(-1, -1),
                new Point(-1, 1),
                new Point(-1, 2),
                new Point(0, 2),
                new Point(1, 2)
            },
            //3
            new List<Point>()
            {
                new Point(0,0),
                new Point(1, -1),
                new Point(1, -2),
                new Point(0, -3),
                new Point(-1, -2),
                new Point(1, 1),
                new Point(1, 2),
                new Point(0, 3),
                new Point(-1, 2)
            },
            //4
            new List<Point>()
            {
                new Point(0,0),
                new Point(0, -1),
                new Point(0, -2),
                new Point(-1, -1),
                new Point(-2, 0),
                new Point(-1, 0),
                new Point(1, 0),
                new Point(0, 1),
                new Point(0, 2)
            },
            //5
            new List<Point>()
            {
                new Point(0,0),
                new Point(-1, 0),
                new Point(-1, -1),
                new Point(-1, -2),
                new Point(0, -2),
                new Point(1, -2),
                new Point(1, 1),
                new Point(1, 2),
                new Point(0, 3),
                new Point(-1, 2)
            },
            //6
            new List<Point>()
            {
                new Point(0,0),
                new Point(-1, 1),
                new Point(-1, 0),
                new Point(-1, -1),
                new Point(0, -2),
                new Point(1, -2),
                new Point(2, -1),
                new Point(1, 0),
                new Point(2, 1),
                new Point(2, 2),
                new Point(1, 3),
                new Point(0, 3),
                new Point(-1, 2)
            },
            //7
            new List<Point>()
            {
                new Point(0,0),
                new Point(1, -1),
                new Point(2, -2),
                new Point(1, -2),
                new Point(0, -2),
                new Point(-1, -2),
                new Point(-1, 1),
                new Point(-2, 2)
            },
            //8
            new List<Point>()
            {
                new Point(0,0),
                new Point(1, -1),
                new Point(1, -2),
                new Point(0, -3),
                new Point(-1, -3),
                new Point(-2, -2),
                new Point(-2, -1),
                new Point(-1, 0),
                new Point(1, 1),
                new Point(1, 2),
                new Point(0, 3),
                new Point(-1, 3),
                new Point(-2, 2),
                new Point(-2, 1)
            },
            //9
            new List<Point>()
            {
                new Point(0,0),
                new Point(1, -1),
                new Point(1, -2),
                new Point(0, -3),
                new Point(-1, -3),
                new Point(-2, -2),
                new Point(-2, -1),
                new Point(-1, 0),
                new Point(1, 1),
                new Point(1, 2),
                new Point(0, 3)
            }
        };

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public class Point
        {
            public bool Covered { get; set; } = false;
            public Rgba32 Color { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int RegionNumber { get; set; } = -1;

            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }

            public Point(Rgba32 color)
            {
                Color = color;
            }

            public Point(int x, int y, Rgba32 color)
            {
                X = x;
                Y = y;
                Color = color;
            }
        }

        //List<List<Point>> points = new List<List<Point>>();

        //List<List<Point>> regions = new List<List<Point>>();
        //List<Point> labelLocations = new List<Point>();

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
                        bool resizeToPdf = true;

                        using (var img = Image<Rgba32>.Load<Rgba32>(memoryStream.ToArray()))
                        {
                            Image<Rgba32> image = img;

                            if (Resize && (image.Height > 720 || image.Width > 720))
                            {
                                double multiplier = .5f;
                                if (image.Height > image.Width)
                                {
                                    multiplier = (720.0f / (double)image.Height);
                                }
                                else
                                {
                                    multiplier = (720.0f / (double)image.Width);
                                }
                                resizeToPdf = false;
                                image.Mutate(x => x.Resize(Convert.ToInt32(image.Width * multiplier), Convert.ToInt32(image.Height * multiplier)));
                            }

                            if (Normalize)
                            {
                                image = NormalizeImage(image, NormalizeFactor);

                                if (ShowDebug)
                                {
                                    using (var normalStream = new MemoryStream())
                                    {
                                        image.SaveAsPng(normalStream);

                                        NormalizedBytes = normalStream.ToArray();
                                    }
                                }
                            }

                            if (Soften)
                            {
                                image = SoftenImage(image);

                                if (ShowDebug)
                                {
                                    using (var softenedStream = new MemoryStream())
                                    {
                                        image.SaveAsPng(softenedStream);

                                        SoftenedBytes = softenedStream.ToArray();
                                    }
                                }
                            }

                            image = ProcessImage(image);

                            using (var processedStream = new MemoryStream())
                            {
                                image.SaveAsPng(processedStream);

                                PBCBytes = processedStream.ToArray();
                            }

                            if (Clean)
                            {
                                image = CleanImage(image);

                                if (ShowDebug)
                                {
                                    using (var cleanedStream = new MemoryStream())
                                    {
                                        image.SaveAsPng(cleanedStream);

                                        CleanedBytes = cleanedStream.ToArray();
                                    }
                                }
                            }

                            //GetRegions(image);

                            var outline = GetOutlines(image);

                            using (var outlineStream = new MemoryStream())
                            {
                                outline.Mutate(x => x.BackgroundColor(Color.White));
                                outline.SaveAsPng(outlineStream);

                                OutlineBytes = outlineStream.ToArray();
                            }

                            using (var resizeStream = new MemoryStream())
                            {
                                if (resizeToPdf)
                                {
                                    if (outline.Height > outline.Width)
                                    {
                                        double multiplier = (720.0f / (double)outline.Height);
                                        outline.Mutate(x => x.Resize(Convert.ToInt32(outline.Width * multiplier), Convert.ToInt32(outline.Height * multiplier)));
                                    }
                                    else
                                    {
                                        double multiplier = (720.0f / (double)outline.Width);
                                        outline.Mutate(x => x.Resize(Convert.ToInt32(outline.Width * multiplier), Convert.ToInt32(outline.Height * multiplier)));
                                        outline.Mutate(y => y.Rotate(90.0f));
                                    }
                                }

                                outline.SaveAsPng(resizeStream);

                                PdfDocument document = new PdfDocument();
                                PdfPage pageColors = document.AddPage();
                                XGraphics gfxColors = XGraphics.FromPdfPage(pageColors);
                                for(int i = 0; i < TopColors.Count; i++)
                                {
                                    gfxColors.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, TopColors[i].StoredColor.R, TopColors[i].StoredColor.G, TopColors[i].StoredColor.B)), new XRect(65, 45 + (i * 20), 150, 10));
                                    gfxColors.DrawString((i + 1).ToString(), new XFont("Verdana", 12), new XSolidBrush(XColor.FromArgb(255, 0, 0, 0)), 45, 55 + (20 * i));
                                }

                                PdfPage page = document.AddPage();
                                XGraphics gfx = XGraphics.FromPdfPage(page);
                                XImage xImage = XImage.FromStream(() => new MemoryStream(resizeStream.ToArray()));
                                gfx.DrawImage(xImage, 45, 45);

                                using (var pdfStream = new MemoryStream())
                                {
                                    document.Save(pdfStream);

                                    PdfBytes = pdfStream.ToArray();

                                    //if (!String.IsNullOrEmpty(EmailAddress))
                                    //{
                                    //    Email email = new Email();

                                    //    string Body = @"<html>
                                    //    <body>
                                    //    {0}
                                    //    </body>
                                    //    </html>";

                                    //    email.Subject = "PBN from Angry Elf Games";

                                    //    Body = string.Format(Body, "You have a new PBN curtesy of Angry Elf Games");

                                    //    email.Body = Body;
                                    //    var mimepart = new MimePart("application/pdf");
                                    //    mimepart.Content = new MimeContent(new MemoryStream(PdfBytes));
                                    //    mimepart.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment);
                                    //    mimepart.ContentTransferEncoding = ContentEncoding.Base64;
                                    //    mimepart.FileName = FormFile.FileName;

                                    //    email.Attachment = mimepart;
                                    //    email.Sender = new KeyValuePair<string, string>("Angry Elf Games", "angryelfstudios@gmail.com");
                                    //    email.ReplyTo = new KeyValuePair<string, string>("Angry Elf Games", "angryelfstudios@dynamictrend.com");
                                    //    email.To.Add(new KeyValuePair<string, string>(EmailAddress, EmailAddress));

                                    //    await email.Send();
                                    //}
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }

            if (PdfOnly)
                return File(PdfBytes, "application/pdf");

            return Page();
        }

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

        private Image<Rgba32> CleanImage(Image<Rgba32> image)
        {
            Image<Rgba32> cleaned = image;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Dictionary<Rgba32, int> colors = new Dictionary<Rgba32, int>();

                    for (int b = Math.Max(y - 1, 0); b <= Math.Min(y + 1, image.Height - 1); b++)
                    {
                        for (int a = Math.Max(x - 1, 0); a <= Math.Min(x + 1, image.Width - 1); a++)
                        {
                            if (colors.ContainsKey(image[a, b]))
                                colors[image[a, b]]++;
                            else
                                colors.Add(image[a, b], 1);
                        }
                    }
                    int currentColorCount = colors.Where(z => z.Key == image[x, y]).FirstOrDefault().Value;
                    if (currentColorCount < 4)
                    {
                        cleaned[x, y] = colors.OrderByDescending(z => z.Value).FirstOrDefault().Key;
                    }
                    else if (currentColorCount < 9)
                    {
                        colors.Clear();
                        for (int b = Math.Max(y - 2, 0); b <= Math.Min(y + 2, image.Height - 1); b++)
                        {
                            for (int a = Math.Max(x - 2, 0); a <= Math.Min(x + 2, image.Width - 1); a++)
                            {
                                if (colors.ContainsKey(image[a, b]))
                                    colors[image[a, b]]++;
                                else
                                    colors.Add(image[a, b], 1);
                            }
                        }

                        currentColorCount = colors.Where(z => z.Key == image[x, y]).FirstOrDefault().Value;
                        if (currentColorCount < 9)
                        {
                            cleaned[x, y] = colors.OrderByDescending(z => z.Value).Where(zz => zz.Key != image[x, y]).FirstOrDefault().Key;
                        }
                    }
                }
            }

            return cleaned;
        }

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

        //private void GetRegions(Image<Rgba32> image)
        //{
        //    points = new List<List<Point>>();
        //    for (int y = 0; y < image.Height; y++)
        //    {
        //        List<Point> row = new List<Point>();
        //        for (int x = 0; x < image.Height; x++)
        //        {
        //            row.Add(new Point(x,y,image[x,y]));
        //        }

        //        points.Add(row);
        //    }

        //    int regionCount = 0;

        //    List<List<Point>> regions = new List<List<Point>>();
        //    try
        //    {
        //        for (int y = 0; y < points.Count; y++)
        //        {
        //            for (int x = 0; x < points[y].Count; x++)
        //            {
        //                if (!points[y][x].Covered)
        //                {
        //                    regionCount++;
        //                    Rgba32 regionColor = points[y][x].Color;
        //                    Queue<Point> queue = new Queue<Point>();
        //                    List<Point> region = new List<Point>();
        //                    queue.Enqueue(points[y][x]);
        //                    while (queue.Count > 0)
        //                    {
        //                        var coord = queue.Dequeue();
        //                        if (coord.Covered == false && coord.Color == regionColor)
        //                        {
        //                            region.Add(coord);
        //                            coord.Covered = true;
        //                            coord.RegionNumber = regionCount;
        //                            if (coord.X > 0)
        //                                queue.Enqueue(points[coord.X - 1][coord.Y]);
        //                            if (coord.X < points[y].Count - 1)
        //                                queue.Enqueue(points[coord.X + 1][coord.Y]);
        //                            if (coord.Y > 0)
        //                                queue.Enqueue(points[coord.X][coord.Y - 1]);
        //                            if (coord.Y < points.Count - 1)
        //                                queue.Enqueue(points[coord.X][coord.Y + 1]);
        //                        }
        //                    }

        //                    if (region.Count < 10)
        //                    {

        //                    }
        //                    else
        //                    {
        //                        regions.Add(region);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {

        //    }
        //}

        private Image<Rgba32> GetOutlines(Image<Rgba32> image)
        {
            Image<Rgba32> outline = new Image<Rgba32>(image.Width, image.Height);
            int previousX = 0;
            int previousY = 0;

            for (int y = 1; y < image.Height; y++)
            {
                for (int x = 1; x < image.Width; x++)
                {
                    if (image[x, y] != image[x - 1, y])
                    {
                        outline[x, y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)128);
                    }

                    if (image[x, y] != image[x, y - 1])
                    {
                        outline[x, y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)128);
                    }

                    if (x > 3 && y > 3 && x < image.Width - 3 && y < image.Height - 3 && (x > (previousX + 10) || y > (previousY + 10)))
                    {
                        previousX = x;
                        previousY = y;
                        Rgba32 currentColor = image[x, y];
                        bool same = true;
                        for (int b = Math.Max(y - 2, 0); b <= Math.Min(y + 2, image.Height - 1); b++)
                        {
                            for (int a = Math.Max(x - 2, 0); a <= Math.Min(x + 2, image.Width - 1); a++)
                            {
                                if (image[a, b] != currentColor)
                                {
                                    same = false;
                                    break;
                                }
                            }

                            if (!same)
                                break;
                        }

                        if (same)
                        {
                            for (int i = 0; i < TopColors.Count; i++)
                            {
                                if (currentColor == TopColors[i].StoredColor)
                                {
                                    i++;
                                    if (i < 10)
                                    {
                                        foreach (var point in Numbers[i])
                                        {
                                            outline[point.X + x, point.Y + y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)64);
                                        }
                                    }
                                    else if (i >= 20)
                                    {
                                        foreach (var point in Numbers[2])
                                        {
                                            outline[point.X + x - 1, point.Y + y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)64);
                                        }

                                        foreach (var point in Numbers[i - 20])
                                        {
                                            outline[point.X + x + 1, point.Y + y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)64);
                                        }
                                    }
                                    else if (i >= 10)
                                    {
                                        foreach (var point in Numbers[1])
                                        {
                                            outline[point.X + x - 1, point.Y + y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)64);
                                        }

                                        foreach (var point in Numbers[i - 10])
                                        {
                                            outline[point.X + x + 1, point.Y + y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)64);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return outline;
        }

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