namespace TwitterEngine.Types

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

type AccountRequest = 
| Login of Credentials

type IdentityMessage = 
| Signup of Credentials
| AccountRequest of AccountRequest

type OperationStatusResponse = 
| Success
| Error of string