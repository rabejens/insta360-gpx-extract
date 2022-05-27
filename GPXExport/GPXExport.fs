module Insta360GPSTools.GPXExport

open System.IO

let private writeEntry writeLine gps =
    sprintf "<trkpt lat=\"%f\" lon=\"%f\">" gps.latitude gps.longitude |> writeLine
    sprintf "  <ele>%f</ele>" gps.elevation |> writeLine
    sprintf "  <time>%04d-%02d-%02dT%02d:%02d:%02dZ</time>" gps.time.Year gps.time.Month gps.time.Day gps.time.Hour gps.time.Minute gps.time.Second |> writeLine
    writeLine "</trkpt>"

/// Writes the GPS entries to a GPX structure. Recommended for F#
let writeGpx writeLine (entries: GPSEntry seq) =
    // The GPS structure is not assembled using an XML library by design.
    // Insta360 GPS entries might be many, so everything is done in a streamed way to avoid out of memory.
    writeLine "<gpx xmlns=\"http://www.topografix.com/GPX/1/1\" xmlns:gpxx=\"http://www.garmin.com/xmlschemas/GpxExtensions/v3\" xmlns:gpxtpx=\"http://www.garmin.com/xmlschemas/TrackPointExtension/v1\" creator=\"Oregon 400t\" version=\"1.1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd http://www.garmin.com/xmlschemas/GpxExtensions/v3 http://www.garmin.com/xmlschemas/GpxExtensionsv3.xsd http://www.garmin.com/xmlschemas/TrackPointExtension/v1 http://www.garmin.com/xmlschemas/TrackPointExtensionv1.xsd\">"
    writeLine "<trk><trkseg>"
    // GPX only has second precision, so just use the first of every second
    do // To dispose resources early
        use enum = entries.GetEnumerator()
        let rec writeSecondEntries maybePrevTime =
            if enum.MoveNext() then
                let current = enum.Current
                let currentTimeSec = (current.time.Year, current.time.Month, current.time.Day, current.time.Hour, current.time.Minute, current.time.Second)
                match maybePrevTime with
                | Some pt when pt = currentTimeSec -> () // ignore
                | _ -> writeEntry writeLine current
                currentTimeSec |> Some |> writeSecondEntries
        writeSecondEntries None
    writeLine "</trkseg></trk>"
    writeLine "</gpx>"

/// Writes a GPX structure to a text writer. Recommended for C#/VB
let WriteGpx entries (writer: TextWriter) =
    writeGpx writer.WriteLine entries
    writer.Flush()
