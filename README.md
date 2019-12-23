# ExistExportToSQL

[Exist.io](https://exist.io) has an API that allows you to build whatever you'd like.  And it provides an option to export a [full JSON archive](https://exist.io/account/export/).  But if you'd just like to play with data in different ways, then it would be nice to have it all in a database.  

This is a small command line utility that generates a SQL import script from the contents of the full JSON archive.  The utility does not directly import the data, just generates a script that uses [OPENJSON](https://docs.microsoft.com/en-us/sql/t-sql/functions/openjson-transact-sql?view=sql-server-ver15) which requires SQL Server 2016 or higher.

Built on [.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1#3.1.0) and multi-platform, although only tested in Windows.

``` cmd
ExistExportToSQL:
  Generates a SQL import script from a folder of a full json export from Exist.io

Usage:
  ExistExportToSQL [options]

Options:
  --input-folder <input-folder>    Folder containing Exist.io json files. Defaults to current directory.
  --output-file <output-file>      Path to output SQL script. Defaults to inputFolder\ImportExistJson.sql
  --version                        Display version information
```

The data available will depend on which attibutes are enabled and populated with data in your Exist account.  

As an example, here is a query that shows the average and median number of steps per day grouped by number of calendar events were present in that day.  This would depend on feeding steps and calendar data into Exist.

``` SQL
--avg and median steps by number of events in calendar
SELECT m.EventCount, m.MedianSteps, AVG(m.steps) AS AvgSteps, COUNT(*) AS DaysCount
FROM (
  -- Window functions like PERCENTILE_CONT cannot also be used with GROUP BY, so use a subquery to get median
  SELECT ISNULL(events.value,0) AS EventCount, steps.value as steps, PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY steps.value)
  OVER (PARTITION BY ISNULL(events.value,0)) AS MedianSteps
  FROM steps
  INNER JOIN events ON events.date=steps.date
) AS m
GROUP BY m.EventCount, m.MedianSteps
ORDER BY m.EventCount DESC
```

