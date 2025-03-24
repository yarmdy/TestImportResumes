using System.Collections.Concurrent;
using System.Collections.Immutable;

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
