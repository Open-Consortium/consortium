using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using OpenSim.Framework;
using OpenMetaverse;
using log4net;
using OpenSim.Server.Base;

namespace OpenSim.Services.Connectors.Hypergrid
{
    public class DisplayNameServiceConnector
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURLHost;
        private string m_ServerURL;

        public DisplayNameServiceConnector(string url) : this(url, true)
        {
        }

        public DisplayNameServiceConnector(string url, bool dnsLookup)
        {
            m_ServerURL = m_ServerURLHost = url;

            if (dnsLookup)
            {
                try
                {
                    Uri m_Uri = new Uri(m_ServerURL);
                    IPAddress ip = Util.GetHostFromDNS(m_Uri.Host);
                    if(ip != null)
                    {
                        m_ServerURL = m_ServerURL.Replace(m_Uri.Host, ip.ToString());
                        if (!m_ServerURL.EndsWith("/"))
                            m_ServerURL += "/";
                    }
                    else
                        m_log.DebugFormat("[DISPLAY NAME CONNECTOR]: Failed to resolve address of {0}", url);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[DISPLAY NAME CONNECTOR]: Malformed Uri {0}: {1}", url, e.Message);
                }
            }
        }

        public Dictionary<UUID, string> GetDisplayNames (UUID[] userIDs)
        {
            string uri = m_ServerURL + "get_display_names";
            
            List<string> str_userIDs = new List<string>();
            foreach(UUID id in userIDs) str_userIDs.Add(id.ToString());

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentIDs"] = new List<string>(str_userIDs);

            string reqString = ServerUtils.BuildQueryString(sendData);

            Dictionary<UUID, string> data = new Dictionary<UUID, string>();
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString, 5, null, false);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if((string)replyData?["success"] == "true")
                    {
                        int i = 0;
                        while(true)
                        {
                            if(replyData.ContainsKey("uuid" + i) && replyData.ContainsKey("name" + i))
                            {
                                string str_uuid = replyData["uuid" + i].ToString();
                                string name = replyData["name" + i].ToString();

                                UUID uuid = UUID.Parse(str_uuid);
                                data.Add(uuid, name);
                                i++;
                            }
                            else break;
                        }
                    }

                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[HGGetDisplayNames Connector]: Exception when contacting display name server at {0}: {1}", uri, e.Message);
            }

            return data;
        }

    }
}
