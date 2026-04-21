using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Documents;

public class UploadModel : PageModel
{
    public IActionResult OnGet()
        => RedirectToPage("/Projects/Index");

    public IActionResult OnPost()
        => RedirectToPage("/Projects/Index");
}
