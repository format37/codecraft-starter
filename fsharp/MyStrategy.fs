namespace Aicup2020

open Aicup2020.Model

type MyStrategy() =
    // One MyStrategy plays the whole game: anything you keep in its fields survives from tick to tick.
    let mutable debug: DebugInterface option = None

    member this.log(text: string) =
        match debug with
        | Some debug -> debug.send (DebugCommand.Add { Data = DebugData.Log { Text = text } })
        | None -> ()

    member this.drawRectangle(x0: single, y0: single, x1: single, y1: single, ?color: Color) =
        let color = defaultArg color { R = 1.0f; G = 0.0f; B = 0.0f; A = 1.0f }
        match debug with
        | None -> ()
        | Some debug ->
            let corners = [| (x0, y0); (x1, y0); (x1, y1); (x0, y1) |]
            let vertices = ResizeArray()
            for i in 0 .. 3 do  // Lines takes vertices in pairs: one pair per side
                for x, y in [ corners.[i]; corners.[(i + 1) % 4] ] do
                    vertices.Add { WorldPos = Some { Vec2Single.X = x; Y = y }
                                   ScreenOffset = { X = 0.0f; Y = 0.0f }; Color = color }
            let primitives = { Vertices = vertices.ToArray(); PrimitiveType = PrimitiveType.Lines }
            debug.send (DebugCommand.Add { Data = DebugData.Primitives primitives })

    member this.getAction(playerView: PlayerView, debugInterface: DebugInterface option): Action =
        debug <- debugInterface  // None on the server and in batch mode: there is no viewer to draw in
        let me = playerView.MyId
        let houseProps = playerView.EntityProperties.[EntityType.House]
        let resources = (playerView.Players |> Array.find (fun p -> p.Id = me)).Resource
        let mine = playerView.Entities |> Array.filter (fun e -> e.PlayerId = Some me)
        let builders = mine |> Array.filter (fun e -> e.EntityType = EntityType.BuilderUnit)
        let builderBase = mine |> Array.tryFind (fun e -> e.EntityType = EntityType.BuilderBase)
        let house = mine |> Array.tryFind (fun e -> e.EntityType = EntityType.House)

        let count = mine |> Array.countBy (fun e -> e.EntityType)
        for entityType, n in count do
            this.log $"{entityType}: {n}"
        match house with
        | Some house -> this.log $"house health: {house.Health}/{houseProps.MaxHealth}"
        | None -> ()

        // One house, right of the builder base (which is 5x5)
        let houseSpot =
            match house, builderBase with
            | None, Some builderBase when resources >= houseProps.InitialCost ->
                let houseSpot = { Vec2Int.X = builderBase.Position.X + 5; Y = builderBase.Position.Y + 4 }
                this.drawRectangle(
                    single houseSpot.X, single houseSpot.Y,
                    single (houseSpot.X + houseProps.Size), single (houseSpot.Y + houseProps.Size))
                Some houseSpot
            | _ -> None

        // Of the actions given to an entity, the first one possible this tick is done, in the order
        // attack, build, repair, move. So "build (or repair), else move" walks a builder to the house
        // and starts the job as soon as it is next to it.
        let mutable result = Map.empty
        for builder in builders do
            match houseSpot, house with
            | Some houseSpot, _ ->
                let build = { BuildAction.EntityType = EntityType.House; Position = houseSpot }
                let move = { Target = houseSpot; FindClosestPosition = true; BreakThrough = false }
                result <- result.Add(builder.Id, { MoveAction = Some move; BuildAction = Some build
                                                   AttackAction = None; RepairAction = None })
            | _, Some house when house.Health < houseProps.MaxHealth ->
                // A new house starts with little health and works only once repaired to full
                let move = { Target = house.Position; FindClosestPosition = true; BreakThrough = false }
                let repair = { RepairAction.Target = house.Id }
                result <- result.Add(builder.Id, { MoveAction = Some move; BuildAction = None
                                                   AttackAction = None; RepairAction = Some repair })
            | _ ->
                // Thoughtless mining: step right. A move into an adjacent cell with a resource mines it.
                let step = { Vec2Int.X = builder.Position.X + 1; Y = builder.Position.Y }
                let move = { Target = step; FindClosestPosition = true; BreakThrough = false }
                result <- result.Add(builder.Id, { MoveAction = Some move; BuildAction = None
                                                   AttackAction = None; RepairAction = None })
        { EntityActions = result }

    member this.debugUpdate(playerView: PlayerView, debugInterface: DebugInterface) =
        // Runner.fs calls this while the viewer waits between ticks; drawings sent here show on every tick
        ()
