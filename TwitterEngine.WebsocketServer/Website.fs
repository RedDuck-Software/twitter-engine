module WebSharper.AspNetCore.Tests.Website

open Microsoft.Extensions.Logging
open WebSharper
open WebSharper.AspNetCore

type MyWebsite(logger: ILogger<MyWebsite>) =
    inherit SiteletService<SPA.EndPoint>()

    override this.Sitelet = Application.Text (fun ctx -> "Hello")
