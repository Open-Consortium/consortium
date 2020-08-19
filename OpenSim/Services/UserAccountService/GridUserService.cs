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

        private GridUserData GetGridUserData(string userID)
        {
            GridUserData d = null;
            if (userID.Length > 36) // it's a UUI
            {
                d = m_Database.Get(userID);
            }
            else // it's a UUID
            {
                GridUserData[] ds = m_Database.GetAll(userID);
                if (ds == null)
                    return null;

                if (ds.Length > 0)
                {
                    d = ds[0];
                    foreach (GridUserData dd in ds)
                        if (dd.UserID.Length > d.UserID.Length) // find the longest
                            d = dd;
                }
            }

            return d;
        }

        public virtual GridUserInfo GetGridUserInfo(string userID)
        {
            GridUserData d = GetGridUserData(userID);

            if (d == null)
                return null;

            GridUserInfo info = new GridUserInfo();
            info.UserID = d.UserID;
            info.HomeRegionID = new UUID(d.Data["HomeRegionID"]);
            info.HomePosition = Vector3.Parse(d.Data["HomePosition"]);
            info.HomeLookAt = Vector3.Parse(d.Data["HomeLookAt"]);

            info.LastRegionID = new UUID(d.Data["LastRegionID"]);
            info.LastPosition = Vector3.Parse(d.Data["LastPosition"]);
            info.LastLookAt = Vector3.Parse(d.Data["LastLookAt"]);

            info.Online = bool.Parse(d.Data["Online"]);
            info.Login = Util.ToDateTime(Convert.ToInt32(d.Data["Login"]));
            info.Logout = Util.ToDateTime(Convert.ToInt32(d.Data["Logout"]));

			info.DisplayName = d.Data.ContainsKey("DisplayName") ? d.Data["DisplayName"] : string.Empty;

            if (d.Data.ContainsKey("NameCached"))
            {
                if(d.Data["NameCached"] != null)
                {
                    if (!string.IsNullOrWhiteSpace(d.Data["NameCached"]))
                        info.NameCached = Util.ToDateTime(Convert.ToInt32(d.Data["NameCached"]));
                }
            }

            if(info.NameCached == null) info.NameCached = DateTime.MinValue;
            
            return info;
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

            return GetGridUserInfo(userID);
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

            return m_Database.Store(d);
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

            return m_Database.Store(d);
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

            return m_Database.Store(d);
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