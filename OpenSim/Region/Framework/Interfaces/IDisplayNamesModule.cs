using OpenMetaverse;
using System;
using System.Collections.Generic;

namespace OpenSim.Region.Framework.Interfaces
{
    public class NameInfo
    {
        public string FirstName = string.Empty;
        public string LastName = string.Empty;

        public string UserName
        {
            get
            {
                return LastName.ToLower() == "resident" ? FirstName.ToLower() : string.Format("{0}.{1}", FirstName, LastName).ToLower();
            }
        }

        public string Name
        {
            get { return LastName.ToLower() == "resident" ? FirstName : string.Format("{0} {1}", FirstName, LastName); }
        }

        private string mDisplayName = string.Empty;

        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(mDisplayName) ? Name : mDisplayName;
            }
            set { mDisplayName = value; }
        }

        public DateTime NameChanged = DateTime.MinValue;

        public bool IsDefault
        {
            get { return string.IsNullOrWhiteSpace(mDisplayName); }
        }

        public DateTime TimeCached = DateTime.Now;

        public string HomeURI = string.Empty;

        public bool IsLocal { get { return HomeURI == string.Empty; } }
    }

    public interface IDisplayNamesModule
    {
        Dictionary<UUID, NameInfo> GetDisplayNames(string[] agents);
        bool SetDisplayName(UUID agentID, string displayName, out NameInfo nameInfo);
        string GetCachedDisplayName(string id);
        string GetDisplayName(string id);
    }
}
