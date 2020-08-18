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
    public class MySqlExperienceKeyValue : MySqlFramework, IExperienceKeyValue
    {
        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlExperienceKeyValue(string connectionString)
                : base(connectionString)
        {
            m_connectionString = connectionString;
        }

        public bool SetKeyValue(UUID experience, string key, string val)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                string table_name = "exp_" + experience.ToString();

                using (MySqlCommand cmd = new MySqlCommand("replace into `" + table_name + "` (`key`, `value`) VALUES (?key, ?value)", dbcon))
                {
                    cmd.Parameters.AddWithValue("?key", key);
                    cmd.Parameters.AddWithValue("?value", val);

                    if (cmd.ExecuteNonQuery() > 0)
                        return true;
                    else return false;
                }
            }
        }

        public string GetKeyValue(UUID experience, string key)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                string table_name = "exp_" + experience.ToString();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `" + table_name + "` WHERE `key` = ?key LIMIT 1", dbcon))
                {
                    cmd.Parameters.AddWithValue("?key", key);
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        if (result.Read())
                        {
							return result["value"].ToString();
                        }
                    }
                }

                dbcon.Close();
            }

            return null;
        }

        public bool CreateKeyValueTable(UUID experience)
        {
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    string table_name = "exp_" + experience.ToString();

                    using (MySqlCommand cmd = new MySqlCommand("CREATE TABLE `" + table_name + "` (`key` VARCHAR(1011) NOT NULL COLLATE 'latin1_bin', `value` VARCHAR(4095) NOT NULL COLLATE 'utf8mb4_bin', PRIMARY KEY(`key`)) COLLATE = 'utf8mb4_bin' ENGINE = InnoDB", dbcon))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteKey(UUID experience, string key)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                string table_name = "exp_" + experience.ToString();

                using (MySqlCommand cmd = new MySqlCommand("DELETE FROM `" + table_name + "` WHERE `key` = ?key LIMIT 1", dbcon))
                {
                    cmd.Parameters.AddWithValue("?key", key);
                    if (cmd.ExecuteNonQuery() > 0)
                        return true;
                    else return false;
                }
            }
        }

        public int GetKeyCount(UUID experience)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                string table_name = "exp_" + experience.ToString();

                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) AS `count` FROM `" + table_name + "`", dbcon))
                {
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        if (result.Read())
                        {
                            return int.Parse(result["count"].ToString());
                        }
                    }
                }

                dbcon.Close();
            }

            return 0;
        }

        public string[] GetKeys(UUID experience, int start, int count)
        {
            List<string> keys = new List<string>();
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                string table_name = "exp_" + experience.ToString();

                using (MySqlCommand cmd = new MySqlCommand("SELECT `key` FROM `" + table_name + "` LIMIT ?start, ?count;", dbcon))
                {
                    cmd.Parameters.AddWithValue("?start", start);
                    cmd.Parameters.AddWithValue("?count", count);

                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            keys.Add(result["key"].ToString());
                        }
                    }
                }

                dbcon.Close();
            }
            return keys.ToArray();
        }

        public int GetSize(UUID experience)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                string table_name = "exp_" + experience.ToString();

                using (MySqlCommand cmd = new MySqlCommand("SELECT IFNULL(SUM(LENGTH(`key`) + LENGTH(`value`)), 0) AS `size` FROM `" + table_name + "`", dbcon))
                {
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        if (result.Read())
                        {
                            return int.Parse(result["size"].ToString());
                        }
                    }
                }

                dbcon.Close();
            }

            return 0;
        }
    }
}
