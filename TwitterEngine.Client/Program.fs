// Learn more about F# at http://fsharp.org

open System
open Akkling
open Akka.Actor
open TwitterEngine.Shared.Types

let rnd = Random()

type ClientUserInfo = {
    mutable subscribersNum: int;
    receivedTweetIDs: Guid System.Collections.Generic.List;
    mutable lastActivity: DateTime Option ;
    mutable activitiesCount: int;
    mutable finishedActivities: int;
}

let addActivity clientInfo =
    clientInfo.lastActivity <- Some DateTime.UtcNow
    clientInfo.activitiesCount <- clientInfo.activitiesCount + 1

let finishActivity info = info.finishedActivities <- info.finishedActivities + 1

let clientActor logReceivedMessages (clientInfo:ClientUserInfo) username password remoteSupervisor (mailBox:Actor<ClientUserActorMessage>) =
    let log (txt:string) = if logReceivedMessages then Console.WriteLine(txt)

    remoteSupervisor <! Signup({username = username; password = password})
    addActivity clientInfo
    log "Told'em to sign-up"


    let rec impl (userState:ClientUserState) = actor {
        let! msg = mailBox.Receive()

        let userState = 
            match msg with
            | UserRef ref ->
                finishActivity clientInfo
                { userState with serverUserRef = Some(ref) }
            | OperationResult res ->
                match res with 
                | Success -> log $"{username}: Successful operation"
                | Error a -> log $"{username}: Error operation, msg: {a}"
                finishActivity clientInfo
                userState
            | ReceivedTweet (tweet, subscription) ->
                let str = sprintf "Account %s received tweet #%O \"%s\" from %s based on subscription %A" username tweet.id tweet.data tweet.author.credentials.username subscription
                log str
                clientInfo.receivedTweetIDs.Add(tweet.id)
                { userState with receivedTweets = tweet::userState.receivedTweets }
            | UserRequest request -> 
                match userState.serverUserRef with 
                | Some ref -> 
                    ref <! request
                    addActivity clientInfo
                | None _ -> printfn "None ref !"
                userState

        return! impl userState
    }

    impl { serverUserRef = None; receivedTweets = [] }

