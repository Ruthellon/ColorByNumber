using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MimeKit.IO.Filters;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;

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
        public bool QuickNumbering { get; set; } = false;
        [BindProperty]
        public bool Resize { get; set; } = true;
        [BindProperty]
        public bool KeepAspect { get; set; } = false;
        [BindProperty]
        public bool PdfOnly { get; set; } = false;
        private double similarityDistance = 20.0f;
        [BindProperty]
        public double SimilarityDistance
        {
            get
            {
                return similarityDistance;
            }
            set
            {
                if (value > 100)
                    similarityDistance = 100;
                else if (value < 0)
                    similarityDistance = 0;
                else
                    similarityDistance = value;
            }
        }
        private int normalizeFactor = 5;
        [BindProperty]
        public int NormalizeFactor
        {
            get
            {
                return normalizeFactor;
            }
            set
            {
                if (value > 255)
                    normalizeFactor = 255;
                else if (value < 0)
                    normalizeFactor = 0;
                else
                    normalizeFactor = value;
            }
        }
        private int outlineDarkness = 64;
        [BindProperty]
        public int OutlineDarkness
        { 
            get
            {
                return outlineDarkness;
            }
            set
            {
                if (value > 255)
                    outlineDarkness = 255;
                else if (value < 0)
                    outlineDarkness = 0;
                else
                    outlineDarkness = value;
            }
        }
        [BindProperty]
        public int ColorCount { get; set; } = 35;
        [BindProperty]
        public bool ShowDebug { get; set; } = false;
        public string ErrorMessage { get; set; } = String.Empty;

        [BindProperty]
        public string OriginalImage { get; set; }
        [BindProperty]
        public string PdfDocument { get; set; }
        [BindProperty]
        public string FileName { get; set; }

        public byte[] Original { get; set; }
        public byte[] NormalizedBytes { get; set; }
        public byte[] SoftenedBytes { get; set; }
        public byte[] PBCBytes { get; set; }
        public byte[] CleanedBytes { get; set; }
        public byte[] OutlineBytes { get; set; }

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

        public FileResult OnPostDownload()
        {
            return File(Convert.FromBase64String(PdfDocument), "application/pdf");
        }

        public class Point
        {
            public int X { get; set; }
            public int Y { get; set; }

            public Point()
            {

            }

            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        public class RegionData : Point
        {
            public bool Covered { get; set; } = false;
            public Rgba32 Color { get; set; }

            public RegionData(int x, int y, Rgba32 color)
            {
                X = x;
                Y = y;
                Color = color;
            }
        }

        public async Task<IActionResult> OnPostUpload()
        {
            try
            {
                var watch = new System.Diagnostics.Stopwatch();
                Image<Rgba32> image;
                if (FormFile == null || FormFile.Length == 0)
                {
                    if (String.IsNullOrEmpty(OriginalImage))
                        return Page();

                    Original = Convert.FromBase64String(OriginalImage);

                    image = Image<Rgba32>.Load<Rgba32>(Original);
                }
                else
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await FormFile.CopyToAsync(memoryStream);

                        using (var img = Image<Rgba32>.Load<Rgba32>(memoryStream.ToArray()))
                        {
                            img.Mutate(x => x.AutoOrient());
                            image = img.Clone();
                        }

                        using (var originalStream = new MemoryStream())
                        {
                            image.SaveAsPng(originalStream);

                            Original = originalStream.ToArray();
                            OriginalImage = Convert.ToBase64String(Original);
                        }
                    }

                    int indexOf = FormFile.FileName.LastIndexOf('.');
                    FileName = "pbn_" + FormFile.FileName.Remove(indexOf, FormFile.FileName.Length-indexOf)  + ".pdf";
                }

                if (Resize)
                {
                    if (image.Height >= image.Width)
                    {
                        if (!KeepAspect)
                        {
                            image.Mutate(x => x.Resize(816, 1056));
                        }
                        else
                        {
                            float multi = 1056.0f / (float)image.Height;
                            image.Mutate(x => x.Resize(Convert.ToInt32(multi * image.Width), 1056));
                        }
                    }
                    else
                    {
                        if (!KeepAspect)
                        {
                            image.Mutate(x => x.Resize(1056, 816));
                        }
                        else
                        {
                            float multi = 1056.0f / (float)image.Width;
                            image.Mutate(x => x.Resize(1056, Convert.ToInt32(multi * image.Height)));
                        }
                    }
                }

                ErrorMessage += $"DPI: {image.Metadata.HorizontalResolution} x {image.Metadata.VerticalResolution} ";

                if (Normalize)
                {
                    watch.Start();
                    image = await Task.Factory.StartNew(() => NormalizeImage(image, NormalizeFactor));
                    watch.Stop();
                    ErrorMessage += ":: Normalize: " + watch.ElapsedMilliseconds.ToString() + "ms ";
                    watch.Reset();

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
                    image = await Task.Factory.StartNew(() => SoftenImage(image));

                    if (ShowDebug)
                    {
                        using (var softenedStream = new MemoryStream())
                        {
                            image.SaveAsPng(softenedStream);

                            SoftenedBytes = softenedStream.ToArray();
                        }
                    }
                }

                watch.Start();
                image = await Task.Factory.StartNew(() => ProcessImage(image));
                watch.Stop();
                ErrorMessage += ":: Process: " + watch.ElapsedMilliseconds.ToString() + "ms ";
                watch.Reset();

                if (!Clean || ShowDebug)
                {
                    using (var processedStream = new MemoryStream())
                    {
                        image.SaveAsPng(processedStream);

                        PBCBytes = processedStream.ToArray();
                    }
                }

                if (Clean)
                {
                    watch.Start();
                    image = await Task.Factory.StartNew(() => CleanImage(image));
                    watch.Stop();
                    ErrorMessage += ":: Clean: " + watch.ElapsedMilliseconds.ToString() + "ms ";
                    watch.Reset();

                    using (var cleanedStream = new MemoryStream())
                    {
                        image.SaveAsPng(cleanedStream);

                        CleanedBytes = cleanedStream.ToArray();
                    }
                }

                watch.Start();
                var outline = await Task.Factory.StartNew(() => GetOutlines(image));
                watch.Stop();
                ErrorMessage += ":: Outline: " + watch.ElapsedMilliseconds.ToString() + "ms ";
                watch.Reset();

                if (!QuickNumbering)
                {
                    watch.Start();
                    outline = await Task.Factory.StartNew(() => GetRegions(image, outline));
                    watch.Stop();
                    ErrorMessage += ":: Regions: " + watch.ElapsedMilliseconds.ToString() + "ms ";
                    watch.Reset();
                }

                using (var outlineStream = new MemoryStream())
                {
                    outline.Mutate(x => x.BackgroundColor(Color.White));
                    outline.SaveAsPng(outlineStream);

                    OutlineBytes = outlineStream.ToArray();
                }

                if (Resize)
                {
                    if (outline.Width > outline.Height)
                    {
                        outline.Mutate(y => y.Rotate(90.0f));
                        image.Mutate(y => y.Rotate(90.0f));
                    }
                }
                else
                {
                    if (image.Height >= image.Width)
                    {
                        if (!KeepAspect)
                        {
                            image.Mutate(x => x.Resize(816, 1056));
                            outline.Mutate(x => x.Resize(816, 1056));
                        }
                        else
                        {
                            float multi = 1056.0f / (float)image.Height;
                            image.Mutate(x => x.Resize(Convert.ToInt32(multi * image.Width), 1056));
                            outline.Mutate(x => x.Resize(Convert.ToInt32(multi * image.Width), 1056));
                        }
                    }
                    else
                    {
                        if (!KeepAspect)
                        {
                            image.Mutate(x => x.Resize(1056, 816));
                            outline.Mutate(x => x.Resize(1056, 816));
                        }
                        else
                        {
                            float multi = 1056.0f / (float)image.Width;
                            image.Mutate(x => x.Resize(1056, Convert.ToInt32(multi * image.Height)));
                            outline.Mutate(x => x.Resize(1056, Convert.ToInt32(multi * image.Height)));
                        }

                        outline.Mutate(y => y.Rotate(90.0f));
                        image.Mutate(y => y.Rotate(90.0f));
                    }
                }

                byte[] resizedOutline = null;
                byte[] resizedPBN = null;
                using (var resizeStream = new MemoryStream())
                {
                    outline.SaveAsPng(resizeStream);
                    var meta = outline.Metadata;
                    resizedOutline = resizeStream.ToArray();
                }

                using (var resizeStream = new MemoryStream())
                {
                    image.SaveAsPng(resizeStream);
                    resizedPBN = resizeStream.ToArray();
                }

                PdfDocument document = new PdfDocument();
                PdfPage pageColors = document.AddPage();
                XGraphics gfxColors = XGraphics.FromPdfPage(pageColors);
                for (int i = 0; i < TopColors.Count; i++)
                {
                    int x = 45 + (250 * (i / 35));
                    gfxColors.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, TopColors[i].StoredColor.R, TopColors[i].StoredColor.G, TopColors[i].StoredColor.B)), new XRect(x + 20, 45 + ((i % 35) * 20), 150, 10));
                    gfxColors.DrawString((i + 1).ToString(), new XFont("Verdana", 12), new XSolidBrush(XColor.FromArgb(255, 0, 0, 0)), x, 55 + (20 * (i % 35)));
                }

                PdfPage page = document.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);
                XImage xImage = XImage.FromStream(() => new MemoryStream(resizedOutline));

                if (KeepAspect)
                    gfx.DrawImage(xImage, (gfx.PageSize.Width - xImage.Size.Width) >= 100 ? 50 : 0, 0, xImage.Size.Width, gfx.PageSize.Height);
                else
                    gfx.DrawImage(xImage, 0, 0, gfx.PageSize.Width, gfx.PageSize.Height);

                PdfPage imagePage = document.AddPage();
                XGraphics imagegfx = XGraphics.FromPdfPage(imagePage);
                XImage imagexImage = XImage.FromStream(() => new MemoryStream(resizedPBN));

                if (KeepAspect)
                    imagegfx.DrawImage(imagexImage, (gfx.PageSize.Width - xImage.Size.Width) >= 100 ? 50 : 0, 0, imagexImage.Size.Width, gfx.PageSize.Height);
                else
                    imagegfx.DrawImage(imagexImage, 0, 0, imagegfx.PageSize.Width, imagegfx.PageSize.Height);

                using (var pdfStream = new MemoryStream())
                {
                    document.Save(pdfStream);

                    PdfDocument = Convert.ToBase64String(pdfStream.ToArray());
                }
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }

            return Page();
        }

        private Image<Rgba32> SoftenImage(Image<Rgba32> image)
        {
            Image<Rgba32> softened = new Image<Rgba32>(image.Width, image.Height);
            Parallel.For(0, image.Height, y =>
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
            });
            return softened;
        }

        private Image<Rgba32> NormalizeImage(Image<Rgba32> image, int factor)
        {
            Image<Rgba32> normalized = new Image<Rgba32>(image.Width, image.Height);
            Parallel.For(0, image.Height, y =>
            {
                for (int x = 0; x < image.Width; x++)
                {
                    normalized[x, y] = NormalizeColor(image[x, y], factor);
                }
            });

            return normalized;
        }

        private Image<Rgba32> CleanImage(Image<Rgba32> image)
        {
            Image<Rgba32> cleaned = new Image<Rgba32>(image.Width, image.Height);
            Parallel.For(0, image.Height, y =>
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
                    else
                    {
                        cleaned[x, y] = image[x,y];
                    }
                }
            });

            return cleaned;
        }

        private Image<Rgba32> ProcessImage(Image<Rgba32> image)
        {
            Image<Rgba32> processedImage = new Image<Rgba32>(image.Width, image.Height);
            CIELab[] pixels = new CIELab[image.Width * image.Height];
            ConcurrentDictionary<Rgba32, int> colorCount = new ConcurrentDictionary<Rgba32, int>();
            Parallel.For(0, image.Height, y =>
            {
                for (int x = 0; x < image.Width; x++)
                {
                    CIELab pixel = new CIELab(image[x, y]);
                    pixels[x + (y * image.Width)] = pixel;

                    colorCount.AddOrUpdate(image[x, y], 1, (key, oldValue) => oldValue + 1);
                }
            });

            var colorDesc = colorCount.OrderByDescending(a => a.Value).ToList();
            foreach (var color in colorCount.OrderByDescending(a => a.Value).Select(b => b.Key))
            {
                bool found = false;
                CIELab pixel = new CIELab(color);
                for (int i = 0; i < TopColors.Count; i++)
                {
                    if (DistanceBetweenColors(pixel, TopColors[i]) < SimilarityDistance)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    TopColors.Add(pixel);
            }

            if (TopColors.Count > ColorCount)
                TopColors.RemoveRange(ColorCount, TopColors.Count - ColorCount);

            Parallel.For(0, image.Height, y =>
            {
                for (int x = 0; x < image.Width; x++)
                {
                    double smallestDistance = Double.MaxValue;
                    int index = 0;
                    int pixelLocation = x + (y * image.Width);
                    for (int j = 0; j < TopColors.Count; j++)
                    {
                        double distance = DistanceBetweenColors(pixels[pixelLocation], TopColors[j]);
                        if (distance < smallestDistance)
                        {
                            smallestDistance = distance;
                            index = j;
                        }
                    }

                    processedImage[x, y] = TopColors[index].StoredColor;
                }
            });

            return processedImage;
        }

        private Image<Rgba32> GetOutlines(Image<Rgba32> image)
        {
            Image<Rgba32> outline = new Image<Rgba32>(image.Width, image.Height);

            Parallel.For(0, image.Height, y =>
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (x > 0 && image[x, y] != image[x - 1, y])
                    {
                        outline[x, y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)OutlineDarkness);
                    }

                    if (y > 0 && image[x, y] != image[x, y - 1])
                    {
                        outline[x, y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)OutlineDarkness);
                    }
                }
            });

            return outline;
        }

        private Image<Rgba32> GetRegions(Image<Rgba32> image, Image<Rgba32> outline)
        {
            RegionData[,] points = new RegionData[image.Height, image.Width];
            Parallel.For(0, image.Height, y =>
            {
                for (int x = 0; x < image.Width; x++)
                {
                    points[y, x] = new RegionData(x, y, image[x, y]);
                }
            });

            Parallel.ForEach(TopColors, color =>
            {
                Rgba32 regionColor = color.StoredColor;

                for (int y = 0; y < points.GetLength(0); y++)
                {
                    for (int x = 0; x < points.GetLength(1); x++)
                    {
                        if (!points[y, x].Covered && points[y, x].Color == regionColor)
                        {
                            Queue<RegionData> queue = new Queue<RegionData>();
                            List<RegionData> region = new List<RegionData>();
                            queue.Enqueue(points[y, x]);
                            while (queue.Count > 0)
                            {
                                var coord = queue.Dequeue();
                                if (coord.Covered == false && coord.Color == regionColor)
                                {
                                    region.Add(coord);
                                    coord.Covered = true;
                                    if (coord.X > 0)
                                        queue.Enqueue(points[coord.Y, coord.X - 1]);
                                    if (coord.X < points.GetLength(1) - 1)
                                        queue.Enqueue(points[coord.Y, coord.X + 1]);
                                    if (coord.Y > 0)
                                        queue.Enqueue(points[coord.Y - 1, coord.X]);
                                    if (coord.Y < points.GetLength(0) - 1)
                                        queue.Enqueue(points[coord.Y + 1, coord.X]);
                                }
                            }

                            if (region.Count > 10)
                            {
                                Point labelLocation = GetLabelLocation(points, region);

                                if (labelLocation == null || labelLocation.Y < 3 || labelLocation.Y >= points.GetLength(0) - 4 || labelLocation.X < 3 || labelLocation.X >= points.GetLength(1) - 4)
                                    continue;

                                for (int i = 0; i < TopColors.Count; i++)
                                {
                                    if (region[0].Color == TopColors[i].StoredColor)
                                    {
                                        i++;
                                        if (i < 10)
                                        {
                                            Parallel.ForEach(Numbers[i], point =>
                                            {
                                                outline[point.X + labelLocation.X, point.Y + labelLocation.Y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)OutlineDarkness);
                                            });
                                        }
                                        else
                                        {
                                            int tens = i / 10;
                                            Parallel.ForEach(Numbers[tens], point =>
                                            {
                                                outline[point.X + labelLocation.X - 1, point.Y + labelLocation.Y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)OutlineDarkness);
                                            });

                                            Parallel.ForEach(Numbers[i - (tens * 10)], point =>
                                            {
                                                outline[point.X + labelLocation.X + 1, point.Y + labelLocation.Y] = new Rgba32((byte)0, (byte)0, (byte)0, (byte)OutlineDarkness);
                                            });
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            });

            return outline;
        }

        private Point GetLabelLocation(RegionData[,] points, List<RegionData> region)
        {
            Point bestPoint = null;
            int best = 0;
            object bestLock = new object();

            Parallel.ForEach(region, point =>
            {
                int goodness = SameCount(point, points, -1, 0) *
                    SameCount(point, points, 1, 0) *
                    SameCount(point, points, 0, -1) *
                    SameCount(point, points, 0, 1);

                lock (bestLock)
                {
                    if (goodness > best)
                    {
                        best = goodness;
                        bestPoint = new Point(point.X, point.Y);
                    }
                }
            });
            return bestPoint;
        }

        private int SameCount (RegionData regionPoint, RegionData[,] points, int incX, int incY)
        {
            int count = -1;
            int x = regionPoint.X;
            int y = regionPoint.Y;

            while (y >= 0 && y < points.GetLength(0) && x >= 0 && x < points.GetLength(1) && regionPoint.Color == points[y,x].Color)
            {
                count++;
                x += incX;
                y += incY;
            }

            return count;
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