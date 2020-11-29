namespace TwitterEngine.Shared.Types

open Akkling
open System

type AccountCredentials = {
    username : string;
    passwordHash : byte[];
    salt : byte[];
}

type Account = {
    credentials : AccountCredentials;
    isLoggedIn : bool;
}

type Credentials = {
    username : string;
    password : string;
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

type ClientUserActorMessage =
| UserRef of IActorRef<UserRequest>
| OperationResult of OperationStatusResponse
| ReceivedTweet of (Tweet * TweetSubscription)
| UserRequest of UserRequest

type SuperviserRequest =
| Signup of Credentials
| Subscription of (TweetSubscription * SubscriptionType)
| Tweet of Tweet

type SubscriptionActorRequest =
| Subscription of SubscriptionType
| Tweet of Tweet

type ClientUserState = {
    receivedTweets: Tweet list
    serverUserRef: UserRequest IActorRef Option
}