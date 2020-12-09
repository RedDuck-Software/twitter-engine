// Learn more about F# at http://fsharp.org

open System
open TwitterEngine.Shared.Types
open WebSharper.AspNetCore.WebSocket
open WebSharper.AspNetCore.WebSocket.Client
open WebSharper

let WebSocketClient (endpoint : WebSocketEndpoint<S2CMessage, C2SMessage>) =
    async {
        let! server =
            Connect endpoint <| fun server -> async {
                return fun msg ->
                    match msg with
                    | Message data ->
                        match data with
                        | OperationResult x -> printfn "Op result: %A" x
                        | ReceivedTweet (tweet, subsc) -> printfn "Received tweet: %s" tweet.data
                    | Close ->
                        printfn "WebSocket connection closed."
                    | Open ->
                        printfn "WebSocket connection open."
                    | Error ->
                        printfn "WebSocket connection error!"
            }

        while true do
            do! Async.Sleep 1000
            server.Post (ClientToServerRequest.TestRequest("HELLO"))
            do! Async.Sleep 1000
            server.Post (ClientToServerRequest.TestRequest("123"))
    }
    |> Async.Start
    
let createEndpoint (url: string) : WebSharper.AspNetCore.WebSocket.WebSocketEndpoint<S2CMessage, C2SMessage> = 
    WebSocketEndpoint.Create(url, "/ws", JsonEncoding.Readable)

[<Literal>]
let text =
    "\r\n0. Print this text. Example: 0" + "\r\n" +
    "1. SendTweet. Format: 1-{text}" + "\r\n" +
    "2. ForwardTweet. Format: 2-{guid}" + "\r\n" + 
    "3. Subscribe to real time events. Format: 3-{type}-{data}. Type can be hashtag, mention, sender. Example: 3-hashtag-university" + "\r\n" +
    "4. Query existing events. Foramt is the same as in p.3"

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

[<EntryPoint>]
let main argv =
    let endpoint = createEndpoint "ws://localhost:7703"
    //WebSocketClient endpoint

    let sendRequest req = 
        let value = WebSharper.Json.Serialize <| req
        printfn "Please push the following json: ---> %s" value

    let promptString str =
        printfn "%s" str
        Console.ReadLine()

    let username = promptString "Please, sign-up. What's your username?"
    let password = promptString "Specify a password:"

    let signupRequest = Signup({Credentials.username=username; Credentials.password=password})

    sendRequest signupRequest

    while true do
        let prompted = promptString <| sprintf "Please specify action. %s" text
        
        if prompted.StartsWith("0") 
            then 
                printfn "%s" text
            else
                let parsed = parseRequest prompted
                let c2sRequest = ClientToServerRequest.UserRequest(parsed, username)
                sendRequest c2sRequest

    printfn "Ready"

    Console.ReadLine () |> ignore
    0 // return an integer exit code
