using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Immutable;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using ZstdSharp;
using System.Xml;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<IResumeImporterProvider,DefaultResumeImporterProvider>();
builder.Services.AddScoped<IResumeImporter,ZhilianResumeImporter>();
builder.Services.AddScoped<IResumeImporter,QianchengResumeImporter>();
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
    return await Task.FromResult(Results.Json(new { ok=true,msg="成功"}));
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

public class ImportResult
{
    public static ImportResult Success(string source, IDictionary<string, string> result)
    {
        ImportResult tresult = new ImportResult(source) { Result = result };
        return tresult;
    }
    public static ImportResult Error(string source, Exception ex)
    {
        ImportResult tresult = new ImportResult(source) { Exception = ex };
        return tresult;
    }
    private ImportResult(string source)
    {
        Source = source;
    }
    public string Source { get; }
    public IDictionary<string, string>? Result { get; init; }
    public Exception? Exception { get; init; }
}
public class CanImportResult:IAsyncDisposable
{
    public static CanImportResult Error(Exception ex)
    {
        return new CanImportResult {Result=false,Exception=ex };
    }
    public static CanImportResult Success()
    {
        return new CanImportResult { Result=true};
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public bool Result { get; init; }
    public Exception? Exception { get; init; }
}
public interface IResumeImporter
{
    string ResumeSource { get; }
    Task<ImportResult> Import(Stream stream);
}
public abstract class ResumeImporter : IResumeImporter
{
    public abstract string ResumeSource { get; }
    public abstract Task<CanImportResult> CheckCanImport(Stream stream);
    public abstract Task<ImportResult> DoImport(Stream stream);

    private ConcurrentDictionary<Stream, Lazy<Task<CanImportResult>>> _cacheCanImport = new ConcurrentDictionary<Stream, Lazy<Task<CanImportResult>>>();
    public async Task<CanImportResult> CanImport(Stream stream)
    {
        stream.Position = 0;
        Lazy<Task<CanImportResult>> task = _cacheCanImport.GetOrAdd(stream, stream =>new Lazy<Task<CanImportResult>>(()=>CheckCanImport(stream),true));
        try
        {
            return await task.Value;
        }
        catch (Exception ex) { 
            return CanImportResult.Error(ex);
        }
    }

    public async Task<ImportResult> Import(Stream stream)
    {
        CanImportResult can = await CanImport(stream);
        if (!can.Result)
        {
            return ImportResult.Error(ResumeSource,can.Exception!);
        }
        try
        {
            stream.Position = 0;
            return await DoImport(stream);
        }
        catch (Exception ex) {
            return ImportResult.Error(ResumeSource,ex);
        }
    }
}
public interface IResumeImporterProvider
{
    IResumeImporter? GetResumeImporter(string resumeSource);
    Task<IDictionary<Stream, ImportResult>> Imports(IEnumerable<Stream> streams,CancellationToken cancellationToken);
    Task<ImportResult> Import(Stream stream);
}

public class DefaultResumeImporterProvider : IResumeImporterProvider
{
    private readonly ImmutableDictionary<string, IResumeImporter> _importerDic = default!;
    public DefaultResumeImporterProvider(IEnumerable<IResumeImporter> importers)
    {
        _importerDic = importers.ToImmutableDictionary(a=>a.ResumeSource,StringComparer.OrdinalIgnoreCase);
    }

    public IResumeImporter? GetResumeImporter(string resumeSource)
    {
        return _importerDic.GetValueOrDefault(resumeSource);
    }

    public async Task<ImportResult> Import(Stream stream)
    {
        return (await Imports(new Stream[] { stream}, default))[stream];
    }

    public async Task<IDictionary<Stream, ImportResult>> Imports(IEnumerable<Stream> streams, CancellationToken cancellationToken)
    {
        ConcurrentDictionary<Stream, ImportResult> dic = new ConcurrentDictionary<Stream, ImportResult>();
        await Parallel.ForEachAsync(streams,async (stream, _) => {
            Dictionary<string,Exception> exceptions = new Dictionary<string, Exception>();
            foreach (var importer in _importerDic)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    ImportResult cancel = ImportResult.Error("Cancel", new OperationCanceledException("已取消"));
                    dic.AddOrUpdate(stream, cancel, (stream, old) =>
                    {
                        return cancel;
                    });
                    break;
                }
                var result = await importer.Value.Import(stream);
                if (result.Result != null)
                {
                    dic.TryAdd(stream, result);
                    break;
                }
                exceptions[importer.Key] = result.Exception!;
            }
            if (!dic.ContainsKey(stream))
            {
                dic.TryAdd(stream,ImportResult.Error(string.Join("|||",exceptions.Keys),new NotSupportedException($"不支持的文件（{string.Join("|||", exceptions.Select(a=>$"{a.Key}:{a.Value.Message}"))}）")));
            }
        });
        return dic;
    }
}

public class ZhilianResumeImporter : ResumeImporter
{
    public override string ResumeSource => "Zhilian";

    public override Task<CanImportResult> CheckCanImport(Stream stream)
    {
        try
        {
            long pos = readToString(stream, "<div class=Section1");
        }
        catch  {
            return Task.FromResult(CanImportResult.Error(new NotSupportedException("不支持的文件")));
        }
        
        return Task.FromResult(CanImportResult.Success());
    }

    public override async Task<ImportResult> DoImport(Stream stream)
    {
        await Task.Delay(500).ConfigureAwait(false);
        throw new NotImplementedException();
    }
    private unsafe long readToString(Stream stream,string str) {
        byte[] data = Encoding.UTF8.GetBytes(str);
        fixed (byte * strb = data)
        {
            byte* cache = stackalloc byte[data.Length];
            int pos = 0;
            long oldPos = stream.Position;
            stream.Seek(0, SeekOrigin.End);
            long streamLen = stream.Position + 1;
            stream.Position = oldPos;
            bool isEnd = false;
            while (stream.Position < streamLen && !isEnd)
            {
                if (stream.Position == streamLen - 1)
                {
                    isEnd = true;
                }
                byte b = (byte)stream.ReadByte();
                cache[pos++] = b;
                if (!isSame(cache, strb, pos))
                {
                    pos = 0;
                    continue;
                }
                if (pos == data.Length)
                {
                    stream.Position -= data.Length;
                    return stream.Position;
                }
            }
            return -1;
        }
        bool isSame(byte* data1,byte* data2,long len)
        {
            while (len-- > 0)
            {
                if (data1[len] != data2[len])
                {
                    return false;
                }
            }
            return true;
        }
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