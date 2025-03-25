using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

public class DefaultResumeImporterProvider : IResumeImporterProvider
{
    private readonly ReadOnlyDictionary<string, IResumeImporter> _importerDic = default!;
    private readonly IEnumerable<IResumeImporter> _importers;
    public DefaultResumeImporterProvider(IEnumerable<IResumeImporter> importers)
    {
        _importers = importers;
        _importerDic = new ReadOnlyDictionary<string, IResumeImporter>(importers.ToDictionary(a => a.ResumeSource, StringComparer.OrdinalIgnoreCase)) ;
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
            foreach (var importer in _importers)
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
                var result = await importer.Import(stream);
                if (result.Result != null)
                {
                    dic.TryAdd(stream, result);
                    break;
                }
                exceptions[importer.ResumeSource] = result.Exception!;
            }
            if (!dic.ContainsKey(stream))
            {
                dic.TryAdd(stream,ImportResult.Error(string.Join("|||",exceptions.Keys),new NotSupportedException($"不支持的文件（{string.Join("|||", exceptions.Select(a=>$"{a.Key}:{a.Value.Message}"))}）")));
            }
        });
        return dic;
    }
}
