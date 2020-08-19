using System;
using Nini.Config;
using OpenSim.Data;
using OpenSim.Services.Base;

namespace OpenSim.Services.AccessControlService
{
    public class AccessControlServiceBase : ServiceBase
    {
        protected IAccessControlData m_Database;

        public AccessControlServiceBase(IConfigSource config) : base(config)
        {
            string dllName = String.Empty;
            string connString = String.Empty;

            //
            // Try reading the [AccessControlService] section first, if it exists
            //
            IConfig authConfig = config.Configs["AccessControlService"];
            if (authConfig != null)
            {
                dllName = authConfig.GetString("StorageProvider", dllName);
                connString = authConfig.GetString("ConnectionString", connString);
            }

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
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName == String.Empty)
                throw new Exception("No StorageProvider configured");

            m_Database = LoadPlugin<IAccessControlData>(dllName, new Object[] {connString});
            if (m_Database == null)
                throw new Exception(string.Format("Could not find a storage interface in module {0}", dllName));
        }

        public bool IsIPBanned(string ip)
        {
            return m_Database.IsIPBanned(ip);
        }

        public bool IsHardwareBanned(string mac, string id0)
        {
            return m_Database.IsHardwareBanned(mac, id0);
        }
    }
}
