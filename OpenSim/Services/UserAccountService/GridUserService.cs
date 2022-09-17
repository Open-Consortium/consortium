/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using OpenSim.Services.Connectors.Hypergrid;
using System.Threading.Tasks;
using System.Linq;

namespace OpenSim.Services.UserAccountService
{
    public class GridUserService : GridUserServiceBase, IGridUserService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool m_Initialized;
        private static bool m_FetchDisplayNames = false;
        private static int m_DisplayNameCacheExpirationInHours = 12;

        public GridUserService(IConfigSource config) : base(config)
        {
            m_log.Debug("[GRID USER SERVICE]: Starting user grid service");

            if (!m_Initialized)
            {
                m_Initialized = true;

                MainConsole.Instance.Commands.AddCommand(
                    "Users", false,
                    "show grid user",
                    "show grid user <ID>",
                    "Show grid user entry or entries that match or start with the given ID.  This will normally be a UUID.",
                    "This is for debug purposes to see what data is found for a particular user id.",
                    HandleShowGridUser);

                MainConsole.Instance.Commands.AddCommand(
                    "Users", false,
                    "show grid users online",
                    "show grid users online",
                    "Show number of grid users registered as online.",
                    "This number may not be accurate as a region may crash or not be cleanly shutdown and leave grid users shown as online\n."
                    + "For this reason, users online for more than 5 days are not currently counted",
                    HandleShowGridUsersOnline);
            }

            IConfig gridUserServiceConfig = config.Configs["GridUserService"];
            if (gridUserServiceConfig != null)
            {
                m_FetchDisplayNames = gridUserServiceConfig.GetBoolean("FetchDisplayNames", m_FetchDisplayNames);
                m_DisplayNameCacheExpirationInHours = gridUserServiceConfig.GetInt("DisplayNamesCacheExpirationInHours", m_DisplayNameCacheExpirationInHours);
            }

            m_log.Info("[GRID USER SERVICE]: Fetch display names is " + (m_FetchDisplayNames ? "enabled" : "disabled"));
            
            if(m_FetchDisplayNames)
                m_log.InfoFormat("[GRID USER SERVICE]: HG display names cache expiration set to {0} hours", m_DisplayNameCacheExpirationInHours);
        }

        protected void HandleShowGridUser(string module, string[] cmdparams)
        {
            if (cmdparams.Length != 4)
            {
                MainConsole.Instance.Output("Usage: show grid user <UUID>");
                return;
            }

            GridUserData[] data = m_Database.GetAll(cmdparams[3]);

            foreach (GridUserData gu in data)
            {
                ConsoleDisplayList cdl = new ConsoleDisplayList();

                cdl.AddRow("User ID", gu.UserID);

                foreach (KeyValuePair<string,string> kvp in gu.Data)
                    cdl.AddRow(kvp.Key, kvp.Value);

                MainConsole.Instance.Output(cdl.ToString());
            }

            MainConsole.Instance.Output("Entries: {0}", data.Length);
        }

        protected void HandleShowGridUsersOnline(string module, string[] cmdparams)
        {
//            if (cmdparams.Length != 4)
//            {
//                MainConsole.Instance.Output("Usage: show grid users online");
//                return;
//            }

//            int onlineCount;
            int onlineRecentlyCount = 0;

            DateTime now = DateTime.UtcNow;

            foreach (GridUserData gu in m_Database.GetAll(""))
            {
                if (bool.Parse(gu.Data["Online"]))
                {
//                    onlineCount++;

                    int unixLoginTime = int.Parse(gu.Data["Login"]);

                    if ((now - Util.ToDateTime(unixLoginTime)).Days < 5)
                        onlineRecentlyCount++;
                }
            }

            MainConsole.Instance.Output("Users online: {0}", onlineRecentlyCount);
        }

        private static ExpiringCacheOS<string, GridUserData> cache = new ExpiringCacheOS<string, GridUserData>(100000);
        private GridUserData GetGridUserData(string userID)
        {
            if (userID.Length > 36)
                userID = userID.Substring(0, 36);

            if (cache.TryGetValue(userID, out GridUserData d))
               return d;

            GridUserData[] ds = m_Database.GetAll(userID);
            if (ds == null || ds.Length == 0)
            {
                cache.Add(userID, null, 300000);
                return null;
            }

            d = ds[0];
            if (ds.Length > 1)
            {
                // try find most recent record
                try
                {
                    int tsta = int.Parse(d.Data["Login"]);
                    int tstb = int.Parse(d.Data["Logout"]);
                    int cur = tstb > tsta? tstb : tsta;

                    for (int i = 1; i < ds.Length; ++i)
                    {
                        GridUserData dd = ds[i];
                        tsta = int.Parse(dd.Data["Login"]);
                        tstb = int.Parse(dd.Data["Logout"]);
                        if(tsta > tstb)
                            tstb = tsta;
                        if (tstb > cur) 
                        {
                            cur = tstb;
                            d = dd;
                        }
                    }
                }
                catch { }
            }
            cache.Add(userID, d, 300000);
            return d;
        }

