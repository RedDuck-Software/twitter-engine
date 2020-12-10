# The solution is able to handle 700.000 users requesting data in parallel (the server is one single laptop). 
## Perhaps it can even handle more than that. The highest I tested was 1 million and it crashed on 750.000th user. It scales easy as hell i.e adding new nodes (beyond my laptop) won't be a pain at all - moreover, it will be just a couple of configuration file tweaks!

# Flow:

1. User client-actor is created
2. Signup is sent by client-actor to the superviser of server-actors of users
3. Supervisor creates and returns server-actor reference to client-actor of the user
4. User client-actor sends LogIn to the server-actor
5. User sends subscription to server-actor
6. server-actor forwards subscription to supervisor
7. supervisor checks if such subscription-actor exists, if not, creates
8. supervisor forwards message to subscription-actor
9. subscription-actor sends message to the server-actor
10. server-actor notifies client-actor about new tweet

So we have client application and on server-side we have server-actors of the users. 1 actor = 1 user. However, in some cases, 1 superviser is used for all users. This is the point to check out as it might appear to be a bottleneck at some point. The supervisor is used to create subscriptions to tweets. 1 subscription = 1 actor. subscription is basically an actor that receives messages whenever a ceirtan hashtag / usermention / fromsender etc. was sent.

The client app also has some actors. One client-side user actor is connected to one server-side user actor. It sends to the server-side actor various requests, logs state, calculates various statistics such as average processing time. 

# App configuration
App logs server-side logs to $Temp/TwitterEngine.Server folder, the latest logs = the latest unix timestamp.

You can specify app startup arguments, or use default ones. If you don't specify some startup arguments, the rest of the arguments will default.

The arguments are:

1. int - num of users; default - 100.000. - Note - this can handle 700.000 on my laptop
2. float - connection time minutes - this is how long users will be sending messages - defaults to 2.0.
3. int - max count of hashtags to be sent via one message, default - 15
4. int - max count of mentions to be sent via one message, default - 15
5. int - the point when the user will be considered to have "a lot of subscribers". The default is 50
6. int - time in miliseconds for which thread will be going to sleep while waiting for an operation to complete (such as logging in, signing up etc)
7. bool - value indicating if the verbose logging is enabled; default - false 