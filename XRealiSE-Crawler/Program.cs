#region

using System;
using System.Threading.Tasks;
using static System.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

#endregion

namespace XRealiSE_Crawler
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            RootCommand rootCommand = new RootCommand
            {
                new Option<string>("--github-api-key", "A github api key"),
                new Option<string>("--mysql-host", "The host for the MySQL server [default: localhost]"),
                new Option<string>("--mysql-username", "The username for the MySQL database [default: root]"),
                new Option<string>("--mysql-password", "The password for the MySQL database"),
                new Option<string>("--mysql-database",
                    "The name of the database where all data will be stored [default: xrealise]"),
                new Option<int>("--mysql-port", "The connection port for the MySQL server [default: 3306]")
            };

            rootCommand.Handler = CommandHandler.Create<string, string, string, string, string, int>(
                async (gitHubApiKey, mySqlHost, mySqlUsername, mySqlPassword, mySqlDatabase, mySqlPort) =>
                {
                    Crawler crawler = new Crawler(gitHubApiKey);
                    DateTime before = DateTime.Now;
                    await crawler.Crawl(0, 50000);
                    WriteLine("Crawling done, took {0}. Exiting", DateTime.Now - before);
                });

            await rootCommand.InvokeAsync(args);
        }
    }
}
