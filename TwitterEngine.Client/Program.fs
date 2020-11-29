// Learn more about F# at http://fsharp.org

open System
open Akkling
open Akka.Actor
open TwitterEngine.Shared.Types

let clientActor username password remoteSupervisor (mailBox:Actor<ClientUserActorMessage>) =
    remoteSupervisor <! Signup({username = username; password = password})
    printfn "Told'em to sign-up"

    let rec impl (userState:ClientUserState) = actor {
        let! msg = mailBox.Receive()

        let userState = 
            match msg with 
            | UserRef ref -> 
                printfn "Received ref"
                { userState with serverUserRef = Some(ref) }
            | OperationResult res -> 
                match res with 
                | Success -> Console.WriteLine($"{username}: Successful operation")
                | Error a -> Console.WriteLine($"{username}: Error operation, msg: {a}")
                userState
            | ReceivedTweet (tweet, subscription) ->
                let str = sprintf "Account %s received tweet #%O \"%s\" from %s based on subscription %A" username tweet.id tweet.data tweet.author.credentials.username subscription
                System.Console.WriteLine str // printfn does not always add a new line ? O_o
                { userState with receivedTweets = tweet::userState.receivedTweets }
            | UserRequest request -> 
                match userState.serverUserRef with 
                | Some ref -> ref <! request
                | None _ -> printfn "None ref !"
                userState

        return! impl userState
    }

    impl { serverUserRef = None; receivedTweets = [] }

[<EntryPoint>]
let main argv =

    let client = System.create "client" <| Configuration.parse """
    akka {
        actor.provider = remote
        remote.dot-netty.tcp {
            hostname = localhost
            port = 0
        }
    }
"""    

    let supervisor = typed <| client.ActorSelection("akka.tcp://server@localhost:4500/user/supervisor").ResolveOne(TimeSpan.FromSeconds(2.0)).GetAwaiter().GetResult()
    
    let user1 = spawnAnonymous client <| props (clientActor "MarkV" "123456" supervisor)
    let user2 = spawnAnonymous client <| props (clientActor "Andrewha" "123456" supervisor)
    let user3 = spawnAnonymous client <| props (clientActor "Dimitry" "123456" supervisor)

    Console.ReadLine() |> ignore

    user1 <! UserRequest(Login("123456"))
    user2 <! UserRequest(Login("123456"))
    user3 <! UserRequest(Login("123456"))

    user1 <! UserRequest(UserRequest.Subscription((Sender("Andrewha"),SubscriptionType.RealtimeTweets)))
    user3 <! UserRequest(UserRequest.Subscription((Sender("MarkV"), SubscriptionType.RealtimeTweets)))    

    Console.ReadLine() |> ignore

    user2 <! UserRequest(SendTweet("Hahahaha. Uhahahhaa. #laughing"))
   
    Console.ReadLine () |> ignore
 
    user1 <! UserRequest(UserRequest.Subscription((Hashtag("laughing"), SubscriptionType.LoadHistoricalTweets)))

    let id = Console.ReadLine() |> Guid

    user1 <! UserRequest(UserRequest.ForwardTweet(id))

    Console.ReadLine() |> ignore
    printfn "Hello World from F#!"
    Console.ReadLine () |> ignore
    0 // return an integer exit code
