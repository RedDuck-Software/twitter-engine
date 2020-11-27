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
    File.Create(filePath).Dispose()

    let log = (((LoggerConfiguration()).MinimumLevel.Debug()).WriteTo).File(filePath).CreateLogger()
    Serilog.Log.Logger <- log

    sessionId

let signUpAndSubscribeUser tweetSenderName userName superviserActorRef =
    superviserActorRef <! Signup({username = userName; password = "123456b"})
    superviserActorRef <! UserRequest(Login("123456b"), userName)
    superviserActorRef <! Subscribe(Sender(tweetSenderName), userName)

[<EntryPoint>]
let main argv =
    use system = System.create "my-system" <| Configuration.load()
    let sessionId = setupLogging ()

    let sender = "D.Trump"
    let recepient1 = "Anton"
    let recepient2 = "Vitaly"
    let recepient3 = "George"
    let recepient4 = "John"

    // todo rename to supervisOr
    let superviserActorRef = spawnAnonymous system <| props(RegisterAccount.superviserActor)

    superviserActorRef <! Signup({ username = sender; password = "123456b" })
    superviserActorRef <! UserRequest(Login("123456b"), sender)

    signUpAndSubscribeUser sender recepient1 superviserActorRef
    signUpAndSubscribeUser recepient1 recepient2 superviserActorRef
    signUpAndSubscribeUser recepient2 recepient3 superviserActorRef
    signUpAndSubscribeUser recepient3 recepient4 superviserActorRef
   
    System.Threading.Thread.Sleep(2500) // allow the above to be completed

    superviserActorRef <! UserRequest(SendTweet("Akka-Akka. Akka47. Oh shit man, goddamn!"), sender)

    System.Threading.Thread.Sleep(2000)

    superviserActorRef <! LoadHistoricalTweets(Sender(recepient3), recepient2)

    Console.ReadLine () |> ignore
    0 // return an integer exit code