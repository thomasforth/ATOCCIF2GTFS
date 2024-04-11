using CsvHelper;
using GeoCoordinatePortable;
using GeoUK.Coordinates;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

Console.WriteLine("Creating ATCOcode keyed dictionary of NaPTAN stops.");
Dictionary<string, NaptanStop> NaPTANStopsDictionary = new Dictionary<string, NaptanStop>();
using (ZipArchive Archive = new ZipArchive(File.Open(@"../../../../Stops_20240410_compatible_with_ttis062.zip", FileMode.Open), ZipArchiveMode.Read))
{
    using (CsvReader csvReader = new CsvReader(new StreamReader(Archive.Entries.First().Open()), CultureInfo.InvariantCulture))
    {
        NaPTANStopsDictionary = csvReader.GetRecords<NaptanStop>().ToDictionary(x => x.ATCOCode, x => x);

        // sometimes (Crossrail stations in 2024) an Easting and Northing but not a Latitude and Longitude is supplied for a stop. We calculate LatLng from EastNorth
        foreach (var Station in NaPTANStopsDictionary.Values)
        {
            if (Station.Latitude == null && Station.Easting != null)
            {
                var GBGridLocation = new Osgb36(Station.Easting.Value, Station.Northing.Value);
                var LatLng = GeoUK.OSTN.Transform.OsgbToEtrs89(GBGridLocation);
                Station.Latitude = LatLng.Latitude;
                Station.Longitude = LatLng.Longitude;
            }
        }
    }
}

Console.WriteLine("Loading CIF stations and matching them to NaPTAN stops.");
List<CIFStation> CIFStations = new List<CIFStation>();

// The GB rail timetable includes Eurostar to Paris (and not, seemingly, Brussels, Lille, or Amsterdam)
// This breaks timetables unless we add it. So we do.

CIFStation ParisGareDuNord = new CIFStation()
{
    ATCOCode = "9100PARISND",
    StationLongCode = "PARISND",
    StationName = "Paris Gare du Nord",
    StationShortCode = "PND"
};

CIFStations.Add(ParisGareDuNord);


// .msn lines are fixed-width columns and look like this.
// "A    WHITTLESFORD PARKWAY          0WTLESFDWLF   WLF15484 62473 5                 "
// A reasonable approach would be to split at the correct position, then trim, then only store entries with all features

