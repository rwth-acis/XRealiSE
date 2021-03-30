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
            InReadme,
            Classname
        }

        [Required] public KeywordInRepositoryType Type { get; set; }

        public double Weight { get; set; }
        public long KeywordId { get; set; }
        public long GitHubRepositoryId { get; set; }
        public virtual Keyword Keyword { get; set; }
        public virtual GitHubRepository Repository { get; set; }
    }
}
