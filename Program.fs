module giraffeSample.App

open System
open System.IO
//open System.Net.WebRequestMethods
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
//open Giraffe.EndpointRouting

// ---------------------------------
// Models
// ---------------------------------

module Models =
    type PositiveInt = private PositiveInt of int

    let (|Negative|_|) x = if x < 0 then Some Negative else None

    let posInt x = function
        | Negative -> None
        | x -> Some <| PositiveInt x

    type String50 = private String50 of string

    let ifTrueThen succ = function
        | true -> Some succ
        | false -> None

    let (|NullOrEmpty|_|) =
        String.IsNullOrEmpty
        >> ifTrueThen NullOrEmpty

    let (|StringLength|_|) l s =
        (String.length s) > l
        |> ifTrueThen StringLength

    let string50 = function
        | NullOrEmpty
        | StringLength 50 -> None
        | s -> String50 s |> Some

    type Message =
        {
            Text : string
        }
        
    [<CLIMutable>]
    type Book = {
        Title : string
        Author : string
        Pages : PositiveInt
    }

// ---------------------------------
// Web app
// ---------------------------------

module Controllers =
    open FSharp.Control.Tasks
    open Microsoft.AspNetCore.Http
    
    let helloController (recipient : string) : HttpHandler =
        let greeting : Models.Message = { Text = sprintf "Hello %s" recipient }
        json greeting
    
    let pongController : HttpHandler =
        let res : Models.Message = { Text = "Pong" }
        json res
    
    let newBookHandler : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                // Bind the body of the POST request to the book object
                let! book = ctx.BindJsonAsync<Models.Book>()
                return! Successful.OK book next ctx
            }

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> Controllers.helloController "World"
                routef "/hello/%s" Controllers.helloController
                route "/ping" >=> Controllers.pongController
            ]
        POST >=>
            choose [
                route "/book" >=> Controllers.newBookHandler
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins(
            "http://localhost:5000",
            "https://localhost:5001")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler)
            .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)


let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0