Dictionary<string, List<StationStop>> StopTimesForJourneyIDDictionary = new Dictionary<string, List<StationStop>>();
Dictionary<string, JourneyDetail> JourneyDetailsForJourneyIDDictionary = new Dictionary<string, JourneyDetail>();
using (ZipArchive Archive = new ZipArchive(File.Open(@"../../../../ttis062_validfrom20240406.zip", FileMode.Open), ZipArchiveMode.Read))
{
    ZipArchiveEntry MSNFile = Archive.Entries.Where(x => x.FullName.EndsWith(".msn")).First();
    StreamReader StopFileStreamReader = new StreamReader(MSNFile.Open());

    while (StopFileStreamReader.EndOfStream == false)
    {
        string StopLine = StopFileStreamReader.ReadLine();
        string firstSlot = StopLine.Substring(0, 5).Trim();
        string secondSlot = StopLine.Substring(5, 30).TrimEnd();
        string thirdSlot = StopLine.Substring(35, 1);
        string fourthSlot = StopLine.Substring(36, 7).Trim();
        string fifthSlot = StopLine.Substring(43, 3).Trim();
        string sixthSlot = StopLine.Substring(49, 8).Trim();
        // The seventh slot is okay to be empty
        string seventhSlot = StopLine.Substring(57, 1).Trim();
        string eighthSlot = StopLine.Substring(58, 5).Trim();
        string ninthSlot = StopLine.Substring(65, 1).Trim();

        if (firstSlot == "A" && secondSlot.StartsWith(" ") == false)
        {
            CIFStation Station = new CIFStation()
            {
                StationName = secondSlot,
                StationShortCode = fifthSlot,
                StationLongCode = fourthSlot
            };

            if (NaPTANStopsDictionary.ContainsKey("9100" + Station.StationLongCode))
            {
                Station.ATCOCode = NaPTANStopsDictionary["9100" + Station.StationLongCode].ATCOCode;
            }
            else
            {
                Console.WriteLine($"No ATCO code found for {Station.StationName} station.");
            }

            CIFStations.Add(Station);
        }
    }

    Console.WriteLine($"Read {CIFStations.Count} stations of which {CIFStations.Where(x => x.ATCOCode != null).Count()} have a matched NaPTAN code.");

    Console.WriteLine("Reading the timetable file.");
    ZipArchiveEntry TimetableFile = Archive.Entries.Where(x => x.FullName.EndsWith(".mca")).First();
    StreamReader TimetableFileStreamReader = new StreamReader(TimetableFile.Open());

    string CurrentJourneyID = "";
    string CurrentOperatorCode = "";
    string CurrentTrainType = "";
    string CurrentTrainClass = "";
    string CurrentTrainMaxSpeed = "";
    Calendar CurrentCalendar = null;

    while (TimetableFileStreamReader.EndOfStream == false)
    {
        string TimetableLine = TimetableFileStreamReader.ReadLine();
        if (TimetableLine.StartsWith("BS"))
        {
            // THIS IS ALMOST CERTAINLY NOT THE CURRENT JOURNEY ID. BUT IT'S OKAY DURING DEVELOPMENT.
            //CurrentJourneyID = TimetableLine;

            // Example line is "BSNY244881905191912080000001 POO2D67    111821020 EMU333 100      S            P"
            CurrentJourneyID = TimetableLine.Substring(2, 7);

            string StartDateString = TimetableLine.Substring(9, 6);
            string EndDateString = TimetableLine.Substring(15, 6);
            string DaysOfOperationString = TimetableLine.Substring(21, 7);

            // Since a single timetable can have a single Journey ID that is valid at different non-overlapping times a unique Journey ID includes the Date strings and the character at position 79.
            CurrentJourneyID = CurrentJourneyID + StartDateString + EndDateString + TimetableLine.Substring(79, 1);

            CurrentCalendar = new Calendar()
            {
                start_date = "20" + StartDateString,
                end_date = "20" + EndDateString,
                service_id = CurrentJourneyID + "_service",
                monday = int.Parse(DaysOfOperationString.Substring(0, 1)),
                tuesday = int.Parse(DaysOfOperationString.Substring(1, 1)),
                wednesday = int.Parse(DaysOfOperationString.Substring(2, 1)),
                thursday = int.Parse(DaysOfOperationString.Substring(3, 1)),
                friday = int.Parse(DaysOfOperationString.Substring(4, 1)),
                saturday = int.Parse(DaysOfOperationString.Substring(5, 1)),
                sunday = int.Parse(DaysOfOperationString.Substring(6, 1))
            };

            CurrentTrainType = TimetableLine.Substring(50, 3);
            CurrentTrainClass = TimetableLine.Substring(53, 3);
            CurrentTrainMaxSpeed = TimetableLine.Substring(57, 3);
        }

        if (TimetableLine.StartsWith("BX"))
        {
            CurrentOperatorCode = TimetableLine.Substring(11, 2);

            JourneyDetail journeyDetail = new JourneyDetail()
            {
                JourneyID = CurrentJourneyID,
                OperatorCode = CurrentOperatorCode,
                OperationsCalendar = CurrentCalendar,
                TrainClass = CurrentTrainClass,
                TrainMaxSpeed = CurrentTrainMaxSpeed,
                TrainType = CurrentTrainType
            };

            JourneyDetailsForJourneyIDDictionary.Add(CurrentJourneyID, journeyDetail);
        }

        if (TimetableLine.StartsWith("LO") || TimetableLine.StartsWith("LI") || TimetableLine.StartsWith("LT"))
        {
            string firstSlot = TimetableLine.Substring(0, 2).Trim();
            string secondSlot = TimetableLine.Substring(2, 7).Trim();
            string thirdSlot = TimetableLine.Substring(10, 4).Trim();
            string fourthSlot = TimetableLine.Substring(15, 4).Trim();
            string fifthSlot = TimetableLine.Substring(25, 8).Trim();

            if (fifthSlot != "00000000")
            {
                StationStop stationStop = new StationStop()
                {
                    StopType = firstSlot,
                    StationLongCode = secondSlot
                };

                if (NaPTANStopsDictionary.ContainsKey("9100" + stationStop.StationLongCode))
                {
                    if (thirdSlot.Count() == 4 && fourthSlot.Count() == 4)
                    {
                        stationStop.WorkingTimetableDepartureTime = stringToTimeSpan(thirdSlot);
                        stationStop.PublicTimetableDepartureTime = stringToTimeSpan(fourthSlot);
                        stationStop.NaPTANStop = NaPTANStopsDictionary["9100" + stationStop.StationLongCode];

                        if (StopTimesForJourneyIDDictionary.ContainsKey(CurrentJourneyID))
                        {
                            List<StationStop> UpdatedStationStops = StopTimesForJourneyIDDictionary[CurrentJourneyID];
                            UpdatedStationStops.Add(stationStop);
                            StopTimesForJourneyIDDictionary.Remove(CurrentJourneyID);
                            StopTimesForJourneyIDDictionary.Add(CurrentJourneyID, UpdatedStationStops);
                        }
                        else
                        {
                            StopTimesForJourneyIDDictionary.Add(CurrentJourneyID, new List<StationStop>() { stationStop });
                        }
                    }
                }
            }
        }
    }
    Console.WriteLine($"Read {StopTimesForJourneyIDDictionary.Keys.Count} journeys.");
}