        private GridUserInfo ToInfo(GridUserData d)
        {
            GridUserInfo info = new GridUserInfo() { UserID = d.UserID };

            string tmpstr;
            Dictionary<string, string> kvp = d.Data;

            if (kvp.TryGetValue("HomeRegionID", out tmpstr))
                UUID.TryParse(tmpstr, out info.HomeRegionID);

            if (kvp.TryGetValue("HomePosition", out tmpstr))
                Vector3.TryParse(tmpstr, out info.HomePosition);

            if (kvp.TryGetValue("HomeLookAt", out tmpstr))
                Vector3.TryParse(tmpstr, out info.HomeLookAt);

            if (kvp.TryGetValue("LastRegionID", out tmpstr))
                UUID.TryParse(tmpstr, out info.LastRegionID);

            if (kvp.TryGetValue("LastPosition", out tmpstr))
                Vector3.TryParse(tmpstr, out info.LastPosition);

            if (kvp.TryGetValue("LastLookAt", out tmpstr))
                Vector3.TryParse(tmpstr, out info.LastLookAt);
                
            if (kvp.TryGetValue("Online", out tmpstr))
                bool.TryParse(tmpstr, out info.Online);

            if (kvp.TryGetValue("Login", out tmpstr) && Int32.TryParse(tmpstr, out int login))
                info.Login = Util.ToDateTime(login);
            else
                info.Login = Util.UnixEpoch;

            if (kvp.TryGetValue("Logout", out tmpstr) && Int32.TryParse(tmpstr, out int logout))
                info.Logout = Util.ToDateTime(logout);
            else
                info.Logout = Util.UnixEpoch;
			
			if(kvp.TryGetValue("DisplayName", out tmpstr))
				info.DisplayName = tmpstr;
			else
				info.DisplayName = string.Empty;

            if (kvp.TryGetValue("NameCached", out tmpstr))
            {
                if(d.Data["NameCached"] != null)
                {
                    if (!string.IsNullOrWhiteSpace(tmpstr))
                        info.NameCached = Util.ToDateTime(Convert.ToInt32(tmpstr));
                }
            }
			else
				info.NameCached = DateTime.MinValue;

            return info;
        }

        public virtual GridUserInfo GetGridUserInfo(string userID)
        {
            GridUserData d = GetGridUserData(userID);

            if (d == null)
                return null;

            return ToInfo(d);
        }

        public virtual GridUserInfo[] GetGridUserInfo(string[] userIDs, bool update_name = false)
        {
            List<GridUserInfo> ret = new List<GridUserInfo>();

            foreach (string id in userIDs)
            {
                GridUserInfo userInfo = GetGridUserInfo(id);
                if (userInfo == null && update_name)
                {
                    userInfo = new GridUserInfo();
                    userInfo.UserID = id;
                }
                ret.Add(userInfo);
            }

            if (update_name)
                return UpdateDisplayNames(ret).ToArray();

            return ret.ToArray();
        }

        // < test updater>
        private List<GridUserInfo> UpdateDisplayNames(List<GridUserInfo> entries)
        {
            Dictionary<string, List<GridUserInfo>> grids_and_infos = new Dictionary<string, List<GridUserInfo>>();

            // Check for out-of-date names and separate them by their grid
            foreach(GridUserInfo info in entries)
            {
                if (info.UserID.Length > 36 && info.NameCached < DateTime.UtcNow.AddHours(-m_DisplayNameCacheExpirationInHours))
                {
                    UUID uuid;
                    string url, first, last, tmp;
                    
                    if (Util.ParseUniversalUserIdentifier(info.UserID, out uuid, out url, out first, out last, out tmp))
                    {
                        List<GridUserInfo> infos = null;

                        if (!grids_and_infos.TryGetValue(url, out infos))
                            infos = new List<GridUserInfo>();

                        infos.Add(info);

                        grids_and_infos[url] = infos;

                        //m_log.InfoFormat("Added {0} to {1} list", info.UserID, url);
                    }
                }
            }

            if (grids_and_infos.Count > 0)
            {
                Dictionary<string, string> results = new Dictionary<string, string>();

                // Start a task for each grid and request the display names
                List<Task> tasks = new List<Task>();
                foreach(KeyValuePair<string, List<GridUserInfo>> pair in grids_and_infos)
                {
                    Task<bool> wrapperTask = Task.Run(() => {
                        Dictionary<UUID, string> users = new Dictionary<UUID, string>();
                        foreach(GridUserInfo info in pair.Value)
                        {
                            UUID uuid;
                            string url, first, last, tmp;
                            
                            if (Util.ParseUniversalUserIdentifier(info.UserID, out uuid, out url, out first, out last, out tmp))
                            {
                                users.Add(uuid, info.UserID);
                            }
                        }

                        // m_log.InfoFormat("Fetching {0} display names from {1}", users.Count, pair.Key);

                        DisplayNameServiceConnector dnService = new DisplayNameServiceConnector(pair.Key);
                        Dictionary<UUID, string> vals = dnService.GetDisplayNames(users.Keys.ToArray());
                        foreach(KeyValuePair<UUID, string> name in vals)
                        {
                            results.Add(users[name.Key], name.Value);
                            //m_log.InfoFormat("Adding {0} to results ({1})", users[name.Key], name.Value);
                        }

                        //m_log.InfoFormat("Received {0} display names of {1} from {2}", vals.Count, users.Keys.Count, pair.Key);
                        
                        return true;
                    });
                    tasks.Add(wrapperTask);
                }

                //m_log.InfoFormat("Waiting for {0} tasks to finish!", tasks.Count);

                // Wait for all the tasks to finish
                Task.WaitAll(tasks.ToArray());

                //m_log.InfoFormat("Fetched {0} display names from {1} grids", results.Count, grids_and_infos.Count);

                // Go through the original entries and update their values and store it
                foreach(GridUserInfo info in entries)
                {
                    if(results.ContainsKey(info.UserID))
                    {
                        info.DisplayName = results[info.UserID];
                        info.NameCached = DateTime.UtcNow;
                        //m_log.InfoFormat("Updating display name of {0} to {1}", info.UserID, results[info.UserID]);
                        SetDisplayName(info.UserID, info.DisplayName);
                    }
                    else
                    {
                        //m_log.InfoFormat("No data received for {0}", info.UserID);
                        SetDisplayName(info.UserID, info.DisplayName);
                    }
                }
            }

            return entries;
        }
        // </test updater>

