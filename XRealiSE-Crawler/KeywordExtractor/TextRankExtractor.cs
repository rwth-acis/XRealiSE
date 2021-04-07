#region

using System.Collections.Generic;
using System.Linq;
using TextRank;

#endregion

namespace XRealiSE_Crawler.KeywordExtractor
{
    internal sealed class TextRankExtractor : BasicExtractor
    {
        internal override Dictionary<string, double> GetKeywords(string text)
        {
            List<string> keywords = text.KeyPhrases().Item2;
            return keywords != null && keywords.Count > 0
                ? keywords.ToDictionary(s => s, s => (double) (keywords.Count - keywords.IndexOf(s)) / keywords.Count)
                : new Dictionary<string, double>();
        }
    }
}
