#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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
            OrderAttribute = 10;
        }

        [BindProperty] public int ResultsPerPage { get; }
        [BindProperty] public string Searchstring { get; set; }
        [BindProperty] public int SearchKey { get; set; }
        [BindProperty] public int Start { get; set; }
        [BindProperty] public GitHubRepository[] ResultSubSet { get; set; }
        [BindProperty] public string[][][] ResultExtraData { get; set; }
        [BindProperty] public int Order { get; set; }
        [BindProperty] public int OrderAttribute { get; set; }
        [BindProperty] public int? ParentSearch { get; set; }
        [BindProperty] public bool[] FilterActive { get; set; }
        [BindProperty] public int[] FilterEquality { get; set; }
        [BindProperty] public string[] FilterValue { get; set; }
        [BindProperty] public List<SelectListItem> VersionFilters { get; set; }
        [BindProperty] public List<long> SelectedVersionFilters { get; set; }

        private void LoadVersionFilters()
        {
            VersionFilters = new List<SelectListItem>();
            foreach (KeyValuePair<long, string> keyValuePair in DbHelper.GetVersionFilters(_connection))
                VersionFilters.Add(new SelectListItem(keyValuePair.Value, keyValuePair.Key.ToString()));
        }

        private void GetRepoExtraData()
        {
            ResultExtraData = new string[ResultSubSet.Length][][];
            for (int i = 0; i < ResultSubSet.Length; i++)
            {
                ResultExtraData[i] = new string[2][];
                List<string> versions = new List<string>();

                versions.AddRange(ResultSubSet[i].KeywordInRepositories
                    .Where(k => k.Type == KeywordInRepository.KeywordInRepositoryType.UnityVersion)
                    .Select(k => k.Keyword.Word));

                List<string> keymatches = new List<string>();

                /*
                foreach (string searchword in DbHelper.SearchResults[SearchKey].SearchString.Split(' '))
                {
                    List<Keyword> kw = _connection.Keywords.FromSqlRaw(
                            "SELECT k.* FROM keywords k JOIN keywordinrepositories i ON k.KeywordId = i.KeywordId WHERE i.GitHubRepositoryId = " +
                            ResultSubSet[i].GitHubRepositoryId + " AND i.Type = 0 AND k.Word LIKE '%" + searchword +
                            "%';")
                        .ToList();

                    keymatches.AddRange(kw.Select(k => k.Word));
                }*/

                ResultExtraData[i][0] = versions.ToArray();
                ResultExtraData[i][1] = keymatches.ToArray();
            }
        }

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
            {
                ResultSubSet = DbHelper.GetRepos(Start, ResultsPerPage, SearchKey, _connection);
                GetRepoExtraData();
            }
            else if (SearchKey != -1)
            {
                SearchKey = -3;
            }

            LoadVersionFilters();
            _connection.Dispose();

            return null;
        }

        public void OnPost()
        {
            LoadVersionFilters();
            Start = 0;
            if (Searchstring != null)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                CancellationToken token = tokenSource.Token;

                Task<int> searchtask =
                    Task.Run(
                        () => DbHelper.Search(_connection, Searchstring, Order, OrderAttribute, FilterActive,
                            FilterEquality, FilterValue, SelectedVersionFilters.ToArray(),
                            ParentSearch), token);
                if (searchtask.Wait(500000))
                {
                    SearchKey = searchtask.Result;
                    ResultSubSet = DbHelper.GetRepos(Start, ResultsPerPage, SearchKey, _connection);
                    GetRepoExtraData();
                    LoadVersionFilters();
                    _connection.Dispose();
                    return;
                }

                LoadVersionFilters();
                _connection.Dispose();
                tokenSource.Cancel();
                SearchKey = -2;
            }
        }

        public string Highlight(string text, string searchstring)
        {
            if (text == null)
                return "";

            string[] highlightwords = searchstring.Split(' ');

            if (highlightwords.Contains("mark"))
            {
                string[] tempwords = new string[highlightwords.Length];
                tempwords[0] = "mark";
                int pos = 1;
                foreach (string highlightword in highlightwords) tempwords[pos++] = highlightword;

                highlightwords = tempwords;
            }


            foreach (string word in highlightwords.Where(w => w.Length > 3))
                text = Regex.Replace(text, word, "<mark><u>" + word.ToUpper() + "</u></mark>", RegexOptions.IgnoreCase);

            return text;
        }
    }
}
