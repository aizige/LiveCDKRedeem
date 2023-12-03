using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveCDKRedeem.Bean
{
    public class ResponseResult
    {

       public String code { get; set; }
       public String message { get; set; }
       public Embeddes embeddes { get; set; }
       public Links links { get; set; }
    }

    

    public class Embeddes
    {
        public Result result { get; set; }
    }

    public class Result
    {
        public int code { get; set; }
    }

    public class Links
    {
        public Self self { get; set; }
    }

    public class Self
    {
        public String href { get; set; }
    }
}
