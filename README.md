# ExistExportToSQL

[Exist.io](https://exist.io) already does a great job of letting you explore relationships between different pices of data that you track, and it has an API that allows you to build whatever you'd like.  But if you'd just like to play with data in different ways, and if like me you channel those thoughts into SQL queries, then it would be nice to have it all in a database.  This is a small multiplatform command line utility that generates a SQL import script from the contents of the [full JSON archive](https://exist.io/account/export/) which can be exported from your account.

``` text
ExistExportToSQL:
  Generates a SQL import script from a folder of a full json export from Exist.io

Usage:
  ExistExportToSQL [options]

Options:
  --input-folder <input-folder>    Folder containing Exist.io json files. Defaults to current directory.
  --output-file <output-file>      Path to output SQL script. Defaults to inputFolder\ImportExistJson.sql
  --version                        Display version information
```

## Details

The utility does not directly import the data, just generates a script that tells SQL Server how to import the data from the json files.  Note that a static script would not be sufficient because different people have different attributes enabled on their accounts, and use different custom tags.

The data available will depend on which attibutes are enabled and populated with data in your Exist account, and each will result in its own table.  A few additional helper tables created as well.

### Helper Tables

* 'location_geo' enhances original table 'location' by storing coordinates in SQL Server's native GEOGRAPHY data type
* 'sleep_end_ex' enhances original table 'sleep_end' by normalizing the 'minutes from midnight as integer' data to TIME and DATETIME
* 'sleep_start_ex' enhances original table 'sleep_start' by normalizing the 'minutes from midday as integer' data to TIME and DATETIME

## Example Queries

If you use custom tags to track positive or negative habits, you might want to look at several tags together and to see how they have gone up or down over time.  This query shows the combination of three tags grouped by month.

``` SQL
--sum of tags per month
Select YEAR(date), MONTH(date), SUM(value)
FROM (
	SELECT date, value FROM tag1
	UNION ALL
	SELECT date, value FROM tag2
	UNION ALL
	SELECT date, value FROM tag3
) as VicesOrVirtues
GROUP BY YEAR(date), MONTH(date)
ORDER BY YEAR(date) DESC, MONTH(date) DESC
```

Here is a query that shows the average and median number of steps per day grouped by number of calendar events were present in that day.  This would depend on feeding steps and calendar data into Exist.

``` SQL
--avg and median steps by number of events in calendar
SELECT m.EventCount, m.MedianSteps, AVG(m.steps) AS AvgSteps, COUNT(*) AS DaysCount
FROM (
  -- Window functions like PERCENTILE_CONT cannot also be used with GROUP BY, so use a subquery to get median
  SELECT ISNULL(events.value,0) AS EventCount, steps.value as steps,
  PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY steps.value)
  OVER (PARTITION BY ISNULL(events.value,0)) AS MedianSteps
  FROM steps
  INNER JOIN events ON events.date=steps.date
) AS m
GROUP BY m.EventCount, m.MedianSteps
ORDER BY m.EventCount DESC
```

This query uses the location_geo helper table to show the dates you were recorded as furthest from home.  Mostly in the interest of learning how the GEOGRAPHY data type works.

``` SQL
--Dates furthest from home
-- (replace with your home coordinates)
DECLARE @home geography = 'POINT (-117.587059 33.589965)';  
SELECT TOP(5) date, geo.ToString() AS point,
  @home.STDistance(geo) * 0.001 as Kilometers,
  @home.STDistance(geo) * 0.000621371 as Miles
FROM [Exist].[dbo].[location_geo]
WHERE geo.STDistance(@home) IS NOT NULL  
ORDER BY geo.STDistance(@home) DESC;  
```

## Requirements

[.NET Core Runtime](https://dotnet.microsoft.com/download) 3.1+  
[SQL Server 2016](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)+ for the resulting script to use the [OPENJSON](https://docs.microsoft.com/en-us/sql/t-sql/functions/openjson-transact-sql?view=sql-server-ver15) command

## Additional details

Uses [Dragonfruit](https://github.com/dotnet/command-line-api/wiki/Your-first-app-with-System.CommandLine.DragonFruit) to provide nice command line functionality with very little work.