        public GridUserInfo LoggedIn(string userID)
        {
            m_log.DebugFormat("[GRID USER SERVICE]: User {0} is online", userID);

            GridUserData d = GetGridUserData(userID);

            if (d == null)
            {
                d = new GridUserData();
                d.UserID = userID;
            }

            d.Data["Online"] = true.ToString();
            d.Data["Login"] = Util.UnixTimeSinceEpoch().ToString();

            m_Database.Store(d);
            if (userID.Length >= 36)
                cache.Add(userID.Substring(0, 36), d, 300000);

            return ToInfo(d);
        }

        public bool LoggedOut(string userID, UUID sessionID, UUID regionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            m_log.DebugFormat("[GRID USER SERVICE]: User {0} is offline", userID);

            GridUserData d = GetGridUserData(userID);

            if (d == null)
            {
                d = new GridUserData();
                d.UserID = userID;
            }

            d.Data["Online"] = false.ToString();
            d.Data["Logout"] = Util.UnixTimeSinceEpoch().ToString();
            d.Data["LastRegionID"] = regionID.ToString();
            d.Data["LastPosition"] = lastPosition.ToString();
            d.Data["LastLookAt"] = lastLookAt.ToString();

            if(m_Database.Store(d))
            {
                if (userID.Length >= 36)
                    cache.Add(userID.Substring(0, 36), d, 300000);
                return true;
            }
            return false;
        }

        public bool SetHome(string userID, UUID homeID, Vector3 homePosition, Vector3 homeLookAt)
        {
            GridUserData d = GetGridUserData(userID);

            if (d == null)
            {
                d = new GridUserData();
                d.UserID = userID;
            }

            d.Data["HomeRegionID"] = homeID.ToString();
            d.Data["HomePosition"] = homePosition.ToString();
            d.Data["HomeLookAt"] = homeLookAt.ToString();

            if(m_Database.Store(d))
            {
                if (userID.Length >= 36)
                    cache.Add(userID.Substring(0, 36), d, 300000);
                return true;
            }
            return false;
        }

        public bool SetLastPosition(string userID, UUID sessionID, UUID regionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
//            m_log.DebugFormat("[GRID USER SERVICE]: SetLastPosition for {0}", userID);

            GridUserData d = GetGridUserData(userID);

            if (d == null)
            {
                d = new GridUserData();
                d.UserID = userID;
            }

            d.Data["LastRegionID"] = regionID.ToString();
            d.Data["LastPosition"] = lastPosition.ToString();
            d.Data["LastLookAt"] = lastLookAt.ToString();

            if(m_Database.Store(d))
            {
                if (userID.Length >= 36)
                    cache.Add(userID.Substring(0, 36), d, 300000);
                return true;
            }
            return false;
        }
        
		public bool SetDisplayName(string userID, string displayName)
        {
//            m_log.InfoFormat("[GRID USER SERVICE]: SetDisplayName for {0} to {1}", userID, displayName);

            GridUserData d = GetGridUserData(userID);

            if (d == null)
            {
                d = new GridUserData();
                d.UserID = userID;
            }

            d.Data["DisplayName"] = displayName;
            d.Data["NameCached"] = Util.UnixTimeSinceEpoch().ToString();

            return m_Database.Store(d);
        }
    }
}