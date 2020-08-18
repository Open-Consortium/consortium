using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Timers;

namespace OpenSim.Region.CoreModules.Avatar.DisplayNames
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "DisplayNames")]
    public class DisplayNamesModule : ISharedRegionModule, IDisplayNamesModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool enabled = true;

        private IUserManagement m_UserManager = null;
        private IUserAccountService m_UserAccountService = null;

        private Dictionary<UUID, NameInfo> m_DisplayNameCache = new Dictionary<UUID, NameInfo>();

        private Timer mCacheTimer = null;

        Scene m_Scene = null;

        #region IRegionModuleBase implementation

        public void Initialise(IConfigSource config)
        {
            IConfig cnf = config.Configs["DisplayNames"];
            if (cnf == null)
            {
                enabled = false;
                return;
            }

            if (cnf != null && cnf.GetString("Enabled", "false") != "true")
            {
                enabled = false;
                return;
            }

            if (!enabled)
                return;

            m_log.Info("[DisplayNames] Plugin enabled!");
            
            mCacheTimer = new Timer();
            mCacheTimer.AutoReset = true;
            mCacheTimer.Elapsed += MCacheTimer_Elapsed;
            mCacheTimer.Interval = 5 * 60 * 1000;
        }

        private void MCacheTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //m_log.Info("Clearing Display Name cache!");

            List<UUID> expired = new List<UUID>();

            foreach (KeyValuePair<UUID, NameInfo> pair in m_DisplayNameCache)
            {
                if (m_Scene.GetScenePresence(pair.Key) != null) continue;
                if (pair.Value.TimeCached.AddMinutes(10) < DateTime.Now)
                {
                    expired.Add(pair.Key);
                }
            }

            foreach (UUID key in expired)
                m_DisplayNameCache.Remove(key);
        }

        public void AddRegion(Scene scene)
        {
            if (!enabled)
                return;

            scene.EventManager.OnNewClient += (X) => 
            {
                ScenePresence sp = scene.GetScenePresence(X.AgentId);

                if (sp != null && sp.PresenceType != PresenceType.Npc)
                {
                    m_UserManager.RemoveUser(X.AgentId);
                    m_DisplayNameCache.Remove(X.AgentId);
                }
            };
        }

        public void RegionLoaded(Scene scene)
        {
            if (!enabled)
                return;

            m_Scene = scene;

            m_UserManager = scene.RequestModuleInterface<IUserManagement>();
            if (m_UserManager == null) return;

            m_UserAccountService = scene.RequestModuleInterface<IUserAccountService>();
            if (m_UserAccountService == null) return;

            scene.RegisterModuleInterface<IDisplayNamesModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!enabled)
                return;

            scene.UnregisterModuleInterface<IDisplayNamesModule>(this);
        }

        public void PostInitialise()
        {
            if (enabled)
                mCacheTimer.Start();
        }

        public string Name
        {
            get { return "DisplayNamesModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        #endregion

        #region Event Handlers
        #endregion

        public Dictionary<UUID, NameInfo> GetCachedDisplayNames(ref string[] ids)
        {
            Dictionary<UUID, NameInfo> result = new Dictionary<UUID, NameInfo>();
            List<string> id_list = new List<string>(ids);

            foreach (string key in ids)
            {
                UUID uuid;
                if (UUID.TryParse(key, out uuid))
                {
                    if (m_DisplayNameCache.ContainsKey(uuid))
                    {
                        result[uuid] = m_DisplayNameCache[uuid];
                        id_list.Remove(key);
                    }
                }
            }

            ids = id_list.ToArray();

            return result;
        }

        public Dictionary<UUID, NameInfo> GetDisplayNames(string[] ids)
        {
            Dictionary<UUID, NameInfo> result = GetCachedDisplayNames(ref ids);

            Dictionary<UUID, UserData> names = m_UserManager.GetUserDatas(ids, UUID.Zero, true);
            
            if (names.Count != 0)
            {
                foreach (KeyValuePair<UUID, UserData> kvp in names)
                {
                    if (kvp.Value == null)
                        continue;
                    if (kvp.Key == UUID.Zero)
                        continue;

                    UserData userdata = kvp.Value;
                    
                    // todo: add bad users list
                    if (kvp.Value.IsUnknownUser)
                        continue;

                    NameInfo nameInfo = null;

                    if (m_DisplayNameCache.TryGetValue(kvp.Key, out nameInfo))
                    {
                        result.Add(kvp.Key, nameInfo);
                        continue;
                    }

                    nameInfo = new NameInfo();
                    nameInfo.FirstName = userdata.FirstName;
                    nameInfo.LastName = userdata.LastName;
                    nameInfo.DisplayName = userdata.DisplayName;
                    nameInfo.HomeURI = userdata.HomeURL;
                    nameInfo.NameChanged = userdata.NameChanged;

                    result.Add(kvp.Key, nameInfo);
                    m_DisplayNameCache[kvp.Key] = nameInfo;
                }
            }

            return result;
        }

        public bool SetDisplayName(UUID agentID, string displayName, out NameInfo nameInfo)
        {
            Dictionary<UUID, NameInfo> names = GetDisplayNames(new string[] { agentID.ToString() });
            
            NameInfo name_info = null;
            if(names.TryGetValue(agentID, out name_info))
            {
                if (m_UserAccountService.SetDisplayName(agentID, displayName))
                {
                    name_info.DisplayName = displayName;
                    name_info.NameChanged = DateTime.UtcNow;

                    m_UserManager.RemoveUser(agentID);

                    m_DisplayNameCache[agentID] = name_info;

                    nameInfo = name_info;
                    return true;
                }
            }

            nameInfo = null;
            return false;
        }

        public string GetCachedDisplayName(string id)
        {
            UUID agentID;
            if(UUID.TryParse(id, out agentID))
            {
                if (m_DisplayNameCache.ContainsKey(agentID))
                {
                    NameInfo nameInfo = m_DisplayNameCache[agentID];
                    if (nameInfo.IsDefault) return nameInfo.Name;
                    else return nameInfo.DisplayName;
                }
            }
            return string.Empty;
        }

        public string GetDisplayName(string id)
        {
            UUID agentID;
            if (UUID.TryParse(id, out agentID))
            {
                var res = GetDisplayNames(new string[] { id });

                if (res.ContainsKey(agentID))
                {
                    NameInfo nameInfo = m_DisplayNameCache[agentID];
                    if (nameInfo.IsDefault) return nameInfo.Name;
                    else return nameInfo.DisplayName;
                }
            }
            return string.Empty;
        }
    }
}
