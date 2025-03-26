using System.Collections;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using ZstdSharp;
using System.Xml;
using LegendaryConverters;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<IResumeImporterProvider,DefaultResumeImporterProvider>();
builder.Services.AddScoped<IResumeImporter,ZhilianResumeImporter>();
builder.Services.AddScoped<IResumeImporter,QianchengResumeImporter>();
builder.Services.AddScoped<IDicToObjConverter, DynamicDicToObjConverter>();
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = (CompressionLevel)4;
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<ZstdCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<DeflateCompressionProvider>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
}
app.UseResponseCompression();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.Map("Import", async(HttpRequest request,IResumeImporterProvider provider) => {
    var file = (await request.ReadFormAsync()).Files[0]!;
    //var stream = new MemoryStream();
    //await file.OpenReadStream().CopyToAsync(stream);
    var stream = file.OpenReadStream();
    var result = await provider.Import(stream);
    return await Task.FromResult(Results.Json(new { ok=true,msg="³É¹¦"}));
});

app.Run();

public class DeflateCompressionProviderOptions
{
    public CompressionLevel Level { get; set; } = CompressionLevel.Optimal;
}
public class DeflateCompressionProvider : ICompressionProvider
{
    public string EncodingName => "deflate";

    public bool SupportsFlush => true;

    public IOptionsMonitor<DeflateCompressionProviderOptions> Options { get; set; }

    public DeflateCompressionProvider(IOptionsMonitor<DeflateCompressionProviderOptions> options)
    {
        Options = options;
    }

    public Stream CreateStream(Stream outputStream)
    {
        return new DeflateStream(outputStream, Options.CurrentValue.Level);
    }
}
public class ZstdCompressionProviderOptions
{
    public CompressionLevel Level { get; set; } = CompressionLevel.Optimal;
}
public class ZstdCompressionProvider : ICompressionProvider
{
    public string EncodingName => "zstd";

    public bool SupportsFlush => true;

    public IOptionsMonitor<ZstdCompressionProviderOptions> Options { get; set; }

    public ZstdCompressionProvider(IOptionsMonitor<ZstdCompressionProviderOptions> options)
    {
        Options = options;
    }

    public Stream CreateStream(Stream outputStream)
    {
        int level = Options.CurrentValue.Level switch {
            CompressionLevel.Optimal =>Compressor.DefaultCompressionLevel,
            CompressionLevel.Fastest=>1,
            CompressionLevel.NoCompression => Compressor.MinCompressionLevel,
            CompressionLevel.SmallestSize => Compressor.MaxCompressionLevel,
            _ => (int)Options.CurrentValue.Level
        };
        return new ZstdSharp.CompressionStream(outputStream, level);
    }
}

public class QianchengResumeImporter : ResumeImporter
{
    public override string ResumeSource => "Qiancheng";

    public override Task<CanImportResult> CheckCanImport(Stream stream)
    {
        return Task.FromResult(CanImportResult.Success());
    }

    public override async Task<ImportResult> DoImport(Stream stream)
    {
        await Task.Delay(500).ConfigureAwait(false);
        throw new NotImplementedException();
    }
}