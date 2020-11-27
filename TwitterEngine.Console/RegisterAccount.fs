namespace TwitterEngine

open Akkling
open TwitterEngine.Types
open System.Security.Cryptography;
open System.Text

module RegisterAccount =
    let generateSalt length =
        let rngCryptoServiceProvider = new RNGCryptoServiceProvider()
        let array = Array.zeroCreate<byte> length 
        rngCryptoServiceProvider.GetBytes array
        array

    let getHash (salt:byte[]) (password:string) = 
        let passwordBytes = Encoding.UTF8.GetBytes password
        let hash = seq { passwordBytes; salt} |> Array.concat |> SHA256.Create().ComputeHash
        hash

    let signup credentials =
        let salt = generateSalt Constants.saltLength
        let hash = getHash salt credentials.password

        let account = { username = credentials.username; salt = salt; passwordHash = hash; }
        account

    let extractHashes (str:string) = str.Split "#" |> Array.map Hashtag
    let extractUserNames (str:string) = str.Split "@" |> Array.map Mention

    // todo jwt token auth etc etc?
    let login account password = 
        { account with isLoggedIn = account.credentials.passwordHash = getHash account.credentials.salt password } // todo here incorrect comparison

    let userActor account (mailBox:Actor<UserRequest>) =
        let rec impl (account, sentTweets) = actor {
            let! data = mailBox.Receive()
            
            let account, sentTweets =
                match data with
                | Login pwd -> 
                    printfn "logged in %s" account.credentials.username
                    login account pwd, sentTweets
                | SendTweet data ->
                    let tweet = { data = data; author = account; sender = account }
                    mailBox.Parent() <! Tweet(tweet)
                    account, tweet::sentTweets
                | ForwardTweet tweet ->
                    let tweet = { tweet with sender = account }
                    if not <| List.contains tweet sentTweets 
                        then
                            mailBox.Parent() <! Tweet(tweet)
                            account, tweet::sentTweets
                        else
                            account, sentTweets
                | ReceivedTweet (tweet, subscription) ->
                    let str = sprintf "Account %s received tweet %s from %s based on subscription %A" account.credentials.username tweet.data tweet.author.credentials.username subscription
                    System.Console.WriteLine str // printfn does not always add a new line ? O_o
                    mailBox.Self <! ForwardTweet(tweet)
                    account, sentTweets

            return! impl (account, sentTweets)
        }
        impl ({ credentials = account; isLoggedIn = false; }, [])

    let subscriptionToActorName subscription = 
        match subscription with 
        | Hashtag a -> sprintf "tag-%s" a
        | Mention a -> sprintf "mention-%s" a
        | Sender a -> sprintf "sender-%s" a

    let subscriptionActor subscription (mailBox:Actor<SubscriptionActorRequest>) = 
        let rec impl (subscribers, tweets) = actor {
            let! data = mailBox.Receive()

            let (subscribers, tweets) = 
                match data with 
                | NewSubscription userRef ->
                    if not <| List.contains userRef subscribers 
                        then userRef :: subscribers, tweets
                        else subscribers, tweets
                | SubscriptionActorRequest.Tweet tweet -> 
                    let userRequest = ReceivedTweet (tweet, subscription)
                    List.iter (fun i -> i <! userRequest) subscribers
                    (subscribers, tweet::tweets)
                | SubscriptionActorRequest.LoadHistoricalTweets userRef ->                     
                    for tweet in tweets do
                        userRef <! ReceivedTweet(tweet, subscription)
                    
                    (subscribers, tweets)
                                        
            return! impl (subscribers, tweets)
        }
        impl ([], [])

    let superviserActor (mailBox:Actor<SuperviserRequest>) = 
        let rec impl () = actor {
            let! data = mailBox.Receive()

            match data with
            | Signup creds ->
                match mailBox.UntypedContext.Child(creds.username) with
                | :? Akka.Actor.Nobody ->
                    let account = signup creds
                    userActor account |> props |> spawn mailBox account.username |> ignore
                    printfn "signed up %s" account.username
                | _ -> 
                    printfn "Returned somebody, not signing up"
            | UserRequest (request, username) ->
                    let childActorRef = typed <| mailBox.UntypedContext.Child(username)
                    childActorRef <! request // what if nobody?
                    ()
            | Subscribe (subscription, username) | LoadHistoricalTweets (subscription, username) -> 
                let userActorRef = typed <| mailBox.UntypedContext.Child(username)
                let actorName = subscriptionToActorName subscription
                let subscriptionActorRef = mailBox.UntypedContext.Child(actorName)

                // create subscription actor
                let subscriptionActorRef = 
                    match subscriptionActorRef with
                    | :? Akka.Actor.Nobody -> subscriptionActor subscription |> props |> spawn mailBox actorName
                    | _ -> typed subscriptionActorRef

                subscriptionActorRef <! NewSubscription(userActorRef)

                match data with 
                | LoadHistoricalTweets _ -> subscriptionActorRef <! SubscriptionActorRequest.LoadHistoricalTweets(userActorRef)
                | _ -> ()
                ()
            | Tweet tweet ->
                let subscriptions = seq {
                    yield! extractHashes tweet.data;
                    yield! extractUserNames tweet.data;
                    yield Sender tweet.sender.credentials.username
                }

                for subscription in subscriptions do
                    let actorName = subscriptionToActorName subscription
                    let actorRef = mailBox.UntypedContext.Child(actorName) |> typed
                    actorRef <! SubscriptionActorRequest.Tweet(tweet)
            return! impl ()
        }
        impl ()