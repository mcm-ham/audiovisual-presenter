using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace Presenter.App_Code
{
    public partial class Flag
    {
        public Color SystemColor
        {
            get { return (Color)ColorConverter.ConvertFromString(Colour); }
        }
    }
}
