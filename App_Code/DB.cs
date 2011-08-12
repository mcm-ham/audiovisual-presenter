using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.EntityClient;
using System.Data.Metadata.Edm;
using System.Reflection;
using System.Data.SqlServerCe;
using System.Configuration;

namespace Presenter.App_Code
{
    public class DB
    {
        private static DatabaseEntities _data;
        public static DatabaseEntities Instance
        {
            get
            {
                if (_data == null)
                {
                    //<add name="DatabaseEntities" connectionString="metadata=res://*/App_Code.Model.csdl|res://*/App_Code.Model.ssdl|res://*/App_Code.Model.msl;provider=System.Data.SqlServerCe.4.0;provider connection string=&quot;Data Source=|DataDirectory|\Database.sdf&quot;" providerName="System.Data.EntityClient" />

                    EntityConnection connection = new EntityConnection(
                        new MetadataWorkspace(
                            new string[] { "res://*/App_Code.Model.csdl", "res://*/App_Code.Model.ssdl", "res://*/App_Code.Model.msl" },
                            new Assembly[] { Assembly.GetExecutingAssembly() }
                        ),
                        new SqlCeConnection(ConfigurationManager.ConnectionStrings["DatabaseConnectionString"].ConnectionString)
                    );
                    
                    _data = new DatabaseEntities(connection);
                }
                return _data;
            }
        }
    }
}
