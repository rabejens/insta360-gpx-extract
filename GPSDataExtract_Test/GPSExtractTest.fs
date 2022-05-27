module Insta360GPSTools.GPSExtractTest

open Xunit
open System.IO
open System.Reflection
open Acadian.FSharp

[<Fact>]
let ``GPS data is extracted correctly`` () =
    // TODO better test
    let rec tryFindTestFile dir = option {
        let file = Path.Combine(dir, "test.insv")
        if File.Exists file then
            return file
        else
            let pd = Directory.GetParent(dir)
            if pd = null then
                return! None
            else
                return! tryFindTestFile pd.FullName
    }
    let maybeTestFile = tryFindTestFile(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName)
    if maybeTestFile.IsSome then
        use stream = new FileStream(maybeTestFile.Value, FileMode.Open, FileAccess.Read, FileShare.Read)
        let gpsRecords = stream |> GPSExtract.enumerateGpsData |> Seq.toList
        Assert.NotEmpty gpsRecords
        if gpsRecords.Length > 1 then
            let sameTime =
                gpsRecords
                |> List.windowed 2
                |> List.exists(fun x -> x.[0].time = x.[1].time)
            Assert.False sameTime
