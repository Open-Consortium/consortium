using System.Reflection;
using System.Data;
using MySql.Data.MySqlClient;
using OpenSim.Framework;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.Packets;
using System.Linq;
using log4net;

namespace OpenSim.Data.MySQL
{
    public class MySqlExperienceData : MySqlFramework, IExperienceData
    {
        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlExperienceData(string connectionString)
                : base(connectionString)
        {
            m_connectionString = connectionString;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "Experience");
                m.Update();
                dbcon.Close();
            }
        }

        public Dictionary<UUID, bool> GetExperiencePermissions(UUID agent_id)
        {
            Dictionary<UUID, bool> experiencePermissions = new Dictionary<UUID, bool>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("select * from `experience_permissions` where avatar = ?avatar", dbcon))
                {
                    cmd.Parameters.AddWithValue("?avatar", agent_id);
                    
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            string uuid = result.GetString(0);
                            bool allow = result.GetBoolean(2);

                            UUID experience_key;
                            if(UUID.TryParse(uuid, out experience_key))
                            {
                                experiencePermissions.Add(experience_key, allow);
                            }
                        }

                        dbcon.Close();
                    }
                }
            }

            return experiencePermissions;
        }

        public bool ForgetExperiencePermissions(UUID agent_id, UUID experience_id)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("delete from `experience_permissions` where avatar = ?avatar AND experience = ?experience LIMIT 1", dbcon))
                {
                    cmd.Parameters.AddWithValue("?avatar", agent_id);
                    cmd.Parameters.AddWithValue("?experience", experience_id);

                    if (cmd.ExecuteNonQuery() > 0)
                        return true;
                    else return false;
                }
            }
        }

        public bool SetExperiencePermissions(UUID agent_id, UUID experience_id, bool allow)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("replace into `experience_permissions` (avatar, experience, allow) VALUES (?avatar, ?experience, ?allow)", dbcon))
                {
                    cmd.Parameters.AddWithValue("?avatar", agent_id);
                    cmd.Parameters.AddWithValue("?experience", experience_id);
                    cmd.Parameters.AddWithValue("?allow", allow);

                    if (cmd.ExecuteNonQuery() > 0)
                        return true;
                    else return false;
                }
            }
        }

        public ExperienceInfoData[] GetExperienceInfos(UUID[] experiences)
        {
            List<string> uuids = new List<string>();
            foreach (var u in experiences)
                uuids.Add("'" + u.ToString() + "'");
            string joined = string.Join(",", uuids);

            List<ExperienceInfoData> infos = new List<ExperienceInfoData>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE public_id IN (" + joined + ")", dbcon))
                {
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            ExperienceInfoData info = new ExperienceInfoData();
                            info.public_id = UUID.Parse(result["public_id"].ToString());
                            info.owner_id = UUID.Parse(result["owner_id"].ToString());
                            info.group_id = UUID.Parse(result["group_id"].ToString());
                            info.name = result["name"].ToString();
                            info.description = result["description"].ToString();
                            info.logo = UUID.Parse(result["logo"].ToString());
                            info.marketplace = result["marketplace"].ToString();
                            info.slurl = result["slurl"].ToString();
                            info.maturity = int.Parse(result["maturity"].ToString());
                            info.properties = int.Parse(result["properties"].ToString());

                            infos.Add(info);
                        }
                    }
                }

                dbcon.Close();
            }

            return infos.ToArray();
        }

        public UUID[] GetAgentExperiences(UUID agent_id)
        {
            List<UUID> experiences = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE owner_id = ?avatar", dbcon))
                {
                    cmd.Parameters.AddWithValue("?avatar", agent_id);
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            experiences.Add(UUID.Parse(result["public_id"].ToString()));
                        }
                    }
                }

                dbcon.Close();
            }

            return experiences.ToArray();
        }

        public bool UpdateExperienceInfo(ExperienceInfoData data)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("replace into `experiences` (public_id, owner_id, name, description, group_id, logo, marketplace, slurl, maturity, properties) VALUES (?public_id, ?owner_id, ?name, ?description, ?group_id, ?logo, ?marketplace, ?slurl, ?maturity, ?properties)", dbcon))
                {
                    cmd.Parameters.AddWithValue("?public_id", data.public_id);
                    cmd.Parameters.AddWithValue("?owner_id", data.owner_id);
                    cmd.Parameters.AddWithValue("?name", data.name);
                    cmd.Parameters.AddWithValue("?description", data.description);
                    cmd.Parameters.AddWithValue("?group_id", data.group_id);
                    cmd.Parameters.AddWithValue("?logo", data.logo);
                    cmd.Parameters.AddWithValue("?marketplace", data.marketplace);
                    cmd.Parameters.AddWithValue("?slurl", data.slurl);
                    cmd.Parameters.AddWithValue("?maturity", data.maturity);
                    cmd.Parameters.AddWithValue("?properties", data.properties);

                    if (cmd.ExecuteNonQuery() > 0)
                        return true;
                    else return false;
                }
            }
        }

        public ExperienceInfoData[] FindExperiences(string search)
        {
            List<ExperienceInfoData> experiences = new List<ExperienceInfoData>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE name LIKE ?search", dbcon))
                {
                    cmd.Parameters.AddWithValue("?search", string.Format("%{0}%", search));

                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            ExperienceInfoData info = new ExperienceInfoData();
                            info.public_id = UUID.Parse(result["public_id"].ToString());
                            info.owner_id = UUID.Parse(result["owner_id"].ToString());
                            info.group_id = UUID.Parse(result["group_id"].ToString());
                            info.name = result["name"].ToString();
                            info.description = result["description"].ToString();
                            info.logo = UUID.Parse(result["logo"].ToString());
                            info.marketplace = result["marketplace"].ToString();
                            info.slurl = result["slurl"].ToString();
                            info.maturity = int.Parse(result["maturity"].ToString());
                            info.properties = int.Parse(result["properties"].ToString());

                            experiences.Add(info);
                        }
                    }
                }

                dbcon.Close();
            }

            return experiences.ToArray();
        }

        public UUID[] GetGroupExperiences(UUID group_id)
        {
            List<UUID> experiences = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE group_id = ?group", dbcon))
                {
                    cmd.Parameters.AddWithValue("?group", group_id);
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            experiences.Add(UUID.Parse(result["public_id"].ToString()));
                        }
                    }
                }

                dbcon.Close();
            }

            return experiences.ToArray();
        }

        public UUID[] GetExperiencesForGroups(UUID[] groups)
        {
            List<string> uuids = new List<string>();
            foreach (var u in groups)
                uuids.Add("'" + u.ToString() + "'");
            string joined = string.Join(",", uuids);

            List<UUID> experiences = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE group_id IN (" + joined + ")", dbcon))
                {
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            experiences.Add(UUID.Parse(result["public_id"].ToString()));
                        }
                    }
                }

                dbcon.Close();
            }

            return experiences.ToArray();
        }
    }
}
