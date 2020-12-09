namespace TwitterEngine.Shared.Types

open Akkling
open System
open WebSharper

type AccountCredentials = {
    username : string;
//    passwordHash : byte[];
//    salt : byte[];
}

type Account = {
    credentials : AccountCredentials;
    isLoggedIn : bool;
}

type Tweet = {
    id : Guid;
    data : string;
    author : Account // todo account includes credentials is it needed? revise data structures
    sender : Account
}

type TweetSubscription =
| Hashtag of string
| Mention of string
| Sender of username: string

type OperationStatusResponse = 
| Success
| Error of string

[<Struct>]
type SubscriptionType = 
| RealtimeTweets
| LoadHistoricalTweets

type UserRequest =
| Login of password : string
| SendTweet of string
| ForwardTweet of Guid
| Subscription of (TweetSubscription * SubscriptionType)
| ReceivedTweet of (Tweet * TweetSubscription)

type ServerToClientResponse =
| OperationResult of OperationStatusResponse
| ReceivedTweet of (Tweet * TweetSubscription)

type Credentials = {
    username : string;
    password : string;
}

type SuperviserRequest =
| Signup of (Credentials * (ServerToClientResponse -> unit))
| UserRequest of (UserRequest * string)
| Subscription of (TweetSubscription * SubscriptionType)
| Tweet of Tweet

type ClientToServerRequest =
| Signup of Credentials
| UserRequest of (UserRequest * string)
| TestRequest of string

type SubscriptionActorRequest =
| Subscription of SubscriptionType
| Tweet of Tweet

type ClientUserState = {
    receivedTweets: Tweet list
    serverUserRef: UserRequest IActorRef Option
}

type [<JavaScript>]
    C2SMessage = ClientToServerRequest

type [<JavaScript>]
    S2CMessage = ServerToClientResponse

type ClientUserInfo = {
    mutable subscribersNum: int;
    receivedTweetIDs: Guid System.Collections.Generic.List;
    mutable lastActivity: DateTime Option;
    mutable activitiesCount: int;
    mutable finishedActivities: int;
}