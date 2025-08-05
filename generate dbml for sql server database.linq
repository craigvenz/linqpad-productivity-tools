<Query Kind="Program">
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

void Main()
{
    var schemas = new[] { "rbs", "dbo" };
    var excludeTables = new[] { "MyTable" };
    
    var schema =
        from t in INFORMATION_SCHEMA.TABLES
        where t.TABLE_TYPE == "BASE TABLE" 
           && (t.TABLE_SCHEMA == schemas[0] 
           ||  t.TABLE_SCHEMA == schemas[1]) 
           && !t.TABLE_NAME.EndsWith("History") 
           && t.TABLE_NAME != excludeTables[0]
        select new
        {
            t.TABLE_SCHEMA,
            t.TABLE_NAME,
            Columns = from c in INFORMATION_SCHEMA.COLUMNS
                      where c.TABLE_NAME == t.TABLE_NAME 
                         && c.TABLE_SCHEMA == t.TABLE_SCHEMA
                         && c.COLUMN_NAME != "PeriodStart" 
                         && c.COLUMN_NAME != "PeriodEnd"
                      select new
                      {
                          c.ORDINAL_POSITION,
                          c.COLUMN_NAME,
                          c.DATA_TYPE,
                          c.IS_NULLABLE,
                          c.CHARACTER_MAXIMUM_LENGTH,
                          c.DATETIME_PRECISION,
                          c.NUMERIC_PRECISION,
                          c.NUMERIC_SCALE,
                          Constraints = from tc in INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                                        join kcu in INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                                        on tc.CONSTRAINT_NAME equals kcu.CONSTRAINT_NAME
                                        where tc.TABLE_NAME == t.TABLE_NAME
                                           && tc.TABLE_SCHEMA == t.TABLE_SCHEMA
                                           && kcu.COLUMN_NAME == c.COLUMN_NAME
                                        select new
                                        {
                                            tc.CONSTRAINT_TYPE,
                                            kcu.CONSTRAINT_NAME,
                                            kcu.COLUMN_NAME,
                                            ReferencedConstraints = 
                                                from rc in INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
                                                join fkcu in INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                                                on rc.UNIQUE_CONSTRAINT_NAME equals fkcu.CONSTRAINT_NAME
                                                where rc.CONSTRAINT_NAME == tc.CONSTRAINT_NAME
                                                select new
                                                {
                                                    rc.CONSTRAINT_SCHEMA,
                                                    rc.UNIQUE_CONSTRAINT_NAME,
                                                    rc.UPDATE_RULE,
                                                    rc.DELETE_RULE,
                                                    fkcu.TABLE_NAME,
                                                    fkcu.COLUMN_NAME
                                                }
                                        }
                      }
        };

    // Define your standard columns that should be pushed to the bottom
    var standardColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CreatedBy",
        "CreatedDate",
        "ModifiedBy",
        "ModifiedDate",
        "IsDeleted",
        "DateStart",
        "DateExpire",
        "DateCreated"
    };
    var standardColumnsArray = standardColumns.ToArray();

    foreach (var table in schema.ToList())
    {
        var sw = new StringWriter();
        
        sw.WriteLine("table {0}.{1} {{", table.TABLE_SCHEMA, table.TABLE_NAME);

        foreach (var c in table.Columns.OrderBy(x => standardColumns.Contains(x.COLUMN_NAME) ? 100 + standardColumnsArray.IndexOf(x.COLUMN_NAME) : 0).ThenBy(x => x.ORDINAL_POSITION))
        {
            var dataType = c.DATA_TYPE switch
            {
                "datetime" or "datetime2" 
                    => c.DATETIME_PRECISION != null ? $"{c.DATA_TYPE}({c.DATETIME_PRECISION})" : c.DATA_TYPE,
                "varchar" or "nvarchar" 
                    => $"{c.DATA_TYPE}({(c.CHARACTER_MAXIMUM_LENGTH == -1 ? "max" : c.CHARACTER_MAXIMUM_LENGTH.ToString())})",
                "numeric" 
                    => $"{c.DATA_TYPE}({c.NUMERIC_PRECISION},{c.NUMERIC_SCALE})",
                  _ => c.DATA_TYPE
            };
            sw.Write("  {0} {1}", c.COLUMN_NAME, dataType);
            
            var constraints = new List<string>();
            constraints.Add(c.IS_NULLABLE == "YES" ? "null" : "not null");
            
            foreach (var ct in c.Constraints)
            {
                if (ct.CONSTRAINT_TYPE == "PRIMARY KEY") 
                    constraints.Add("primary key");
                if (ct.CONSTRAINT_TYPE == "FOREIGN KEY")
                {
                    var r = ct.ReferencedConstraints.FirstOrDefault();
                    if (r != null)
                        constraints.Add($"ref: > {r.CONSTRAINT_SCHEMA}.{r.TABLE_NAME}.{r.COLUMN_NAME}");
                }
            }
            if (constraints.Count > 0)
                sw.WriteLine(" [{0}]", string.Join(", ", constraints));
        }
        sw.WriteLine("}");
        Console.WriteLine(sw.GetStringBuilder().ToString());
    }
}
