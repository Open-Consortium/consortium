using System;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;

namespace OpenSim.Services.ExperienceService
{
    public class ExperienceServiceBase : ServiceBase
    {
        protected IExperienceData m_Database = null;
        protected IExperienceKeyValue m_KeyValueDatabase = null;

        public ExperienceServiceBase(IConfigSource config)
            : base(config)
        {
            string dllName = String.Empty;
            string connString = String.Empty;
            string kvconnString = String.Empty;
            string realm = "experiences";

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (connString == String.Empty)
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            //
            // [ExperienceService] section overrides [DatabaseService], if it exists
            //
            IConfig presenceConfig = config.Configs["ExperienceService"];
            if (presenceConfig != null)
            {
                dllName = presenceConfig.GetString("StorageProvider", dllName);
                connString = presenceConfig.GetString("ConnectionString", connString);
                kvconnString = presenceConfig.GetString("KeyValueConnectionString", kvconnString);
                realm = presenceConfig.GetString("Realm", realm);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName.Equals(String.Empty))
                throw new Exception("No StorageProvider configured");

            m_Database = LoadPlugin<IExperienceData>(dllName, new Object[] { connString });
            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module " + dllName);

            if (kvconnString.Equals(String.Empty))
                throw new Exception("No StorageProvider configured");

            m_KeyValueDatabase = LoadPlugin<IExperienceKeyValue>(dllName, new Object[] { kvconnString });
            if (m_KeyValueDatabase == null)
                throw new Exception("Could not find a storage interface in the given module " + dllName);

        }
    }
}
