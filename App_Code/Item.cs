using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Presenter.App_Code
{
    public partial class Item
    {
        public string Name
        {
            get { return Filename.Substring(Filename.LastIndexOf("\\") + 1); }
        }
    }
}
