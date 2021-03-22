using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace XRealiSE_DBConnection.data
{
    public class KeywordInRepository
    {
        public enum KeywordInRepositoryType
        {
            InReadme, Classname
        }
        [Required]
        public KeywordInRepositoryType Type { get; set; }
        public double Weight { get; set; }
        public long KeywordId { get; set; }
        public long GitHubRepositoryId { get; set; }
        public virtual Keyword Keyword { get; set; }
        public virtual GitHubRepository Repository { get; set; }
    }
}
