using System.Data;

namespace DbMetaTool
{
	public class Domain
	{
		public string Name { get; set; } = string.Empty;
		public string DataType { get; set; } = string.Empty;
		public int? Length { get; set; }
		public int? Scale { get; set; }
		public int? Precision { get; set; }
		public bool NotNull { get; set; }
		public string? DefaultValue { get; set; }
		public string? ValidationSource { get; set; }
	}

	public class Column
	{
		public string Name { get; set; } = string.Empty;
		public string DataType { get; set; } = string.Empty;
		public bool IsDomain { get; set; }
		public int? Length { get; set; }
		public int? Scale { get; set; }
		public int? Precision { get; set; }
		public bool NotNull { get; set; }
		public string? DefaultValue { get; set; }
		public bool IsPrimaryKey { get; set; }
		public ForeignKeyInfo? ForeignKey { get; set; }
		public bool IsForeignKey => ForeignKey != null;
	}

	public class TableConstraint
	{
		public string ConstraintName { get; set; } = string.Empty;
		public string ConstraintType { get; set; } = string.Empty; // PRIMARY KEY, FOREIGN KEY, CHECK, UNIQUE
		public string ColumnName { get; set; } = string.Empty;
		public string? RefTableName { get; set; }
		public string? RefColumnName { get; set; }
		public string? CheckExpression { get; set; }
	}

	public class Table
	{
		public string Name { get; set; } = string.Empty;
		public List<Column> Columns { get; set; } = [];
		public List<TableConstraint> TableConstraints { get; set; } = [];
	}

	public class Procedure
	{
		public string Name { get; set; } = string.Empty;
		public string Source { get; set; } = string.Empty;
		public List<ProcedureParameter> Parameters { get; set; } = [];
	}

	public class ProcedureParameter
	{
		public string Name { get; set; } = string.Empty;
		public string DataType { get; set; } = string.Empty;
		public string ParameterType { get; set; } = "INPUT"; // INPUT or OUTPUT
		public int? Length { get; set; }
		public int? Scale { get; set; }
		public int? Precision { get; set; }
	}

	public class ForeignKeyInfo
	{
		public string ConstraintName { get; set; } = string.Empty;
		public string ColumnName { get; set; } = string.Empty;
		public string ReferencedTable { get; set; } = string.Empty;
		public string ReferencedColumn { get; set; } = string.Empty;
		public string? UpdateRule { get; set; }
		public string? DeleteRule { get; set; }
	}

	public class MetadataExtractor(FirebirdConnectionManager connectionManager)
	{
		public List<Domain> ExtractDomains()
		{
			var domains = new List<Domain>();
			var query = @"
				SELECT 
					RDB$FIELD_NAME as FieldName,
					RDB$FIELD_TYPE as FieldType,
					RDB$FIELD_LENGTH as FieldLength,
					RDB$FIELD_SCALE as FieldScale,
					RDB$CHARACTER_LENGTH AS CharLength,
					RDB$FIELD_PRECISION as FieldPrecision,
					RDB$NULL_FLAG AS NullFlag,
					RDB$DEFAULT_SOURCE AS DefaultSource,
					RDB$VALIDATION_SOURCE AS ValidationSource
				FROM RDB$FIELDS
				WHERE RDB$SYSTEM_FLAG = 0 AND NOT RDB$FIELD_NAME STARTING WITH 'RDB$'
				ORDER BY RDB$FIELD_NAME";

			var dataTable = connectionManager.ExecuteQuery(query);
			foreach (DataRow row in dataTable.Rows)
			{
				var fieldNameObj = row["FieldName"];
				if (fieldNameObj == DBNull.Value)
					continue;

				var fieldTypeObj = row["FieldType"];
				int fieldType = 0;
				if (fieldTypeObj != DBNull.Value)
				{
					if (fieldTypeObj is short shortVal)
						fieldType = (int)shortVal;
					else if (fieldTypeObj is int intVal)
						fieldType = intVal;
				}

				int scale = row["FieldScale"] != DBNull.Value ? Convert.ToInt16(row["FieldScale"]) : (short)0;

				int? resolvedLength = null;
				if (row["CharLength"] != DBNull.Value && Convert.ToInt32(row["CharLength"]) > 0)
				{
					resolvedLength = Convert.ToInt32(row["CharLength"]);
				}
				else if (row["FieldLength"] != DBNull.Value)
				{
					resolvedLength = Convert.ToInt32(row["FieldLength"]);
				}

				bool isNotNull = row["NullFlag"] != DBNull.Value && Convert.ToInt32(row["NullFlag"]) == 1;

				var domain = new Domain
				{
					Name = fieldNameObj.ToString()?.Trim() ?? string.Empty,
					DataType = GetDataTypeName(fieldType, scale),
					Length = resolvedLength,
					Scale = row["FieldScale"] != DBNull.Value ? (int?)scale : null,
					Precision = row["FieldPrecision"] != DBNull.Value ? (int?)Convert.ToInt16(row["FieldPrecision"]) : null,
					NotNull = isNotNull,
					DefaultValue = row["DefaultSource"] != DBNull.Value ? row["DefaultSource"].ToString() : null,
					ValidationSource = row["ValidationSource"] != DBNull.Value ? row["ValidationSource"].ToString() : null
				};

				domains.Add(domain);
			}

			return domains;
		}

