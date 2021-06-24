#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;
using XRealiSE_DBConnection;
using XRealiSE_DBConnection.data;

#endregion

namespace XRealiSE_Frontend
{
    public static class DbHelper
    {
        private static readonly Random Rnd;

        public static ConcurrentDictionary<int, SearchResult> SearchResults;

        static DbHelper()
        {
            SearchResults = new ConcurrentDictionary<int, SearchResult>();
            Rnd = new Random();
        }

        private static void CleanupOldSearches()
        {
            foreach (KeyValuePair<int, SearchResult> oldSearchKeyValuePair in SearchResults.Where(s =>
                DateTime.Now - s.Value.LastAccess > TimeSpan.FromMinutes(15)))
                SearchResults.TryRemove(oldSearchKeyValuePair);
        }

        private static Expression<Func<GitHubRepository, bool>> Filter(int attribute, int equality, int value)
        {
            switch (attribute)
            {
                case 0:
                    switch (equality)
                    {
                        case 0: //>=
                            return repository => repository.StargazersCount >= value;
                        case 1: //<=
                            return repository => repository.StargazersCount <= value;
                        case 2: //==
                            return repository => repository.StargazersCount == value;
                    }

                    break;

                case 1:
                    switch (equality)
                    {
                        case 0: //>=
                            return repository => repository.ForksCount >= value;
                        case 1: //<=
                            return repository => repository.ForksCount <= value;
                        case 2: //==
                            return repository => repository.ForksCount == value;
                    }

                    break;

                case 2:
                    switch (equality)
                    {
                        case 0: //>=
                            return repository => EF.Functions.DateDiffDay(repository.CreatedAt, DateTime.Now) >= value;
                        case 1: //<=
                            return repository => EF.Functions.DateDiffDay(repository.CreatedAt, DateTime.Now) <= value;
                    }

                    break;

                case 3:
                    switch (equality)
                    {
                        case 0: //>=
                            return repository => EF.Functions.DateDiffDay(repository.PushedAt, DateTime.Now) >= value;
                        case 1: //<=
                            return repository => EF.Functions.DateDiffDay(repository.PushedAt, DateTime.Now) <= value;
                    }

                    break;
            }

            return repository => true;
        }

        private static long[] OrderAndFilterItems(DatabaseConnection connection, long[] rawResult, int order,
            int orderAttribute,
            bool[] filters, int[] filterEuqalities, string[] filterValue)
        {
            IQueryable<GitHubRepository> query =
                connection.GitHubRepositories.Where(r => rawResult.Contains(r.GitHubRepositoryId));

            for (int i = 0; i < 4; i++)
                if (filters[i])
                    try
                    {
                        query = query.Where(Filter(i, filterEuqalities[i], int.Parse(filterValue[i])));
                    }
                    catch (FormatException)
                    {
                        //ignored -- wrong parameter input in formular, then ignore that filter argument
                    }

            return OrderItems(query.ToList(), order, orderAttribute).Select(repo => repo.GitHubRepositoryId).ToArray();
        }

        internal static async Task<int> Search(DatabaseConnection connection, string searchString, int order,
            int orderAttribute,
            bool[] filters, int[] filterEuqalities, string[] filterValue, bool matchAllWords = false,
            int? parentSearch = null)
        {
            Task.Factory.StartNew(CleanupOldSearches);

            Stopwatch stopwatch = new Stopwatch();
            Regex regexUnwanted = new Regex("[^a-zA-Z ]");

            stopwatch.Start();

            searchString = regexUnwanted.Replace(searchString, "").ToLower();

            List<long> foundRepos = connection.SearchIndex.FromSqlRaw(
                "SELECT * FROM SearchIndex WHERE MATCH (SearchString) AGAINST (\"" + searchString +
                "\" IN NATURAL LANGUAGE MODE);").Select(s => s.GitHubRepositoryId).ToList();

            long[] resultSet = OrderAndFilterItems(connection, foundRepos.ToArray(), order, orderAttribute, filters,
                filterEuqalities, filterValue);

            stopwatch.Stop();

            int dbSearchId = SaveSearch(connection, searchString, OrderingToString(order, orderAttribute),
                resultSet.Length, stopwatch.ElapsedMilliseconds, matchAllWords,
                FilterToString(filters, filterEuqalities, filterValue), parentSearch);

            SearchResult result = new SearchResult(searchString, resultSet, order, orderAttribute, dbSearchId);

            int key = Rnd.Next(0, int.MaxValue);

            SearchResults[key] = result;

            return key;
        }

