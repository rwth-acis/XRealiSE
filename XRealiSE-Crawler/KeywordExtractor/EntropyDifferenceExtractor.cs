#region

using System.Collections.Generic;
using KeywordExtraction;

#endregion

namespace XRealiSE_Crawler.KeywordExtractor
{
    internal sealed class EntropyDifferenceExtractor : BasicExtractor
    {
        private readonly bool _max;

        public EntropyDifferenceExtractor(bool max = false)
        {
            _max = max;
        }

        internal override Dictionary<string, double> GetKeywords(string text)
        {
            Dictionary<string, double> keywords = new Dictionary<string, double>();
            WORDSFRE[] wf = MyFun.StatisticsWords(MyFun.DocStandardization(MyFun.RemoveStop(text)));

            for (int i = 0; i < wf.Length; i++)
                if (_max)
                    wf[i].EntropyDifference_Max();
                else
                    wf[i].EntropyDifference_Normal();

            int wordsNum = 0;
            MyFun.QuickSort(wf, 0, wf.Length - 1);
            foreach (WORDSFRE t in wf)
                if (t.ED > 0)
                    wordsNum++;
                else
                    break;

            for (int i = 0; i < wordsNum; i++)
                keywords.Add(wf[i].Word, wf[i].ED);

            if (keywords.Count == 0)
                return keywords;

            return normalize(keywords);
        }
    }
}
