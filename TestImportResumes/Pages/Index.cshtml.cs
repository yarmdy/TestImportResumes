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
            var res = await _resumeImporterProvider.Imports(new Stream[] { new MemoryStream(), new MemoryStream(), new MemoryStream(), new MemoryStream(), new MemoryStream() },HttpContext.RequestAborted);
            return Page();
        }
    }
}
