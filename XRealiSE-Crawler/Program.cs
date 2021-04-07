#region

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using static System.Console;

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
                new Option<string>("--mysql-host", () => "localhost", "The host for the MySQL server"),
                new Option<string>("--mysql-username", () => "root", "The username for the MySQL database"),
                new Option<string>("--mysql-password", () => "", "The password for the MySQL database"),
                new Option<string>("--mysql-database", () => "xrealise",
                    "The name of the database where all data will be stored"),
                new Option<uint>("--mysql-port", () => 3306, "The connection port for the MySQL server")
            };

            rootCommand.Handler = CommandHandler.Create<string, string, string, string, string, uint>(
                async (gitHubApiKey, mySqlHost, mySqlUsername, mySqlPassword, mySqlDatabase, mySqlPort) =>
                {
                    Crawler crawler = new Crawler(gitHubApiKey, mySqlHost, mySqlUsername, mySqlPassword, mySqlDatabase,
                        mySqlPort);
                    DateTime before = DateTime.Now;
                    await crawler.Crawl(0, 50000);
                    WriteLine("Crawling done, took {0}. Exiting", DateTime.Now - before);
                });

            await rootCommand.InvokeAsync(args);
        }
    }
}
