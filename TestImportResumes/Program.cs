using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Immutable;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<IResumeImporterProvider,DefaultResumeImporterProvider>();
builder.Services.AddScoped<IResumeImporter,ZhilianResumeImporter>();
builder.Services.AddScoped<IResumeImporter,QianchengResumeImporter>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();


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
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        return (await Imports(new Stream[] { stream}, cancellationTokenSource.Token))[stream];
    }

    public async Task<IDictionary<Stream, ImportResult>> Imports(IEnumerable<Stream> streams, CancellationToken cancellationToken)
    {
        ConcurrentDictionary<Stream, ImportResult> dic = new ConcurrentDictionary<Stream, ImportResult>();
        await Parallel.ForEachAsync(streams,async (stream, cancellationToken) => {
            Dictionary<string,Exception> exceptions = new Dictionary<string, Exception>();
            foreach (var importer in _importerDic)
            {
                if (cancellationToken.IsCancellationRequested)
                {
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
        throw new NotImplementedException();
    }

    public override Task<ImportResult> DoImport(Stream stream)
    {
        throw new NotImplementedException();
    }
}

public class QianchengResumeImporter : ResumeImporter
{
    public override string ResumeSource => "Qiancheng";

    public override Task<CanImportResult> CheckCanImport(Stream stream)
    {
        throw new NotImplementedException();
    }

    public override Task<ImportResult> DoImport(Stream stream)
    {
        throw new NotImplementedException();
    }
}