namespace DbMetaTool
{
	public static class Program
	{
		// Example usage:
		// DbMetaTool build-db --db-dir "C:\db\fb5" --scripts-dir "C:\scripts"
		// DbMetaTool export-scripts --connection-string "..." --output-dir "C:\out"
		// DbMetaTool update-db --connection-string "..." --scripts-dir "C:\scripts"
		public static int Main(string[] args)
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Użycie:");
				Console.WriteLine("  build-db --db-dir <ścieżka> --scripts-dir <ścieżka>");
				Console.WriteLine("  export-scripts --connection-string <connStr> --output-dir <ścieżka>");
				Console.WriteLine("  update-db --connection-string <connStr> --scripts-dir <ścieżka>");
				return 1;
			}

			try
			{
				var command = args[0].ToLowerInvariant();

				switch (command)
				{
					case "build-db":
						{
							string dbDir = GetArgValue(args, "--db-dir");
							string scriptsDir = GetArgValue(args, "--scripts-dir");

							BuildDatabase(dbDir, scriptsDir);
							Console.WriteLine("Database built successfully.");
							return 0;
						}

					case "export-scripts":
						{
							string connStr = GetArgValue(args, "--connection-string");
							string outputDir = GetArgValue(args, "--output-dir");

							ExportScripts(connStr, outputDir);
							Console.WriteLine("Scripts exported successfully.");
							return 0;
						}

					case "update-db":
						{
							string connStr = GetArgValue(args, "--connection-string");
							string scriptsDir = GetArgValue(args, "--scripts-dir");

							UpdateDatabase(connStr, scriptsDir);
							Console.WriteLine("Database updated successfully.");
							return 0;
						}

					default:
						Console.WriteLine($"Unknown command: {command}");
						return 1;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error: " + ex.Message);
				return -1;
			}
		}

		private static string GetArgValue(string[] args, string name)
		{
			int idx = Array.IndexOf(args, name);
			if (idx == -1 || idx + 1 >= args.Length)
				throw new ArgumentException($"Missing required argument {name}");
			return args[idx + 1];
		}

		/// <summary>
		/// Builds a new Firebird 5.0 database based on scripts.
		/// </summary>
		public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
		{
			if (string.IsNullOrWhiteSpace(databaseDirectory))
				throw new ArgumentException("Database directory cannot be empty", nameof(databaseDirectory));
			if (string.IsNullOrWhiteSpace(scriptsDirectory))
				throw new ArgumentException("Scripts directory cannot be empty", nameof(scriptsDirectory));


			var builder = new DbMetaTool.DatabaseBuilder();
			builder.BuildDatabase(databaseDirectory, scriptsDirectory);
		}

		/// <summary>
		/// Generates metadata scripts from an existing Firebird 5.0 database.
		/// </summary>
		public static void ExportScripts(string connectionString, string outputDirectory)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));
			if (string.IsNullOrWhiteSpace(outputDirectory))
				throw new ArgumentException("Output directory cannot be empty", nameof(outputDirectory));

			var connectionManager = new DbMetaTool.FirebirdConnectionManager(connectionString);
			var extractor = new DbMetaTool.MetadataExtractor(connectionManager);

			Console.WriteLine("Extracting metadata from database...");
			var domains = extractor.ExtractDomains();
			var tables = extractor.ExtractTables();
			var procedures = extractor.ExtractProcedures();

			Console.WriteLine($"Extracted {domains.Count} domains, {tables.Count} tables, {procedures.Count} procedures");

			var generator = new DbMetaTool.ScriptGenerator();
			generator.GenerateScripts(domains, tables, procedures, outputDirectory, DbMetaTool.ScriptGenerator.OutputFormat.SQL);
		}

		/// <summary>
		/// Updates an existing Firebird 5.0 database based on scripts.
		/// </summary>
		public static void UpdateDatabase(string connectionString, string scriptsDirectory)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));
			if (string.IsNullOrWhiteSpace(scriptsDirectory))
				throw new ArgumentException("Scripts directory cannot be empty", nameof(scriptsDirectory));

			var updater = new DbMetaTool.DatabaseUpdater();
			updater.UpdateDatabase(connectionString, scriptsDirectory);
		}
	}
}
