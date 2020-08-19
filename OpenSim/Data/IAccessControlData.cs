namespace OpenSim.Data
{
    public interface IAccessControlData
    {
        bool IsHardwareBanned(string mac, string id0);
        bool IsIPBanned(string ip);

        bool BanIPAddress(string ip);
        bool UnbanIPAddress(string ip);

        bool BanMacAddress(string mac);
        bool UnbanMacAddress(string mac);

        bool BanID0(string id0);
        bool UnbanID0(string id0);
    }
}
