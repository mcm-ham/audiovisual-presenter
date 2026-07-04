using System;
using System.Data;
using System.Data.SqlServerCe;
using System.IO;
using System.Text.Json;

namespace Presenter.SdfExport
{
    /// <summary>
    /// One-time exporter: reads a legacy SQL Server Compact Database.sdf and writes its
    /// Schedules/Items/Flags to JSON for import into the new SQLite database.
    /// Runs on .NET Framework 4.8 because the SQL CE provider does not run on .NET 10.
    /// The JSON shape must stay in sync with Presenter.Data/Legacy/LegacyDatabaseDto.cs.
    ///
    /// Usage: Presenter.SdfExport &lt;input.sdf&gt; &lt;output.json&gt;
    /// Exit codes: 0 ok, 1 usage error, 2 SQL CE runtime missing, 3 input not found, 4 export failed.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: Presenter.SdfExport <input.sdf> <output.json>");
                return 1;
            }

            string sdfPath = args[0];
            string outPath = args[1];

            if (!File.Exists(sdfPath))
            {
                Console.Error.WriteLine("Input file not found: " + sdfPath);
                return 3;
            }

            // Work on a copy: opening can require an in-place format upgrade (3.5 -> 4.0)
            // and we must not modify the user's original file.
            string tempSdf = Path.Combine(Path.GetTempPath(), "presenter-export-" + Guid.NewGuid().ToString("N") + ".sdf");
            try
            {
                File.Copy(sdfPath, tempSdf, overwrite: true);
                string connString = "Data Source=" + tempSdf;

                try
                {
                    Export(connString, outPath);
                }
                catch (SqlCeInvalidDatabaseFormatException)
                {
                    // 3.5-format database: upgrade the copy, then export
                    new SqlCeEngine(connString).Upgrade();
                    Export(connString, outPath);
                }

                return 0;
            }
            catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or FileNotFoundException or BadImageFormatException)
            {
                Console.Error.WriteLine("SQL Server Compact 4.0 runtime is not installed: " + ex.Message);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Export failed: " + ex.Message);
                return 4;
            }
            finally
            {
                try { if (File.Exists(tempSdf)) File.Delete(tempSdf); } catch { }
            }
        }

        private static void Export(string connString, string outPath)
        {
            var export = new LegacyDatabase();

            using (var conn = new SqlCeConnection(connString))
            {
                conn.Open();

                using (var reader = ExecuteReader(conn, "SELECT ID, Date, Name FROM Schedules"))
                    while (reader.Read())
                        export.Schedules.Add(new LegacySchedule
                        {
                            ID = reader.GetGuid(0),
                            Date = reader.GetDateTime(1),
                            Name = reader.GetString(2),
                        });

                using (var reader = ExecuteReader(conn, "SELECT ID, ScheduleID, Filename, Ordinal FROM Items"))
                    while (reader.Read())
                        export.Items.Add(new LegacyItem
                        {
                            ID = reader.GetGuid(0),
                            ScheduleID = reader.GetGuid(1),
                            Filename = reader.GetString(2),
                            Ordinal = reader.GetInt16(3),
                        });

                using (var reader = ExecuteReader(conn, "SELECT ItemID, [Index], Colour FROM Flags"))
                    while (reader.Read())
                        export.Flags.Add(new LegacyFlag
                        {
                            ItemID = reader.GetGuid(0),
                            Index = reader.GetInt16(1),
                            Colour = reader.GetString(2),
                        });
            }

            File.WriteAllText(outPath, JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Exported " + export.Schedules.Count + " schedules, " + export.Items.Count + " items, " + export.Flags.Count + " flags.");
        }

        private static SqlCeDataReader ExecuteReader(SqlCeConnection conn, string sql)
        {
            // command intentionally not disposed here: the reader must stay open;
            // it is released when the connection closes (one-shot tool)
            var cmd = new SqlCeCommand(sql, conn);
            return cmd.ExecuteReader(CommandBehavior.Default);
        }
    }

    // JSON contract — keep in sync with Presenter.Data/Legacy/LegacyDatabaseDto.cs
    internal class LegacyDatabase
    {
        public System.Collections.Generic.List<LegacySchedule> Schedules { get; set; } = new System.Collections.Generic.List<LegacySchedule>();
        public System.Collections.Generic.List<LegacyItem> Items { get; set; } = new System.Collections.Generic.List<LegacyItem>();
        public System.Collections.Generic.List<LegacyFlag> Flags { get; set; } = new System.Collections.Generic.List<LegacyFlag>();
    }

    internal class LegacySchedule
    {
        public Guid ID { get; set; }
        public DateTime Date { get; set; }
        public string Name { get; set; }
    }

    internal class LegacyItem
    {
        public Guid ID { get; set; }
        public Guid ScheduleID { get; set; }
        public string Filename { get; set; }
        public short Ordinal { get; set; }
    }

    internal class LegacyFlag
    {
        public Guid ItemID { get; set; }
        public short Index { get; set; }
        public string Colour { get; set; }
    }
}