		public List<Table> ExtractTables()
		{
			var tables = new List<Table>();
			var query = @"
				SELECT 
					RDB$RELATION_NAME as TableName
				FROM RDB$RELATIONS
				WHERE RDB$SYSTEM_FLAG = 0 AND RDB$RELATION_TYPE = 0
				ORDER BY RDB$RELATION_NAME";

			var dataTable = connectionManager.ExecuteQuery(query);

			foreach (DataRow row in dataTable.Rows)
			{
				var tableNameObj = row["TableName"];
				if (tableNameObj == DBNull.Value)
					continue;

				var tableName = tableNameObj.ToString()?.Trim() ?? string.Empty;
				if (string.IsNullOrEmpty(tableName))
					continue;

				var table = new Table
				{
					Name = tableName,
					Columns = ExtractTableColumns(tableName),
					TableConstraints = ExtractTableConstraints(tableName)
				};

				tables.Add(table);

			}

			return tables;
		}

		private List<string> ExtractPrimaryKeyColumns(string tableName)
		{
			var pkColumns = new List<string>();

			var query = $@"
				SELECT TRIM(SEG.RDB$FIELD_NAME) AS ColumnName
				FROM RDB$RELATION_CONSTRAINTS RC
				JOIN RDB$INDEX_SEGMENTS SEG ON RC.RDB$INDEX_NAME = SEG.RDB$INDEX_NAME
				WHERE RC.RDB$RELATION_NAME = '{tableName}'
				  AND RC.RDB$CONSTRAINT_TYPE = 'PRIMARY KEY'
				ORDER BY SEG.RDB$FIELD_POSITION";

			var dataTable = connectionManager.ExecuteQuery(query);

			foreach (DataRow row in dataTable.Rows)
			{
				if (row["ColumnName"] != DBNull.Value)
					pkColumns.Add(row["ColumnName"].ToString()!.Trim());
			}

			return pkColumns;
		}

		private List<ForeignKeyInfo> ExtractForeignKeys(string tableName)
		{
			var foreignKeys = new List<ForeignKeyInfo>();

			var query = $@"
				SELECT
					TRIM(RC.RDB$CONSTRAINT_NAME) AS ConstraintName,
					TRIM(SEG.RDB$FIELD_NAME) AS ColumnName,
					TRIM(SEG.RDB$FIELD_POSITION) AS SegPos,
					TRIM(REFRC.RDB$RELATION_NAME) AS ReferencedTable,
					TRIM(REFSEG.RDB$FIELD_NAME) AS ReferencedColumn,
					TRIM(REFC.RDB$UPDATE_RULE) AS UpdateRule,
					TRIM(REFC.RDB$DELETE_RULE) AS DeleteRule
				FROM RDB$RELATION_CONSTRAINTS RC
				JOIN RDB$INDEX_SEGMENTS SEG
					ON RC.RDB$INDEX_NAME = SEG.RDB$INDEX_NAME
				JOIN RDB$REF_CONSTRAINTS REFC
					ON RC.RDB$CONSTRAINT_NAME = REFC.RDB$CONSTRAINT_NAME
				JOIN RDB$RELATION_CONSTRAINTS REFRC
					ON REFC.RDB$CONST_NAME_UQ = REFRC.RDB$CONSTRAINT_NAME
				JOIN RDB$INDEX_SEGMENTS REFSEG
					ON REFRC.RDB$INDEX_NAME = REFSEG.RDB$INDEX_NAME
					AND REFSEG.RDB$FIELD_POSITION = SEG.RDB$FIELD_POSITION
				WHERE RC.RDB$RELATION_NAME = '{tableName}'
				  AND RC.RDB$CONSTRAINT_TYPE = 'FOREIGN KEY'
				ORDER BY RC.RDB$CONSTRAINT_NAME, SEG.RDB$FIELD_POSITION";

			var dataTable = connectionManager.ExecuteQuery(query);

			foreach (DataRow row in dataTable.Rows)
			{
				var fk = new ForeignKeyInfo
				{
					ConstraintName = row["ConstraintName"] != DBNull.Value ? row["ConstraintName"].ToString()!.Trim() : string.Empty,
					ColumnName = row["ColumnName"] != DBNull.Value ? row["ColumnName"].ToString()!.Trim() : string.Empty,
					ReferencedTable = row["ReferencedTable"] != DBNull.Value ? row["ReferencedTable"].ToString()!.Trim() : string.Empty,
					ReferencedColumn = row["ReferencedColumn"] != DBNull.Value ? row["ReferencedColumn"].ToString()!.Trim() : string.Empty,
					UpdateRule = row["UpdateRule"] != DBNull.Value ? row["UpdateRule"].ToString()!.Trim() : null,
					DeleteRule = row["DeleteRule"] != DBNull.Value ? row["DeleteRule"].ToString()!.Trim() : null
				};

				foreignKeys.Add(fk);
			}

			return foreignKeys;
		}

