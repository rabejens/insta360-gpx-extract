namespace Insta360GPSTools

open System

type GPSEntry = {
    time: DateTime
    latitude: float
    longitude: float
    speed: float
    elevation: float
    heading: float
}

module GPSEntry =
    
    [<CompiledName("CreateInvalid")>]
    let invalid time = {
        time = time
        latitude = Double.NaN
        longitude = Double.NaN
        speed = Double.NaN
        elevation = Double.NaN
        heading = Double.NaN
    }

    [<CompiledName("IsValid")>]
    let isValid entry =
        [ entry.latitude; entry.longitude; entry.speed; entry.elevation; entry.heading ]
        |> List.exists(Double.IsNaN)
        |> not