let elementAtOrDefault (array:'a[]) indx def = if indx < array.Length then array.[indx] else def

[<Literal>]
let userNameBeginning = "User"

[<EntryPoint>]
let main argv =
    let elementAtOrDefault = elementAtOrDefault argv
    let users = elementAtOrDefault 0 "100000" |> int
    let connectionTimeMinutes = elementAtOrDefault 1 "2.0" |> float |> TimeSpan.FromMinutes
    let hashtagsCount = elementAtOrDefault 2 "15" |> int
    let mentionsCount = elementAtOrDefault 3 "15" |> int
    let subscribersCountReach = elementAtOrDefault 4 "50" |> int
    let actionPause = elementAtOrDefault 5 "100" |> int
    let logReceivedMessages = elementAtOrDefault 6 "false" |> bool.Parse

    let hashtags = Array.init hashtagsCount (fun i -> sprintf "Hashtag%i" i)
    
    let getRndTag () = 
        let hashTagIndx = rnd.Next(0, hashtagsCount)
        hashtags.[hashTagIndx]

    let getRndUser () =
        let userIndx = rnd.Next(0, users)
        sprintf "%s%i" userNameBeginning userIndx

    let getRandomTweetText () = 
        let mentionsCount = rnd.Next(1, mentionsCount)
        let hashtagsCount = rnd.Next(1, hashtagsCount / 2)
        let mentions = Array.init mentionsCount (fun _ -> "@" + getRndUser())
        let hashTags =  Array.init hashtagsCount (fun _ -> "#" + getRndTag())

        let text = sprintf "Hashtags: %s | Mentions: %s" (String.Join(" ", hashTags)) (String.Join(" ", mentions))
        text

    let initWithBase baseNum count key = Array.init count (fun i -> (baseNum + i, key));

    let getRandomMsg users clientUserInfo = 
        let num = rnd.Next(0, 10)
        let res = 
            let probs = 
                if clientUserInfo.subscribersNum >= subscribersCountReach
                    then [5;4;1]
                    else [4;4;2]
            let dict = Array.init 3 (fun i -> 
                let baseValue = if i = 0 then 0 else probs.GetSlice(Some 0, Some (i - 1)) |> List.sum
                let count = probs.[i]
                initWithBase baseValue count i) |> Array.concat |> dict
            dict.[num]

        match res with
        | 0 -> // send tweet
            let text = getRandomTweetText ()
            SendTweet(text)
        | 1 -> // forward tweet
            if clientUserInfo.receivedTweetIDs.Count = 0
                then
                    let text = getRandomTweetText ()
                    SendTweet(text)
                else
                    let rndTweet = clientUserInfo.receivedTweetIDs.Item(rnd.Next(0, clientUserInfo.receivedTweetIDs.Count))
                    ForwardTweet(rndTweet)
        | 2 -> // subscription
            let subscriptionNum = rnd.Next(0, 3) // tag, mention or fromUser
            let subscription = 
                match subscriptionNum with 
                | 0 -> Hashtag(getRndTag())
                | 1 -> Mention(getRndUser())
                | 2 -> Sender(getRndUser())
                        
            let subscriptionType = RealtimeTweets

            UserRequest.Subscription(subscription, subscriptionType)

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
    
    let users = Array.init users (fun i -> 
        let guidList = new System.Collections.Generic.List<Guid>()
        let data = {subscribersNum = 0; receivedTweetIDs = guidList; lastActivity = None; activitiesCount = 0; finishedActivities = 0; }
        let actor = spawnAnonymous client <| props (clientActor logReceivedMessages data (sprintf "%s%i" userNameBeginning i) "123456" supervisor)
        (actor, data))

    let watch = System.Diagnostics.Stopwatch.StartNew()

    printfn "---CREATING ACCOUNTS---"

    ///////// waiting
    while not <| Array.forall (fun (_, i) -> i.activitiesCount = i.finishedActivities) users do
        System.Threading.Thread.Sleep(actionPause)
    ///////// waiting

    printfn "---LOGGING IN---"

    for (actor, _) in users do
        actor <! UserRequest(Login("123456"))

    ///////// waiting
    while not <| Array.forall (fun (_, i) -> i.activitiesCount = i.finishedActivities) users do
        System.Threading.Thread.Sleep(actionPause)
    ///////// waiting


    let startTime = DateTime.UtcNow
    while (DateTime.UtcNow - startTime) < connectionTimeMinutes do
        let userIndx = rnd.Next(0, users.Length)
        let (rndActor, rndUserInfo) = users.[userIndx]
        let rndAction = getRandomMsg users rndUserInfo
        match rndAction with 
        | UserRequest.Subscription(Sender name, _) -> 
            let userIndx = name.Remove(0, userNameBeginning.Length) |> int
            let (actor, user) = users.[userIndx]
            user.subscribersNum <- user.subscribersNum + 1
            if user.subscribersNum = subscribersCountReach then
                if logReceivedMessages then Console.WriteLine "reached subscribersCountReach"
        | _ -> ()

        if logReceivedMessages then Console.WriteLine(sprintf "Performing random action on the user: %i; Action: %A" userIndx rndAction)
        rndActor <! UserRequest(rndAction)
        //System.Threading.Thread.Sleep(actionPause)

    ///////// waiting
    while not <| Array.forall (fun (_, i) -> i.activitiesCount = i.finishedActivities) users do
        System.Threading.Thread.Sleep(actionPause)
    ///////// waiting

    watch.Stop()

    Console.WriteLine("---Finished processing---")

    let getMsg = sprintf "Total number of requests: %i, Time spent: %s, average processing time: %f ms"
    let totalRequests = Array.sumBy (fun (_, i) -> i.activitiesCount) users
    let totalTime = watch.Elapsed //- (TimeSpan.FromMilliseconds(float <| actionPause * totalRequests))
    let avg = totalTime.TotalMilliseconds / float totalRequests

    Console.WriteLine(getMsg totalRequests (totalTime.ToString()) avg)

    Console.ReadLine() |> ignore

    // get historical in console at the end

    Console.ReadLine() |> ignore
    printfn "Hello World from F#!"
    Console.ReadLine () |> ignore
    0 // return an integer exit code
