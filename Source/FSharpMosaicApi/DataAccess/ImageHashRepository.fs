namespace FSharpMosaicApi.DataAccess

open Microsoft.Data.Sqlite

module ImageHashRepository =
    let private OpenConnection() =
        let connection = new SqliteConnection("Data Source=images.db")
        connection.Open()
        connection

    let CreateTableIfNotExists() =
        use connection = OpenConnection()

        let command = connection.CreateCommand()
        command.CommandText <- "
            CREATE TABLE IF NOT EXISTS ImageHash (
                Id INTEGER PRIMARY KEY,
                Hash INTEGER NOT NULL,
                FileName TEXT NOT NULL
            )
        "
        command.ExecuteNonQuery()

    let Insert(hash: int, fileName: string) =
        use connection = OpenConnection()

        let command = connection.CreateCommand()
        command.CommandText <- "
            INSERT INTO ImageHash (Hash, FileName)
            VALUES ($hash, $fileName);
        "
        command.Parameters.AddWithValue("$hash", hash) |> ignore
        command.Parameters.AddWithValue("$fileName", fileName) |> ignore

        let createdRows = command.ExecuteNonQuery()
        if createdRows <> 1 then
            raise(invalidOp("Didn't create the record"))

    let FindClosestHashes(hashes: int list) =
        use connection = OpenConnection()

        let command = connection.CreateCommand()
        let valuesClause =
            hashes
            |> List.map(fun h -> $"({h})")
            |> String.concat ", "

        command.CommandText <- $"
            WITH inputHash(Hash) AS (
                VALUES {valuesClause}
            ),
            inputRGB AS (
                SELECT 
                    Hash AS InputHash,
                    (Hash >> 16) & 0xFF AS B,
                    (Hash >> 8) & 0xFF AS G,
                    (Hash & 0xFF) AS R
                FROM inputHash
            ),
            rgbFilename AS (
                SELECT 
                    FileName,
                    (Hash >> 16) & 0xFF AS B,
                    (Hash >> 8) & 0xFF AS G,
                    (Hash & 0xFF) AS R
                FROM ImageHash
            ),
            rankedByColorDistance AS (
                SELECT 
                    i.InputHash,
                    r.FileName,
                    ROW_NUMBER() OVER (PARTITION BY i.InputHash ORDER BY 
                        ((r.R - i.R)*(r.R - i.R) + (r.G - i.G)*(r.G - i.G) + (r.B - i.B)*(r.B - i.B))
                    ) AS row_no
                FROM inputRGB i
                JOIN rgbFilename r
            )
            SELECT InputHash, FileName
            FROM rankedByColorDistance
            WHERE row_no = 1;
            "
        let reader = command.ExecuteReader()
        let inputHashIndex = reader.GetOrdinal("InputHash")
        let fileNameIndex = reader.GetOrdinal("FileName")
        let foundImages = [
            while reader.Read() do
                let hash = reader.GetInt32(inputHashIndex)
                let fileName = reader.GetString(fileNameIndex)
                yield (hash, fileName)
        ]
        let fileNamesInOriginalOrder =
            hashes
            |> List.map(fun x -> foundImages |> List.find(fun (foundHash, _) -> x = foundHash) )
            |> List.map(fun (_, fileName) -> fileName)
        fileNamesInOriginalOrder
