#region

using System;

#endregion

namespace XRealiSE_Frontend
{
    public class SearchResult
    {
        private readonly long[] _resultSet;

        public SearchResult(string searchString, long[] resultSet, int order, int orderAttribute, int databaseId)
        {
            SearchString = searchString;
            _resultSet = resultSet;
            LastAccess = DateTime.Now;
            DatabaseId = databaseId;
            Order = order;
            OrderAttribute = orderAttribute;
        }

        public string SearchString { get; }

        public long[] ResultSet
        {
            get
            {
                LastAccess = DateTime.Now;
                return _resultSet;
            }
        }

        public DateTime LastAccess { get; private set; }
        public int Order { get; }
        public int OrderAttribute { get; }
        public int DatabaseId { get; }
    }
}
