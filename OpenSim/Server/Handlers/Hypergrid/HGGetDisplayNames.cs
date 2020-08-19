using System;
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;
using OpenSim.Framework.ServiceAuth;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class HGGetDisplayNames : ServiceConnector
    {
		private string m_ConfigName = "UserAccountService";

        IUserAccountService m_UserAccountService = null;

        // Called from Robust
        public HGGetDisplayNames(IConfigSource config, IHttpServer server, string configName)
        {
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            string service = serverConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (service == String.Empty)
                throw new Exception("No LocalServiceModule in config file");

            Object[] args = new Object[] { config };
            m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(service, args);

            IServiceAuth auth = ServiceAuth.Create(config, m_ConfigName);
            server.AddStreamHandler(new HGGetDisplayNamesPostHandler(m_UserAccountService));
        }
    }
}
