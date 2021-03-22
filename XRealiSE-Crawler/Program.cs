using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using XRealiSE_DBConnection;
using XRealiSE_DBConnection.data;
using RakeExtraction = Rake.Rake;
using Range = Octokit.Range;

namespace XRealiSE_Crawler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Write("GitHubAPI key: ");
            string apikey = Console.ReadLine();

            GitHubClient = new GitHubClient(new ProductHeaderValue("xrealise"),
                new InMemoryCredentialStore(new Credentials(apikey)));
            RepositoryContentsClient = new RepositoryContentsClient(
                new ApiConnection(new Connection(new ProductHeaderValue("xrealize"),
                    new InMemoryCredentialStore(new Credentials(apikey)))));
            RepositoriesClient = new RepositoriesClient(new ApiConnection(new Connection(
                new ProductHeaderValue("xrealize"),
                new InMemoryCredentialStore(new Credentials(apikey)))));
            //blobsClient = new BlobsClient(new ApiConnection(new Connection(new ProductHeaderValue("xrealise"))));

            CrawledRepos = new List<long>();
            HashSet<string> stopwords = loadStopWords("SmartStoplist.txt");
            rake = new RakeExtraction(stopwords, 2, 4);
            //crawl all repo data
            await crawl(0, 50000);



        }

        private static HashSet<string> loadStopWords(string path)
        {
            HashSet<string> words = new HashSet<string>();
            using (StreamReader f = File.OpenText(path))
                while (!f.EndOfStream)
                {
                    string line = f.ReadLine();
                    if (line[0] != '#')
                        words.Add(line);
                }

            return words;
        }



        private static RakeExtraction rake;
        private static List<long> CrawledRepos;

        private static GitHubClient GitHubClient;

        private static RepositoriesClient RepositoriesClient;
        //private static BlobsClient blobsClient;
        private static RepositoryContentsClient RepositoryContentsClient;
        private static int SearchLimit;
        private static int RateLimit;

        private static void WaitUntil(DateTimeOffset Time)
        {
            TimeSpan waittime = Time - DateTimeOffset.Now;
            Console.WriteLine("RATE LIMIT REACHED! Waiting {0} seconds.", waittime.TotalSeconds);
            if (waittime > new TimeSpan(0))
                Thread.Sleep(waittime);
        }

        private static async Task WaitForApiLimit(bool search = false)
        {
            //check if local managed limit exceeded!
            if (search && SearchLimit == 0 || !search && RateLimit == 0)
            {
                //update local values in case of limit reset after time
                MiscellaneousRateLimit limits = await GitHubClient.Miscellaneous.GetRateLimits();
                //if a limit is really exceeded!
                while (search && limits.Resources.Search.Remaining == 0)
                {
                    WaitUntil(limits.Resources.Search.Reset);
                    limits = await GitHubClient.Miscellaneous.GetRateLimits();
                }

                while (!search && limits.Resources.Core.Remaining == 0)
                {
                    WaitUntil(limits.Resources.Core.Reset);
                    limits = await GitHubClient.Miscellaneous.GetRateLimits();
                }

                SearchLimit = limits.Resources.Search.Remaining;
                RateLimit = limits.Resources.Core.Remaining;
            }

            if (search)
                SearchLimit--;
            else
                RateLimit--;
        }

        private static async Task<string> GetReadmeMd(long repositoryId)
        {
            await WaitForApiLimit();
            try
            {
                return (await RepositoryContentsClient.GetReadme(repositoryId)).Content;
            }
            catch (Octokit.NotFoundException)
            {
                return "";
            }
        }

        private static async Task<string[]> GetFiles(long repositoryId, string extension)
        {
            List<string> files = new List<string>();
            await WaitForApiLimit();
            IReadOnlyList<GitHubCommit> commits = await RepositoriesClient.Commit.GetAll(repositoryId);
            await WaitForApiLimit();
            TreeResponse tree = await GitHubClient.Git.Tree.GetRecursive(repositoryId, commits[0].Sha);
            foreach (TreeItem treeItem in tree.Tree)
            {
                if (treeItem.Path.EndsWith(extension)) 
                    files.Add(Path.GetFileNameWithoutExtension(treeItem.Path).ToLower());
            }


            return files.Distinct().ToArray();


        }



        private static async Task<SearchCodeResult> SearchCode(SearchCodeRequest request)
        {
            await WaitForApiLimit(true);
            return await GitHubClient.Search.SearchCode(request);
        }

        private static async Task<Repository> GetRepository(long repositoryId)
        {
            await WaitForApiLimit();
            return await GitHubClient.Repository.Get(repositoryId);
        }

        private static GitHubRepository fromCrawled(Repository repo)
        {
            return new GitHubRepository
            {
                CreatedAt = repo.CreatedAt,
                Description = repo.Description?.Length > 255
                    ? repo.Description.Substring(0, 255)
                    : repo.Description,
                ForksCount = repo.ForksCount,
                GitHubRepositoryId = repo.Id,
                HasDownloads = repo.HasDownloads,
                HasIssues = repo.HasIssues,
                HasPages = repo.HasPages,
                HasWiki = repo.HasWiki,
                License = repo.License?.Name,
                Name = repo.Name,
                OpenIssuesCount = repo.OpenIssuesCount,
                Owner = repo.Owner.Login,
                PushedAt = repo.PushedAt,
                StargazersCount = repo.StargazersCount,
                UpdatedAt = repo.UpdatedAt,
                WatchersCount = repo.WatchersCount
            };
        }

        private static Keyword keywordFromString(string keyword)
        {
            return new Keyword {Word = keyword};
        }

        private static KeywordInRepository fromExisting(Keyword keyword, GitHubRepository repository)
        {
            return new KeywordInRepository {Repository = repository, Keyword = keyword};
        }

        private static string StripHTML(string input)
        {
            return Regex.Replace(input, @"</?[a-zA-Z]+[^>]*>", String.Empty);
        }

        private static string StripURLs(string input)
        {
            return Regex.Replace(input, @"https?\:\/\/[^\s]*", String.Empty);
        }

        private static string StripNonAlphanumeric(string input)
        {
            return Regex.Replace(input, @"[^\s\w\d'-]", String.Empty);
        }

        private static async Task crawl(int from, int to, string extra = "")
        {
            Console.Write("Crawling sizes from {0} to {1}, ", from, to);

            ApiPagination pagination = new ApiPagination();

            SearchCodeRequest request = new SearchCodeRequest("com.unity.xr" + extra)
            {
                Size = new Range(from, to),
                FileName = "manifest.json",
                PerPage = 100,
                Page = 1
            };


            SearchCodeResult result = await SearchCode(request);

            Console.Write("{0} results, ", result.TotalCount);
            if (result.TotalCount > 1000)
            {
                if (from != to)
                {
                    int middle = (from + to) / 2;
                    Console.WriteLine("split at {0}", middle);
                    await crawl(from, middle);
                    await crawl(middle + 1, to);
                }
                else
                {
                    Console.WriteLine("split with dirty hack :/");
                    await crawl(from, to, " path:packages");
                    await crawl(from, to, " NOT path:packages");
                }
            }
            else
            {
                Console.WriteLine("NO split");
                using (DatabaseConnection databaseConnection = new DatabaseConnection())
                {
                    do
                    {
                        Console.WriteLine("Page {0}", request.Page);
                        foreach (SearchCode file in result.Items)
                        {
                            Console.Write("Repository {0} ", file.Repository.FullName);
                            if (!CrawledRepos.Contains(file.Repository.Id) && databaseConnection.GitHubRepositories.Find(file.Repository.Id) == null)
                            {
                                CrawledRepos.Add(file.Repository.Id);

                                Repository repo = await GetRepository(file.Repository.Id);

                                GitHubRepository gitHubRepository = fromCrawled(repo);

                                databaseConnection.insertOrUpdateRepository(ref gitHubRepository);

                                Console.Write("[Repository]");

                                string readme = await GetReadmeMd(repo.Id);
                                string strippedreadme = StripNonAlphanumeric(StripURLs(StripHTML(readme)));


                                if (readme.Length > 0)
                                {
                                    Dictionary<string, double> keywords = rake.Run(strippedreadme);
                                    foreach (KeyValuePair<string, double> keyword in keywords)
                                    {
                                        if (keyword.Key.Length <= 100)
                                        {

                                            databaseConnection.addKeywordConnection(gitHubRepository, keyword.Key,
                                                KeywordInRepository.KeywordInRepositoryType.InReadme, keyword.Value);
                                        }
                                    }
                                }
                                Console.Write("[Readme]");
                                databaseConnection.SaveChanges();

                                
                                
                                string[] files = await GetFiles(repo.Id, "cs");

                                foreach (string filename in files)
                                {
                          
                                    databaseConnection.addKeywordConnection(gitHubRepository,filename,KeywordInRepository.KeywordInRepositoryType.Classname);

                                }
                                Console.Write("[ClassNames]");
                                
                                databaseConnection.SaveChanges();
                                Console.WriteLine("[Saved]");

                            }
                            else
                            {
                                Console.WriteLine("Already Crawled this run!");
                            }
                        }

                        request.Page++;
                        if (request.Page <= 10)
                            result = await SearchCode(request);
                    } while (result.Items.Count > 0 && request.Page <= 10);
                }
            }
        }

    }
}
