namespace DbMetaTool
{
	public class DatabaseUpdater
	{
		public void UpdateDatabase(string connectionString, string scriptsDirectory)
		{
			if (!Directory.Exists(scriptsDirectory))
				throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsDirectory}");

			var connectionManager = new FirebirdConnectionManager(connectionString);

			Console.WriteLine("Extracting current database metadata...");
			var extractor = new MetadataExtractor(connectionManager);
			var currentDomains = extractor.ExtractDomains();
			var currentTables = extractor.ExtractTables();
			var currentProcedures = extractor.ExtractProcedures();

			Console.WriteLine($"Current database has:");
			Console.WriteLine($"  - {currentDomains.Count} domains");
			Console.WriteLine($"  - {currentTables.Count} tables");
			Console.WriteLine($"  - {currentProcedures.Count} procedures");

			Console.WriteLine("Parsing scripts from directory...");
			var parser = new ScriptParser();
			var scripts = parser.ParseScriptsFromDirectory(scriptsDirectory);

			try
			{
				if (scripts.DomainScripts.Count != 0)
				{
					Console.WriteLine("Updating domains...");
					ApplyDomainUpdates(connectionManager, scripts.DomainScripts, currentDomains);
				}

				if (scripts.TableScripts.Count != 0)
				{
					Console.WriteLine("Updating tables...");
					ApplyTableUpdates(connectionManager, scripts.TableScripts, currentTables);
				}

				if (scripts.ProcedureScripts.Count != 0)
				{
					Console.WriteLine("Updating procedures...");
					ApplyProcedureUpdates(connectionManager, scripts.ProcedureScripts, currentProcedures);
				}

				Console.WriteLine("Database updated successfully!");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error updating database: {ex.Message}");
				throw;
			}
		}

		private void ApplyDomainUpdates(FirebirdConnectionManager connectionManager, List<string> scripts, List<Domain> currentDomains)
		{
			foreach (var script in scripts)
			{
				if (string.IsNullOrWhiteSpace(script))
					continue;

				try
				{
					var domainName = ExtractObjectName(script, "CREATE DOMAIN");
					var exists = currentDomains.Any(d => d.Name.Equals(domainName, StringComparison.OrdinalIgnoreCase));

					if (exists)
					{
						Console.WriteLine($"  Domain '{domainName}' already exists, skipping.");
					}
					else
					{
						Console.WriteLine($"  Creating domain: {domainName}");
						connectionManager.ExecuteNonQuery(script);
						currentDomains.Add(new Domain { Name = domainName });
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"  Warning: Error applying domain update: {ex.Message}");
				}
			}
		}

		private void ApplyTableUpdates(FirebirdConnectionManager connectionManager, List<string> scripts, List<Table> currentTables)
		{
			foreach (var script in scripts)
			{
				if (string.IsNullOrWhiteSpace(script))
					continue;

				try
				{
					var tableName = ExtractObjectName(script, "CREATE TABLE");
					if (string.IsNullOrEmpty(tableName))
						continue;

					var existingTable = currentTables.FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

					if (existingTable != null)
					{
						Console.WriteLine($"  Table '{tableName}' exists. Checking for missing columns...");

						var scriptColumns = ParseColumnDefinitionsFromScript(script);

						foreach (var colDef in scriptColumns)
						{
							bool columnExists = existingTable.Columns.Any(c => c.Name.Equals(colDef.Name, StringComparison.OrdinalIgnoreCase));

							if (!columnExists)
							{
								string alterQuery = $"ALTER TABLE {tableName} ADD {colDef.RawDefinition};";
								Console.WriteLine($"  -> Adding column: {alterQuery}");

								connectionManager.ExecuteNonQuery(alterQuery);

								existingTable.Columns.Add(new Column { Name = colDef.Name });
							}
						}
					}
					else
					{
						Console.WriteLine($"  Creating new table: {tableName}");
						connectionManager.ExecuteNonQuery(script);

						currentTables.Add(new Table { Name = tableName });
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"  Error applying table update: {ex.Message}");
				}
			}
		}

		private class ParsedColumn
		{
			public string Name { get; set; } = string.Empty;
			public string RawDefinition { get; set; } = string.Empty;
		}

		private List<ParsedColumn> ParseColumnDefinitionsFromScript(string script)
		{
			var result = new List<ParsedColumn>();

			int firstParen = script.IndexOf('(');
			int lastParen = script.LastIndexOf(')');

			if (firstParen == -1 || lastParen == -1 || lastParen <= firstParen)
				return result;

			string body = script.Substring(firstParen + 1, lastParen - firstParen - 1);

			var rawLines = SplitByCommaOutsideParens(body);

			foreach (var line in rawLines)
			{
				string trimmed = line.Trim();
				if (string.IsNullOrWhiteSpace(trimmed))
					continue;

				if (trimmed.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase) ||
					trimmed.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
					trimmed.StartsWith("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
					trimmed.StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
					trimmed.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var parts = trimmed.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length > 0)
				{
					result.Add(new ParsedColumn
					{
						Name = parts[0].Trim(),
						RawDefinition = trimmed
					});
				}
			}

			return result;
		}

		private List<string> SplitByCommaOutsideParens(string text)
		{
			var result = new List<string>();
			int parenDepth = 0;
			int startIndex = 0;

			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c == '(') parenDepth++;
				else if (c == ')') parenDepth--;
				else if (c == ',' && parenDepth == 0)
				{
					result.Add(text[startIndex..i]);
					startIndex = i + 1;
				}
			}

			if (startIndex < text.Length)
			{
				result.Add(text[startIndex..]);
			}

			return result;
		}

		private void ApplyProcedureUpdates(FirebirdConnectionManager connectionManager, List<string> scripts, List<Procedure> currentProcedures)
		{
			foreach (var script in scripts)
			{
				if (string.IsNullOrWhiteSpace(script))
					continue;

				try
				{
					Console.WriteLine($"  Executing procedure script...");
					connectionManager.ExecuteNonQuery(script);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"  Warning: Error applying procedure update: {ex.Message}");
				}
			}
		}

		private string ExtractObjectName(string script, string keyword)
		{
			var idx = script.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
			if (idx == -1)
				return "UNKNOWN";

			var startIdx = idx + keyword.Length;
			var remaining = script[startIdx..].TrimStart();

			// Find the next non-identifier character
			var endIdx = 0;
			for (int i = 0; i < remaining.Length; i++)
			{
				if (!char.IsLetterOrDigit(remaining[i]) && remaining[i] != '_')
				{
					endIdx = i;
					break;
				}
				if (i == remaining.Length - 1)
					endIdx = i + 1;
			}

			return remaining[..endIdx].Trim();
		}
	}
}
