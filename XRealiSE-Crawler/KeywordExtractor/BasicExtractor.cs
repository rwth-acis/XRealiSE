#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace XRealiSE_Crawler.KeywordExtractor
{
    internal abstract class BasicExtractor
    {
        internal abstract Dictionary<string, double> GetKeywords(string text);

        protected Dictionary<string, double> normalize(Dictionary<string, double> keywords)
        {
            return keywords.Values.Max() > 1
                ? keywords.ToDictionary(k => k.Key, k => k.Value / keywords.Values.Max())
                : keywords;
        }
    }
}
