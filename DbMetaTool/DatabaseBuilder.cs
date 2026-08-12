namespace DbMetaTool
{
	public class DatabaseBuilder
	{
		public void BuildDatabase(string databasePath, string scriptsDirectory)
		{
			if (!Directory.Exists(scriptsDirectory))
				throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsDirectory}");

			string dbPath;
			if (databasePath.EndsWith(".fdb", StringComparison.OrdinalIgnoreCase))
			{
				dbPath = databasePath;
			}
			else
			{
				dbPath = Path.Combine(databasePath, "metadata.fdb");
			}

			var directory = Path.GetDirectoryName(dbPath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			Console.WriteLine($"Creating database at: {dbPath}");
			FirebirdConnectionManager.CreateDatabase(dbPath);

			var appconfigservice = new AppConfigService();
			var connectionManager = new FirebirdConnectionManager(appconfigservice.GetConnectionString(dbPath));

			var parser = new ScriptParser();
			var scripts = parser.ParseScriptsFromDirectory(scriptsDirectory);

			Console.WriteLine($"Found {scripts.DomainScripts.Count} domain scripts");
			Console.WriteLine($"Found {scripts.TableScripts.Count} table scripts");
			Console.WriteLine($"Found {scripts.ProcedureScripts.Count} procedure scripts");

			try
			{
				if (scripts.DomainScripts.Count != 0)
				{
					Console.WriteLine("Executing domain scripts...");
					ExecuteScripts(connectionManager, scripts.DomainScripts, "domain");
				}

				if (scripts.TableScripts.Count != 0)
				{
					Console.WriteLine("Executing table scripts...");
					ExecuteScripts(connectionManager, scripts.TableScripts, "table");
				}

				if (scripts.ProcedureScripts.Count != 0)
				{
					Console.WriteLine("Executing procedure scripts...");
					ExecuteScripts(connectionManager, scripts.ProcedureScripts, "procedure");
				}

				Console.WriteLine("Database built successfully!");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error building database: {ex.Message}");
				throw;
			}
		}

		private void ExecuteScripts(FirebirdConnectionManager connectionManager, List<string> scripts, string type)
		{
			foreach (var script in scripts)
			{
				if (string.IsNullOrWhiteSpace(script))
					continue;

				try
				{
					connectionManager.ExecuteNonQuery(script);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"  Warning: Error executing {type} script: {ex.Message}");
				}
			}
		}
	}
}
