#region

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

#endregion

namespace XRealiSE_Frontend.Pages
{
    public class AboutModel : PageModel
    {
        private readonly ILogger<AboutModel> _logger;

        public AboutModel(ILogger<AboutModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
