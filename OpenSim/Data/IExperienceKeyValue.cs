using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    public interface IExperienceKeyValue
    {
        string GetKeyValue(UUID experience, string key);
        bool SetKeyValue(UUID experience, string key, string val);
        bool CreateKeyValueTable(UUID experience);
        bool DeleteKey(UUID experience, string key);
        int GetKeyCount(UUID experience);
        string[] GetKeys(UUID experience, int start, int count);
        int GetSize(UUID experience);
    }
}