		private List<TableConstraint> ExtractTableConstraints(string tableName)
		{
			var constraints = new List<TableConstraint>();

			var query = $@"
				SELECT 
					TRIM(RC.RDB$CONSTRAINT_NAME) AS ConstraintName,
					TRIM(RC.RDB$CONSTRAINT_TYPE) AS ConstraintType,
					TRIM(IXS.RDB$FIELD_NAME) AS ColumnName,
					TRIM(REFC2.RDB$RELATION_NAME) AS RefTableName,
					TRIM(IXS2.RDB$FIELD_NAME) AS RefColumnName,
					TRIM(T.RDB$TRIGGER_SOURCE) AS CheckExpression
				FROM RDB$RELATION_CONSTRAINTS RC
        
				-- Join columns for PK, FK, UNIQUE
				LEFT JOIN RDB$INDEX_SEGMENTS IXS 
					ON RC.RDB$INDEX_NAME = IXS.RDB$INDEX_NAME
            
				-- Join target table and columns for Foreign Keys
				LEFT JOIN RDB$REF_CONSTRAINTS REFC 
					ON RC.RDB$CONSTRAINT_NAME = REFC.RDB$CONSTRAINT_NAME
				LEFT JOIN RDB$RELATION_CONSTRAINTS REFC2 
					ON REFC.RDB$CONST_NAME_UQ = REFC2.RDB$CONSTRAINT_NAME
				LEFT JOIN RDB$INDEX_SEGMENTS IXS2 
					ON REFC2.RDB$INDEX_NAME = IXS2.RDB$INDEX_NAME 
					AND IXS.RDB$FIELD_POSITION = IXS2.RDB$FIELD_POSITION
            
				-- Join CHECK constraints to extract expression text
				LEFT JOIN RDB$CHECK_CONSTRAINTS CC 
					ON RC.RDB$CONSTRAINT_NAME = CC.RDB$CONSTRAINT_NAME
				LEFT JOIN RDB$TRIGGERS T 
					ON CC.RDB$TRIGGER_NAME = T.RDB$TRIGGER_NAME
            
				WHERE RC.RDB$RELATION_NAME = '{tableName}'
				ORDER BY RC.RDB$CONSTRAINT_TYPE, RC.RDB$CONSTRAINT_NAME, IXS.RDB$FIELD_POSITION";

			var dataTable = connectionManager.ExecuteQuery(query);

			foreach (DataRow row in dataTable.Rows)
			{
				string? rawCheckExpr = row["CheckExpression"] != DBNull.Value 
					? row["CheckExpression"].ToString()?.Trim() 
					: null;

				if (!string.IsNullOrEmpty(rawCheckExpr))
				{
					if (rawCheckExpr.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
					{
						rawCheckExpr = rawCheckExpr.Substring(5).Trim();
					}
					if (rawCheckExpr.StartsWith("(") && rawCheckExpr.EndsWith(")"))
					{
						rawCheckExpr = rawCheckExpr.Substring(1, rawCheckExpr.Length - 2).Trim();
					}
				}

				constraints.Add(new TableConstraint
				{

					ConstraintName = row["ConstraintName"]?.ToString()?.Trim() ?? string.Empty,
					ConstraintType = row["ConstraintType"]?.ToString()?.Trim() ?? string.Empty,
					ColumnName = row["ColumnName"] != DBNull.Value ? row["ColumnName"].ToString()!.Trim() : string.Empty,
					RefTableName = row["RefTableName"] != DBNull.Value ? row["RefTableName"].ToString()?.Trim() : null,
					RefColumnName = row["RefColumnName"] != DBNull.Value ? row["RefColumnName"].ToString()?.Trim() : null,
					CheckExpression = rawCheckExpr
				});

				constraints = [.. constraints
					.GroupBy(c => new
					{
						c.ConstraintName,
						c.ConstraintType,
						c.ColumnName,
						c.RefTableName,
						c.RefColumnName,
						c.CheckExpression
					})
					.Select(g => g.First())];
					}

			return constraints;
		}

		private List<Column> ExtractTableColumns(string tableName)
		{
			var columns = new List<Column>();
			var pkColumns = ExtractPrimaryKeyColumns(tableName);
			var foreignKeys = ExtractForeignKeys(tableName);


			var query = $@"
				SELECT 
						TRIM(RF.RDB$FIELD_NAME) AS ColumnName,
						TRIM(RF.RDB$FIELD_SOURCE) AS FieldSource,
						F.RDB$FIELD_TYPE AS FieldType,
						F.RDB$FIELD_LENGTH AS FieldLength,
						F.RDB$CHARACTER_LENGTH AS CharLength,
						F.RDB$FIELD_SCALE AS FieldScale,
						F.RDB$FIELD_PRECISION AS FieldPrecision,
						RF.RDB$NULL_FLAG AS ColNullFlag,
						F.RDB$NULL_FLAG AS DomNullFlag,
						COALESCE(RF.RDB$DEFAULT_SOURCE, F.RDB$DEFAULT_SOURCE) AS DefaultSource
					FROM RDB$RELATION_FIELDS RF
					JOIN RDB$FIELDS F ON RF.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
					WHERE RF.RDB$RELATION_NAME = '{tableName}'
					ORDER BY RF.RDB$FIELD_POSITION";

			var dataTable = connectionManager.ExecuteQuery(query);

			foreach (DataRow row in dataTable.Rows)
			{
				var colNameObj = row["ColumnName"];
				if (colNameObj == DBNull.Value)
					continue;

				string fieldSource = row["FieldSource"] != DBNull.Value ? row["FieldSource"].ToString()!.Trim() : string.Empty;

				var fieldTypeObj = row["FieldType"];
				int fieldType = 0;
				if (fieldTypeObj != DBNull.Value)
				{
					if (fieldTypeObj is short shortVal)
						fieldType = (int)shortVal;
					else if (fieldTypeObj is int intVal)
						fieldType = intVal;
				}

				bool isUserDomain = !string.IsNullOrEmpty(fieldSource) && !fieldSource.StartsWith("RDB$", StringComparison.OrdinalIgnoreCase);

				string dataType = isUserDomain ? fieldSource : GetDataTypeName(fieldType, Convert.ToInt16(row["FieldScale"]));

				bool isNotNull = (row["ColNullFlag"] != DBNull.Value && Convert.ToInt32(row["ColNullFlag"]) == 1) ||
								 (row["DomNullFlag"] != DBNull.Value && Convert.ToInt32(row["DomNullFlag"]) == 1);

				int? resolvedLength = null;
				if (!isUserDomain)
				{
					if (row["CharLength"] != DBNull.Value && Convert.ToInt32(row["CharLength"]) > 0)
					{
						resolvedLength = Convert.ToInt32(row["CharLength"]);
					}
					else if (row["FieldLength"] != DBNull.Value)
					{
						resolvedLength = Convert.ToInt32(row["FieldLength"]);
					}
				}

				var column = new Column
				{
					Name = colNameObj.ToString()?.Trim() ?? string.Empty,
					DataType = dataType,
					IsDomain = isUserDomain,
					Length = resolvedLength,
					Scale = isUserDomain ? null : (row["FieldScale"] != DBNull.Value ? (int?)Convert.ToInt16(row["FieldScale"]) : null),
					Precision = isUserDomain ? null : (row["FieldPrecision"] != DBNull.Value ? (int?)Convert.ToInt16(row["FieldPrecision"]) : null),
					NotNull = isNotNull,
					DefaultValue = row["DefaultSource"] != DBNull.Value ? row["DefaultSource"].ToString() : null,
					IsPrimaryKey = pkColumns.Contains(colNameObj.ToString()!.Trim(), StringComparer.OrdinalIgnoreCase),
					ForeignKey = foreignKeys.FirstOrDefault(fk =>
					fk.ColumnName.Equals(colNameObj.ToString()!.Trim(), StringComparison.OrdinalIgnoreCase))

				};

				columns.Add(column);
			}

			return columns;
		}

		public List<Procedure> ExtractProcedures()
		{
			var procedures = new List<Procedure>();
			var query = @"
				SELECT 
					RDB$PROCEDURE_NAME as ProcedureName,
					RDB$PROCEDURE_SOURCE as Source
				FROM RDB$PROCEDURES
				WHERE RDB$SYSTEM_FLAG = 0
				ORDER BY RDB$PROCEDURE_NAME";

			var dataTable = connectionManager.ExecuteQuery(query);

			foreach (DataRow row in dataTable.Rows)
			{
				var procNameObj = row["ProcedureName"];
				if (procNameObj == DBNull.Value)
					continue;

				var procedureName = procNameObj.ToString()?.Trim() ?? string.Empty;
				if (string.IsNullOrEmpty(procedureName))
					continue;

				var procedure = new Procedure
				{
					Name = procedureName,
					Source = row["Source"] != DBNull.Value ? (row["Source"].ToString() ?? string.Empty) : string.Empty,
					Parameters = ExtractProcedureParameters(procedureName)
				};

				procedures.Add(procedure);
			}

			return procedures;
		}

		private List<ProcedureParameter> ExtractProcedureParameters(string procedureName)
		{
			var parameters = new List<ProcedureParameter>();
			var query = $@"
				SELECT 
					TRIM(PP.RDB$PARAMETER_NAME) AS ParameterName,
					PP.RDB$PARAMETER_TYPE AS ParameterType,
					F.RDB$FIELD_TYPE AS FieldType,
					F.RDB$FIELD_LENGTH AS FieldLength,
					F.RDB$FIELD_SCALE AS FieldScale,
					F.RDB$FIELD_PRECISION AS FieldPrecision
				FROM RDB$PROCEDURE_PARAMETERS PP
				JOIN RDB$FIELDS F ON PP.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
				WHERE PP.RDB$PROCEDURE_NAME = '{procedureName}'
				ORDER BY PP.RDB$PARAMETER_NUMBER";

			var dataTable = connectionManager.ExecuteQuery(query);

			foreach (DataRow row in dataTable.Rows)
			{
				var paramNameObj = row["ParameterName"];
				if (paramNameObj == DBNull.Value)
					continue;

				var fieldTypeObj = row["FieldType"];
				int fieldType = 0;
				if (fieldTypeObj != DBNull.Value)
				{
					if (fieldTypeObj is short shortVal)
						fieldType = (int)shortVal;
					else if (fieldTypeObj is int intVal)
						fieldType = intVal;
				}

				var paramTypeObj = row["ParameterType"];
				int paramType = 0;
				if (paramTypeObj != DBNull.Value)
				{
					if (paramTypeObj is short shortVal2)
						paramType = (int)shortVal2;
					else if (paramTypeObj is int intVal2)
						paramType = intVal2;
				}

				var parameter = new ProcedureParameter
				{
					Name = paramNameObj.ToString()?.Trim() ?? string.Empty,
					DataType = GetDataTypeName(fieldType, Convert.ToInt16(row["FieldScale"])),
					ParameterType = paramType == 0 ? "INPUT" : "OUTPUT",
					Length = row["FieldLength"] != DBNull.Value ? Convert.ToInt32(row["FieldLength"]) : null,
					Scale = row["FieldScale"] != DBNull.Value ? (int?)Convert.ToInt16(row["FieldScale"]) : null,
					Precision = row["FieldPrecision"] != DBNull.Value ? (int?)Convert.ToInt16(row["FieldPrecision"]) : null
				};

				parameters.Add(parameter);
			}

			return parameters;
		}

		private string GetDataTypeName(int fieldType, int scale)
		{
			if (scale < 0)
			{
				return "NUMERIC";
			}

			return fieldType switch
			{
				7 => "SMALLINT",
				8 => "INTEGER",
				10 => "FLOAT",
				12 => "DATE",
				13 => "TIME",
				14 => "CHAR",
				16 => "BIGINT",
				27 => "DOUBLE PRECISION",
				35 => "TIMESTAMP",
				37 => "VARCHAR",
				40 => "CSTRING",
				45 => "BLOB",
				261 => "BLOB",
				_ => "UNKNOWN"
			};
		}
	}
}