Console.WriteLine("Creating GTFS output.");

// We have two dictionaries that let us create all our GTFS output.
// StopTimesForJourneyIDDictionary
// JourneyDetailsForJourneyIDDictionary
// CIFStations

List<string> Agencies = JourneyDetailsForJourneyIDDictionary.Values.Select(x => x.OperatorCode).Distinct().ToList();

// AgencyList will hold the GTFS agency.txt file contents
List<Agency> AgencyList = new List<Agency>();

// Get all unique agencies from our output
foreach (string agency in Agencies)
{
    Agency NewAgency = new Agency()
    {
        agency_id = agency,
        agency_name = agency,
        agency_url = "https://www.google.com/search?q=" + agency + "%20rail%20operator%20code", // google plus name of agency by default
        agency_timezone = "Europe/London" // Europe/London by default
    };
    AgencyList.Add(NewAgency);
}


List<GTFSNaptanStop> GTFSStopsList = new List<GTFSNaptanStop>();
foreach (CIFStation cIFStation in CIFStations.Where(x => x.ATCOCode != null))
{
    var NaPTANEntry = NaPTANStopsDictionary["9100" + cIFStation.StationLongCode];

    GTFSNaptanStop gTFSNaptanStop = new GTFSNaptanStop()
    {
        stop_id = cIFStation.ATCOCode,
        stop_name = cIFStation.StationName,
        stop_code = cIFStation.StationShortCode,
        stop_lat = Math.Round(NaPTANEntry.Latitude.Value, 5),
        stop_lon = Math.Round(NaPTANEntry.Longitude.Value, 5)
    };

    GTFSStopsList.Add(gTFSNaptanStop);
}

List<Route> RoutesList = new List<Route>();
foreach (string journeyID in JourneyDetailsForJourneyIDDictionary.Keys)
{
    JourneyDetail journeyDetail = JourneyDetailsForJourneyIDDictionary[journeyID];
    Route route = new Route()
    {
        agency_id = journeyDetail.OperatorCode,
        route_id = journeyDetail.JourneyID + "_route",
        route_type = "2",
        route_short_name = journeyDetail.OperatorCode + "_" + journeyDetail.JourneyID
    };
    RoutesList.Add(route);
}

