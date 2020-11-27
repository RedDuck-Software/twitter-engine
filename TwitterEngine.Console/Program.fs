// Learn more about F# at http://fsharp.org

open System
open System.IO
open Akkling
open TwitterEngine.Types
open TwitterEngine
open System.Reflection
open Serilog

let setupLogging () =
    let logDirectory = Path.Combine(Path.GetTempPath(), Assembly.GetCallingAssembly().FullName)
    if not <| System.IO.Directory.Exists(logDirectory)
        then Directory.CreateDirectory(logDirectory) |> ignore

    let sessionId = DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds().ToString()
    let logFile = sprintf "logs-%s.json" sessionId
    let filePath = Path.Combine(logDirectory, logFile)
    File.Create(filePath).Dispose() |> ignore

    let log = (((LoggerConfiguration()).MinimumLevel.Debug()).WriteTo).File(filePath).CreateLogger()
    Serilog.Log.Logger <- log

    sessionId


[<EntryPoint>]
let main argv =
    let sessionId = setupLogging ()

    use system = System.create "my-system" <| Configuration.defaultConfig()
    let identityActorRef = spawnAnonymous system <| props(RegisterAccount.identityActor)

    identityActorRef <! AccountRequest(Login({ username = "MarkV"; password = "123456b" }))
    identityActorRef <! Signup({ username = "MarkV"; password = "123456b" })
    identityActorRef <! AccountRequest(Login({ username = "MarkV"; password = "123456b" }))
    identityActorRef <! Signup({ username = "MarkV"; password = "123456b" })

    Console.ReadLine ()
    0 // return an integer exit code