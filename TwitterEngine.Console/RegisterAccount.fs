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

    let identityActor (mailBox:Actor<IdentityMessage>) = 
        let rec impl () = actor {
            let! data = mailBox.Receive()

            match data with
            | Signup creds ->
                match mailBox.UntypedContext.Child(creds.username) with
                | :? Akka.Actor.Nobody ->
                        let account = signup creds
                        accountActor account |> props |> spawn mailBox account.username |> ignore
                        printfn "signed up %s" account.username
                | _ -> printfn "Returned somebody, not signing up"
            | AccountRequest(Login creds) ->
                let childActorRef = typed <| mailBox.UntypedContext.Child(creds.username)
                childActorRef <! Login(creds) // what if nobody?
                printfn "Logged in, childActor: %A" childActorRef
                ()
            return! impl ()
        }
        impl ()