List<Trip> tripList = new List<Trip>();
foreach (JourneyDetail journeyDetail in JourneyDetailsForJourneyIDDictionary.Values)
{
    Trip trip = new Trip()
    {
        route_id = journeyDetail.JourneyID + "_route",
        service_id = journeyDetail.JourneyID + "_service",
        trip_id = journeyDetail.JourneyID + "_trip"
    };
    tripList.Add(trip);
}

// This export line is more complicated than it might at first seem sensible to be because of an understandable quirk in the GTFS format.
// Stop times are only given as a time of day, and not a datetime. This causes problems when a service runs over midnight.
// To fix this we express stop times on a service that started the previous day with times such as 24:12 instead of 00:12 and 25:20 instead of 01:20.
// I assume that no journey runs into a third day.

List<StopTime> stopTimesList = new List<StopTime>();

foreach (string JourneyID in StopTimesForJourneyIDDictionary.Keys)
{
    List<StationStop> StationStops = StopTimesForJourneyIDDictionary[JourneyID];
    int count = 1;

    bool JourneyStartedYesterdayFlag = false;
    TimeSpan PreviousStopDepartureTime = new TimeSpan(0);

    foreach (StationStop stationStop in StationStops)
    {
        if (stationStop.PublicTimetableDepartureTime < PreviousStopDepartureTime)
        {
            JourneyStartedYesterdayFlag = true;
        }

        StopTime stopTime = new StopTime()
        {
            trip_id = JourneyID + "_trip",
            stop_id = stationStop.NaPTANStop.ATCOCode,
            stop_sequence = count
        };

        if (JourneyStartedYesterdayFlag == true)
        {
            stationStop.WorkingTimetableDepartureTime = stationStop.WorkingTimetableDepartureTime.Add(new TimeSpan(24, 0, 0));
            stationStop.PublicTimetableDepartureTime = stationStop.PublicTimetableDepartureTime.Add(new TimeSpan(24, 0, 0));
            stopTime.arrival_time = Math.Floor(stationStop.PublicTimetableDepartureTime.TotalHours).ToString() + stationStop.PublicTimetableDepartureTime.ToString(@"hh\:mm\:ss").Substring(2, 6);
            stopTime.departure_time = Math.Floor(stationStop.PublicTimetableDepartureTime.TotalHours).ToString() + stationStop.PublicTimetableDepartureTime.ToString(@"hh\:mm\:ss").Substring(2, 6);
        }
        else
        {
            stopTime.arrival_time = stationStop.PublicTimetableDepartureTime.ToString(@"hh\:mm\:ss");
            stopTime.departure_time = stationStop.PublicTimetableDepartureTime.ToString(@"hh\:mm\:ss");
        }
        stopTimesList.Add(stopTime);

        PreviousStopDepartureTime = stationStop.PublicTimetableDepartureTime;
        count++;
    }
}

List<Calendar> calendarList = JourneyDetailsForJourneyIDDictionary.Values.Select(x => x.OperationsCalendar).ToList();

Console.WriteLine("Creating GTFS files.");
// write GTFS txts.
// agency.txt, calendar.txt, calendar_dates.txt, routes.txt, stop_times.txt, stops.txt, trips.txt
if (Directory.Exists("output") == false)
{
    Directory.CreateDirectory("output");
}


using (CsvWriter CSVwriter = new CsvWriter(File.CreateText(@"output/agency.txt"), CultureInfo.InvariantCulture))
{
    CSVwriter.WriteRecords(AgencyList);
}

using (CsvWriter CSVwriter = new CsvWriter(File.CreateText(@"output/stops.txt"), CultureInfo.InvariantCulture))
{
    CSVwriter.WriteRecords(GTFSStopsList);
}

using (CsvWriter CSVwriter = new CsvWriter(File.CreateText(@"output/routes.txt"), CultureInfo.InvariantCulture))
{
    CSVwriter.WriteRecords(RoutesList);
}

using (CsvWriter CSVwriter = new CsvWriter(File.CreateText(@"output/trips.txt"), CultureInfo.InvariantCulture))
{
    CSVwriter.WriteRecords(tripList);
}

