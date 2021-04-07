#region

using System.Collections.Generic;
using System.IO;
using RakeExtraction = Rake.Rake;

#endregion

namespace XRealiSE_Crawler.KeywordExtractor
{
    internal sealed class RakeExtractor : BasicExtractor
    {
        private readonly RakeExtraction _rake;

        public RakeExtractor(int maxWordsLength)
        {
            _rake = new RakeExtraction(LoadStopWords("SmartStoplist.txt"), 2, maxWordsLength);
        }

        internal override Dictionary<string, double> GetKeywords(string text)
        {
            Dictionary<string, double> keywords = _rake.Run(text);
            if (keywords.Count == 0)
                return keywords;

            return normalize(keywords);
        }

        private static HashSet<string> LoadStopWords(string path)
        {
            HashSet<string> words = new HashSet<string>();
            using StreamReader f = File.OpenText(path);
            while (!f.EndOfStream)
            {
                string line = f.ReadLine();
                if (line?[0] != '#')
                    words.Add(line);
            }

            return words;
        }
    }
}
