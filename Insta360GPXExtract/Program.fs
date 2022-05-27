open System.IO
open System.Text
open Acadian.FSharp
open System

type Stats =
     | Accelerometer of Accelerometer
     | GPS of GPS

and Accelerometer = {
    timeCode: float
    g: float * float * float
    av: float * float * float
}

and GPS = {
    time: DateTime
    latitude: float
    longitude: float
    speed: float
    track: float
    elevation: float
}

let trailerBytes = Encoding.ASCII.GetBytes "8db42d694ccc418790edff439fe026bf"

let trailerPos (reader: BinaryReader) pos len =
    if len >= 32 then
        reader.BaseStream.Seek(pos, SeekOrigin.Begin) |> ignore
        let bytes = reader.ReadBytes len
        bytes
        |> Array.toSeq
        |> Seq.windowed 32
        |> Seq.mapi(fun i b -> i, b = trailerBytes)
        |> Seq.filter(fun (_, b) -> b)
        |> Seq.map(fun (i, _) -> i)
        |> Seq.tryHead
    else
        None

let tryFindTrailer (reader: BinaryReader) =
    // Get block offsets of 1 MiB blocks in 512KiB steps
    let rec blocks offset = seq {
        let ofsEnd = offset + 1048576L
        if ofsEnd >= reader.BaseStream.Length then
            yield offset, int(reader.BaseStream.Length - offset)
        else
            yield offset, 1048576
            yield! blocks (offset + 524288L)
    }
    0L
    |> blocks
    |> Seq.rev
    |> Seq.choose(fun (pos, len) -> trailerPos reader pos len |> Option.map(fun i -> int64(i) + pos + 32L))
    |> Seq.tryHead

let readStats file = seq {
    use stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)
    use reader = new BinaryReader(stream)
    let maybeTrailerPos = option {
        let! trailerPos = tryFindTrailer reader
        stream.Seek(trailerPos - 40L, SeekOrigin.Begin) |> ignore
        let trailerSize = reader.ReadUInt32() |> int64
        if trailerSize > stream.Length then return! None
        return (trailerPos - 78L, trailerSize - 78L)
    }
    match maybeTrailerPos with
    | None -> yield! Seq.empty
    | Some (trailerPos, trailerSize) ->
        let rec enumerateTrailers pos size = seq {
            if size > 0L then
                stream.Seek(pos, SeekOrigin.Begin) |> ignore
                let id = reader.ReadUInt16()
                let len = reader.ReadUInt32()
                let blockPos = pos - int64(len)
                match id with
                | 0x300us when len % 56u = 0u ->
                    stream.Seek(blockPos, SeekOrigin.Begin) |> ignore
                    let rec enumerateAccelerometers left = seq {
                        if left > 0L then
                            yield {
                                Accelerometer.timeCode = float(reader.ReadUInt64()) / 1e3
                                g = reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble()
                                av = reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble()
                            }
                            yield! enumerateAccelerometers (left - 56L)
                    }
                    yield! len |> int64 |> enumerateAccelerometers |> Seq.map Stats.Accelerometer
                | 0x700us when len % 53u = 0u ->
                    stream.Seek(blockPos, SeekOrigin.Begin) |> ignore
                    let rec enumerateGpsData left = seq {
                        if left > 0L then
                            let time = reader.ReadUInt32()
                            stream.Seek(6L, SeekOrigin.Current) |> ignore
                            let fix = Encoding.ASCII.GetString(reader.ReadBytes 1) // 11
                            let lat = reader.ReadDouble() // 19
                            let latDir = Encoding.ASCII.GetString(reader.ReadBytes 1) // 20
                            let lon = reader.ReadDouble() // 28
                            let lonDir = Encoding.ASCII.GetString(reader.ReadBytes 1) // 29
                            let spd = reader.ReadDouble() // 37
                            let trk = reader.ReadDouble() // 45
                            let alt = reader.ReadDouble() // 53
                            if fix = "A" then yield {
                                time = DateTime.UnixEpoch.AddSeconds(float time)
                                latitude = if latDir = "S" then -lat else lat
                                longitude = if lonDir = "E" then lon else -lon
                                speed = spd
                                track = trk
                                elevation = alt
                            }
                            yield! enumerateGpsData (left - 53L)
                    }
                    yield! len
                           |> int64
                           |> enumerateGpsData
                           |> Seq.groupBy(fun x -> x.time)
                           |> Seq.map(fun (_, gpses) -> gpses |> Seq.head)
                           |> Seq.map Stats.GPS
                | _ -> ()
                if id = 0x300us then
                    stream.Seek(blockPos, SeekOrigin.Begin) |> ignore
                let size' = size - 6L - int64(len)
                let pos' = blockPos - 6L
                yield! enumerateTrailers pos' size'
        }
        yield! enumerateTrailers trailerPos trailerSize
}

[<EntryPoint>]
let main args =
    if args.Length <> 2 then
        printfn "Usage: Insta360GPXExtract videofile gpxfile"
        1
    else
        use stream = new FileStream(args.[1], FileMode.Create, FileAccess.Write, FileShare.Read)
        use writer = new StreamWriter(stream, Encoding.ASCII)
        writer.WriteLine "<gpx xmlns=\"http://www.topografix.com/GPX/1/1\" xmlns:gpxx=\"http://www.garmin.com/xmlschemas/GpxExtensions/v3\" xmlns:gpxtpx=\"http://www.garmin.com/xmlschemas/TrackPointExtension/v1\" creator=\"Oregon 400t\" version=\"1.1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd http://www.garmin.com/xmlschemas/GpxExtensions/v3 http://www.garmin.com/xmlschemas/GpxExtensionsv3.xsd http://www.garmin.com/xmlschemas/TrackPointExtension/v1 http://www.garmin.com/xmlschemas/TrackPointExtensionv1.xsd\">"
        writer.WriteLine "<trk><trkseg>"
        for stat in readStats args.[0] do
            match stat with
            | Stats.GPS gps ->
                sprintf "<trkpt lat=\"%f\" lon=\"%f\">" gps.latitude gps.longitude |> writer.WriteLine
                sprintf "  <ele>%f</ele>" gps.elevation |> writer.WriteLine
                sprintf "  <time>%04d-%02d-%02dT%02d:%02d:%02d.%03dZ</time>" gps.time.Year gps.time.Month gps.time.Day gps.time.Hour gps.time.Minute gps.time.Second gps.time.Millisecond |> writer.WriteLine
                sprintf "  <speed>%f</speed>" gps.speed |> writer.WriteLine
                writer.WriteLine "</trkpt>"
            | _ -> ()
        writer.WriteLine "</trkseg></trk>"
        writer.WriteLine "</gpx>"
        writer.Flush()
        stream.Flush()
        0
