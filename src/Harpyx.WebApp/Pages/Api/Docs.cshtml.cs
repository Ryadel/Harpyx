using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harpyx.WebApp.Pages.Api;

public class DocsModel : PageModel
{
    public string BaseUrl { get; private set; } = string.Empty;

    public void OnGet()
    {
        BaseUrl = $"{Request.Scheme}://{Request.Host}";
    }
}
