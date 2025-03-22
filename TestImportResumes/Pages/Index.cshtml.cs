using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TestImportResumes.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IResumeImporterProvider _resumeImporterProvider;
        public IndexModel(ILogger<IndexModel> logger, IResumeImporterProvider resumeImporterProvider)
        {
            _logger = logger;
            _resumeImporterProvider = resumeImporterProvider;
        }

        public async Task<IActionResult> OnGet()
        {
            CancellationTokenSource tokenSource = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
            var res = await _resumeImporterProvider.Imports(new Stream[] { new MemoryStream(), new MemoryStream(), new MemoryStream(), new MemoryStream(), new MemoryStream() }, tokenSource.Token);

            return Page();
        }
    }
}
