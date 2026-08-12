namespace DbMetaTool
{
	public class ScriptGenerator
	{
		public enum OutputFormat
		{
			SQL
		}

		public void GenerateScripts(List<Domain> domains, List<Table> tables, List<Procedure> procedures, string outputDirectory, OutputFormat format = OutputFormat.SQL)
		{
			if (!Directory.Exists(outputDirectory))
				Directory.CreateDirectory(outputDirectory);

			switch (format)
			{
				case OutputFormat.SQL:
					GenerateSqlScripts(domains, tables, procedures, outputDirectory);
					break;
			}

			Console.WriteLine($"Scripts generated in: {outputDirectory}");
		}

		private void GenerateSqlScripts(List<Domain> domains, List<Table> tables, List<Procedure> procedures, string outputDirectory)
		{
			if (domains.Count != 0)
			{
				var domainsScript = GenerateDomainsSQL(domains);
				File.WriteAllText(Path.Combine(outputDirectory, "01_domains.sql"), domainsScript);
			}

			if (tables.Count != 0)
			{
				var tablesScript = GenerateTablesSQL(tables);
				File.WriteAllText(Path.Combine(outputDirectory, "02_tables.sql"), tablesScript);
			}

			if (procedures.Count != 0)
			{
				var proceduresScript = GenerateProceduresSQL(procedures);
				File.WriteAllText(Path.Combine(outputDirectory, "03_procedures.sql"), proceduresScript);
			}
		}

		private string GenerateDomainsSQL(List<Domain> domains)
		{
			var script = new System.Text.StringBuilder();
			script.AppendLine("-- Firebird Domains Script\n");

			foreach (var domain in domains)
			{
				script.Append($"CREATE DOMAIN {domain.Name} AS {domain.DataType}");

				if (domain.DataType.Contains("CHAR") || domain.DataType.Contains("VARCHAR"))
				{
					if (domain.Length.HasValue)
						script.Append($"({domain.Length})");
				}
				else if (domain.DataType == "NUMERIC" || domain.DataType == "DECIMAL")
				{
					if (domain.Precision.HasValue && domain.Scale.HasValue)
						script.Append($"({domain.Precision},{Math.Abs(domain.Scale.Value)})");
				}

				if (!string.IsNullOrEmpty(domain.DefaultValue))
					script.Append($"\n	{domain.DefaultValue}");

				if (domain.NotNull)
					script.Append("\n	NOT NULL");

				if(!string.IsNullOrEmpty(domain.ValidationSource))
					script.Append($"\n	{domain.ValidationSource}");

				script.AppendLine();
				script.AppendLine(";");
			}

			return script.ToString();
		}

		private string GenerateTablesSQL(List<Table> tables)
		{
			var script = new System.Text.StringBuilder();
			script.AppendLine("-- Firebird Tables Script\n");

			foreach (var table in tables)
			{

				script.AppendLine($"CREATE TABLE {table.Name} (");

				var tableElements = new List<string>();

				foreach (var col in table.Columns)
				{
					var def = $"  {col.Name} {col.DataType}";

					if (!col.IsDomain)
					{
						if (col.DataType.Contains("CHAR") || col.DataType.Contains("VARCHAR"))
						{
							if (col.Length.HasValue)
								def += $"({col.Length})";
						}
						else if (col.DataType == "NUMERIC" || col.DataType == "DECIMAL")
						{
							if (col.Precision.HasValue && col.Scale.HasValue)
								def += $"({col.Precision}, {Math.Abs(col.Scale.Value)})";
						}

						if (!string.IsNullOrEmpty(col.DefaultValue))
							def += $" {col.DefaultValue}";

						if (col.NotNull)
							def += " NOT NULL";
					}

					tableElements.Add(def);
				}

				// Primary Key Constraint
				var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
				if (pkColumns.Any())
				{
					string pkConstraintName = $"PK_{table.Name}";
					tableElements.Add($"  CONSTRAINT {pkConstraintName} PRIMARY KEY ({string.Join(", ", pkColumns)})");
				}

				// Unique Constraints (from table.Constraints list)
				if (table.TableConstraints != null)
				{
					var uniqueConstraints = table.TableConstraints
						.Where(c => c.ConstraintType.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase))
						.ToList();

					foreach (var unq in uniqueConstraints)
					{
						string unqName = $"UNQ_{table.Name}_{unq.ColumnName}";

						tableElements.Add($"  CONSTRAINT {unqName} UNIQUE ({unq.ColumnName})");
					}
				}

				// Check Constraints (Table or Column level)
				if (table.TableConstraints != null)
				{
					var checkConstraints = table.TableConstraints
						.Where(c => c.ConstraintType.Equals("CHECK", StringComparison.OrdinalIgnoreCase))
						.ToList();

					foreach (var chk in checkConstraints)
					{
						string chkName = !string.IsNullOrEmpty(chk.ConstraintName)
							? chk.ConstraintName
							: $"CHK_{table.Name}";

						tableElements.Add($"  CONSTRAINT {chkName} CHECK ({chk.CheckExpression})");
					}
				}

				// Foreign Key Constraints
				var fkColumns = table.Columns.Where(c => c.ForeignKey != null).ToList();
				foreach (var col in fkColumns)
				{
					var fk = col.ForeignKey!;
					var constraintName = !string.IsNullOrEmpty(fk.ConstraintName)
						? fk.ConstraintName
						: $"FK_{table.Name}_{col.Name}";

					var def = $"  CONSTRAINT {constraintName} FOREIGN KEY ({col.Name}) " +
							  $"REFERENCES {fk.ReferencedTable}({fk.ReferencedColumn})" +
							  BuildFkRules(fk);

					tableElements.Add(def);
				}

				script.AppendLine(string.Join(",\n", tableElements));
				script.AppendLine(")");
				script.AppendLine(";");
				script.AppendLine();
			}

			return script.ToString();
		}

		private string BuildFkRules(ForeignKeyInfo fk)
		{
			var rules = string.Empty;

			if (!string.IsNullOrEmpty(fk.UpdateRule) && !fk.UpdateRule.Equals("RESTRICT", StringComparison.OrdinalIgnoreCase))
				rules += $" ON UPDATE {fk.UpdateRule}";

			if (!string.IsNullOrEmpty(fk.DeleteRule) && !fk.DeleteRule.Equals("RESTRICT", StringComparison.OrdinalIgnoreCase))
				rules += $" ON DELETE {fk.DeleteRule}";

			return rules;
		}

		private string GenerateProceduresSQL(List<Procedure> procedures)
		{
			var script = new System.Text.StringBuilder();
			script.AppendLine("-- Firebird Procedures Script\n");

			foreach (var procedure in procedures)
			{
				script.Append($"CREATE OR ALTER PROCEDURE {procedure.Name}");

				if (procedure.Parameters.Count != 0)
				{
					var inputParams = procedure.Parameters.Where(p => p.ParameterType == "INPUT").ToList();
					var outputParams = procedure.Parameters.Where(p => p.ParameterType == "OUTPUT").ToList();

					if (inputParams.Count != 0)
					{
						script.Append(" (\n");
						foreach (var param in inputParams)
						{
							var paramDef = $"  {param.Name} {param.DataType}";
							if (param.DataType.Contains("CHAR") || param.DataType.Contains("VARCHAR"))
							{
								if (param.Length.HasValue)
									paramDef += $"({param.Length})";
							}
							else if (param.DataType == "NUMERIC" || param.DataType == "DECIMAL")
							{
								if (param.Precision.HasValue && param.Scale.HasValue)
									paramDef += $"({param.Precision}, {Math.Abs(param.Scale.Value)})";
							}
							script.AppendLine(paramDef + (inputParams.Last() == param ? "" : ","));
						}
						script.AppendLine(")");
					}

					if (outputParams.Count != 0)
					{
						script.AppendLine("\nRETURNS (");
						foreach (var param in outputParams)
						{
							var paramDef = $"  {param.Name} {param.DataType}";
							if (param.DataType.Contains("CHAR") || param.DataType.Contains("VARCHAR"))
							{
								if (param.Length.HasValue)
									paramDef += $"({param.Length})";
							}
							else if (param.DataType == "NUMERIC" || param.DataType == "DECIMAL")
							{
								if (param.Precision.HasValue && param.Scale.HasValue)
									paramDef += $"({param.Precision}, {Math.Abs(param.Scale.Value)})";
							}
							script.AppendLine(paramDef + (outputParams.Last() == param ? "" : ","));
						}
						script.AppendLine(")");
					}
				}

				script.AppendLine("AS");
				script.AppendLine(procedure.Source);
				script.AppendLine(";");
				script.AppendLine();
			}

			return script.ToString();
		}
	}
}
