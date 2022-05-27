module Insta360GPSTools.GPSExtract

open System.IO
open System.Text
open Acadian.FSharp
open System
open System.Collections.Generic

let private trailerBytes = Encoding.ASCII.GetBytes "8db42d694ccc418790edff439fe026bf"

// TODO: This is very brute force. Should analyze how ExifTool finds the trailer and port this approach.
let private tryFindTrailerPos (reader: BinaryReader) =
    let stream = reader.BaseStream
    // From the end of the stream, get chunks of 1MiB, overlapped by 64 bytes.
    // This way, if the trailer is there, the trailer bytes are hit.
    // Since the Insta360 trailer is usually at the very end of the file, this
    // approach is fast enough, albeit a bit crude.
    let rec searchTrailerBytes pos =
        // Position the pointer, and read max. 1MiB
        stream.Position <- pos
        let len = (stream.Length - pos) |> min 1048576L
        let maybeTrailerEnd =
            if len >= 32L then
                let bytes = len |> int |> reader.ReadBytes
                // Find the magic bytes by putting a sliding window of size 32 over the bytes
                // and get the portion where it is the same as the trailer bytes, from the end
                bytes
                |> Array.toSeq
                |> Seq.windowed 32
                |> Seq.tryFindIndexBack((=) trailerBytes)
                |> Option.map((+) 32)
            else None
        match maybeTrailerEnd with
        | Some trailerEnd ->
            // Convert to int64 and return
            trailerEnd |> int64 |> (+) pos |> Some
        | None ->
            // Shift the pointer 1MiB - 64 bytes back
            let pos' = (pos - 1048576L + 64L) |> max 0L
            // If the pointer was actually shifted, try again
            if pos' < pos then searchTrailerBytes pos' else None
    (stream.Length - 1048576L) |> max 0L |> searchTrailerBytes

let rec private enumerateRecordsFromGpsBlock (reader: BinaryReader) left = seq {
    if left > 0L && left % 53L = 0L then
        // Can extract a record
        // Time comes first. It's a unix time in seconds. Convert to DateTime.
        let time = reader.ReadUInt32() |> float |> DateTime.UnixEpoch.AddSeconds
        // Two unused values with 6 bytes in total
        reader.BaseStream.Seek(6L, SeekOrigin.Current) |> ignore
        // Rest of the data
        let fix = Encoding.ASCII.GetString(reader.ReadBytes 1)
        let lat = reader.ReadDouble()
        let latDir = Encoding.ASCII.GetString(reader.ReadBytes 1).ToLowerInvariant()
        let lon = reader.ReadDouble()
        let lonDir = Encoding.ASCII.GetString(reader.ReadBytes 1).ToLowerInvariant()
        let spd = reader.ReadDouble()
        let trk = reader.ReadDouble()
        let alt = reader.ReadDouble()
        // Create a record.
        yield
            if fix = "A" then
                {
                    time = time
                    latitude = if latDir = "s" then -lat else lat
                    longitude = if lonDir = "e" then lon else -lon
                    speed = spd
                    elevation = alt
                    heading = trk
                }
            else
                GPSEntry.invalid time
        // Return the rest
        yield! enumerateRecordsFromGpsBlock reader (left - 53L)
}

let rec private enumerateGpsRecords (reader: BinaryReader) pos left = seq {
    if pos >= 0L && left > 0L then
        reader.BaseStream.Position <- pos
        let id = reader.ReadUInt16()
        let len = int64(reader.ReadUInt32())
        let blockPos = pos - len
        if id = 0x700us then
            // Hit a GPS entry
            reader.BaseStream.Position <- blockPos
            yield! enumerateRecordsFromGpsBlock reader len
        // The next block info is 6 bytes before blockPos
        let pos' = blockPos - 6L
        let left' = left - len - 6L
        // Try next
        yield! enumerateGpsRecords reader pos' left'
}

let private fixTimes (records: GPSEntry seq) =
    // The timestamps only have second precision, but the GPS remote delivers 10 records per second.
    // So this has to be accounted for.
    let fixBatch first batch =
        // The batch is reversed because lists are built from the end.
        // So, if there are less than 10 elements and it's the first batch, append.
        // If there are less than 10 elements and it's NOT the first, prepend
        let batch' =
            if batch |> List.length >= 10 || batch |> List.isEmpty then
                batch
            else
                let time = batch.Head.time
                let padding = List.init (10 - batch.Length) (fun _ -> GPSEntry.invalid time)
                if first then List.append batch padding else List.append padding batch
        if batch'.IsEmpty then
            []
        else
            // Now, reverse the list, make the times more precise, and remove the invalid entries.
            let bl = batch'.Length |> float // Should always be 10 but this helps catch occasions where it's not
            batch'
            |> List.rev
            |> List.mapi(fun i e -> { e with time = e.time.AddSeconds(float(i) / bl)})
            |> List.filter(GPSEntry.isValid)

    let rec doFixTimes first pending (enum: IEnumerator<GPSEntry>) = seq {
        if enum.MoveNext() then
            let current = enum.Current
            // If next record's time is NOT the same as previous time, output the pending batch.
            match current.time, pending with
            | t1, { time = t0 } :: _ when t0 <> t1 ->
                // A batch is completed
                yield! fixBatch first pending
                yield! doFixTimes false [ current ] enum
            | _ ->
                // Add the current entry to the pending ones
                yield! doFixTimes first (current :: pending) enum
        else
            // Return the last batch and stop.
            yield! fixBatch first pending
    }

    // This is wrapped in a seq{} block to make sure the enumerator is kept open long enough.
    seq {
        use enum = records.GetEnumerator()
        yield! doFixTimes true [] enum
    }

[<CompiledName("EnumerateGpsData")>]
let enumerateGpsData stream =
    let maybeGpsData = option {
        let reader = new BinaryReader(stream)
        let! trailerPos = tryFindTrailerPos reader
        // The trailer size is a little-endian uint32 40 bytes from the end of the trailer
        stream.Position <- (trailerPos - 40L)
        let trailerSize = reader.ReadUInt32() |> int64
        // Invalid trailer size
        if trailerPos - trailerSize < 0L then return Seq.empty
        // The trailer must be unrolled from the back.
        // Each block starts with a two-byte block id and a four-byte block size,
        // with the block data before this info. The first info is at 78 bytes before trailer end.
        return enumerateGpsRecords reader (trailerPos - 78L) (trailerSize - 78L) |> fixTimes
    }
    maybeGpsData |> Option.defaultValue Seq.empty
