module Insta360GPSTools.Insta360GPXExtract

open cli4net
open System.IO
open System.Text
open System.Text.RegularExpressions
open System

let private mkOpt opt longOpt maybeArgName description isRequired =
    let cliOpt = new Option(opt, maybeArgName |> Option.isSome, description)
    cliOpt.SetLongOpt longOpt
    cliOpt.SetRequired isRequired
    maybeArgName
    |> Option.iter(cliOpt.SetArgName)
    cliOpt

let private opts =
    let opts = new Options()
    opts.AddOption("h", "help", false, "Display this help")
        .AddOption(mkOpt "i" "in-path" (Some "PATH") "Input file or directory with insv files" true)
        .AddOption(mkOpt "o" "out-path" (Some "PATH") "Output file or directory. If the path is an existing directory, the output file(s) will be written there. If not, and only one file is to be written, the path denotes the file. If not, and multiple files are to be written, a new directory is created." true)
        .AddOption("s", "single", false, "Assemble all input files into one single GPX file. Make sure the timestamps do NOT overlap! For this to work, all files must conform to the Insta360 naming convention, VID_yyyyMMdd_HHmmss_00_nnn.insv")
        .AddOption("r", "recursive", false, "If given, recurse into all input directories")

let rec private enumerateInsvFiles recursive path = seq {
    if File.Exists path then
        yield path
    elif Directory.Exists path then
        yield! Directory.EnumerateFiles(path, "*.insv")
        if recursive then yield! Directory.EnumerateDirectories(path) |> Seq.collect(enumerateInsvFiles recursive)
    else
        invalidArg "-i" (sprintf "%s does not exist" path)
}

let private writeGpxFileToFile outFile inFile =
    printf "Exporting GPS data from %s to %s ... " inFile outFile
    let outDir = Directory.GetParent(outFile).FullName
    if not <| Directory.Exists outDir then Directory.CreateDirectory outDir |> ignore
    use ins = new FileStream(inFile, FileMode.Open, FileAccess.Read, FileShare.Read)
    let gpsData = GPSExtract.enumerateGpsData ins
    use outs = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.Write)
    use writer = new StreamWriter(outs, Encoding.ASCII)
    gpsData |> GPXExport.writeGpx writer.WriteLine
    writer.Flush()
    outs.Flush()
    printfn "Done."

let private writeGpxFileToDir outDir (inFile: string) =
    let rec getUnusedFile i =
        let fileName = if i = 0 then sprintf "%s.gpx" (Path.GetFileName inFile) else sprintf "%s_%03d.gpx" (Path.GetFileName inFile) i
        let outFile = Path.Combine(outDir, fileName)
        if File.Exists outFile || Directory.Exists outFile then getUnusedFile (i + 1) else outFile
    let outFile = getUnusedFile 0
    writeGpxFileToFile outFile inFile

let private writeConcatGpxFile outPath inputFiles =
    // Resolve the file to write to, create parent dirs if needed
    let outFile =
        if Directory.Exists outPath then
            let rec getUnusedFile i =
                let fileName = if i = 0 then "track.gpx" else sprintf "track_%03d.gpx" i
                let outFile = Path.Combine(outPath, fileName)
                if File.Exists outFile || Directory.Exists outFile then getUnusedFile (i + 1) else outFile
            getUnusedFile 0
        else
            let dir = Directory.GetParent(outPath).FullName
            if not <| Directory.Exists dir then Directory.CreateDirectory dir |> ignore
            outPath
    // Write the data
    printfn "Writing data from %d files to %s ... " (inputFiles |> Seq.length) outFile
    let rec enumerateDataInOrder files = seq {
        match files with
        | [] -> yield! Seq.empty
        | inFile :: files' ->
            printfn "  Using file %s" inFile
            // Double yield to make sure the stream is correctly closed and to not break tail-recursion
            yield! seq {
                use ins = new FileStream(inFile, FileMode.Open, FileAccess.Read, FileShare.Read)
                yield! GPSExtract.enumerateGpsData ins
            }
            yield! enumerateDataInOrder files'
    }
    let gpsData =
        inputFiles
        |> Seq.choose(fun (f: string) ->
            let m = Regex.Match(Path.GetFileName(f), "^[vV][iI][dD]_(\\d{4})(\\d{2})(\\d{2})_(\\d{2})(\\d{2})(\\d{2})_00_(\\d+)\\.[iI][nN][sS][vV]$")
            if m.Success then
                let date = Int32.Parse(m.Groups.[1].Value), Int32.Parse(m.Groups.[2].Value), Int32.Parse(m.Groups.[3].Value), Int32.Parse(m.Groups.[4].Value), Int32.Parse(m.Groups.[5].Value), Int32.Parse(m.Groups.[6].Value)
                Some(f, date)
            else
                printfn "  Not using file %s, these don't contain any data" f
                None)
        |> Seq.sortBy(fun (_, d) -> d)
        |> Seq.map(fun (f, _) -> f)
        |> Seq.toList
        |> enumerateDataInOrder
    // Write the data
    do
        use outs = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.Write)
        use writer = new StreamWriter(outs, Encoding.ASCII)
        gpsData |> GPXExport.writeGpx writer.WriteLine
        writer.Flush()
        outs.Flush()
    printfn "Done."

[<EntryPoint>]
let main args =
    if args |> Array.contains "-h" || args |> Array.contains "--help" then
        let hf = new HelpFormatter()
        hf.PrintHelp(80, "Insta360GPXExtract", "Extracts GPS data from one or more insv files and writes one or more GPX files", opts, "", true)
        0
    else
        let cl = (new DefaultParser()).Parse(opts, args)
        let inputFiles = 
            cl.GetOptionValues "i"
            |> Seq.collect(enumerateInsvFiles (cl.HasOption "r"))
            |> Seq.map Path.GetFullPath
            |> Seq.distinct
            |> Seq.toList
        if inputFiles.Length > 1 && not <| cl.HasOption "s" then
            // Output multiple
            let outDir = cl.GetOptionValue "o"
            if File.Exists outDir then invalidArg "-o" (sprintf "%s is a file" outDir)
            if not <| Directory.Exists outDir then Directory.CreateDirectory outDir |> ignore
            inputFiles |> List.iter(writeGpxFileToDir outDir)
            0
        else
            // Extract single file
            let outPath = cl.GetOptionValue "o"
            if inputFiles.Length = 1 then
                if Directory.Exists outPath then writeGpxFileToDir outPath inputFiles.Head else writeGpxFileToFile outPath inputFiles.Head
            else
                writeConcatGpxFile outPath inputFiles
            0
