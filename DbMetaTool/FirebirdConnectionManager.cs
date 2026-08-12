using FirebirdSql.Data.FirebirdClient;
using System.Data;

namespace DbMetaTool
{
	public class FirebirdConnectionManager(string connectionString)
	{
		private readonly string _connectionString = connectionString;

		public FbConnection CreateConnection()
		{
			return new FbConnection(_connectionString);
		}

		public void ExecuteNonQuery(string sql)
		{
			using var connection = CreateConnection();
			connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = sql;
			command.ExecuteNonQuery();
		}

		public DataTable ExecuteQuery(string sql)
		{
			using var connection = CreateConnection();
			connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = sql;
			using var reader = command.ExecuteReader();
			var dataTable = new DataTable();
			dataTable.Load(reader);
			return dataTable;
		}

		public static void CreateDatabase(string dbPath)
		{
			if (File.Exists(dbPath))
				File.Delete(dbPath);

			var directory = Path.GetDirectoryName(dbPath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			var appconfigservice = new AppConfigService();

			var connectionString = appconfigservice.GetConnectionString(dbPath);

			try
			{
				FbConnection.CreateDatabase(connectionString);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Debug - Connection String 1: {connectionString}");
				Console.WriteLine($"Debug - Error: {ex.Message}");
				throw;
			}
		}
	}
}
