using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Implementations;
using AutoCapture.Wpf.Services.Interfaces;
using Moq;
using Serilog;
using System.Drawing;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AutoCapture.Tests.Services;

public class ScreenCaptureServiceTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory();
    private readonly ScreenCaptureService _service = new(Mock.Of<ILogger>(), (graphics, _) => graphics.Clear(Color.Blue));

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public async Task Capture_CreatesDistinctPngFilesWithRequestedPrefixAndPixels()
    {
        var area = new CaptureArea(0, 0, 100, 50);
        var paths = new HashSet<string>();
        foreach (var prefix in new[] { "capture", "capture", "sample" })
        {
            var path = await _service.CaptureAndSaveAsync(area, _folder.FullName, prefix);
            Assert.NotNull(path);
            Assert.StartsWith(prefix + "_", Path.GetFileName(path));
            Assert.EndsWith(".png", path);
            Assert.True(paths.Add(path));
            using var image = new Bitmap(path);
            Assert.Equal(new Size(100, 50), image.Size);
            Assert.Equal(Color.Blue.ToArgb(), image.GetPixel(10, 10).ToArgb());
        }
    }

    [Fact]
    public async Task InvalidOutputDirectory_FailsAfterRetries()
    {
        var file = Path.Combine(_folder.FullName, "existing-file");
        File.WriteAllText(file, "");
        Assert.Null(await _service.CaptureAndSaveAsync(new CaptureArea(0, 0, 10, 10), file));
    }

    [Fact]
    public async Task CaptureRetry_RechecksGuardAndDoesNotPublishPartialFile()
    {
        int guards = 0;
        int copies = 0;
        var service = new ScreenCaptureService(Mock.Of<ILogger>(), (_, _) =>
        {
            copies++;
            throw new IOException("retry");
        });
        Task Guard() => ++guards == 2 ? Task.FromCanceled(new CancellationToken(true)) : Task.CompletedTask;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CaptureAndSaveAsync(new CaptureArea(0, 0, 10, 10), _folder.FullName, "guard", Guard));
        Assert.Equal(1, copies);
        Assert.Equal(2, guards);
        Assert.Empty(_folder.GetFiles());
    }

    [Fact]
    public async Task Pdf_RendersAllImagesInInputOrder()
    {
        var colors = new[] { Color.Red, Color.Lime, Color.Blue };
        var paths = new List<string>();
        foreach (var color in colors)
        {
            var service = new ScreenCaptureService(Mock.Of<ILogger>(), (g, _) => g.Clear(color));
            paths.Add((await service.CaptureAndSaveAsync(new CaptureArea(0, 0, 160, 160), _folder.FullName))!);
        }
        var path = await _service.SaveAsPdfAsync(paths, _folder.FullName);
        var file = await StorageFile.GetFileFromPathAsync(path);
        var pdf = await PdfDocument.LoadFromFileAsync(file);
        Assert.Equal((uint)colors.Length, pdf.PageCount);
        for (var i = 0; i < colors.Length; i++)
        {
            var pixel = await SamplePageAsync(pdf, i);
            Assert.InRange(Math.Abs(colors[i].R - pixel.R), 0, 5);
            Assert.InRange(Math.Abs(colors[i].G - pixel.G), 0, 5);
            Assert.InRange(Math.Abs(colors[i].B - pixel.B), 0, 5);
        }
    }

    private static async Task<Color> SamplePageAsync(PdfDocument document, int index)
    {
        using var page = document.GetPage((uint)index);
        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = 180 });
        stream.Seek(0);
        using var input = stream.AsStreamForRead();
        using var bitmap = new Bitmap(input);
        return bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 3);
    }

    [Fact]
    public async Task HundredPages_PngAndRenderedPdfPreserveEveryPageInOrder()
    {
        // Encode the page number in three independent color channels.
        static Color Encode(int index) =>
            Color.FromArgb((index / 25) * 50, ((index / 5) % 5) * 50, (index % 5) * 50);
        static int Decode(Color color) =>
            ((color.R + 25) / 50) * 25 + ((color.G + 25) / 50) * 5 + (color.B + 25) / 50;

        var index = 0;
        var logger = Mock.Of<ILogger>();
        var capture = new ScreenCaptureService(logger, (g, _) => g.Clear(Encode(index)));
        var input = new Mock<IInputService>();
        input.Setup(s => s.SendKeyWithRandomTimingAsync("Right", It.IsAny<Func<Task>>()))
            .Returns<string, Func<Task>>(async (_, guard) => { await guard(); index++; });
        var engine = new MacroEngine(logger, capture, input.Object, (_, token) => Task.Delay(0, token));
        MacroCompletedEventArgs? result = null;
        engine.Completed += (_, e) => result = e;
        await engine.StartAsync(new CaptureSettings
        {
            CaptureCount = 100, AutoInputKey = "Right", SaveFolderPath = _folder.FullName,
            SaveAsPdf = true, Target = Mock.Of<ICaptureTarget>(t => t.IsOpen && t.IsForeground)
        }, new CaptureArea(0, 0, 64, 64));

        Assert.NotNull(result);
        Assert.Equal(100, result.TotalCaptured);
        Assert.Equal(99, index);
        var paths = Assert.IsType<List<string>>(result.CapturedFiles);
        Assert.Equal(100, paths.Distinct().Count());
        var pdfFile = Assert.Single(_folder.GetFiles("*.pdf"));
        var pdf = await PdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(pdfFile.FullName));
        Assert.Equal(100u, pdf.PageCount);
        for (var i = 0; i < 100; i++)
        {
            using var png = new Bitmap(paths[i]);
            Assert.Equal(i, Decode(png.GetPixel(32, 32)));
            var rendered = await SamplePageAsync(pdf, i);
            Assert.Equal(i, Decode(rendered));
        }
    }

    [Fact]
    public async Task Pdf_RejectsEmptyOrMissingSource()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsPdfAsync([], _folder.FullName));
        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.SaveAsPdfAsync(
            [Path.Combine(_folder.FullName, "missing.png")], _folder.FullName));
        Assert.Empty(_folder.GetFiles("*.pdf"));
    }
}
