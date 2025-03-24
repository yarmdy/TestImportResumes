using System.Collections.Concurrent;

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
