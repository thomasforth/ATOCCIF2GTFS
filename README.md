# ATOCCIF2GTFS
Create GTFS format timetables for Great Britain's railways from the publicly available ATOC CIF format timetables.

Railway timetables for Great Britain are available from [The Rail Delivery Group](http://data.atoc.org/). You will need to create an account to download the data. The data is available for free, and I believe that it is licensed under [The Creative Commons Attribution 2.0 England and Wales license]( https://creativecommons.org/licenses/by/2.0/uk/legalcode). This permits me to share the original timetable file and its derivative version (the GTFS version of that timetable) while recognising its origin, as above.

This program uses [the NaPTAN database](), specifically the stops.csv file. An up to date version should be used or newer stations risk being ommitted from the output file.

This program will convert the downloaded GB Rail Tiemtable to GTFS format. The output has been tested with [OpenTripPlanner](https://www.opentripplanner.org/) to ensure its usability for the creation of a Great Britain route planner, and analysis as provided by OpenTripPlanner.

## Usage
The tool is written in C# .NET Core 3.1. It was developed and tested in Visual Studio 2019 Community Edition for Windows. There are ways to open the solution and compile the project on Mac and Linux, either using Visual Studio or the [.NET Core Runtime or SDK](https://dotnet.microsoft.com/download) but I have not tested them.

## Better alternatives
ATOC CIF is a complicated format and the conversions in this tool are not perfect. If you want a reliable and frequently updated GTFS format timetable for Great Britain I recommend that you contact [ITO World](https://www.itoworld.com/) or [TransportAPI](https://www.transportapi.com/). Both of these companies, and probably others, can provide such services for a fee.

## Other conversion tools
GTFS format timetables for passenger railway services in Great Britain are available via [Open Mobility Data](https://transitfeeds.com/p/association-of-train-operating-companies/284). I cannot get these to succesfully load into OpenTripPlanner and so they are not useful for me, which is why I've written this tool. I think that these feeds are created by the [dtd2mysql](https://github.com/planarnetwork/dtd2mysql) tool.

An excellent overview of working with public transport timetables for Great Britain is provided by the [propeR](https://github.com/datasciencecampus/propeR) project, by the UK's Office for National Statistics.

## License
This code is released under the MIT License, as included in this repository.
Both timetables, the original in ATOC CIF format and the derviative work in GTFS format are provided under [The Creative Commons Attribution 2.0 England and Wales license]( https://creativecommons.org/licenses/by/2.0/uk/legalcode) as required by the original data provider. This license includes the specific clause that "You must not sublicense the Work". All usage should acknowledge both this repository and the original data source.

## Thanks
This project is supported indirectly (and with no guarantee or liability) by sponsors of [The Open Data Institute Leeds](odileeds.org) who include [Network Rail](https://www.networkrail.co.uk/) and [Transport for the North](https://transportforthenorth.com/).
