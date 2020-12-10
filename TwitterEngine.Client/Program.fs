// Learn more about F# at http://fsharp.org

open System
open TwitterEngine.Shared.Types
open WebsocketClientLite.PCL
open System.Security.Authentication

[<Literal>]
let text =
    "\r\n0. Print this text. Example: 0" + "\r\n" +
    "1. SendTweet. Format: 1-{text}" + "\r\n" +
    "2. ForwardTweet. Format: 2-{guid}" + "\r\n" +
    "3. Subscribe to real time events. Format: 3-{type}-{data}. Type can be hashtag, mention, sender. Example: 3-hashtag-university" + "\r\n" +
    "4. Query existing events. Foramt is the same as in p.3"

let promptString str =
    printfn "%s" str
    Console.ReadLine()

let parseRequest (text:string) = 
    let num = text.[0] |> string |> int

    let splitCount = 
        match num with 
        | 0 -> 1
        | 1 | 2 -> 2
        | 3 | 4 -> 3

    let splitted = text.Split('-', splitCount)
    printfn "Splitted: %A" splitted
    let num = splitted.[0] |> int
    let data = splitted.[1]

    match num with 
    | 1 -> 
        SendTweet(data)
    | 2 -> 
        let guid = Guid.Parse(data)
        ForwardTweet(guid)
    | 3|4 ->
        let reqType = splitted.[1]
        let data = splitted.[2]
        
        let subscription = 
            match reqType with 
            | "hashtag" -> Hashtag(data)
            | "mention" -> Mention(data)
            | "sender" -> Sender(data)

        let subscriptionType = 
            match num with 
            | 3 -> SubscriptionType.RealtimeTweets
            | 4 -> SubscriptionType.LoadHistoricalTweets

        UserRequest.Subscription(subscription, subscriptionType)
    | _ -> 
        printfn "bogus number"
        raise <| invalidOp("bogus number")

let runWebSocketClient url = async {
    let websocketClient = new MessageWebSocketRx()

    websocketClient.IgnoreServerCertificateErrors <- true
    websocketClient.Headers <-  [("Pragma", "no-cache");("Cache-Control", "no-cache")] |> dict
    websocketClient.TlsProtocolType <- SslProtocols.Tls12

    do! websocketClient.ConnectAsync(url) |> Async.AwaitTask

    let disposableMessageReceiver = 
        fun msg -> 
            let value = WebSharper.Json.Deserialize<ServerToClientResponse> msg
            printfn "Server message: %A" value
        |> websocketClient.MessageReceiverObservable.Subscribe

    let websocketLoggerSubscriber = printfn "Status changed: %O" |> websocketClient.ConnectionStatusObservable.Subscribe

    // sending messages logic
    let username = promptString "Please, sign-up. What's your username?"
    let password = promptString "Specify a password:"
    let signupRequest = Signup({Credentials.username=username; Credentials.password=password})

    let sendRequest req =
        let value = WebSharper.Json.Serialize <| req
        websocketClient.SendTextAsync value |> Async.AwaitTask

    do! sendRequest signupRequest

    while true do
        let prompted = promptString <| sprintf "Please specify action. Or 0 to print actions"
        if prompted.StartsWith("0") 
            then 
                printfn "%s" text
            else
                    try 
                        let parsed = parseRequest prompted
                        let c2sRequest = ClientToServerRequest.UserRequest(parsed, username)
                        do! sendRequest c2sRequest
                    with 
                    | ex -> 
                        printfn "Exception. Message: %s; Type:%s" ex.Message (ex.GetType().Name)

    return websocketClient
}

[<EntryPoint>]
let main argv =
    printfn "Ready"
    let a = runWebSocketClient <| Uri "ws://localhost:7703/ws" |> Async.RunSynchronously

    Console.ReadLine () |> ignore
    0 // return an integer exit code