using (CsvWriter CSVwriter = new CsvWriter(File.CreateText(@"output/calendar.txt"), CultureInfo.InvariantCulture))
{
    CSVwriter.WriteRecords(calendarList);
}

using (CsvWriter CSVwriter = new CsvWriter(File.CreateText(@"output/stop_times.txt"), CultureInfo.InvariantCulture))
{
    CSVwriter.WriteRecords(stopTimesList);
}


Console.WriteLine("Creating a GTFS .zip file.");
if (File.Exists("output_gtfs.zip"))
{
    File.Delete("output_gtfs.zip");
}
ZipFile.CreateFromDirectory("output", "output_gtfs.zip", CompressionLevel.Optimal, false, Encoding.UTF8);

Console.WriteLine("You may wish to validate the GTFS output using a tool such as https://github.com/google/transitfeed/");

/*
var OrderedStopTimesForJourneyDictionary = StopTimesForJourneyIDDictionary.OrderByDescending(x => x.Value.Count);
Console.WriteLine("Calculating average velocities for all journeys.");
List<JourneyVelocity> JourneyVelocities = new List<JourneyVelocity>();
foreach (var JourneyID in StopTimesForJourneyIDDictionary.Where(x => x.Value.Count > 1)) {
    var FirstStop = JourneyID.Value.First();
    var LastStop = JourneyID.Value.Last();

    GeoCoordinate GeoFirstStop = new GeoCoordinate(FirstStop.NaPTANStop.Latitude, FirstStop.NaPTANStop.Longitude);
    GeoCoordinate GeoSecondStop = new GeoCoordinate(LastStop.NaPTANStop.Latitude, LastStop.NaPTANStop.Longitude);
    var Distance = GeoFirstStop.GetDistanceTo(GeoSecondStop) / 1000;
    var Time = LastStop.DepartureTime - FirstStop.DepartureTime;

    // if a journey time is negative, add a day to it.
    while (Time.TotalSeconds < 0)
    {
        Time = Time.Add(new TimeSpan(24, 0, 0));
    }
    var Velocity = Math.Round(Distance / Time.TotalHours, 1);

    JourneyVelocity journeyVelocity = new JourneyVelocity()
    {
        Velocity = Velocity,
        JourneyDetails = JourneyID
    };
    JourneyVelocities.Add(journeyVelocity);
}

var orderedJourneyVelocities = JourneyVelocities.OrderByDescending(x => x.Velocity);
*/

/*
static DateTime stringToDate(string input)
{
    // input is expected to be YYMMDD
    string Years = input.Substring(0, 2);
    string Months = input.Substring(2, 2);
    string Days = input.Substring(4, 2);
    int YearsInt = int.Parse(Years);
    int MonthsInt = int.Parse(Months);
    int DaysInt = int.Parse(Days);

    DateTime dateTime = new DateTime(YearsInt, MonthsInt, DaysInt);
    return dateTime;
}
*/

static TimeSpan stringToTimeSpan(string input)
{
    // input is expected to be HHMM
    string hours = input.Substring(0, 2);
    string minutes = input.Substring(2, 2);
    if (hours.StartsWith("0"))
    {
        hours = hours.Substring(1, 1);
    }
    int hoursint = int.Parse(hours);
    int minutesint = int.Parse(minutes);
    TimeSpan timeSpan = new TimeSpan(hoursint, minutesint, 0);
    return timeSpan;
}


// Classes to hold the CIF input
// .tsi file looks unimportant
// .set file looks unimportant
// .msn looks like station names (lines starting with an A) and alternative names (lines starting with an L)
// .mca looks like the actual train services
// .flf file looks unimportant (additional links which our trip planner will sort out for us)
// .dat file looks unimportant (a list of the files)
// .alf file looks unimportant (additional links, but connection times may matter I suppose. Tough one)
// .ztr file looks unimportant (includes international services, including Northern Ireland, some connecting buses from Liverpool South Parkway to the airport, and links to heritage railways

// SO THE TWO MAIN FILES TO PARSE ARE .MCA AND .MSN
// DO WE ALSO NEED NAPTAN TO LOCATE THE STATIONS?


