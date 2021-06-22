#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using XRealiSE_DBConnection;
using XRealiSE_DBConnection.data;

#endregion

namespace XRealiSE_Frontend.Pages
{
    public class IndexModel : PageModel
    {
        private readonly DatabaseConnection _connection;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger, DatabaseConnection connection)
        {
            _logger = logger;
            _connection = connection;
            ResultsPerPage = 25;
        }

        [BindProperty] public int ResultsPerPage { get; }
        [BindProperty] public string Searchstring { get; set; }
        [BindProperty] public int SearchKey { get; set; }
        [BindProperty] public int Start { get; set; }
        [BindProperty] public GitHubRepository[] ResultSubSet { get; set; }
        [BindProperty] public int SearchType { get; set; }
        [BindProperty] public int Order { get; set; }
        [BindProperty] public int OrderAttribute { get; set; }
        [BindProperty] public int? ParentSearch { get; set; }
        [BindProperty] public bool[] FilterActive { get; set; }
        [BindProperty] public int[] FilterEquality { get; set; }
        [BindProperty] public string[] FilterValue { get; set; }

        public IActionResult OnGet()
        {
            SearchKey = RouteData.Values.Keys.Contains("key") ? int.Parse(RouteData.Values["key"]?.ToString()!) : -1;
            Start = RouteData.Values.Keys.Contains("start") ? int.Parse(RouteData.Values["start"]?.ToString()!) : 0;

            if (RouteData.Values.Keys.Contains("repository"))
            {
                long repository = long.Parse(RouteData.Values["repository"]?.ToString()!);
                GitHubRepository repo = _connection.GitHubRepositories.Find(repository);
                if (repo != null)
                {
                    _connection.SearchActions.Add(new SearchAction
                    {
                        ActionDescription = "VisitRepository " + repo.Owner + "/" + repo.Name,
                        ActionTime = DateTime.Now, SearchId = DbHelper.SearchResults[SearchKey].DatabaseId
                    });
                    _connection.SaveChanges();
                    _connection.Dispose();
                    return Redirect("https://github.com/" + repo.Owner + "/" + repo.Name);
                }
            }

            if (DbHelper.SearchResults.ContainsKey(SearchKey))
                ResultSubSet = DbHelper.GetRepos(Start, ResultsPerPage, SearchKey, _connection);
            else if (SearchKey != -1)
                SearchKey = -3;

            _connection.Dispose();
            return null;
        }

        public void OnPost()
        {
            Start = 0;
            if (Searchstring != null)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                CancellationToken token = tokenSource.Token;

                Task<int> searchtask =
                    Task.Run(
                        () => DbHelper.Search(_connection, Searchstring, Order, OrderAttribute, FilterActive,
                            FilterEquality, FilterValue, SearchType == 1,
                            ParentSearch), token);
                if (searchtask.Wait(5000))
                {
                    SearchKey = searchtask.Result;
                    ResultSubSet = DbHelper.GetRepos(Start, ResultsPerPage, SearchKey, _connection);
                    _connection.Dispose();
                    return;
                }

                _connection.Dispose();
                tokenSource.Cancel();
                SearchKey = -2;
            }
        }
    }
}
