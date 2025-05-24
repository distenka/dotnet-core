using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Distenka.Client
{
    public class Pagination<T>
    {
        public bool HasMore { get; set; }
        public IEnumerable<T> Data { get; set; }
    }
}
