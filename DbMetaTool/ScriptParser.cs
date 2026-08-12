namespace DbMetaTool
{
	public class ParsedScript
	{
		public List<string> DomainScripts { get; set; } = [];
		public List<string> TableScripts { get; set; } = [];
		public List<string> ProcedureScripts { get; set; } = [];
	}

	public class ScriptParser
	{
		public ParsedScript ParseScriptsFromDirectory(string scriptsDirectory)
		{
			var result = new ParsedScript();

			if (!Directory.Exists(scriptsDirectory))
				throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsDirectory}");

			var sqlFiles = Directory.GetFiles(scriptsDirectory, "*.sql").OrderBy(f => f).ToList();
			if (sqlFiles.Count != 0)
			{
				result = ParseSqlFiles(sqlFiles);
			}
		
			return result;
		}

		private ParsedScript ParseSqlFiles(List<string> sqlFiles)
		{
			var result = new ParsedScript();

			foreach (var file in sqlFiles)
			{
				var content = File.ReadAllText(file);
				var statements = SplitSqlStatements(content);

				var fileName = Path.GetFileName(file).ToLowerInvariant();

				if (fileName.Contains("domain"))
				{
					result.DomainScripts.AddRange(statements);
				}
				else if (fileName.Contains("table"))
				{
					result.TableScripts.AddRange(statements);
				}
				else if (fileName.Contains("procedure"))
				{
					result.ProcedureScripts.AddRange(statements);
				}
				else
				{
					// Try to detect by content
					foreach (var stmt in statements)
					{
						if (stmt.Contains("CREATE DOMAIN", StringComparison.OrdinalIgnoreCase))
							result.DomainScripts.Add(stmt);
						else if (stmt.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
							result.TableScripts.Add(stmt);
						else if (stmt.Contains("CREATE PROCEDURE", StringComparison.OrdinalIgnoreCase))
							result.ProcedureScripts.Add(stmt);
					}
				}
			}

			return result;
		}

		private List<string> SplitSqlStatements(string content)
		{
			var statements = new List<string>();
			var lines = new List<string>();

			foreach (var line in content.Split('\n'))
			{
				if (string.IsNullOrEmpty(line.Trim()) || line.StartsWith("--"))
					continue;

				lines.Add(line);

				if (line.Trim().EndsWith(';') && line.Trim().Length == 1)
				{
					var statement = string.Join("\n", lines).TrimEnd(';').Trim();
					if (!string.IsNullOrEmpty(statement))
						statements.Add(statement);

					lines.Clear();
				}
			}

			return statements;
		}
	}
}
