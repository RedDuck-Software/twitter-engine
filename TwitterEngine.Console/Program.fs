// Learn more about F# at http://fsharp.org

open System
open Akkling
open TwitterEngine.Types
open System.Security.Cryptography;
open System.Text

let system = System.create "my-system" <| Configuration.defaultConfig()

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

// todo jwt token auth etc etc?
let login account credentials = 
    { account with isLoggedIn = account.credentials.passwordHash = getHash account.credentials.salt credentials.password }

let accountActor account (mailBox:Actor<AccountRequest>) =
    let rec impl account = actor {
        let! data = mailBox.Receive()
        
        let account = 
            match data with 
            | Login(creds) -> login account creds

        printfn "logged in %s" account.credentials.username

        return! impl account
    }
    impl { credentials = account; isLoggedIn = false; }

let authenticationActorRef =
    spawnAnonymous system
    <| props(fun mailBox ->
        let rec impl () = actor {
            let! data = mailBox.Receive()

            match data with
            | Signup creds ->
                if mailBox.UntypedContext.Child(creds.username) :? Akka.Actor.Nobody
                    then
                        let account = signup creds
                        printfn "signed up %s" account.username
                        accountActor account |> props |> spawn mailBox account.username |> ignore
            | AccountRequest(Login creds)->
                let childActorRef = mailBox.UntypedContext.Child(creds.username)
                childActorRef.Tell (Login(creds), mailBox.UntypedContext.Self) // what if nobody?
                printfn "childActor: %A" childActorRef
                ()
            return! impl ()
        }
        impl ())

[<EntryPoint>]
let main argv =
    authenticationActorRef <! AccountRequest(Login({ username = "MarkV"; password = "123456b" }))
    authenticationActorRef <! Signup({ username = "MarkV"; password = "123456b" })
    authenticationActorRef <! AccountRequest(Login({ username = "MarkV"; password = "123456b" }))
    printfn "Hello World from F#!"
    Console.ReadLine ()
    0 // return an integer exit code
