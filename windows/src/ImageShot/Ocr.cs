using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
#if WINRT
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
#endif

namespace ImageShot
{
    // Tekstgenkendelse med Windows' indbyggede motor (Windows 10/11, ingen internet, ingen ekstra filer).
    // Bruger de sprog, der er installeret i Windows. Returnerer null, hvis motoren ikke findes.
    static class Ocr
    {
        public static string Languages = "";

#if WINRT
        static OcrEngine Engine()
        {
            OcrEngine e = OcrEngine.TryCreateFromUserProfileLanguages();
            if (e == null)
            {
                foreach (Windows.Globalization.Language l in OcrEngine.AvailableRecognizerLanguages)
                {
                    e = OcrEngine.TryCreateFromLanguage(l);
                    if (e != null) break;
                }
            }
            if (e != null) Languages = e.RecognizerLanguage.LanguageTag;
            return e;
        }

        public static async Task<string> RecognizeAsync(Bitmap image)
        {
            OcrEngine engine = Engine();
            if (engine == null) return null;
            int max = (int)OcrEngine.MaxImageDimension;
            // Lille tekst genkendes bedre forstørret.
            float up = Math.Max(image.Width, image.Height) < 1200 ? 2f : 1f;
            int chunk = (int)(max / up) - 200;
            StringBuilder sb = new StringBuilder();
            for (int y = 0; y < image.Height; y += chunk)
            {
                // Lange (scrollede) billeder deles i stykker; motoren tager højst ~10000 px.
                int h = Math.Min(chunk, image.Height - y);
                int w = Math.Min(image.Width, (int)(max / up) - 1);
                using (Bitmap part = new Bitmap((int)(w * up), (int)(h * up), PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(part))
                    {
                        g.InterpolationMode = up > 1 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(image, new Rectangle(0, 0, part.Width, part.Height), new Rectangle(0, y, w, h), GraphicsUnit.Pixel);
                    }
                    string t = await RecognizePart(engine, part);
                    if (t.Length > 0) { if (sb.Length > 0) sb.AppendLine(); sb.Append(t); }
                }
            }
            return sb.ToString();
        }

        static async Task<string> RecognizePart(OcrEngine engine, Bitmap bmp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                BitmapDecoder dec = await BitmapDecoder.CreateAsync(WindowsRuntimeStreamExtensions.AsRandomAccessStream(ms));
                using (SoftwareBitmap sbm = await dec.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                {
                    OcrResult res = await engine.RecognizeAsync(sbm);
                    StringBuilder sb = new StringBuilder();
                    foreach (OcrLine line in res.Lines)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(line.Text);
                    }
                    return sb.ToString();
                }
            }
        }
#else
        public static Task<string> RecognizeAsync(Bitmap image) { return Task.FromResult<string>(null); }
#endif
    }
}
