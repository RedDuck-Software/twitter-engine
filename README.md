# The solution is able to handle 700.000 users requesting data in parallel (the server is one single laptop). 
## Perhaps it can even handle more than that. The highest I tested was 1 million and it crashed on 750.000th user. It scales easy as hell i.e adding new nodes (beyond my laptop) won't be a pain at all - moreover, it will be just a couple of configuration file tweaks!

# Flow:

1. Client-User (WS user) makes request to the WS server
2. WS server forwards the request to "supervisor" actor
3. "supervisor" actor creates user actor if it doesn't exist, and forwards message to it
4. user actor has reference to websocket of the client user, and sends all notifications there
5. If subscription for realtime or historical tweets is needed, supervisor creates subscription actor with references to user-actors who are subscribed
6. subscription actor will then send message to user-actor, and user-actor will send WS message to client-user

#Dependencies scheme
UW         UW
+ \      /  +
|  +    +   |
|    SV     |
|  / |  \   |
| /  |   \  |
|+   +    + |
US+--SUB--+US

+ indicates the ending of a one-directional arrow. Or in other words, --> is the same as --+

UW - user websocket
US - user server (actor)
SV - supervisor
SUB - subscription actor

so, as you can see, only US knows how to talk to UW.
SV and SUB actors send messages to US, then US talks to UW.
SUB and US actors are all created by SV actor.
SUB actor knows its subscribers i.e user actors.

# App configuration
The WS app was redone in such a way that there is no simulation, so you don't need to type any arguments.
# How to run the app
Please to run the app navigate to a folder with project you want to run and enter "dotnet run" command.

For this particular project, we need to:
1) go to websocketServer proj folder
2) enter dotnet run
3) go to client proj folder
4) enter dotnet run