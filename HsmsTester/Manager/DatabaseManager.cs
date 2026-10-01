using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Manager
{

    public class DatabaseManager : IDisposable
    {
        public static DatabaseManager Instance { get;} = new DatabaseManager();

        private DatabaseManager() 
        {
            LibraryController.Instance.RegisterDisposable(this);
        
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

    }

    public class DataBaseDefine : IDisposable
    {
        public DataBaseDefine()
        {
            LibraryController.Instance.RegisterDisposable(this);

        }
        public string Path { get; set; } = string.Empty;
        private SQLiteConnection _sqlConnection = null;

        private object _dbLock = new object();

        public bool Init()
        {
            if (File.Exists(Path) == false)
            {
                if (CreateDB() == false)
                {
                    return false;
                }
            }

            _sqlConnection = new SQLiteConnection(Path);

            return true;
        }

        public bool ExecuteQuery(string sql, params object[] args)
        {
            lock (_dbLock)
            {
                try
                {
                    _sqlConnection.Open();

                    using (SQLiteCommand cmd = new SQLiteCommand(sql, _sqlConnection))
                    {
                        cmd.ExecuteNonQuery();

                    }
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    _sqlConnection.Close();
                }
            }
            return true;
        }

        public DataTable GettDataTable(string sql)
        {
            lock (_dbLock)
            {
                try
                {
                    _sqlConnection.Open();

                    DataTable outData = new DataTable();

                    using (SQLiteCommand cmd = new SQLiteCommand(sql, _sqlConnection))
                    {
                        using (SQLiteDataAdapter adapter = new SQLiteDataAdapter())
                        {
                            adapter.SelectCommand = cmd;

                            adapter.Fill(outData);

                            adapter.Dispose();
                        }
                        cmd.Dispose();
                    }

                    return outData;
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    _sqlConnection.Close();
                }
            }
            return new DataTable();
        }

        private bool CreateDB()
        {

            SQLiteConnection.CreateFile(Path);

            return true;
        }

        public void Dispose()
        {
            _sqlConnection?.Close();
            _sqlConnection?.Dispose();
        }
    }

}
