#region

using System.ComponentModel.DataAnnotations;
using MySql.EntityFrameworkCore.DataAnnotations;

#endregion

namespace XRealiSE_DBConnection.data
{
    [MySqlCharset("utf8")]
    public class KeywordInRepository
    {
        public enum KeywordInRepositoryType
        {
            Classname,
            InReadmeRake1,
            InReadmeRake2,
            InReadmeRake3,
            InReadmeRake4,
            InReadmeTextRank,
            InReadmeEdNormal,
            InReadmeEdMax,
            UnityVersion
        }

        [Required] public KeywordInRepositoryType Type { get; set; }

        public double Weight { get; set; }
        public long KeywordId { get; set; }
        public long GitHubRepositoryId { get; set; }
        public virtual Keyword Keyword { get; set; }
        public virtual GitHubRepository Repository { get; set; }
    }
}
