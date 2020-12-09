namespace TwitterEngine.Shared

open Akkling
open TwitterEngine.Shared.Types
open System
open WebSharper.AspNetCore.WebSocket.Server

module Actors =
    let userActor sendToClient account (mailBox:Actor<UserRequest>) =
        let rec impl (account, knownTweets) = actor {
            let! data = mailBox.Receive()
           
            let isAccessAllowed = true

            if not isAccessAllowed 
                then 
                    return! impl (account, knownTweets)
                else
                    return! impl <|
                        match data with
                        | Login pwd -> 
                            printfn "logged in %s" account.credentials.username
                            let account = Helpers.login account pwd
                            
                            let response = if account.isLoggedIn then Success else OperationStatusResponse.Error("invalid password")                            
                            sendToClient <| OperationResult(response)
                            
                            account, knownTweets
                        | SendTweet data ->
                            let tweet = { id = (Guid.NewGuid()); data = data; author = account; sender = account; }
                            mailBox.Parent() <! SuperviserRequest.Tweet(tweet)
                            sendToClient <| OperationResult(Success)
                            account, tweet::knownTweets
                        | ForwardTweet id ->
                            let tweet = List.tryFind (fun i -> i.id = id) knownTweets
                            match tweet with
                            | Some tweet -> 
                                let tweet = { tweet with sender = account }
                                if not <| List.contains tweet knownTweets
                                    then
                                        mailBox.Parent() <! SuperviserRequest.Tweet(tweet)
                                        sendToClient <| OperationResult(Success)
                                        account, tweet::knownTweets
                                    else
                                        sendToClient <| OperationResult(OperationStatusResponse.Error("Tweet has already been forwarded"))
                                        account, knownTweets
                            | None ->
                                sendToClient <| OperationResult(OperationStatusResponse.Error(sprintf "Unknown tweet id: %O" id))
                                account, knownTweets
                        | UserRequest.Subscription subscription ->
                            mailBox.Parent() <! SuperviserRequest.Subscription(subscription)
                            sendToClient <| OperationResult(Success)
                            account, knownTweets
                        | UserRequest.ReceivedTweet data ->
                            sendToClient <| ReceivedTweet(data)
                            let (tweet, _) = data
                            account, tweet::knownTweets
        }
        impl ({ credentials = account; isLoggedIn = false; }, [])
    let subscriptionActor subscription (mailBox:Actor<SubscriptionActorRequest>) = 
        let rec impl (subscribers, tweets) = actor {
            let! data = mailBox.Receive()
            let (sender:IActorRef<UserRequest>) = mailBox.Sender()

            let (subscribers, tweets) = 
                match data with 
                | Subscription subscribeType ->
                    match subscribeType with 
                    | RealtimeTweets -> 
                        printfn "subscribed2"
                        if not <| List.contains sender subscribers 
                            then sender :: subscribers, tweets
                            else subscribers, tweets
                    | LoadHistoricalTweets -> 
                        printfn "loading hist tweets"
                        for tweet in tweets do
                            sender <! UserRequest.ReceivedTweet(tweet, subscription)
                            
                        (subscribers, tweets)
                | SubscriptionActorRequest.Tweet tweet ->
                    let userRequest = UserRequest.ReceivedTweet (tweet, subscription)
                    printfn "sending receivedTweet to users: %A" subscribers
                    List.iter (fun i -> i <! userRequest) subscribers
                    (subscribers, tweet::tweets)

            return! impl (subscribers, tweets)
        }
        impl ([], [])

    let superviserActor (mailBox:Actor<SuperviserRequest>) = 
        let getSubscriptionActor subscription =
            let actorName = Helpers.subscriptionToActorName subscription
            let subscriptionActorRef = mailBox.UntypedContext.Child(actorName)
            match subscriptionActorRef with
            | :? Akka.Actor.Nobody -> subscriptionActor subscription |> props |> spawn mailBox actorName
            | _ -> typed subscriptionActorRef
    
        let rec impl () = actor {
            let! data = mailBox.Receive()

            match data with
            | SuperviserRequest.Signup (creds, sendToUser) ->
                let userActor = 
                    match mailBox.UntypedContext.Child(creds.username) with
                    | :? Akka.Actor.Nobody ->
                        let account = Helpers.signup creds
                        let userActor = userActor sendToUser account |> props |> spawn mailBox account.username
                        printfn "signed up %s" account.username
                        userActor
                    | a -> 
                        printfn "Already signed up: %s" creds.username
                        typed a
                ()
            | SuperviserRequest.UserRequest (ur, from) -> 
                let userActor = typed <| mailBox.UntypedContext.Child(from)
                userActor <! ur
            | SuperviserRequest.Subscription (sub, stype) -> 
                (getSubscriptionActor sub) <<! Subscription(stype)
                printfn "forwarding stype"
                ()
            | SuperviserRequest.Tweet tweet ->
                let subscriptions = seq {
                    yield! Helpers.extractHashes tweet.data;
                    yield! Helpers.extractUserNames tweet.data;
                    yield Sender tweet.sender.credentials.username
                }

                for subscription in subscriptions do
                    (getSubscriptionActor subscription) <! SubscriptionActorRequest.Tweet(tweet)

            return! impl ()
        }
        impl ()

    let clientActor websocketUser logReceivedMessages (clientInfo:ClientUserInfo) username password remoteSupervisor (mailBox:Actor<ServerToClientResponse>) =
        let log (txt:string) = if logReceivedMessages then Console.WriteLine(txt)

        let addActivity clientInfo =
            clientInfo.lastActivity <- Some DateTime.UtcNow
            clientInfo.activitiesCount <- clientInfo.activitiesCount + 1
        
        let finishActivity info = info.finishedActivities <- info.finishedActivities + 1

        remoteSupervisor <! Signup({username = username; password = password})
        addActivity clientInfo
        log "Told'em to sign-up"

        let rec impl (userState:ClientUserState) = actor {
            let! msg = mailBox.Receive()

            let userState = 
                match msg with
                | OperationResult res ->
                    match res with
                    | Success -> log $"{username}: Successful operation"
                    | OperationStatusResponse.Error a -> log $"{username}: Error operation, msg: {a}"
                    finishActivity clientInfo
                    userState
                | ReceivedTweet (tweet, subscription) ->
                    let str = sprintf "Account %s received tweet #%O \"%s\" from %s based on subscription %A" username tweet.id tweet.data tweet.author.credentials.username subscription
                    log str
                    clientInfo.receivedTweetIDs.Add(tweet.id)
                    { userState with receivedTweets = tweet::userState.receivedTweets }

            return! impl userState
        }

        impl { serverUserRef = None; receivedTweets = [] }