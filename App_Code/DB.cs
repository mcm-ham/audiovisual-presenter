using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SongPresenter.App_Code
{
    public class DB
    {
        private static DatabaseEntities _data;
        public static DatabaseEntities Instance
        {
            get
            {
                if (_data == null)
                    _data = new DatabaseEntities();
                return _data;
            }
        }
    }
}
