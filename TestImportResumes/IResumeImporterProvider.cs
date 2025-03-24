public interface IResumeImporterProvider
{
    IResumeImporter? GetResumeImporter(string resumeSource);
    Task<IDictionary<Stream, ImportResult>> Imports(IEnumerable<Stream> streams,CancellationToken cancellationToken);
    Task<ImportResult> Import(Stream stream);
}
