namespace OpenSim.Services.Interfaces
{
    public interface IAccessControlService
    {
        bool IsIPBanned(string ip);
        bool IsHardwareBanned(string mac, string id0);
    }
}
