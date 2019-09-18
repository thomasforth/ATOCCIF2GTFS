# ATOCCIF2TransXChange
Create GTFS format timetables for Great Britain's railways from the publicly available ATOCCIF format timetables.

Railway timetables for Great Britain are available from (The Rail Delivery Group)[http://data.atoc.org/]. You will need to create an account to download the data.
The data is available for free, but it is not open data. There are restrictions on what can be done with it. Because of this I do not think that I am allowed to share an example of the data, either before or after conversion to GTFS format.

This program will convert the downloaded GB Rail Tiemtable

## Usage
The tool is written in C# .NET Core 2. It will run without problem in Visual Studio 2019 Community for Windows. There are ways to open solution and compile the project on Mac and Linux but I have not tested them.

## Better alternatives
ATOC CIF is a complicated format and the conversions in this tool are not perfect. If you want a reliable and frequently updated GTFS format timetable for Great Britain I recommend that you contact ITOWorld or TransportAPI. Both of these companies can provide such services for a fee.
