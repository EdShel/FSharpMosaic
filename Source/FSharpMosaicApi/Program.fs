namespace FSharpMosaicApi
#nowarn "20"
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open FSharpMosaicApi.Database

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =
        ImageHashRepository.createTableIfNotExists()

        let builder = WebApplication.CreateBuilder(args)

        builder.Services.AddControllers()

        let app = builder.Build()


        app.UseAuthorization()
        app.MapControllers()

        app.Run()

        exitCode
