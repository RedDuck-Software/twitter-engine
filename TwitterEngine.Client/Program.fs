// Learn more about F# at http://fsharp.org

open System
open TwitterEngine.Shared.Types
open WebSharper.AspNetCore.WebSocket
open WebSharper.AspNetCore.WebSocket.Client

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

[<EntryPoint>]
let main argv =
    let endpoint = createEndpoint "ws://localhost:7703"

    WebSocketClient endpoint

    printfn "Ready"

    Console.ReadLine () |> ignore
    0 // return an integer exit code
