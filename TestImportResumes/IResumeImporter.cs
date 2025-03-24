public interface IResumeImporter
{
    string ResumeSource { get; }
    Task<ImportResult> Import(Stream stream);
}
