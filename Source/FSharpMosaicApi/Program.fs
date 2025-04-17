namespace FSharpMosaicApi
#nowarn "20"
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open FSharpMosaicApi.DataAccess

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =
        ImageHashRepository.createTableIfNotExists()

        let builder = WebApplication.CreateBuilder(args)

        builder.Services.AddControllers()
        builder.Services.AddCors(fun opt ->
            opt.AddDefaultPolicy(
                fun policy -> 
                    policy.AllowAnyOrigin() |> ignore
            )
        )

        let app = builder.Build()

        app.UseCors()
        app.UseAuthorization()
        app.MapControllers()

        app.Run()

        exitCode
