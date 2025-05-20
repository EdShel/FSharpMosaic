namespace FSharpMosaicApi.Networking

open System.Text.Json
open System.Threading
open Microsoft.AspNetCore.Http

module SseHelper =
    let private toJson value =
        JsonSerializer.Serialize(value, JsonSerializerOptions(JsonSerializerDefaults.Web))

    let raiseEvent (response: HttpResponse, cancellationToken: CancellationToken) (eventName: string) obj = 
        async {
            let data = toJson obj
            let sse = $"event: {eventName}\ndata: {data}\n\n"
            do! response.WriteAsync(sse, cancellationToken) |> Async.AwaitTask
        }

    let setEventStreamHeader (response: HttpResponse) =
        response.Headers.Add("Content-Type", "text/event-stream")
        