        private static int SaveSearch(DatabaseConnection connection, string searchString, string order, int resultSize,
            long duration, bool matchAll, string filter = "",
            int? parentSearch = null)
        {
            Search s = new Search
            {
                ParentSearchId = parentSearch,
                SearchString = searchString,
                SearchFilters = filter,
                SearchOrdering = order,
                SearchResultSize = resultSize,
                SearchDuration = duration,
                SearchMatchAll = matchAll,
                SearchTime = DateTime.Now
            };
            connection.Searches.Add(s);
            connection.SaveChanges();
            return s.SearchId;
        }

        private static GitHubRepository[] OrderItems(List<GitHubRepository> data, int order, int orderAttribute)
        {
            switch (orderAttribute)
            {
                case 1:
                    data = data.OrderBy(repo => repo.ForksCount).ToList();
                    break;
                case 2:
                    data = data.OrderBy(repo => repo.CreatedAt).ToList();
                    break;
                case 3:
                    data = data.OrderBy(repo => repo.PushedAt).ToList();
                    break;
                case 0:
                    data = data.OrderBy(repo => repo.StargazersCount).ToList();
                    break;
            }

            if (order == 0)
                data.Reverse();
            return data.ToArray();
        }

        private static string OrderingToString(int order, int orderAttribute)
        {
            string text = "";
            switch (orderAttribute)
            {
                case 1:
                    text += "ForksCount ";
                    break;
                case 2:
                    text += "CreatedAt ";
                    break;
                case 3:
                    text += "PushedAt ";
                    break;
                case 0:
                    text += "StargazersCount ";
                    break;
            }

            switch (order)
            {
                case 0:
                    text += "DESC";
                    break;
                case 1:
                    text += "ASC";
                    break;
            }

            return text;
        }

        private static string FilterToString(bool[] attributes, int[] equalities, string[] values)
        {
            string text = "";
            for (int i = 0; i < 4; i++)
                if (attributes[i])
                {
                    if (text != "")
                        text += "; ";

                    switch (i)
                    {
                        case 1:
                            text += "ForksCount ";
                            break;
                        case 2:
                            text += "CreatedAt ";
                            break;
                        case 3:
                            text += "PushedAt ";
                            break;
                        case 0:
                            text += "StargazersCount ";
                            break;
                    }

                    switch (equalities[i])
                    {
                        case 0:
                            text += "<= ";
                            break;
                        case 1:
                            text += ">= ";
                            break;
                        case 2:
                            text += "== ";
                            break;
                    }

                    text += values[i];
                }

            return text;
        }

        internal static GitHubRepository[] GetRepos(int page, int itemsperpage, int searchKey,
            DatabaseConnection connection)
        {
            //track usage
            connection.SearchActions.Add(new SearchAction
            {
                ActionDescription = "ViewPage " + page,
                ActionTime = DateTime.Now,
                SearchId = SearchResults[searchKey].DatabaseId
            });
            connection.SaveChanges();

            // order the partial result (skip pages, take pagesize) again because the where does not preserve order.
            return OrderItems(connection.GitHubRepositories.Where(repo =>
                    SearchResults[searchKey].ResultSet.Skip(itemsperpage * page).Take(itemsperpage)
                        .Contains(repo.GitHubRepositoryId)).ToList(), SearchResults[searchKey].Order,
                SearchResults[searchKey].OrderAttribute);
        }
    }
}
