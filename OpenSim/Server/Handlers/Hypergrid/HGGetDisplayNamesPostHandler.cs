using log4net;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class HGGetDisplayNamesPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IUserAccountService m_UserAccountService;

        public HGGetDisplayNamesPostHandler(IUserAccountService userAccountService) :
                base("POST", "/get_display_names")
        {
            m_UserAccountService = userAccountService;

            if (m_UserAccountService == null)
                m_log.ErrorFormat("[HGGetDisplayNames Handler]: UserAccountService is null!");
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string body;
            using(StreamReader sr = new StreamReader(requestData))
                body = sr.ReadToEnd();
            body = body.Trim();
            
            //m_log.DebugFormat("[get_display_names]: query String: {0}", body);

            Dictionary<string, object> request = ServerUtils.ParseQueryString(body);
            
            
            List<string> userIDs;

            if (!request.ContainsKey("AgentIDs"))
            {
                m_log.DebugFormat("[GRID USER HANDLER]: get_display_names called without required uuids argument");
                return new byte[0];
            }

            if (!(request["AgentIDs"] is List<string>))
            {
                m_log.DebugFormat("[GRID USER HANDLER]: get_display_names input argument was of unexpected type {0}", request["uuids"].GetType().ToString());
                return new byte[0];
            }

            userIDs = (List<string>)request["AgentIDs"];

            List<UserAccount> userAccounts = m_UserAccountService.GetUserAccounts(UUID.Zero, userIDs);

            Dictionary<string, object> result = new Dictionary<string, object>();

            int i = 0;
            foreach(UserAccount user in userAccounts)
            {
                result["uuid" + i] = user.PrincipalID;
                result["name" + i] = user.DisplayName;
                i++;
            }

            result["success"] = "true";
			
            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.InfoFormat("[get_display_name]: response string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }
    }
}
