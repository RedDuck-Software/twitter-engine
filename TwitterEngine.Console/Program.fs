// Learn more about F# at http://fsharp.org

open System
open System.IO
open System.Reflection
open Akkling
open TwitterEngine.Shared
open Serilog

let setupLogging () =
    let logDirectory = Path.Combine(Path.GetTempPath(), Assembly.GetCallingAssembly().FullName)
    if not <| System.IO.Directory.Exists(logDirectory)
        then Directory.CreateDirectory(logDirectory) |> ignore

    let sessionId = DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds().ToString()
    let logFile = sprintf "logs-%s.json" sessionId
    let filePath = Path.Combine(logDirectory, logFile)
    File.Create(filePath).Dispose()

    let log = (((LoggerConfiguration()).MinimumLevel.Debug()).WriteTo).File(filePath).CreateLogger()
    Serilog.Log.Logger <- log

    sessionId

[<EntryPoint>]
let main argv =
    use system = System.create "server" <| Configuration.load()

    let sessionId = setupLogging ()   
    
    // todo rename to supervisOr
    let superviserActorRef = spawn system "supervisor" <| props(Actors.superviserActor)
            
    Console.ReadLine () |> ignore
    0 // return an integer exit code