# ExistExportToSQL

ExistExportToSQL is a command line utility to create a script that lets SQL Server import the contents of a [full JSON archive](https://exist.io/account/export/) from an account on [Exist.io](https://exist.io).

Exist already does a great job of letting you explore relationships between different pices of data that you track, and it has an API that allows you to build whatever you'd like.  But for me, the natural way to play with data is through SQL queries.

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

The utility does not directly import the data, just generates a script that tells SQL Server how to import the data from the json files.  Note that a static script would not be sufficient because different people have different attributes enabled on their accounts, and use different custom tags.  The data available will depend on which attibutes are enabled and populated with data in your Exist account, and each will result in its own table.  A few additional helper tables created as well.

### Helper Tables and Views

* 'location_geo' enhances original table 'location' by storing coordinates in SQL Server's native GEOGRAPHY data type
* 'sleep_end_ex' enhances original table 'sleep_end' by normalizing the 'minutes from midnight as integer' data to TIME and DATETIME
* 'sleep_start_ex' enhances original table 'sleep_start' by normalizing the 'minutes from midday as integer' data to TIME and DATETIME
* 'AllBoolTraits' is a view that is a union of all tables with boolean data (usually custom tags), allowing for easier queries across a set of different tags
* 'AllIntTraits' is a view that is a union of all tables with int data whether a built-in attribute, or from manual tracking, or custom tag (1 or 0)
* 'LastTag' is a view that shows the date of the last occurrence of each tag
* 'TagUsePast60Days' is a view that view that shows, for any given tag and date, the use of that tag in the past 60 days

## Example Queries

### Mood notes and tags

Show tags and mood score for each day that has a note.

``` SQL
SELECT mn.date, m.value AS Mood, mn.value AS Note, t.Tags
FROM mood_note mn
LEFT JOIN mood m ON m.date=mn.date
LEFT JOIN (
  SELECT act.date, STRING_AGG(act.name, ', ') AS Tags
  FROM AllBoolTraits act
  WHERE act.value>0
  GROUP BY act.date
  ) AS t ON t.date=mn.date
WHERE mn.value IS NOT NULL
ORDER BY mn.date DESC
```

### Last occurrence and recent use of each tag

For each tag, this shows the last date it was used, and the number of times it has been used in the past 60 days.

``` SQL
SELECT lt.name, lt.LastOccurrence, p60.TagCount AS Past60DayTagUse
FROM LastTag AS lt
INNER JOIN TagUsePast60Days p60 ON p60.name=lt.name 
  AND p60.date=(SELECT MAX(date) FROM TagUsePast60Days)
ORDER BY lt.LastOccurrence DESC
```

### Rolling sum of tag occurrences

This is the inertia of a custom tag: for any given day, how many times the tag was used in the preceding 60 days.

``` SQL
-- rolling sum of occurrences of a tag
SELECT date, value, SUM(value) OVER
  (PARTITION BY name ORDER BY date ASC ROWS 59 PRECEDING) --59 + current row
  AS OccurrencesInPrev60Days
FROM tag1 as c
ORDER BY date DESC
```

Using the helper view, this is now the same as:

``` SQL
SELECT name, date, value, TagCount
FROM TagUsePast60Days
WHERE name='tag1'
ORDER BY date DESC
```

### Group of tags over time

If you use custom tags to track positive or negative habits, you might want to look at several tags together and to see how they have gone up or down over time.  This query shows the combined occurrences of several tags, grouped by month.

``` SQL
--sum of tags per month
SELECT YEAR(date) AS Year, MONTH(date) AS Month, SUM(CAST(value AS INT)) AS Total
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

### Steps per day by number calendar events

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

### Distance from home

This query uses the location_geo helper table to show the dates you were recorded as furthest from home.  Mostly in the interest of learning how the GEOGRAPHY data type works.

``` SQL
-- replace with your home coordinates
DECLARE @home geography = 'POINT (-117.587059 33.589965)';

--Dates furthest from home  
SELECT TOP(5) date, geo.ToString() AS point,
  @home.STDistance(geo) * 0.001 as Kilometers,
  @home.STDistance(geo) * 0.000621371 as Miles
FROM [Exist].[dbo].[location_geo]
WHERE geo.STDistance(@home) IS NOT NULL  
ORDER BY geo.STDistance(@home) DESC;  
```

## Requirements

[.NET Runtime](https://dotnet.microsoft.com/download) 7.0+  
[SQL Server 2016](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)+ for the resulting script to use the [OPENJSON](https://docs.microsoft.com/en-us/sql/t-sql/functions/openjson-transact-sql?view=sql-server-ver15) command

## Implementation details

Part of the initial reason I did this project in 2019 was to use a number of things that I wanted to play with:

* .NET Core 3.1 and C# 8.0 with excuses to touch at least some of the new things: [System.Text.Json](https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-apis/), [Nullable reference types](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#nullable-reference-types), [Switch Expressions](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#switch-expressions), [Using declarations](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#using-declarations).
* [Dragonfruit](https://github.com/dotnet/command-line-api/wiki/Your-first-app-with-System.CommandLine.DragonFruit) provides nice command line functionality with very little work.
* [Single File Publishing](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#single-file-executables) the official .NET Core way, rather than [ILMerge](https://www.nuget.org/packages/MSBuild.ILMerge.Task/) or [Fody/Costura](https://github.com/Fody/Costura).  Although for size I still prefer to make it framework dependent rather than self contained.
* [Github Actions](https://github.com/actions/upload-release-asset) to automate build and release.
* Presumably cross platform code, which is to say, I only develop and test on Windows but it should work cross platform. 😉