public class JourneyDetail
{
    public string JourneyID { get; set; }
    public string OperatorCode { get; set; }
    public string TrainType { get; set; }
    public string TrainClass { get; set; }
    public string TrainMaxSpeed { get; set; }
    public Calendar OperationsCalendar { get; set; }
}

public class JourneyVelocity
{
    public KeyValuePair<string, List<StationStop>> JourneyDetails { get; set; }
    public double Velocity { get; set; }
}

public class CIFStation
{
    public string StationName { get; set; }
    public string StationShortCode { get; set; }
    public string StationLongCode { get; set; }
    public string ATCOCode { get; set; }
}

public class CIFJourney
{
    public string JourneyID { get; set; }
    public List<StationStop> StationStops { get; set; }
}
public class StationStop
{
    public string StationLongCode { get; set; }
    public string StopType { get; set; } // Origin, Intermediate, or Terminus
    public TimeSpan WorkingTimetableDepartureTime { get; set; }
    public TimeSpan PublicTimetableDepartureTime { get; set; }
    public NaptanStop NaPTANStop { get; set; }
}

// Classes to hold the GTFS output
// A LIST OF THESE CALENDAR OBJECTS CREATE THE GTFS calendar.txt file
public class Calendar
{
    public string service_id { get; set; }
    public int monday { get; set; }
    public int tuesday { get; set; }
    public int wednesday { get; set; }
    public int thursday { get; set; }
    public int friday { get; set; }
    public int saturday { get; set; }
    public int sunday { get; set; }
    public string start_date { get; set; }
    public string end_date { get; set; }
}

// A LIST OF THESE CALENDAR EXCEPTIONS CREATES THE GTFS  calendar_dates.txt file
public class CalendarException
{
    public string service_id { get; set; }
    public string date { get; set; }
    public string exception_type { get; set; }
}

// A LIST OF THESE TRIPS CREATES THE GTFS trips.txt file.
public class Trip
{
    public string route_id { get; set; }
    public string service_id { get; set; }
    public string trip_id { get; set; }
    public string trip_headsign { get; set; }
    public string direction_id { get; set; }
    public string block_id { get; set; }
    public string shape_id { get; set; }
}

// A LIST OF THESE STOPTIMES CREATES THE GTFS stop_times.txt file
public class StopTime
{
    public string trip_id { get; set; }
    public string arrival_time { get; set; }
    public string departure_time { get; set; }
    public string stop_id { get; set; }
    public int stop_sequence { get; set; }
    public string stop_headsign { get; set; }
    public string pickup_type { get; set; }
    public string drop_off_type { get; set; }
    public string shape_dist_traveled { get; set; }
}

//A LIST OF THESE NAPTANSTOPS CREATES THE GTFS stops.txt file
public class GTFSNaptanStop
{
    public string stop_id { get; set; }
    public string stop_code { get; set; }
    public string stop_name { get; set; }
    public double stop_lat { get; set; }
    public double stop_lon { get; set; }
    public string stop_url { get; set; }
    //public string vehicle_type { get; set; }
}

// A LIST OF THESE ROUTES CREATES THE GTFS routes.txt file.
public class Route
{
    public string route_id { get; set; }
    public string agency_id { get; set; }
    public string route_short_name { get; set; }
    public string route_long_name { get; set; }
    public string route_desc { get; set; }
    public string route_type { get; set; }
    public string route_url { get; set; }
    public string route_color { get; set; }
    public string route_text_color { get; set; }
}

// A LIST OF THESE AGENCIES CREATES THE GTFS agencies.txt file.
public class Agency
{
    public string agency_id { get; set; }
    public string agency_name { get; set; }
    public string agency_url { get; set; }
    public string agency_timezone { get; set; }
}
public class NaptanStop
{
    public string ATCOCode { get; set; }
    public string NaptanCode { get; set; }
    public string CommonName { get; set; }
    public double? Easting { get; set; }
    public double? Northing { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string StopType { get; set; }
}
