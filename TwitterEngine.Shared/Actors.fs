namespace TwitterEngine.Shared

open Akkling
open TwitterEngine.Shared.Types
open System

module Actors =
    let userActor (client:IActorRef<ClientUserActorMessage>) account (mailBox:Actor<UserRequest>) =
        let rec impl (account, knownTweets) = actor {
            let! data = mailBox.Receive()
            let account, sentTweets =
                match data with
                | Login pwd -> 
                    printfn "logged in %s" account.credentials.username
                    client <! OperationResult(Success)
                    Helpers.login account pwd, knownTweets // fix login
                | SendTweet data ->
                    let tweet = { id = (Guid.NewGuid()); data = data; author = account; sender = account; }
                    mailBox.Parent() <! SuperviserRequest.Tweet(tweet)
                    client <! OperationResult(Success)
                    account, tweet::knownTweets
                | ForwardTweet id ->
                    let tweet = List.tryFind (fun i -> i.id = id) knownTweets
                    match tweet with
                    | Some tweet -> 
                        let tweet = { tweet with sender = account }
                        if not <| List.contains tweet knownTweets
                            then
                                mailBox.Parent() <! SuperviserRequest.Tweet(tweet)
                                client <! OperationResult(Success)
                                account, tweet::knownTweets
                            else
                                client <! OperationResult(Error("Tweet has already been forwarded"))
                                account, knownTweets
                    | None ->
                        client <! OperationResult(Error(sprintf "Unknown tweet id: %O" id))
                        account, knownTweets
                | UserRequest.Subscription subscription ->
                    printfn "subscribing"
                    mailBox.Parent() <! SuperviserRequest.Subscription(subscription)
                    account, knownTweets
                | UserRequest.ReceivedTweet data ->
                    client <! ReceivedTweet(data)
                    let (tweet, _) = data
                    account, tweet::knownTweets

            return! impl (account, sentTweets)
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
            | Signup creds ->
                let sender = mailBox.Sender()
                let userActor = 
                    match mailBox.UntypedContext.Child(creds.username) with
                    | :? Akka.Actor.Nobody ->
                        let account = Helpers.signup creds
                        let userActor = userActor sender account |> props |> spawn mailBox account.username
                        printfn "signed up %s" account.username
                        userActor
                    | _ -> raise <| invalidOp("already signed up") // here if it's another clientUserRef might not work - but we dont need to so its fine
                sender <! UserRef(userActor)
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