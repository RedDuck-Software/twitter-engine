module Helpers

open System.Security.Cryptography;
open System.Text
open TwitterEngine.Shared.Types
open System

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

let login account password = 
    { account with isLoggedIn = account.credentials.passwordHash = getHash account.credentials.salt password } // todo here incorrect comparison

let extractSpecials specialChar (str:string) =
    str.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
    |> Seq.filter (String.IsNullOrWhiteSpace >> not)
    |> Seq.filter (fun str -> str.StartsWith(specialChar.ToString()))
    |> Seq.map (fun s -> s.Split([|'#'|], StringSplitOptions.RemoveEmptyEntries))
    |> Seq.concat
    |> Seq.map Hashtag

let extractHashes = extractSpecials '#'
let extractUserNames = extractSpecials '@'

let subscriptionToActorName subscription = 
    match subscription with 
    | Hashtag a -> sprintf "tag-%s" a
    | Mention a -> sprintf "mention-%s" a
    | Sender a -> sprintf "sender-%s" a