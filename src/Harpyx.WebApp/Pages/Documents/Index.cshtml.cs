using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harpyx.WebApp.Pages.Documents;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
        => RedirectToPage("/Projects/Index");
}
